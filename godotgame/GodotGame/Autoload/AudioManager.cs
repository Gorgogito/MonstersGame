using Godot;
using GodotGame.Audio;
using System.Collections.Generic;
using System.Linq;

namespace GodotGame;

/// <summary>
/// Autoload (ver project.godot) que reemplaza a <c>AudioManager</c> de
/// MonoGame: los mismos 12 efectos se sintetizan una sola vez al arrancar
/// (ver <see cref="SoundSynth"/>) y se reproducen por clave logica con
/// <see cref="PlaySfx"/>. A diferencia de <c>SoundEffect.Play()</c> de
/// MonoGame (que ya permite solapar instancias del mismo sonido sin gestion
/// manual), un <see cref="AudioStreamPlayer"/> de Godot corta el sonido
/// anterior si se le pide reproducir de nuevo mientras suena -- por eso se
/// usa un pool chico de reproductores en vez de uno solo, para que dos
/// eventos simultaneos (ej. dano a ambos jugadores en el mismo instante) no
/// se corten entre si.
/// </summary>
public partial class AudioManager : Node
{
    private const int PoolSize = 8;

    public float MasterVolume { get; set; } = 1f;

    private readonly Dictionary<string, AudioStreamWav> _sfx = new();
    private readonly List<AudioStreamPlayer> _players = new();

    public override void _Ready()
    {
        _sfx["summon"] = SoundSynth.Tone(300, 700, 0.18, WaveShape.Sine, 0.5);
        _sfx["set"] = SoundSynth.Tone(260, 180, 0.12, WaveShape.Triangle, 0.35);
        _sfx["attack"] = SoundSynth.Tone(500, 150, 0.12, WaveShape.Square, 0.4);
        _sfx["damage"] = SoundSynth.Tone(220, 60, 0.16, WaveShape.Noise, 0.5);
        _sfx["chain"] = SoundSynth.Tone(400, 900, 0.10, WaveShape.Triangle, 0.35);
        _sfx["error"] = SoundSynth.Tone(150, 150, 0.15, WaveShape.Square, 0.3);
        _sfx["win"] = SoundSynth.Arpeggio(new double[] { 261.6, 329.6, 392.0, 523.3 }, 0.11, WaveShape.Sine, 0.5);
        _sfx["lose"] = SoundSynth.Arpeggio(new double[] { 349.2, 293.7, 246.9, 196.0 }, 0.15, WaveShape.Triangle, 0.45);

        // Fase 7 del plan de mejoras visuales (MonoGame): momentos que antes
        // no tenian sonido propio.
        _sfx["destroy_battle"] = SoundSynth.Tone(180, 40, 0.18, WaveShape.Noise, 0.45);
        _sfx["fusion"] = SoundSynth.Arpeggio(new double[] { 392.0, 523.3, 659.3 }, 0.09, WaveShape.Sine, 0.4);
        _sfx["ritual"] = SoundSynth.Arpeggio(new double[] { 220.0, 277.2, 329.6, 440.0 }, 0.11, WaveShape.Triangle, 0.45);
        _sfx["impact"] = SoundSynth.Tone(90, 40, 0.08, WaveShape.Square, 0.5);

        // Contador de LP (un "tic" por paso mientras baja/sube) y banner de fase/turno.
        _sfx["lp_tick"] = SoundSynth.Tone(1250, 1250, 0.025, WaveShape.Square, 0.16);
        _sfx["banner"] = SoundSynth.Tone(320, 880, 0.16, WaveShape.Triangle, 0.28);

        for (int i = 0; i < PoolSize; i++)
        {
            var player = new AudioStreamPlayer();
            AddChild(player);
            _players.Add(player);
        }
    }

    /// <summary>Reproduce un efecto de sonido por clave logica. Claves invalidas se ignoran (defensivo, nunca interrumpe el juego).</summary>
    public void PlaySfx(string key)
    {
        if (!_sfx.TryGetValue(key, out var stream)) return;

        var player = _players.FirstOrDefault(p => !p.Playing) ?? _players[0];
        player.Stream = stream;
        player.VolumeDb = Mathf.LinearToDb(Mathf.Clamp(MasterVolume, 0.0001f, 1f));
        player.Play();
    }
}
