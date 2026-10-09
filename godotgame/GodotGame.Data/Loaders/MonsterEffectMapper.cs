using GodotGame.Core.Effects;
using GodotGame.Core.Effects.Monster;

namespace GodotGame.Data.Loaders;

/// <summary>
/// Convierte los <see cref="MonsterEffectDto"/> (forma de archivo/editor) en
/// <see cref="MonsterEffect"/> de Core, y genera un resumen legible de cada
/// efecto para el editor y el juego. Puro: lo comparten el cargador y el editor.
/// </summary>
public static class MonsterEffectMapper
{
    public static IReadOnlyList<MonsterEffect> ToEffects(IEnumerable<MonsterEffectDto>? dtos) =>
        (dtos ?? Enumerable.Empty<MonsterEffectDto>()).Select(ToEffect).ToList();

    public static MonsterEffect ToEffect(MonsterEffectDto dto) => new(
        CardDtoMapper.ParseEnum(dto.Type, MonsterEffectType.Ignition),
        dto.Steps.Select(ToStep).ToList(),
        CardDtoMapper.ParseEnum(dto.TriggerEvent, EffectEvent.None),
        dto.Optional,
        CardDtoMapper.ParseEnum(dto.ActivationZone, EffectZone.Field),
        dto.OncePerTurn,
        dto.Text,
        dto.ActivationConditions.Select(ToCondition).ToList(),
        dto.HasTarget ? Params(dto.TargetParams) : null,
        dto.Costs.Select(ToStep).ToList(),
        CardDtoMapper.ParseEnum(dto.Subject, EventSubject.ThisCard),
        dto.EventFilter.Count > 0 ? Params(dto.EventFilter) : null);

    private static EffectStep ToStep(MonsterEffectStepDto dto) =>
        new(dto.ActionKind, Params(dto.Params), dto.Optional, dto.Conditions.Select(ToCondition).ToList());

    private static StepCondition ToCondition(EffectConditionDto dto) => new(dto.Kind, dto.Negate, Params(dto.Params));

    private static EffectActionParams Params(Dictionary<string, string>? values) =>
        values == null || values.Count == 0 ? EffectActionParams.Empty : new EffectActionParams(new Dictionary<string, string>(values));

    // ------------------------------------------------------------------ Resumen legible

    /// <summary>Una linea que resume el efecto (tipo, cuando, costos y pasos), para listas del editor y la ayuda del juego.</summary>
    public static string Summary(MonsterEffectDto dto, Func<int, string?>? cardName = null)
    {
        var type = CardDtoMapper.ParseEnum(dto.Type, MonsterEffectType.Ignition);
        var parts = new List<string> { $"[{MonsterEffectCatalog.TypeLabel(type)}]" };

        if (type == MonsterEffectType.Trigger)
        {
            string evt = LowerFirst(MonsterEffectCatalog.EventLabel(CardDtoMapper.ParseEnum(dto.TriggerEvent, EffectEvent.None)));
            string who = CardDtoMapper.ParseEnum(dto.Subject, EventSubject.ThisCard) == EventSubject.AnyCard
                ? QuerySummary(WithCount(dto.EventFilter, "un(a)"), cardName)
                : "esta carta";
            parts.Add($"Si {who} {evt}{(dto.Optional ? " (opcional)" : "")}:");
        }
        else if (type is MonsterEffectType.Ignition or MonsterEffectType.Quick or MonsterEffectType.Unclassified)
            parts.Add($"Desde {LowerFirst(MonsterEffectCatalog.ZoneLabel(CardDtoMapper.ParseEnum(dto.ActivationZone, EffectZone.Field)))}:");

        if (dto.Costs.Count > 0)
            parts.Add("Costo: " + string.Join(", ", dto.Costs.Select(s => StepSummary(s, cardName))) + ";");
        if (dto.HasTarget)
            parts.Add("selecciona " + QuerySummary(dto.TargetParams, cardName) + ";");
        parts.Add(string.Join(", y después ", dto.Steps.Select(s => StepSummary(s, cardName))));
        if (dto.OncePerTurn) parts.Add("(1 vez por turno)");
        return string.Join(" ", parts.Where(p => p.Length > 0));
    }

    public static string StepSummary(MonsterEffectStepDto step, Func<int, string?>? cardName = null)
    {
        var info = MonsterEffectCatalog.Step(step.ActionKind);
        string label = info?.Label ?? step.ActionKind;
        var details = new List<string>();
        if (step.ActionKind == "fusion_summon")
        {
            details.Add("1 " + QuerySummary(WithCount(step.Params, ""), cardName).Trim().Replace("carta", "Monstruo de Fusión"));
            var zones = string.Join("/", CardQuery.ParseZones(step.Params.GetValueOrDefault("From", "MonsterZone,Graveyard")).Select(CardRef.ZoneName));
            details.Add($"materiales de {zones}");
        }
        else if (step.Params.TryGetValue("UseTargets", out var useTargets) && useTargets == "true") details.Add("los objetivos");
        else if (step.Params.TryGetValue("UseLastAffected", out var useLast) && useLast == "true") details.Add("las cartas del paso anterior");
        else if (info != null && info.Params.Any(p => p.Key == "From") && step.Params.Count > 0) details.Add(QuerySummary(step.Params, cardName));
        foreach (var key in new[] { "Count", "Amount", "Attack", "Defense", "Level" })
            if (step.Params.TryGetValue(key, out var value) && value != "0" && value != "" && !(key == "Count" && details.Count > 0))
                details.Add($"{KeyLabel(key)} {value}");

        string conditions = step.Conditions.Count == 0 ? "" :
            "si " + string.Join(" y ", step.Conditions.Select(c => (c.Negate ? "NO: " : "") + LowerFirst(MonsterEffectCatalog.Condition(c.Kind)?.Label ?? c.Kind))) + ": ";
        string optional = step.Optional ? "puedes " : "";
        return $"{conditions}{optional}{LowerFirst(label)}{(details.Count > 0 ? " (" + string.Join(", ", details) + ")" : "")}";
    }

    private static Dictionary<string, string> WithCount(Dictionary<string, string> values, string count) =>
        new(values.Where(v => v.Key is not ("Count" or "From"))) { ["Count"] = count };

    private static string LowerFirst(string text) => text.Length == 0 ? text : char.ToLowerInvariant(text[0]) + text[1..];

    private static string KeyLabel(string key) => key switch
    {
        "Count" => "x",
        "Amount" => "",
        "Attack" => "ATK",
        "Defense" => "DEF",
        "Level" => "Nivel",
        _ => key
    };

    /// <summary>Resumen de una busqueda: "1 Monstruo Nivel ≤4 de Deck".</summary>
    public static string QuerySummary(IReadOnlyDictionary<string, string> p, Func<int, string?>? cardName = null)
    {
        string Get(string key, string fallback = "") => p.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : fallback;
        var words = new List<string> { p.ContainsKey("Count") ? Get("Count", "1") : "todas:" };

        string kind = Get("CardKind", "Any");
        words.Add(kind switch { "Monster" => "monstruo", "Spell" => "Mágica", "Trap" => "Trampa", "SpellTrap" => "Mágica/Trampa", _ => "carta" });
        if (Get("Type") is { Length: > 0 } type) words.Add(type);
        if (Get("Attribute") is { Length: > 0 } attribute) words.Add(attribute);
        if (int.TryParse(Get("LevelMin", "0"), out int min) && min > 0) words.Add($"Nivel ≥{min}");
        if (int.TryParse(Get("LevelMax", "0"), out int max) && max > 0) words.Add($"Nivel ≤{max}");
        if (int.TryParse(Get("AttackMin", "-1"), out int atkMin) && atkMin >= 0) words.Add($"ATK ≥{atkMin}");
        if (int.TryParse(Get("AttackMax", "-1"), out int atkMax) && atkMax >= 0) words.Add($"ATK ≤{atkMax}");
        if (int.TryParse(Get("CardId", "0"), out int id) && id > 0) words.Add($"\"{cardName?.Invoke(id) ?? "#" + id}\"");
        if (Get("NameContains") is { Length: > 0 } archetype) words.Add($"\"{archetype}\"");
        if (Get("SubType", "Any") is var subType && subType != "Any") words.Add(subType switch
        {
            "Continuous" => "Continua", "Equip" => "de Equipo", "Field" => "de Campo", "QuickPlay" => "de Juego Rápido",
            "Counter" => "de Contraefecto", _ => subType
        });
        if (Get("SameNameAsSource") == "true") words.Add("con el nombre de esta carta");
        if (Get("ExcludeSourceName") == "true") words.Add("excepto el nombre de esta carta");
        if (Get("Face") == "FaceUp") words.Add("boca arriba");
        if (Get("Face") == "FaceDown") words.Add("boca abajo");

        string from = string.Join("/", CardQuery.ParseZones(Get("From")).Select(CardRef.ZoneName));
        if (from.Length > 0) words.Add("en " + from);
        string side = Get("Side");
        if (side == "Opponent") words.Add("del adversario");
        else if (side == "Both") words.Add("de cualquiera");
        return string.Join(" ", words);
    }
}
