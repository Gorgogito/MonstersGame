using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonstersGame.Audio;
using MonstersGame.Data;
using MonstersGame.Graphics;
using MonstersGame.Input;
using MonstersGame.Screens;
using MonstersGame.Screens.MainMenu;

namespace MonstersGame;

/// <summary>
/// Clase principal de MonoGame. Su unica responsabilidad es inicializar los
/// subsistemas (graficos, entrada, audio, datos), construir el contexto
/// compartido y delegar el bucle de juego al <see cref="ScreenManager"/>. Toda
/// la logica de juego vive fuera de aqui (Core), respetando la separacion entre
/// motor y presentacion.
/// </summary>
public sealed class MonstersGameApp : Microsoft.Xna.Framework.Game
{
    private const int ScreenWidth = 1280;
    private const int ScreenHeight = 720;

    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private RenderTarget2D _sceneTarget = null!;

    private InputManager _input = null!;
    private ScreenManager _screens = null!;
    private GameContext _context = null!;

    public MonstersGameApp()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = ScreenWidth,
            PreferredBackBufferHeight = ScreenHeight
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.Title = "Monsters Game - Duelo de Monstruos";
        Window.AllowUserResizing = true;
        Window.ClientSizeChanged += (_, _) => SyncBackBufferToWindow();
    }

    /// <summary>
    /// La ventana es redimensionable/maximizable, pero toda la logica de las
    /// pantallas sigue dibujando en un lienzo virtual fijo de 1280x720 (ver
    /// Draw). Este metodo solo mantiene el back buffer al tamano real de la
    /// ventana para que ese lienzo se pueda escalar sin distorsion.
    /// </summary>
    private void SyncBackBufferToWindow()
    {
        var bounds = Window.ClientBounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        if (_graphics.PreferredBackBufferWidth == bounds.Width &&
            _graphics.PreferredBackBufferHeight == bounds.Height) return;

        _graphics.PreferredBackBufferWidth = bounds.Width;
        _graphics.PreferredBackBufferHeight = bounds.Height;
        _graphics.ApplyChanges();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _sceneTarget = new RenderTarget2D(GraphicsDevice, ScreenWidth, ScreenHeight);

        var primitives = new Primitives(GraphicsDevice);
        var font = new BitmapFont(GraphicsDevice);
        var icons = new IconAtlas(GraphicsDevice);
        var textures = new TextureCache(GraphicsDevice, Path.Combine(AppContext.BaseDirectory, "Data", "Art"));
        var cardRenderer = new CardRenderer(primitives, font, icons, textures);
        var particles = new ParticleSystem();
        var shake = new ScreenShake();
        var flash = new ScreenFlash();
        var camera = new CameraPunch();
        _input = new InputManager();
        var audio = new AudioManager();

        // Carga de datos externos (cartas, mazos, fusiones).
        var data = GameData.LoadFromDisk();

        _screens = new ScreenManager();
        _context = new GameContext
        {
            GraphicsDevice = GraphicsDevice,
            SpriteBatch = _spriteBatch,
            Primitives = primitives,
            Font = font,
            Icons = icons,
            Textures = textures,
            CardRenderer = cardRenderer,
            Particles = particles,
            Shake = shake,
            Flash = flash,
            Camera = camera,
            Input = _input,
            Audio = audio,
            Data = data,
            Screens = _screens,
            Random = new Random(),
            RequestExit = Exit,
            ScreenWidth = ScreenWidth,
            ScreenHeight = ScreenHeight
        };

        _screens.Set(new MainMenuScreen(_context));
    }

    protected override void Update(GameTime gameTime)
    {
        var dest = ComputeLetterboxRect();
        float scale = dest.Width > 0 ? (float)dest.Width / ScreenWidth : 1f;
        _input.SetViewportTransform(scale, new Point(dest.X, dest.Y));

        _input.Update();
        _screens.Update(gameTime);
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _context.Particles.Update(dt);
        _context.Shake.Update(dt);
        _context.Flash.Update(dt);
        _context.Camera.Update(dt);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        // 1) Se dibuja todo el juego al lienzo virtual fijo (1280x720), igual
        //    que antes: ninguna pantalla necesita saber el tamano real de la
        //    ventana.
        GraphicsDevice.SetRenderTarget(_sceneTarget);
        GraphicsDevice.Clear(Theme.Background);

        // PointClamp mantiene nitidos los pixeles de la fuente al escalar.
        // El offset de Shake se aplica aca como traslacion de todo el lote, y
        // el zoom de Camera como escala alrededor de un foco (Fase B del plan
        // de adaptaciones de esfuerzo medio): en reposo ambos son la
        // identidad, sin ningun efecto (mismo principio que el Flash de mas
        // abajo).
        var shakeOffset = _context.Shake.CurrentOffset;
        var cameraTransform = _context.Camera.GetTransform();
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp,
            transformMatrix: cameraTransform * Matrix.CreateTranslation(shakeOffset.X, shakeOffset.Y, 0f));
        _screens.Draw(gameTime);
        _context.Particles.Draw(_spriteBatch, _context.Primitives);
        // Destello de pantalla completa (Fase 6): en reposo Alpha=0, FillRectAlpha
        // ya se lo salta sin dibujar nada.
        _context.Primitives.FillRectAlpha(_spriteBatch, new Rectangle(0, 0, ScreenWidth, ScreenHeight),
            _context.Flash.CurrentColor, _context.Flash.CurrentAlpha);
        _spriteBatch.End();

        // 2) El lienzo se vuelca a la ventana real, escalado y centrado
        //    (letterbox) para conservar la proporcion sin deformar el
        //    contenido, sea cual sea el tamano de la ventana.
        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);

        var dest = ComputeLetterboxRect();
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _spriteBatch.Draw(_sceneTarget, dest, Color.White);
        _spriteBatch.End();

        base.Draw(gameTime);
    }

    /// <summary>
    /// Rectangulo (en coordenadas de back buffer) donde se dibuja el lienzo
    /// virtual de 1280x720 dentro de la ventana actual, escalado al maximo
    /// que entre sin recortar y centrado (barras negras si la proporcion de
    /// la ventana no coincide con 16:9).
    /// </summary>
    private Rectangle ComputeLetterboxRect()
    {
        int bbWidth = GraphicsDevice.PresentationParameters.BackBufferWidth;
        int bbHeight = GraphicsDevice.PresentationParameters.BackBufferHeight;
        if (bbWidth <= 0 || bbHeight <= 0) return new Rectangle(0, 0, ScreenWidth, ScreenHeight);

        float scale = Math.Min((float)bbWidth / ScreenWidth, (float)bbHeight / ScreenHeight);
        scale = Math.Max(scale, 0.01f);

        int destW = (int)(ScreenWidth * scale);
        int destH = (int)(ScreenHeight * scale);
        int offsetX = (bbWidth - destW) / 2;
        int offsetY = (bbHeight - destH) / 2;
        return new Rectangle(offsetX, offsetY, destW, destH);
    }
}
