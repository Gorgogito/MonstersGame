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
        _values.TryGetValue(key, out var raw) ? raw : fallback;
}
