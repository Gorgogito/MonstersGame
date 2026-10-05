using Microsoft.Xna.Framework;

namespace MonstersGame.Screens;

/// <summary>
/// Administra la pantalla activa y los cambios entre pantallas. La transicion se
/// aplica de forma diferida (al inicio del siguiente Update) para evitar mutar
/// la pantalla mientras se esta dibujando o actualizando.
/// </summary>
public sealed class ScreenManager
{
    private Screen? _current;
    private Screen? _pending;

    public Screen? Current => _current;

    /// <summary>Solicita cambiar a una nueva pantalla.</summary>
    public void Set(Screen screen) => _pending = screen;

    public void Update(GameTime gameTime)
    {
        if (_pending != null)
        {
            _current = _pending;
            _pending = null;
            _current.OnEnter();
        }
        _current?.Update(gameTime);
    }

    public void Draw(GameTime gameTime) => _current?.Draw(gameTime);
}
