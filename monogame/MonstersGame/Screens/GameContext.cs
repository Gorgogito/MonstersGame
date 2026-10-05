using Microsoft.Xna.Framework.Graphics;
using MonstersGame.Audio;
using MonstersGame.Data;
using MonstersGame.Graphics;
using MonstersGame.Input;

namespace MonstersGame.Screens;

/// <summary>
/// Servicios compartidos disponibles para todas las pantallas. Se inyecta una
/// unica instancia para evitar acoplar las pantallas al objeto Game.
/// </summary>
public sealed class GameContext
{
    public required GraphicsDevice GraphicsDevice { get; init; }
    public required SpriteBatch SpriteBatch { get; init; }
    public required Primitives Primitives { get; init; }
    public required BitmapFont Font { get; init; }
    public required IconAtlas Icons { get; init; }
    public required TextureCache Textures { get; init; }
    public required CardRenderer CardRenderer { get; init; }
    public required ParticleSystem Particles { get; init; }
    public required ScreenShake Shake { get; init; }
    public required ScreenFlash Flash { get; init; }
    public required CameraPunch Camera { get; init; }
    public required InputManager Input { get; init; }
    public required AudioManager Audio { get; init; }
    public required GameData Data { get; init; }
    public required ScreenManager Screens { get; init; }
    public required Random Random { get; init; }
    public required Action RequestExit { get; init; }

    public int ScreenWidth { get; set; }
    public int ScreenHeight { get; set; }
}
