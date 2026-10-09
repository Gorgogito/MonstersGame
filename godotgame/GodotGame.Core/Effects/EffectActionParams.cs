namespace GodotGame.Core.Effects;

/// <summary>
/// Parametros de una accion o condicion, ya resueltos desde datos (ej. el
/// <c>ParamsJson</c> de una fila <c>EffectActionSteps</c>/<c>EffectConditions</c>).
/// Deliberadamente un diccionario plano string-a-string: la deserializacion
/// JSON es responsabilidad de la capa de datos, no de Core.
/// </summary>
public sealed class EffectActionParams
{
    public static readonly EffectActionParams Empty = new(new Dictionary<string, string>());

    private readonly IReadOnlyDictionary<string, string> _values;

    public EffectActionParams(IReadOnlyDictionary<string, string> values) => _values = values;

    public int GetInt(string key, int fallback = 0) =>
        _values.TryGetValue(key, out var raw) && int.TryParse(raw, out var value) ? value : fallback;

    public string GetString(string key, string fallback = "") =>
        _values.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(raw) ? raw : fallback;

    public bool GetBool(string key, bool fallback = false) =>
        _values.TryGetValue(key, out var raw) && bool.TryParse(raw, out var value) ? value : fallback;

    public TEnum GetEnum<TEnum>(string key, TEnum fallback) where TEnum : struct =>
        _values.TryGetValue(key, out var raw) && Enum.TryParse<TEnum>(raw, ignoreCase: true, out var value) ? value : fallback;

    /// <summary>Todos los valores (para mostrarlos/serializarlos).</summary>
    public IReadOnlyDictionary<string, string> Values => _values;

    /// <summary>Copia con un valor reemplazado (los parametros son inmutables).</summary>
    public EffectActionParams With(string key, string value)
    {
        var copy = new Dictionary<string, string>(_values) { [key] = value };
        return new EffectActionParams(copy);
    }

    public static EffectActionParams Of(params (string Key, string Value)[] values) =>
        new(values.ToDictionary(v => v.Key, v => v.Value));
}
