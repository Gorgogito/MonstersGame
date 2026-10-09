using GodotGame.Core.Entities;

namespace GodotGame.Core.Effects.Monster;

/// <summary>Tipo de un parametro (decide que control muestra el editor).</summary>
public enum ParamType { Int, Bool, Choice, Zones, Card, MonsterType, Attribute }

public sealed record ParamOption(string Value, string Label);

/// <summary>Un parametro editable de un paso o condicion.</summary>
public sealed record ParamInfo(string Key, string Label, ParamType Type, string Default = "", IReadOnlyList<ParamOption>? Options = null, string Help = "");

/// <summary>Para que se puede usar un paso.</summary>
[Flags]
public enum StepUsage
{
    /// <summary>Como accion al resolverse.</summary>
    Action = 1,

    /// <summary>Como costo al activar.</summary>
    Cost = 2,

    /// <summary>Como efecto pasivo de un efecto Continuo.</summary>
    Continuous = 4
}

/// <summary>Descripcion de un paso disponible: clave, nombre, explicacion, parametros y su implementacion.</summary>
public sealed record StepInfo(string Kind, string Label, string Description, StepUsage Usage, IReadOnlyList<ParamInfo> Params, Func<IMonsterEffectStep> Factory);

/// <summary>Descripcion de una condicion disponible.</summary>
public sealed record ConditionInfo(string Kind, string Label, string Description, IReadOnlyList<ParamInfo> Params, Func<MonsterEffectContext, EffectActionParams, bool> Evaluate);

/// <summary>Nombre y explicacion de un valor de enum (tipos de efecto, eventos, zonas).</summary>
public sealed record EnumLabel<T>(T Value, string Label, string Description) where T : struct;

/// <summary>
/// Catalogo de todo lo que se puede usar para componer un efecto de Monstruo:
/// tipos de efecto, eventos disparadores, zonas de activacion, pasos (con sus
/// parametros) y condiciones. Es la fuente de verdad comun del motor (que
/// ejecuta los pasos) y del editor de cartas (que arma los formularios a
/// partir de estas descripciones). Agregar una mecanica nueva = agregar una
/// entrada aqui con su <see cref="IMonsterEffectStep"/>.
/// </summary>
public static class MonsterEffectCatalog
{
    // ------------------------------------------------------------------ Tipos

    public static readonly IReadOnlyList<EnumLabel<MonsterEffectType>> Types = new EnumLabel<MonsterEffectType>[]
    {
        new(MonsterEffectType.Continuous, "Continuo",
            "No inicia una Cadena. Se aplica de forma pasiva en todo momento mientras el monstruo esté boca arriba en el Campo."),
        new(MonsterEffectType.Ignition, "De Encendido",
            "Efecto opcional que solo se activa manualmente durante tu propia Main Phase (Velocidad de Hechizo 1). Suele tener un costo (descartar una carta, sacrificar un monstruo, pagar LP)."),
        new(MonsterEffectType.Trigger, "Disparado",
            "Se activa automáticamente (obligatorio u opcional) cuando ocurre una condición específica, por ejemplo \"Cuando esta carta es destruida...\" o \"Si esta carta es mandada al Cementerio...\". Puede activarse en el turno de cualquier jugador."),
        new(MonsterEffectType.Quick, "Rápido",
            "Velocidad de Hechizo 2. Se activa en el turno de cualquier jugador, incluso en la Battle Phase o en respuesta dentro de una Cadena (como las \"trampas de mano\")."),
        new(MonsterEffectType.Flip, "De Volteo",
            "Subcategoría de los Disparados. Se activa obligatoriamente en el momento en que el monstruo es volteado boca arriba (por un ataque, por Invocación por Volteo o por un efecto)."),
        new(MonsterEffectType.Unclassified, "No clasificado",
            "Altera reglas de invocación o posición sin iniciar una Cadena; por ejemplo, un monstruo que puede Invocarse de Modo Especial desde la mano o el Cementerio pagando una condición."),
    };

    public static readonly IReadOnlyList<EnumLabel<EffectEvent>> Events = new EnumLabel<EffectEvent>[]
    {
        new(EffectEvent.DiscardedByCardEffect, "Es descartada al Cementerio por efecto de una carta", "Desde la mano, por el efecto de una carta al resolverse (no cuenta un costo ni el límite de mano)."),
        new(EffectEvent.Discarded, "Es descartada (por cualquier motivo)", "Desde la mano al Cementerio: por costo, por efecto o por el límite de mano."),
        new(EffectEvent.SentToGraveyard, "Es mandada al Cementerio", "Desde cualquier lugar (mano, Deck o Campo)."),
        new(EffectEvent.Destroyed, "Es destruida", "En batalla o por un efecto, y mandada al Cementerio."),
        new(EffectEvent.DestroyedByBattle, "Es destruida en batalla y mandada al Cementerio", ""),
        new(EffectEvent.DestroyedByEffect, "Es destruida por efecto de una carta", ""),
        new(EffectEvent.Banished, "Es desterrada", ""),
        new(EffectEvent.Summoned, "Es Invocada (de cualquier modo)", "Normal, por Volteo o de Modo Especial."),
        new(EffectEvent.NormalSummoned, "Es Invocada de Modo Normal", "Incluye la Invocación por Sacrificio."),
        new(EffectEvent.SpecialSummoned, "Es Invocada de Modo Especial", "Por efecto, Fusión o Ritual."),
        new(EffectEvent.SpecialSummonedByEffect, "Es Invocada de Modo Especial por efecto de una carta", ""),
        new(EffectEvent.Flipped, "Es volteada boca arriba", "Evento de los efectos de Volteo."),
        new(EffectEvent.InflictsBattleDamage, "Inflige daño de batalla a tu adversario", ""),
        new(EffectEvent.StandbyPhase, "Durante tu Standby Phase", "Si está boca arriba en el Campo."),
        new(EffectEvent.EndPhase, "Durante tu End Phase", "Si está boca arriba en el Campo."),
    };

    public static readonly IReadOnlyList<EnumLabel<EffectZone>> Zones = new EnumLabel<EffectZone>[]
    {
        new(EffectZone.Field, "Campo (boca arriba)", "Esta carta debe estar boca arriba en tu Zona de Monstruos."),
        new(EffectZone.Hand, "Mano", "Esta carta debe estar en tu mano (ej. \"Puedes descartar esta carta...\", \"Puedes mostrar esta carta...\")."),
        new(EffectZone.Graveyard, "Cementerio", "Esta carta debe estar en tu Cementerio."),
        new(EffectZone.Banished, "Desterrada", "Esta carta debe estar desterrada."),
    };

    public static string TypeLabel(MonsterEffectType type) => Types.First(t => t.Value == type).Label;
    public static string EventLabel(EffectEvent evt) => Events.FirstOrDefault(e => e.Value == evt)?.Label ?? evt.ToString();
    public static string ZoneLabel(EffectZone zone) => Zones.First(z => z.Value == zone).Label;

    // ------------------------------------------------------------------ Parametros comunes

    private static readonly ParamOption[] ZoneOptions =
    {
        new("Hand", "Mano"), new("Deck", "Deck"), new("Graveyard", "Cementerio"), new("Banished", "Desterradas"),
        new("MonsterZone", "Zona de Monstruos"), new("SpellTrapZone", "Zona de Magia/Trampa"), new("FieldZone", "Zona del Campo"),
    };

    private static readonly ParamOption[] SideOptions = { new("Own", "Tuyas"), new("Opponent", "Del adversario"), new("Both", "De cualquier jugador") };
    private static readonly ParamOption[] KindOptions = { new("Any", "Cualquier carta"), new("Monster", "Monstruo"), new("Spell", "Mágica"), new("Trap", "Trampa"), new("SpellTrap", "Mágica o Trampa") };
    private static readonly ParamOption[] FaceOptions = { new("Any", "Cualquiera"), new("FaceUp", "Boca arriba"), new("FaceDown", "Boca abajo (Colocada)") };
    private static readonly ParamOption[] WhoOptions = { new("Controller", "Tú"), new("Opponent", "Tu adversario"), new("Both", "Ambos jugadores") };
    private static readonly ParamOption[] ChooserOptions = { new("Controller", "Tú (quien controla el efecto)"), new("Opponent", "Tu adversario"), new("Owner", "El dueño de las cartas"), new("Random", "Al azar") };
    private static readonly ParamOption[] FieldOptions = { new("Own", "Tu Campo"), new("Opponent", "Campo del adversario"), new("Choose", "Cualquier Campo (eliges al resolver)") };
    private static readonly ParamOption[] PositionOptions = { new("Attack", "Ataque"), new("Defense", "Defensa"), new("Choose", "A elección") };
    private static readonly ParamOption[] DurationOptions = { new("Permanent", "Mientras siga en el Campo"), new("UntilEndOfTurn", "Hasta el final del turno") };
    private static readonly ParamOption[] ApplyOptions = { new("Self", "Esta carta"), new("Targets", "Los objetivos seleccionados"), new("Select", "Elegir al resolver"), new("AllMatching", "Todos los que cumplan el filtro") };
    private static readonly ParamOption[] PhaseOptions = { new("Main1", "Main Phase 1"), new("Battle", "Battle Phase"), new("Main2", "Main Phase 2"), new("End", "End Phase") };

    private static ParamInfo P(string key, string label, ParamType type, string def = "", IReadOnlyList<ParamOption>? options = null, string help = "") =>
        new(key, label, type, def, options, help);

    /// <summary>Filtros de carta (sin zonas ni cantidad).</summary>
    private static IEnumerable<ParamInfo> FilterParams(string defaultKind = "Any") => new[]
    {
        P("CardKind", "Clase de carta", ParamType.Choice, defaultKind, KindOptions),
        P("LevelMin", "Nivel mínimo", ParamType.Int, "0", help: "0 = sin mínimo. \"Nivel 5 o mayor\" = 5."),
        P("LevelMax", "Nivel máximo", ParamType.Int, "0", help: "0 = sin máximo. \"Nivel 4 o menor\" = 4."),
        P("Type", "Tipo de monstruo", ParamType.MonsterType, "", help: "Vacío = cualquiera (ej. Demonio)."),
        P("Attribute", "Atributo", ParamType.Attribute, ""),
        P("CardId", "Carta específica", ParamType.Card, "0", help: "Una carta concreta por nombre (ej. \"Las Puertas del Mundo Oscuro\")."),
        P("SameNameAsSource", "Solo cartas con el nombre de esta", ParamType.Bool, "false"),
        P("ExcludeSourceName", "Excepto cartas con el nombre de esta", ParamType.Bool, "false", help: "\"... excepto 'Nombre de esta carta'\"."),
        P("ExcludeSource", "Excepto esta misma carta", ParamType.Bool, "false"),
        P("Face", "Boca arriba / abajo", ParamType.Choice, "Any", FaceOptions, "Solo aplica a cartas en el Campo."),
    };

    /// <summary>Busqueda completa: zonas, lado, filtros, cantidad y quien elige.</summary>
    private static IReadOnlyList<ParamInfo> QueryParams(string defaultFrom, string defaultSide, string defaultKind = "Any", bool targets = true, string defaultChooser = "Controller")
    {
        var list = new List<ParamInfo>();
        if (targets) list.Add(P("UseTargets", "Usar los objetivos seleccionados", ParamType.Bool, "false", help: "Actúa sobre las cartas elegidas al activar (\"selecciona ...; destrúyelo\"). Ignora el resto de la búsqueda."));
        list.Add(P("From", "Desde", ParamType.Zones, defaultFrom, ZoneOptions));
        list.Add(P("Side", "De quién", ParamType.Choice, defaultSide, SideOptions));
        list.AddRange(FilterParams(defaultKind));
        list.Add(P("Count", "Cantidad", ParamType.Int, "1"));
        list.Add(P("Min", "Mínimo", ParamType.Int, "1", help: "Para \"hasta N\": Mínimo 1 y Cantidad N."));
        list.Add(P("Chooser", "Quién elige", ParamType.Choice, defaultChooser, ChooserOptions));
        return list;
    }

    private static IReadOnlyList<ParamInfo> SummonDestinationParams() => new[]
    {
        P("ToField", "A qué Campo", ParamType.Choice, "Own", FieldOptions),
        P("Position", "Posición", ParamType.Choice, "Attack", PositionOptions),
    };

    /// <summary>Parametros del objetivo de un efecto ("selecciona ..." al activar).</summary>
    public static readonly IReadOnlyList<ParamInfo> TargetParams = QueryParams("MonsterZone", "Both", targets: false);

    // ------------------------------------------------------------------ Pasos

    public static readonly IReadOnlyList<StepInfo> Steps = new StepInfo[]
    {
        new("draw", "Robar cartas", "\"Roba N carta(s)\" o \"ambos jugadores roban N carta(s)\".", StepUsage.Action,
            new[] { P("Who", "Quién roba", ParamType.Choice, "Controller", WhoOptions), P("Count", "Cantidad", ParamType.Int, "1") },
            () => new DrawStep()),

        new("discard", "Descartar cartas de la mano", "\"Descarta N carta(s)\", \"tu adversario descarta 1 carta\", \"ambos jugadores descartan 1 carta\", o \"tu adversario elige al azar 1 carta de tu mano y la descartas\" (Quién elige = Al azar).",
            StepUsage.Action | StepUsage.Cost,
            new[] { P("Who", "Quién descarta", ParamType.Choice, "Controller", WhoOptions), P("Count", "Cantidad", ParamType.Int, "1"),
                    P("Chooser", "Quién elige la carta", ParamType.Choice, "Owner", ChooserOptions) }
                .Concat(FilterParams()).ToList(),
            () => new DiscardStep()),

        new("discard_self", "Descartar esta carta", "Costo \"Puedes descartar esta carta al Cementerio; ...\". Esta carta debe estar en la mano.", StepUsage.Cost | StepUsage.Action,
            Array.Empty<ParamInfo>(), () => new DiscardSelfStep()),

        new("reveal_self", "Mostrar esta carta de la mano", "Costo \"Puedes mostrar esta carta en tu mano; ...\".", StepUsage.Cost,
            Array.Empty<ParamInfo>(), () => new RevealSelfStep()),

        new("pay_lp", "Pagar LP", "Costo \"paga N LP\".", StepUsage.Cost,
            new[] { P("Amount", "LP a pagar", ParamType.Int, "500") }, () => new PayLifeStep()),

        new("special_summon_self", "Invocar esta carta de Modo Especial", "\"Invoca esta carta de Modo Especial\" desde donde esté (mano, Cementerio, desterrada).", StepUsage.Action,
            SummonDestinationParams(), () => new SpecialSummonSelfStep()),

        new("special_summon", "Invocar monstruo(s) de Modo Especial", "\"Invoca de Modo Especial, desde tu Deck/mano/Cementerio/desterradas, N monstruo(s) que ...\". Con \"Usar los objetivos\": \"Invócalo de Modo Especial\".", StepUsage.Action,
            QueryParams("Graveyard", "Own", "Monster").Concat(SummonDestinationParams()).ToList(), () => new SpecialSummonStep()),

        new("add_to_hand", "Añadir a la mano", "\"Añade a tu mano 1 ... de tu Deck/Cementerio\".", StepUsage.Action,
            QueryParams("Deck", "Own"), () => new AddToHandStep()),

        new("return_to_hand", "Devolver a la mano", "\"Devuelve a la mano 1 monstruo que controles/del Campo\". También sirve como costo.", StepUsage.Action | StepUsage.Cost,
            QueryParams("MonsterZone", "Own", "Monster"), () => new ReturnToHandStep()),

        new("destroy", "Destruir carta(s)", "\"Selecciona/elige N carta(s) en el Campo; destrúyelas\". Con \"Usar los objetivos\": \"destruye esos objetivos\".", StepUsage.Action,
            QueryParams("MonsterZone,SpellTrapZone", "Opponent"), () => new DestroyStep()),

        new("destroy_all", "Destruir todas las que cumplan", "\"Destruye todas las cartas Mágicas/Trampas de tu adversario\", \"destruye todos los monstruos ...\".", StepUsage.Action,
            new[] { P("From", "Zonas", ParamType.Zones, "SpellTrapZone", ZoneOptions), P("Side", "De quién", ParamType.Choice, "Opponent", SideOptions) }
                .Concat(FilterParams()).ToList(),
            () => new DestroyAllStep()),

        new("banish", "Desterrar carta(s)", "\"Destierra N carta(s) ...\". También sirve como costo.", StepUsage.Action | StepUsage.Cost,
            QueryParams("Graveyard", "Opponent"), () => new BanishStep()),

        new("send_to_graveyard", "Mandar al Cementerio", "\"Manda al Cementerio N carta(s) de tu Deck/mano/Campo\". También sirve como costo (ej. sacrificar).", StepUsage.Action | StepUsage.Cost,
            QueryParams("Deck", "Own"), () => new SendToGraveyardStep()),

        new("deck_bottom", "Poner cartas de la mano bajo el Deck", "\"Tu adversario pone en la parte inferior de su Deck exactamente N cartas de su mano, en cualquier orden\".", StepUsage.Action,
            new[] { P("Who", "Quién", ParamType.Choice, "Opponent", WhoOptions), P("Count", "Cantidad", ParamType.Int, "2"),
                    P("Chooser", "Quién elige", ParamType.Choice, "Owner", ChooserOptions) },
            () => new DeckBottomStep()),

        new("modify_stats", "Ganar / perder ATK y DEF", "\"Esta carta gana 500 ATK\", \"ese objetivo gana 500 ATK\", \"todos los monstruos ... pierden 300 DEF\".", StepUsage.Action,
            new[] { P("Apply", "A quién", ParamType.Choice, "Self", ApplyOptions), P("Attack", "ATK (+/-)", ParamType.Int, "0"), P("Defense", "DEF (+/-)", ParamType.Int, "0"),
                    P("Duration", "Duración", ParamType.Choice, "Permanent", DurationOptions), P("Side", "De quién (Elegir/Todos)", ParamType.Choice, "Own", SideOptions) }
                .Concat(FilterParams("Monster")).Append(P("Count", "Cantidad (Elegir)", ParamType.Int, "1")).ToList(),
            () => new ModifyStatsStep()),

        new("damage", "Infligir daño", "\"Inflige N puntos de daño a tu adversario\".", StepUsage.Action,
            new[] { P("Who", "A quién", ParamType.Choice, "Opponent", WhoOptions), P("Amount", "Daño", ParamType.Int, "500") }, () => new DamageStep()),

        new("gain_lp", "Ganar LP", "\"Ganas N LP\".", StepUsage.Action,
            new[] { P("Who", "Quién", ParamType.Choice, "Controller", WhoOptions), P("Amount", "LP", ParamType.Int, "500") }, () => new GainLifeStep()),

        new("negate_activation", "Negar la activación", "Para efectos Rápidos: niega la activación de la carta o efecto al que responde en la Cadena.", StepUsage.Action,
            Array.Empty<ParamInfo>(), () => new NegateActivationStep()),

        // Pasivos (efectos Continuos)
        new("stat_modifier", "Modificar ATK/DEF (pasivo)", "Continuo: \"Esta carta gana N ATK\" o \"Todos los monstruos Demonio que controlas ganan N ATK\" mientras esta carta esté boca arriba.", StepUsage.Continuous,
            new[] { P("Apply", "A quién", ParamType.Choice, "Self", new[] { ApplyOptions[0], ApplyOptions[3] }), P("Attack", "ATK (+/-)", ParamType.Int, "0"),
                    P("Defense", "DEF (+/-)", ParamType.Int, "0"), P("Side", "De quién (Todos)", ParamType.Choice, "Own", SideOptions) }
                .Concat(FilterParams("Monster")).ToList(),
            () => new PassiveStep()),

        new("battle_indestructible", "No puede ser destruida en batalla", "Continuo: esta carta no puede ser destruida en batalla.", StepUsage.Continuous,
            Array.Empty<ParamInfo>(), () => new PassiveStep()),

        new("direct_attack", "Puede atacar directamente", "Continuo: esta carta puede atacar directamente a tu adversario aunque controle monstruos.", StepUsage.Continuous,
            Array.Empty<ParamInfo>(), () => new PassiveStep()),
    };

    public static StepInfo? Step(string kind) => Steps.FirstOrDefault(s => s.Kind == kind);

    // ------------------------------------------------------------------ Condiciones

    public static readonly IReadOnlyList<ConditionInfo> Conditions = new ConditionInfo[]
    {
        new("discarded_by_opponent", "Fue descartada de tu mano por efecto de una carta del adversario",
            "\"... si esta carta fue descartada de tu mano a tu Cementerio por efecto de una carta del adversario\".",
            Array.Empty<ParamInfo>(), (ctx, _) => ctx.Activation.DiscardedByOpponent),

        new("discarded_by_card_effect", "Fue descartada por efecto de una carta", "De cualquiera de los dos jugadores.",
            Array.Empty<ParamInfo>(), (ctx, _) => ctx.Activation.DiscardedByCardEffect),

        new("previous_step_succeeded", "El paso anterior se realizó (\"si lo haces\")", "Ej. \"... Invoca 1 X y, si lo haces, roba 1 carta\".",
            Array.Empty<ParamInfo>(), (ctx, _) => ctx.Activation.LastStepSucceeded),

        new("last_affected_not_source_name", "La carta afectada en el paso anterior NO tiene el nombre de esta carta",
            "Ej. \"Después, si la carta descartada no fue 'Nombre de esta carta', ...\".",
            Array.Empty<ParamInfo>(),
            (ctx, _) => ctx.Activation.LastAffected.Count > 0 && ctx.Activation.LastAffected.All(c => !CardQuery.SameName(c, ctx.Source))),

        new("is_your_turn", "Es tu turno", "Marca \"Negar\" para \"durante el turno de tu adversario\".",
            Array.Empty<ParamInfo>(), (ctx, _) => ctx.State.ActivePlayer.Side == ctx.ControllerSide),

        new("phase_is", "La fase actual es", "",
            new[] { P("Phase", "Fase", ParamType.Choice, "Main1", PhaseOptions) },
            (ctx, p) => ctx.State.Phase == p.GetEnum("Phase", DuelPhase.Main1)),

        new("count_at_least", "Hay al menos N cartas que cumplan", "Ej. \"si controlas un monstruo Demonio\", \"si tu adversario controla 2 o más monstruos\", \"si no hay cartas en tu Cementerio\" (con Negar).",
            new[] { P("From", "Dónde", ParamType.Zones, "MonsterZone", ZoneOptions), P("Side", "De quién", ParamType.Choice, "Own", SideOptions) }
                .Concat(FilterParams()).Append(P("Amount", "Al menos", ParamType.Int, "1")).ToList(),
            (ctx, p) => new CardQuery(p).Candidates(ctx.State, ctx.ControllerSide, ctx.Source, ctx.Activation.SourceRef).Count >= Math.Max(1, p.GetInt("Amount", 1))),

        new("lp_at_most", "Tus LP son N o menos", "",
            new[] { P("Amount", "LP", ParamType.Int, "4000") },
            (ctx, p) => ctx.Controller.LifePoints <= p.GetInt("Amount")),
    };

    public static ConditionInfo? Condition(string kind) => Conditions.FirstOrDefault(c => c.Kind == kind);

    /// <summary>Evalua una lista de condiciones (todas deben cumplirse; una desconocida se considera no cumplida).</summary>
    public static bool AllMet(IEnumerable<StepCondition> conditions, MonsterEffectContext ctx)
    {
        foreach (var condition in conditions)
        {
            var info = Condition(condition.Kind);
            bool met = info != null && info.Evaluate(ctx, condition.Params);
            if (condition.Negate) met = !met;
            if (!met) return false;
        }
        return true;
    }
}
