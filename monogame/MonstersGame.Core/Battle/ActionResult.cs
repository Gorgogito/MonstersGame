namespace MonstersGame.Core.Battle;

/// <summary>
/// Resultado de una accion solicitada al motor. Evita lanzar excepciones para
/// el control de flujo: la UI/IA consultan <see cref="Success"/> y muestran
/// <see cref="Message"/> cuando una jugada es ilegal.
/// </summary>
public readonly struct ActionResult
{
    public bool Success { get; }
    public string Message { get; }

    private ActionResult(bool success, string message)
    {
        Success = success;
        Message = message;
    }

    public static ActionResult Ok(string message = "") => new(true, message);
    public static ActionResult Fail(string message) => new(false, message);
}
