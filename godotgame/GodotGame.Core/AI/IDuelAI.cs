using GodotGame.Core.Battle;

namespace GodotGame.Core.AI;

/// <summary>
/// Contrato de una inteligencia artificial de duelo. La IA actua por "pasos"
/// para que la UI pueda animar/espaciar las jugadas. Depende exclusivamente de
/// la API publica del <see cref="DuelEngine"/>, por lo que esta totalmente
/// desacoplada del motor y puede mejorarse sin tocar la logica de reglas.
/// </summary>
public interface IDuelAI
{
    /// <summary>
    /// Ejecuta una unica decision (invocar, fusionar, atacar, cambiar de fase...).
    /// Devuelve <c>true</c> si la IA desea continuar actuando en el mismo turno,
    /// o <c>false</c> cuando ha terminado su turno.
    /// </summary>
    bool Step(DuelEngine engine, int selfIndex);
}
