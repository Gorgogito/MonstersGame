using Microsoft.Xna.Framework;

namespace MonstersGame.Screens;

/// <summary>
/// Clase base de una pantalla del juego. Cada pantalla encapsula su propia
/// logica de actualizacion y dibujo, manteniendo la UI desacoplada del motor.
/// </summary>
public abstract class Screen
{
    protected GameContext Ctx { get; }

    protected Screen(GameContext ctx) => Ctx = ctx;

    /// <summary>Se invoca cuando la pantalla pasa a ser la activa.</summary>
    public virtual void OnEnter() { }

    public abstract void Update(GameTime gameTime);
    public abstract void Draw(GameTime gameTime);
}
