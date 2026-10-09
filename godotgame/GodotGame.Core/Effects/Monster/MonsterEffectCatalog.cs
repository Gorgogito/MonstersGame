using GodotGame.Core.Battle;
using GodotGame.Core.Entities;

namespace GodotGame.Core.Effects.Monster;

/// <summary>Tipo de un parametro (decide que control muestra el editor).</summary>
public enum ParamType { Int, Bool, Choice, Zones, Card, MonsterType, Attribute, Text }

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
/// Catalogo de todo lo que se puede usar para componer un efecto de carta
/// (de Monstruo, Magia o Trampa):
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
        new(MonsterEffectType.Activation, "Al activar la carta",
            "Solo Mágicas y Trampas: lo que hace la carta al activarse (\"Selecciona ...; destrúyelo\", \"Roba 2 cartas\"). Entra en la Cadena con la Velocidad de su subtipo. Después, una Normal / de Juego Rápido / Trampa Normal va al Cementerio; una Continua, de Equipo o de Campo se queda en el Campo. Una carta tiene como mucho uno."),
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
        new(EffectEvent.CardActivated, "Se activa (Mágica/Trampa)", "La carta se activa en la Cadena. Con \"Otra carta que cumpla el filtro\" (Clase = Trampa): \"cuando una Carta de Trampa es activada mientras esta carta está en tu Cementerio\". El efecto se aplica cuando esa Cadena termina de resolverse."),
        new(EffectEvent.LeavesField, "Deja el Campo", "Destruida, desterrada, devuelta a la mano o al Deck, Sacrificada... Combínalo con la condición \"El evento fue causado por ...\" para \"a causa de una carta del adversario\"."),
    };

    public static readonly IReadOnlyList<EnumLabel<EventSubject>> Subjects = new EnumLabel<EventSubject>[]
    {
        new(EventSubject.ThisCard, "Esta carta", "\"Si esta carta es ...\". Se activa desde donde quede la carta después del evento."),
        new(EventSubject.AnyCard, "Otra carta que cumpla el filtro", "\"Si un monstruo Demonio es descartado de tu mano ...\". Esta carta tiene que estar en su zona de activación (ej. boca arriba en el Campo) cuando ocurre el evento."),
    };

    public static readonly IReadOnlyList<EnumLabel<EffectZone>> Zones = new EnumLabel<EffectZone>[]
    {
        new(EffectZone.Field, "Campo (boca arriba)", "Esta carta debe estar boca arriba en tu Campo: en la Zona de Monstruos si es un monstruo, o en la Zona de Magia/Trampa o del Campo si es Mágica/Trampa."),
        new(EffectZone.Hand, "Mano", "Esta carta debe estar en tu mano (ej. \"Puedes descartar esta carta...\", \"Puedes mostrar esta carta...\")."),
        new(EffectZone.Graveyard, "Cementerio", "Esta carta debe estar en tu Cementerio."),
        new(EffectZone.Banished, "Desterrada", "Esta carta debe estar desterrada."),
    };

    public static string TypeLabel(MonsterEffectType type) => Types.First(t => t.Value == type).Label;
    public static string EventLabel(EffectEvent evt) => Events.FirstOrDefault(e => e.Value == evt)?.Label ?? evt.ToString();
    public static string ZoneLabel(EffectZone zone) => Zones.First(z => z.Value == zone).Label;
    public static string SubjectLabel(EventSubject subject) => Subjects.First(s => s.Value == subject).Label;

    public static string ExcavateLabel(ExcavateAction action) => ExcavateOptions.First(o => o.Value == action.ToString()).Label;

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
    private static readonly ParamOption[] ChooserOptions = { new("Controller", "Tú (quien controla el efecto)"), new("Opponent", "Tu adversario"), new("Owner", "El dueño de las cartas"), new("OwnersOpponent", "El adversario del dueño"), new("Random", "Al azar") };
    private static readonly ParamOption[] FieldOptions = { new("Own", "Tu Campo"), new("Opponent", "Campo del adversario"), new("Choose", "Cualquier Campo (eliges al resolver)") };
    private static readonly ParamOption[] PositionOptions = { new("Attack", "Ataque"), new("Defense", "Defensa"), new("Choose", "A elección") };
    private static readonly ParamOption[] DurationOptions = { new("Permanent", "Mientras siga en el Campo"), new("UntilEndOfTurn", "Hasta el final del turno") };
    private static readonly ParamOption[] ApplyOptions = { new("Self", "Esta carta"), new("Targets", "Los objetivos seleccionados"), new("Select", "Elegir al resolver"), new("AllMatching", "Todos los que cumplan el filtro"), new("LastAffected", "Las cartas del paso anterior") };
    private static readonly ParamOption[] PassiveApplyOptions = { new("Self", "Esta carta"), new("Equipped", "El monstruo equipado (Mágica de Equipo)"), new("AllMatching", "Todos los que cumplan el filtro") };
    private static readonly ParamOption[] ScaleOptions = { new("None", "Tal cual"), new("PerLastAffected", "× cada carta afectada en el paso anterior (o el costo)"), new("LastAffectedLevel", "× el Nivel de la carta del paso anterior") };
    private static readonly ParamOption[] SubTypeOptions =
    {
        new("Any", "Cualquiera"), new("Normal", "Normal"), new("Continuous", "Continua"), new("Equip", "De Equipo"), new("Field", "De Campo"),
        new("QuickPlay", "De Juego Rápido"), new("Ritual", "De Ritual"), new("Counter", "De Contraefecto"),
    };
    private static readonly ParamOption[] ExcavateOptions = { new("SetOnField", "Colocarla en tu Campo"), new("AddToHand", "Añadirla a tu mano"), new("SpecialSummon", "Invocarla de Modo Especial"), new("SendToGraveyard", "Mandarla al Cementerio") };
    private static readonly ParamOption[] OtherwiseOptions = { new("Choose", "Arriba o abajo del Deck (eliges)"), new("Top", "Arriba del Deck"), new("Bottom", "Abajo del Deck"), new("Graveyard", "Al Cementerio") };
    private static readonly ParamOption[] MaterialMoveOptions = { new("Banish", "Desterrarlos"), new("Graveyard", "Mandarlos al Cementerio") };
    private static readonly ParamOption[] SummonMethodOptions = { new("Normal", "Normal"), new("Set", "Colocada"), new("Flip", "Por Volteo"), new("Special", "Especial (por efecto)"), new("Fusion", "Por Fusión"), new("Ritual", "Por Ritual") };
    private static readonly ParamOption[] RespondWhoOptions = { new("Opponent", "Tu adversario"), new("Controller", "Tú"), new("Both", "Cualquiera") };
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
        P("NameContains", "Nombre contiene (arquetipo)", ParamType.Text, "", help: "Para cartas de un arquetipo: \"Mundo Oscuro\" busca todas las cartas cuyo nombre lo contenga. Vacío = cualquiera."),
        P("SubType", "Subtipo (Mágica/Trampa)", ParamType.Choice, "Any", SubTypeOptions, "Ej. \"Trampa Normal\": Clase = Trampa y Subtipo = Normal."),
        P("AttackMin", "ATK mínimo", ParamType.Int, "-1", help: "-1 = sin mínimo. \"con 2000 ATK o más\" = 2000. En el Campo cuenta el ATK actual."),
        P("AttackMax", "ATK máximo", ParamType.Int, "-1", help: "-1 = sin máximo. \"con 1500 ATK o menos\" = 1500."),
        P("SameNameAsSource", "Solo cartas con el nombre de esta", ParamType.Bool, "false"),
        P("ExcludeSourceName", "Excepto cartas con el nombre de esta", ParamType.Bool, "false", help: "\"... excepto 'Nombre de esta carta'\"."),
        P("ExcludeSource", "Excepto esta misma carta", ParamType.Bool, "false"),
        P("Face", "Boca arriba / abajo", ParamType.Choice, "Any", FaceOptions, "Solo aplica a cartas en el Campo."),
    };

    /// <summary>Busqueda completa: zonas, lado, filtros, cantidad y quien elige.</summary>
    private static IReadOnlyList<ParamInfo> QueryParams(string defaultFrom, string defaultSide, string defaultKind = "Any", bool targets = true, string defaultChooser = "Controller")
    {
        var list = new List<ParamInfo>();
        if (targets)
        {
            list.Add(P("UseTargets", "Usar los objetivos seleccionados", ParamType.Bool, "false", help: "Actúa sobre las cartas elegidas al activar (\"selecciona ...; destrúyelo\"). Ignora el resto de la búsqueda."));
            list.Add(P("UseLastAffected", "Usar las cartas del paso anterior", ParamType.Bool, "false", help: "Actúa sobre las cartas que movió o miró el paso anterior (ej. \"mira 1 carta al azar ...; si es un monstruo, Invócalo\"). El filtro de abajo sigue aplicándose (Clase = Monstruo)."));
        }
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

    /// <summary>Filtro de la carta que sufre el evento de un Disparado sobre "otra carta" (de quien es, clase, Tipo, nombre...).</summary>
    public static readonly IReadOnlyList<ParamInfo> EventFilterParams =
        new[] { P("Side", "De quién es la carta", ParamType.Choice, "Own", SideOptions) }.Concat(FilterParams("Monster")).ToList();

    // ------------------------------------------------------------------ Pasos

    public static readonly IReadOnlyList<StepInfo> Steps = new StepInfo[]
    {
        new("draw", "Robar cartas", "\"Roba N carta(s)\" o \"ambos jugadores roban N carta(s)\".", StepUsage.Action,
            new[] { P("Who", "Quién roba", ParamType.Choice, "Controller", WhoOptions), P("Count", "Cantidad", ParamType.Int, "1"),
                    P("SameAsLastPerPlayer", "Tantas como descartó cada uno (paso anterior)", ParamType.Bool, "false", help: "\"... y después cada jugador roba el mismo número de cartas que descartó\". Ignora la Cantidad.") },
            () => new DrawStep()),

        new("discard", "Descartar cartas de la mano", "\"Descarta N carta(s)\", \"tu adversario descarta 1 carta\", \"cada jugador descarta 1 carta\" (Quién = Ambos; se salta a quien no tenga cartas), \"tu adversario elige al azar 1 carta de tu mano y la descartas\" (Quién elige = Al azar), \"cada uno elige 1 carta de la mano de su adversario\" (Quién elige = El adversario del dueño).",
            StepUsage.Action | StepUsage.Cost,
            new[] { P("Who", "Quién descarta", ParamType.Choice, "Controller", WhoOptions), P("Count", "Cantidad", ParamType.Int, "1"),
                    P("AnyNumber", "Cualquier número (1 o más)", ParamType.Bool, "false", help: "\"Descarta cualquier número de cartas\": se elige al menos 1. Combínalo con \"× cada carta afectada\" en un paso de ATK."),
                    P("All", "Toda la mano (tantas como sea posible)", ParamType.Bool, "false", help: "\"Descartan tantas cartas como sea posible de sus manos\"."),
                    P("Chooser", "Quién elige la carta", ParamType.Choice, "Owner", ChooserOptions) }
                .Concat(FilterParams()).ToList(),
            () => new DiscardStep()),

        new("discard_self", "Descartar esta carta", "Costo \"Puedes descartar esta carta al Cementerio; ...\". Esta carta debe estar en la mano.", StepUsage.Cost | StepUsage.Action,
            Array.Empty<ParamInfo>(), () => new DiscardSelfStep()),

        new("reveal_self", "Mostrar esta carta de la mano", "Costo \"Puedes mostrar esta carta en tu mano; ...\".", StepUsage.Cost,
            Array.Empty<ParamInfo>(), () => new RevealSelfStep()),

        new("pay_lp", "Pagar LP", "Costo \"paga N LP\".", StepUsage.Cost,
            new[] { P("Amount", "LP a pagar", ParamType.Int, "500") }, () => new PayLifeStep()),

        new("banish_self", "Desterrar esta carta", "\"Puedes desterrar esta carta de tu Cementerio; ...\" (costo) o \"destierra esta carta\" (acción). Funciona desde el Cementerio, la mano o el Campo.", StepUsage.Cost | StepUsage.Action,
            Array.Empty<ParamInfo>(), () => new BanishSelfStep()),

        new("add_self_to_hand", "Añadir esta carta a la mano", "\"Puedes añadir esta carta a tu mano\" (desde el Cementerio, desterrada o el Campo).", StepUsage.Action | StepUsage.Cost,
            Array.Empty<ParamInfo>(), () => new AddSelfToHandStep()),

        new("tribute", "Sacrificar monstruo(s)", "Costo \"Sacrifica 1 monstruo\": manda monstruos que controlas al Cementerio (puede incluir a esta carta).", StepUsage.Cost | StepUsage.Action,
            QueryParams("MonsterZone", "Own", "Monster", targets: false), () => new TributeStep()),

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

        new("set_spell_trap", "Colocar Mágica/Trampa en tu Campo", "\"Elige 1 Trampa de tu mano o Deck y Colócala en tu Campo\". Queda boca abajo; como cualquier carta Colocada, una Trampa no se puede activar el mismo turno.", StepUsage.Action,
            QueryParams("Hand,Deck", "Own", "Trap"), () => new SetSpellTrapStep()),

        new("reveal_hand", "Mostrar la mano", "\"Ambos jugadores muestran sus manos\" / \"tu adversario muestra su mano\". Si es la mano rival, se te enseña.", StepUsage.Action,
            new[] { P("Who", "Quién la muestra", ParamType.Choice, "Both", WhoOptions) }, () => new RevealHandStep()),

        new("reveal_random_hand", "Mirar cartas al azar de una mano", "\"Mira 1 carta al azar en la mano de tu adversario\". La(s) carta(s) vista(s) quedan como \"las cartas del paso anterior\" para el paso siguiente (ej. \"si es un monstruo, puedes Invocarlo\").", StepUsage.Action,
            new[] { P("Who", "De la mano de", ParamType.Choice, "Opponent", WhoOptions), P("Count", "Cantidad", ParamType.Int, "1") }, () => new RevealRandomHandStep()),

        new("excavate", "Excavar la carta superior del Deck", "\"Excava la carta superior de tu Deck y, si es una Trampa Normal, Colócala; si no, ponla arriba o abajo de tu Deck\". El filtro de abajo dice qué tiene que ser.", StepUsage.Action,
            new[] { P("IfMatch", "Si cumple el filtro", ParamType.Choice, "SetOnField", ExcavateOptions), P("Otherwise", "Si no cumple", ParamType.Choice, "Choose", OtherwiseOptions) }
                .Concat(FilterParams("Trap")).ToList(),
            () => new ExcavateStep()),

        new("fusion_summon", "Invocar por Fusión (por efecto)", "\"Invoca por Fusión 1 Monstruo de Fusión ..., desterrando de tu Campo o Cementerio los materiales\". El filtro de abajo es para el Monstruo de Fusión (ej. Tipo Demonio). Los materiales salen de las recetas de Fusión.", StepUsage.Action,
            new[]
            {
                P("From", "Materiales desde", ParamType.Zones, "MonsterZone,Graveyard", ZoneOptions),
                P("MaterialMove", "Los materiales se", ParamType.Choice, "Banish", MaterialMoveOptions),
                P("HandIfNameContains", "También desde la mano (descartando) si el resultado se llama", ParamType.Text, "", help: "\"Si Invocas por Fusión un monstruo 'Mundo Oscuro' de esta forma, también puedes descartar monstruos como material\" → Mundo Oscuro."),
                P("Position", "Posición", ParamType.Choice, "Attack", PositionOptions),
            }.Concat(FilterParams("Monster").Where(f => f.Key is not ("SameNameAsSource" or "ExcludeSource" or "Face" or "SubType"))).ToList(),
            () => new FusionSummonStep()),

        new("summon_lock", "No puedes Invocar el resto del turno", "\"No puedes Invocar monstruos en el turno en que activas esta carta, excepto por este efecto (pero puedes Colocar)\": ponlo DESPUÉS de la Invocación del efecto. Para que no se pueda activar si ya Invocaste, agrega la condición \"Ya Invocaste un monstruo este turno\" con Negar.", StepUsage.Action,
            Array.Empty<ParamInfo>(), () => new SummonLockStep()),

        new("replace_responded_effect", "Cambiar el efecto activado por \"descarta\"", "Para efectos Rápidos o Trampas de respuesta: \"el efecto activado se convierte en 'Tu adversario descarta 1 carta'\". Se aplica al eslabón al que responde (quien lo activó hace que SU adversario descarte).", StepUsage.Action,
            new[] { P("Count", "Cartas a descartar", ParamType.Int, "1"), P("Random", "Al azar", ParamType.Bool, "false", help: "\"Tu adversario descarta 1 carta al azar\".") },
            () => new ReplaceRespondedEffectStep()),

        new("negate_summon", "Negar la Invocación (y destruir)", "\"Cuando uno o más monstruos fueran a ser Invocados: niega la Invocación y destruye esos monstruos\". Úsalo en una Trampa con la condición \"Un monstruo del adversario está siendo Invocado\": aparece una ventana para activarla cuando el adversario Invoca (Normal, por Volteo, por Fusión/Ritual o por procedimiento).", StepUsage.Action,
            Array.Empty<ParamInfo>(), () => new NegateSummonStep()),

        new("declare_card_discard", "Declarar un nombre de carta", "\"Declara 1 nombre de carta; si esa carta está en la mano de tu adversario, debe descartar todas sus copias; de otro modo tú descartas 1 carta al azar\".", StepUsage.Action,
            new[] { P("Who", "Mano revisada", ParamType.Choice, "Opponent", WhoOptions.Take(2).ToArray()),
                    P("PenaltyIfMissing", "Si no está: tú descartas 1 al azar", ParamType.Bool, "true") },
            () => new DeclareCardDiscardStep()),

        new("watch_draws", "Revisar los robos durante N turnos", "\"Mira todas las cartas que robe tu adversario hasta el final de su 3er turno después de que esta carta se resuelva, y destruye los monstruos con 1500 ATK o menos\". El filtro de abajo dice qué cartas robadas se destruyen.", StepUsage.Action,
            new[] { P("Who", "Robos de", ParamType.Choice, "Opponent", WhoOptions.Take(2).ToArray()), P("Turns", "Turnos", ParamType.Int, "3") }
                .Concat(FilterParams("Monster")).ToList(),
            () => new WatchDrawsStep()),

        new("special_summon_self_as_monster", "Invocar esta Mágica/Trampa como monstruo", "\"Puedes Invocar esta carta de Modo Especial como un Monstruo Normal (Aqua/AGUA/Nivel 2/ATK 1200/DEF 0)\". Funciona desde el Cementerio (o la mano/desterrada).", StepUsage.Action,
            new[]
            {
                P("Type", "Tipo", ParamType.MonsterType, "Aqua"), P("Attribute", "Atributo", ParamType.Attribute, "Water"),
                P("Level", "Nivel", ParamType.Int, "2"), P("Attack", "ATK", ParamType.Int, "1200"), P("Defense", "DEF", ParamType.Int, "0"),
                P("Position", "Posición", ParamType.Choice, "Attack", PositionOptions.Take(2).ToArray()),
                P("UnaffectedByMonsterEffects", "No es afectada por efectos de monstruos", ParamType.Bool, "true"),
                P("BanishWhenLeavesField", "Destiérrala cuando deje el Campo", ParamType.Bool, "true"),
            },
            () => new SpecialSummonSelfAsMonsterStep()),

        new("deck_bottom", "Poner cartas de la mano bajo el Deck", "\"Tu adversario pone en la parte inferior de su Deck exactamente N cartas de su mano, en cualquier orden\".", StepUsage.Action,
            new[] { P("Who", "Quién", ParamType.Choice, "Opponent", WhoOptions), P("Count", "Cantidad", ParamType.Int, "2"),
                    P("Chooser", "Quién elige", ParamType.Choice, "Owner", ChooserOptions) },
            () => new DeckBottomStep()),

        new("modify_stats", "Ganar / perder ATK, DEF o Nivel", "\"Esta carta gana 500 ATK\", \"ese objetivo gana 500 ATK\", \"todos los monstruos ... pierden 300 DEF\", \"gana 1 Nivel y 400 ATK por cada carta descartada\" (Multiplicar = × cada carta afectada), \"ganan ATK igual al Nivel del monstruo descartado × 100\" (ATK 100, Multiplicar = × el Nivel).", StepUsage.Action,
            new[] { P("Apply", "A quién", ParamType.Choice, "Self", ApplyOptions), P("Attack", "ATK (+/-)", ParamType.Int, "0"), P("Defense", "DEF (+/-)", ParamType.Int, "0"),
                    P("Level", "Nivel (+/-)", ParamType.Int, "0"), P("Scale", "Multiplicar", ParamType.Choice, "None", ScaleOptions),
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
        new("stat_modifier", "Modificar ATK/DEF (pasivo)", "Continuo: \"Esta carta gana N ATK\", \"Todos los monstruos Demonio ganan 300 ATK/DEF\" (Mágica de Campo: De quién = De cualquier jugador) o \"El monstruo equipado gana 500 ATK\" (Mágica de Equipo), mientras esta carta esté boca arriba.", StepUsage.Continuous,
            PassiveParams().Concat(new[] { P("Attack", "ATK (+/-)", ParamType.Int, "0"), P("Defense", "DEF (+/-)", ParamType.Int, "0") }).Concat(FilterParams("Monster")).ToList(),
            () => new PassiveStep()),

        new("battle_indestructible", "No puede ser destruido en batalla", "Continuo: esta carta (o el monstruo equipado, o los que cumplan el filtro) no puede ser destruido en batalla.", StepUsage.Continuous,
            PassiveParams().Concat(FilterParams("Monster")).ToList(), () => new PassiveStep()),

        new("negate_monster_effects", "Negar los efectos de los monstruos boca arriba", "Continuo (Mágica/Trampa): \"Niega los efectos de todos los monstruos boca arriba mientras estén boca arriba en el Campo (pero sus efectos todavía pueden ser activados)\". Sus efectos Continuos dejan de aplicarse y los que se activan en el Campo se resuelven sin efecto.", StepUsage.Continuous,
            Array.Empty<ParamInfo>(), () => new PassiveStep()),

        new("destruction_substitute", "Desterrarse del Cementerio en lugar de una destrucción", "Continuo desde el Cementerio: \"Si uno o más monstruos 'Mundo Oscuro' que controlas fueran a ser destruidos en batalla o por efecto de una carta del adversario, puedes desterrar esta carta de tu Cementerio en su lugar\". Se aplica automáticamente. El filtro dice qué monstruos protege.", StepUsage.Continuous,
            new[]
            {
                P("ByBattle", "Destruidos en batalla", ParamType.Bool, "true"),
                P("ByOpponentEffect", "Por efecto de una carta del adversario", ParamType.Bool, "true"),
                P("ByAnyEffect", "Por efecto de cualquier carta", ParamType.Bool, "false"),
                P("OncePerTurn", "Solo una vez por turno", ParamType.Bool, "true"),
                P("Side", "Monstruos de", ParamType.Choice, "Own", SideOptions.Take(1).ToArray()),
            }.Concat(FilterParams("Monster")).ToList(),
            () => new PassiveStep()),

        new("direct_attack", "Puede atacar directamente", "Continuo: esta carta (o el monstruo equipado, o los que cumplan el filtro) puede atacar directamente aunque el adversario controle monstruos.", StepUsage.Continuous,
            PassiveParams().Concat(FilterParams("Monster")).ToList(), () => new PassiveStep()),
    };

    public static StepInfo? Step(string kind) => Steps.FirstOrDefault(s => s.Kind == kind);

    /// <summary>A quien afecta un pasivo: esta carta, el monstruo equipado o todos los que cumplan el filtro (de quien).</summary>
    private static IEnumerable<ParamInfo> PassiveParams() => new[]
    {
        P("Apply", "A quién", ParamType.Choice, "Self", PassiveApplyOptions),
        P("Side", "De quién (Todos)", ParamType.Choice, "Own", SideOptions),
    };

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

        new("event_caused_by", "El evento fue causado por una carta (del adversario y/o de un arquetipo)",
            "Para Disparados: \"... por efecto de una carta 'Mundo Oscuro' o por efecto de una carta del adversario\", \"si deja el Campo a causa de una carta del adversario\". Se cumple si se cumple CUALQUIERA de las dos casillas marcadas (sin marcar nada: cualquier carta).",
            new[]
            {
                P("ByOpponent", "Por una carta del adversario", ParamType.Bool, "false"),
                P("SourceNameContains", "O por una carta cuyo nombre contenga", ParamType.Text, "", help: "Ej. Mundo Oscuro."),
            },
            (ctx, p) => EventCausedBy(ctx, p)),

        new("event_summoned_by", "Había sido Invocada de cierta forma", "Para \"si esta carta Invocada por Fusión deja el Campo ...\".",
            new[] { P("Method", "Invocada", ParamType.Choice, "Fusion", SummonMethodOptions) },
            (ctx, p) => ctx.Activation.Trigger?.SummonedBy is { } method && method == p.GetEnum("Method", SummonMethod.Fusion)),

        new("event_controlled_by_owner", "La controlaba su dueño", "Para \"si esta carta controlada por su dueño deja el Campo ...\".",
            Array.Empty<ParamInfo>(), (ctx, _) => ctx.Activation.Trigger?.ControlledByOwner ?? true),

        new("responding_to_activation", "En respuesta a la activación de ...",
            "Para efectos Rápidos: \"Cuando tu adversario activa el efecto de un monstruo, o una Carta Mágica/de Trampa Normal\". Solo se puede activar respondiendo en una Cadena a algo de lo marcado.",
            new[]
            {
                P("Who", "Activado por", ParamType.Choice, "Opponent", RespondWhoOptions),
                P("MonsterEffect", "Efecto de un monstruo", ParamType.Bool, "true"),
                P("NormalSpell", "Mágica Normal", ParamType.Bool, "true"),
                P("NormalTrap", "Trampa Normal", ParamType.Bool, "true"),
                P("OtherSpellTrap", "Otra Mágica/Trampa", ParamType.Bool, "false"),
            },
            (ctx, p) => RespondingTo(ctx, p)),

        new("not_sent_to_graveyard_this_turn", "Esta carta NO fue mandada al Cementerio este turno", "\"... excepto en el turno en el que esta carta fue mandada al Cementerio\".",
            Array.Empty<ParamInfo>(), (ctx, _) => !ctx.State.SentToGraveyardThisTurn.Contains(ctx.Source)),

        new("responding_to_summon", "Un monstruo del adversario está siendo Invocado", "Para Trampas como \"cuando uno o más monstruos fueran a ser Invocados\": la carta se ofrece en una ventana justo cuando tu adversario Invoca (Normal, por Volteo, Fusión, Ritual o por procedimiento; no las Invocaciones a mitad de una Cadena).",
            Array.Empty<ParamInfo>(),
            (ctx, _) => ctx.State.PendingSummonsBy is { } by && by != ctx.ControllerSide && ctx.State.PendingSummons.Any(r => CardMover.Locate(ctx.State, r) != null)),

        new("summoned_this_turn", "Ya Invocaste un monstruo este turno", "Con Negar: \"no puedes activar esta carta si ya Invocaste este turno\" (Colocar no cuenta).",
            Array.Empty<ParamInfo>(), (ctx, _) => ctx.Controller.HasSummonedThisTurn),
    };

    private static bool EventCausedBy(MonsterEffectContext ctx, EffectActionParams p)
    {
        if (ctx.Activation.Trigger is not { } t || t.Cause.Kind is not (CauseKind.Effect or CauseKind.Battle)) return false;
        bool byOpponent = p.GetBool("ByOpponent");
        string name = p.GetString("SourceNameContains").Trim();
        if (!byOpponent && name.Length == 0) return true;
        if (byOpponent && t.Cause.By is { } by && by != ctx.ControllerSide) return true;
        return name.Length > 0 && t.Cause.Source != null && t.Cause.Source.Name.IndexOf(name, StringComparison.CurrentCultureIgnoreCase) >= 0;
    }

    private static bool RespondingTo(MonsterEffectContext ctx, EffectActionParams p)
    {
        if (ctx.Activation.RespondingTo is not { } link) return false;
        bool whoOk = p.GetString("Who", "Opponent") switch
        {
            "Controller" => link.Controller == ctx.ControllerSide,
            "Both" => true,
            _ => link.Controller != ctx.ControllerSide
        };
        if (!whoOk) return false;
        if (link.IsMonsterEffect) return p.GetBool("MonsterEffect", true);
        return link.Card switch
        {
            SpellCard { SubType: SpellSubType.Normal } => p.GetBool("NormalSpell", true),
            TrapCard { SubType: TrapSubType.Normal } => p.GetBool("NormalTrap", true),
            SpellCard or TrapCard => p.GetBool("OtherSpellTrap"),
            _ => false
        };
    }

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
