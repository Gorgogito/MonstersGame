namespace GodotGame.Data.Loaders;

/// <summary>
/// DTOs de deserializacion JSON. Se mantienen separados de las entidades de
/// dominio para que el formato de archivo pueda evolucionar sin afectar al
/// modelo del juego. <see cref="CardDto"/> es publico porque tambien es el
/// modelo de edicion que usa <c>GodotGame.CardEditor</c>.
/// </summary>
public sealed class CardDto
{
    public int Id { get; set; }
    public string Kind { get; set; } = "Monster";
    public string Name { get; set; } = "";
    public int Attack { get; set; }
    public int Defense { get; set; }
    public int Level { get; set; }
    public string Type { get; set; } = "Unknown";
    public string Attribute { get; set; } = "Dark";
    public string Image { get; set; } = "";
    public string Description { get; set; } = "";
    public string EffectId { get; set; } = "";
    /// <summary>Solo para Kind = "Spell" (SpellSubType) o "Trap" (TrapSubType).</summary>
    public string SubType { get; set; } = "Normal";
    /// <summary>Solo para Kind = "Monster" (MonsterCategory): Normal, Effect, Fusion o Ritual.</summary>
    public string Category { get; set; } = "Normal";
    /// <summary>Solo para Kind = "Monster": las dos Estrellas Guardianas (nombre de <c>GuardianStar</c>). Vacio = las de por defecto de su Atributo.</summary>
    public string GuardianStar1 { get; set; } = "";
    public string GuardianStar2 { get; set; } = "";
    /// <summary>Solo para Kind = "Spell" con SubType = "Ritual": Id del Monstruo de Ritual asociado.</summary>
    public int RitualMonsterId { get; set; }
    /// <summary>Solo para Kind = "Spell" con SubType = "Ritual": suma minima de Niveles a Sacrificar.</summary>
    public int RequiredRitualLevel { get; set; }
    /// <summary>Solo para Kind = "Spell" con SubType = "Ritual": restringe que monstruos cuentan como Sacrificio valido. Vacio/null = cualquiera (igual que hoy).</summary>
    public FilterDto? RitualFilter { get; set; }

    /// <summary>Solo para Kind = "Monster" con Category = "Fusion": huecos de material de la receta de Fusion (vacio = sin receta configurada).</summary>
    public List<FusionSlotDto> FusionMaterials { get; set; } = new();

    /// <summary>Solo para Kind = "Spell" con SubType = "Equip": a que Monstruos se puede equipar. Null = cualquiera.</summary>
    public FilterDto? EquipTargetFilter { get; set; }
    /// <summary>Solo para Kind = "Spell" con SubType = "Equip": modificador de ATK al equiparse (admite negativos).</summary>
    public int EquipAttackModifier { get; set; }
    /// <summary>Solo para Kind = "Spell" con SubType = "Equip": modificador de DEF al equiparse (admite negativos).</summary>
    public int EquipDefenseModifier { get; set; }
    /// <summary>Solo para Kind = "Spell" con SubType = "Equip": nombre del <c>ModifierDuration</c>.</summary>
    public string EquipDuration { get; set; } = "WhileEquipped";
    /// <summary>Solo para Kind = "Spell" con SubType = "Equip" y EquipDuration = "ForNTurns": cuantos turnos dura.</summary>
    public int EquipDurationTurns { get; set; }

    /// <summary>Solo para Kind = "Spell" con SubType = "Field": Id del <c>FieldType</c> del catalogo (null = ninguno configurado).</summary>
    public string? FieldTypeId { get; set; }

    /// <summary>
    /// Si es verdadero, <see cref="EffectId"/> se genera automaticamente
    /// (<c>card_{Id}_effect</c>) y se guarda/actualiza un <c>EffectDefinition</c>
    /// compuesto a partir de <see cref="EffectTrigger"/>/<see cref="EffectRequiresTarget"/>/
    /// <see cref="EffectActionSteps"/>, en vez de referenciar uno de los
    /// efectos ya registrados en codigo.
    /// </summary>
    public bool ComposeCustomEffect { get; set; }
    /// <summary>Nombre del <c>EffectTrigger</c> del efecto compuesto.</summary>
    public string EffectTrigger { get; set; } = "Any";
    public bool EffectRequiresTarget { get; set; }
    /// <summary>Nombre del <c>EffectTargetKind</c> ("MonsterZone"/"OwnGraveyard"), solo si <see cref="EffectRequiresTarget"/>.</summary>
    public string EffectTargetKind { get; set; } = "MonsterZone";
    /// <summary>Restringe los objetivos legales ademas de la validacion propia de la accion, si la tiene. Null = sin restriccion adicional.</summary>
    public FilterDto? EffectTargetFilter { get; set; }
    /// <summary>Pasos de accion del efecto compuesto, en orden.</summary>
    public List<EffectActionStepDto> EffectActionSteps { get; set; } = new();
    /// <summary>Clave de <c>VisualProfileCatalog</c> que esta carta elige para su efecto compuesto (activacion/volteo). Vacio = usa el perfil generico del tipo de evento.</summary>
    public string EffectVisualProfileKey { get; set; } = "";

    /// <summary>
    /// Solo para Kind = "Monster": efectos de Monstruo compuestos por datos
    /// (Continuo, de Encendido, Disparado, Rapido, de Volteo, No clasificado).
    /// Ver <c>GodotGame.Core.Effects.Monster.MonsterEffect</c>.
    /// </summary>
    public List<MonsterEffectDto> MonsterEffects { get; set; } = new();
}

/// <summary>Forma de archivo de <c>GodotGame.Core.Effects.Monster.MonsterEffect</c>.</summary>
public sealed class MonsterEffectDto
{
    /// <summary>Nombre de <c>MonsterEffectType</c>: Continuous, Ignition, Trigger, Quick, Flip o Unclassified.</summary>
    public string Type { get; set; } = "Ignition";
    /// <summary>Solo Trigger: nombre de <c>EffectEvent</c>.</summary>
    public string TriggerEvent { get; set; } = "None";
    /// <summary>Solo Trigger: "puedes" (opcional) u obligatorio.</summary>
    public bool Optional { get; set; }
    /// <summary>Encendido/Rapido/No clasificado: nombre de <c>EffectZone</c> (Field, Hand, Graveyard, Banished).</summary>
    public string ActivationZone { get; set; } = "Field";
    public bool OncePerTurn { get; set; }
    /// <summary>Texto del efecto tal como aparece en la carta (opcional).</summary>
    public string Text { get; set; } = "";
    public List<EffectConditionDto> ActivationConditions { get; set; } = new();
    /// <summary>Si el efecto "selecciona" objetivos al activarse (parametros en <see cref="TargetParams"/>).</summary>
    public bool HasTarget { get; set; }
    public Dictionary<string, string> TargetParams { get; set; } = new();
    public List<MonsterEffectStepDto> Costs { get; set; } = new();
    public List<MonsterEffectStepDto> Steps { get; set; } = new();
}

/// <summary>Un paso (costo o accion) de un <see cref="MonsterEffectDto"/>.</summary>
public sealed class MonsterEffectStepDto
{
    /// <summary>Clave de <c>MonsterEffectCatalog.Steps</c> (ej. "draw", "special_summon_self").</summary>
    public string ActionKind { get; set; } = "";
    public Dictionary<string, string> Params { get; set; } = new();
    /// <summary>"Puedes ...": se pregunta al resolverse.</summary>
    public bool Optional { get; set; }
    /// <summary>"Y despues, si ...": todas deben cumplirse para ejecutar el paso.</summary>
    public List<EffectConditionDto> Conditions { get; set; } = new();
}

/// <summary>Una condicion (clave de <c>MonsterEffectCatalog.Conditions</c>) con su negacion y parametros.</summary>
public sealed class EffectConditionDto
{
    public string Kind { get; set; } = "";
    public bool Negate { get; set; }
    public Dictionary<string, string> Params { get; set; } = new();
}

/// <summary>Una condicion atomica editable de un <see cref="FilterDto"/> (forma de archivo de <c>GodotGame.Core.Requirements.FilterCondition</c>).</summary>
public sealed class FilterConditionDto
{
    /// <summary>Nombre de <c>FilterConditionKind</c>: SpecificCard/Type/Category/Attribute/ControllerSide/Any.</summary>
    public string Kind { get; set; } = "Type";
    public bool Negate { get; set; }
    public string Value { get; set; } = "";
}

/// <summary>
/// Forma de archivo de <c>GodotGame.Core.Requirements.TargetFilter</c>:
/// grupos OR de condiciones AND. Sin grupos = sin restriccion (cualquier
/// Monstruo), mismo convenio que el motor de predicados.
/// </summary>
public sealed class FilterDto
{
    public List<List<FilterConditionDto>> OrGroups { get; set; } = new();

    /// <summary>Verdadero si no hay ninguna condicion configurada (equivalente a un filtro null/"cualquiera").</summary>
    public bool IsEmpty => OrGroups.Count == 0 || OrGroups.All(g => g.Count == 0);
}

/// <summary>Un hueco de material de una receta de Fusion generica (forma de archivo de <c>GodotGame.Core.Requirements.RequirementSlot</c>).</summary>
public sealed class FusionSlotDto
{
    public FilterDto Filter { get; set; } = new();
    public int MinCount { get; set; } = 1;
    public int MaxCount { get; set; } = 1;
}

/// <summary>Un paso de accion de un efecto compuesto (forma de archivo de <c>GodotGame.Core.Effects.EffectActionStepSpec</c>).</summary>
public sealed class EffectActionStepDto
{
    /// <summary>Clave registrada en <c>EffectActionCatalog</c> (ej. "draw_card", "destroy_target_monster").</summary>
    public string ActionKind { get; set; } = "";

    /// <summary>Parametros en formato simple <c>Clave=Valor;Clave2=Valor2</c> (se traduce a JSON al guardar).</summary>
    public string ParamsText { get; set; } = "";
}

/// <summary>Un tipo de Campo del catalogo <c>FieldTypes</c> (forma de archivo de <c>GodotGame.Core.Entities.FieldType</c>), editable desde <c>FieldTypeEditorForm</c>.</summary>
public sealed class FieldTypeDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Color { get; set; } = "#FFFFFF";
    public string BackgroundImage { get; set; } = "";
    public string VisualEffectsKey { get; set; } = "";
    public FilterDto? AffectedFilter { get; set; }
    public int StatModifierAmount { get; set; }
    /// <summary>Nombre de <c>FieldStatKind</c>: None/Attack/Defense/Both.</summary>
    public string StatModifierStat { get; set; } = "None";
    /// <summary>Terreno elemental: filtro/monto INDEPENDIENTE de <see cref="AffectedFilter"/>/<see cref="StatModifierAmount"/>, tipicamente en signo opuesto. Null = sin penalizacion.</summary>
    public FilterDto? OpposedFilter { get; set; }
    public int OpposedStatModifierAmount { get; set; }

    public override string ToString() => string.IsNullOrEmpty(Id) ? "(nuevo)" : $"{Name} ({Id})";
}

internal sealed class DeckDto
{
    public string Name { get; set; } = "Mazo";
    public List<int> Cards { get; set; } = new();
}

internal sealed class FusionDto
{
    public int MaterialA { get; set; }
    public int MaterialB { get; set; }
    public int Result { get; set; }
}
