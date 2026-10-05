using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MonstersGame.Input;

/// <summary>
/// Gestiona el estado de raton y teclado con deteccion de flancos (pulsaciones
/// nuevas en el fotograma actual). Centraliza la entrada para que las pantallas
/// no consulten directamente los dispositivos.
/// </summary>
public sealed class InputManager
{
    private MouseState _currentMouse, _previousMouse;
    private KeyboardState _currentKeyboard, _previousKeyboard;

    // La ventana puede redimensionarse, pero las pantallas siguen razonando
    // en el lienzo virtual fijo de 1280x720; esta transformacion convierte la
    // posicion real del mouse (en pixeles de ventana) a ese espacio virtual.
    private float _viewportScale = 1f;
    private Point _viewportOffset = Point.Zero;

    /// <summary>Define la escala/offset actuales del lienzo virtual dentro de la ventana real.</summary>
    public void SetViewportTransform(float scale, Point offset)
    {
        _viewportScale = scale > 0f ? scale : 1f;
        _viewportOffset = offset;
    }

    public void Update()
    {
        _previousMouse = _currentMouse;
        _currentMouse = Mouse.GetState();
        _previousKeyboard = _currentKeyboard;
        _currentKeyboard = Keyboard.GetState();
    }

    /// <summary>Posicion actual del puntero en pixeles del lienzo virtual (1280x720).</summary>
    public Point MousePosition => new(
        (int)((_currentMouse.Position.X - _viewportOffset.X) / _viewportScale),
        (int)((_currentMouse.Position.Y - _viewportOffset.Y) / _viewportScale));

    /// <summary>Verdadero el fotograma en que se presiona el boton izquierdo.</summary>
    public bool LeftClicked =>
        _currentMouse.LeftButton == ButtonState.Pressed &&
        _previousMouse.LeftButton == ButtonState.Released;

    /// <summary>Verdadero el fotograma en que se presiona el boton derecho.</summary>
    public bool RightClicked =>
        _currentMouse.RightButton == ButtonState.Pressed &&
        _previousMouse.RightButton == ButtonState.Released;

    /// <summary>Verdadero el fotograma en que se presiona la tecla indicada.</summary>
    public bool KeyPressed(Keys key) =>
        _currentKeyboard.IsKeyDown(key) && _previousKeyboard.IsKeyUp(key);

    /// <summary>Indica si un rectangulo contiene el puntero.</summary>
    public bool IsHovering(Rectangle rect) => rect.Contains(MousePosition);

    /// <summary>Verdadero si se hizo clic izquierdo dentro del rectangulo.</summary>
    public bool ClickedIn(Rectangle rect) => LeftClicked && rect.Contains(MousePosition);

    /// <summary>Verdadero si se hizo clic derecho dentro del rectangulo.</summary>
    public bool RightClickedIn(Rectangle rect) => RightClicked && rect.Contains(MousePosition);

    /// <summary>Cuanto giro la rueda del mouse este fotograma (positivo = hacia arriba/alejando).</summary>
    public int ScrollDelta => _currentMouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;
}
