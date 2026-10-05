using Godot;
using GodotGame.Audio;
using System;
using System.Collections.Generic;

namespace GodotGame;

/// <summary>
/// Autoload de musica de fondo: una pista por momento del juego ("menu",
/// "duel", "critical", "victory", "defeat") con fundido cruzado entre ellas.
/// Si existe <c>res://Data/Music/&lt;clave&gt;.ogg</c> se usa ese archivo (asi se
/// puede poner musica propia o con licencia libre sin tocar codigo); si no,
/// la version sintetizada de <see cref="MusicSynth"/>.
/// </summary>
public partial class MusicManager : Node
{
    private const double FadeSeconds = 0.8;
    private static readonly HashSet<string> Jingles = new() { "victory", "defeat" };

    /// <summary>Volumen de la musica (0-1), aparte de los efectos.</summary>
    public float Volume { get; set; } = 0.45f;

    private readonly Dictionary<string, AudioStream> _cache = new();
    private AudioStreamPlayer _a = null!, _b = null!;
    private AudioStreamPlayer _current = null!;
    private string? _currentKey;

    public override void _Ready()
    {
        _a = new AudioStreamPlayer();
        _b = new AudioStreamPlayer();
        AddChild(_a);
        AddChild(_b);
        _current = _a;
    }

    /// <summary>Cambia a la pista <paramref name="key"/> con fundido; no hace nada si ya esta sonando.</summary>
    public void Play(string key)
    {
        if (key == _currentKey && _current.Playing) return;
        var stream = StreamFor(key);
        if (stream == null) return;
        _currentKey = key;

        var previous = _current;
        _current = previous == _a ? _b : _a;
        _current.Stream = stream;
        _current.VolumeDb = -60f;
        _current.Play();

        var tween = CreateTween().SetParallel();
        tween.TweenMethod(Callable.From<float>(v => _current.VolumeDb = Mathf.LinearToDb(Mathf.Max(v, 0.0001f))), 0f, Volume, FadeSeconds);
        if (previous.Playing)
        {
            float from = Mathf.DbToLinear(previous.VolumeDb);
            tween.TweenMethod(Callable.From<float>(v => previous.VolumeDb = Mathf.LinearToDb(Mathf.Max(v, 0.0001f))), from, 0f, FadeSeconds);
            tween.Chain().TweenCallback(Callable.From(previous.Stop));
        }
    }

    public void Stop()
    {
        _currentKey = null;
        var player = _current;
        var tween = CreateTween();
        tween.TweenMethod(Callable.From<float>(v => player.VolumeDb = Mathf.LinearToDb(Mathf.Max(v, 0.0001f))), Mathf.DbToLinear(player.VolumeDb), 0f, FadeSeconds);
        tween.TweenCallback(Callable.From(player.Stop));
    }

    private AudioStream? StreamFor(string key)
    {
        if (_cache.TryGetValue(key, out var cached)) return cached;

        AudioStream? stream = null;
        string ogg = ProjectSettings.GlobalizePath($"res://Data/Music/{key}.ogg");
        if (System.IO.File.Exists(ogg))
        {
            var vorbis = AudioStreamOggVorbis.LoadFromFile(ogg);
            if (vorbis != null)
            {
                vorbis.Loop = !Jingles.Contains(key);
                stream = vorbis;
            }
        }

        stream ??= key switch
        {
            "menu" => MusicSynth.Menu(),
            "duel" => MusicSynth.Duel(),
            "critical" => MusicSynth.Critical(),
            "victory" => MusicSynth.Victory(),
            "defeat" => MusicSynth.Defeat(),
            _ => null
        };
        if (stream != null) _cache[key] = stream;
        return stream;
    }
}
