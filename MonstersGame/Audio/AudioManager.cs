using Microsoft.Xna.Framework.Audio;

namespace MonstersGame.Audio;

/// <summary>
/// Gestor de audio. Los efectos de sonido se sintetizan por codigo al
/// construirse (ver <see cref="SoundSynth"/>) — no hay archivos de audio
/// externos ni musica de fondo: sintetizar musica con calidad razonable no es
/// viable por codigo, asi que <see cref="PlayMusic"/>/<see cref="StopMusic"/>
/// siguen siendo no-op a proposito (decision de alcance del Bloque 10).
/// </summary>
public sealed class AudioManager
{
    public float MasterVolume { get; set; } = 1f;

    private readonly Dictionary<string, SoundEffect> _sfx;

    public AudioManager()
    {
        _sfx = new Dictionary<string, SoundEffect>
        {
            ["summon"] = SoundSynth.Tone(300, 700, 0.18, WaveShape.Sine, 0.5),
            ["set"] = SoundSynth.Tone(260, 180, 0.12, WaveShape.Triangle, 0.35),
            ["attack"] = SoundSynth.Tone(500, 150, 0.12, WaveShape.Square, 0.4),
            ["damage"] = SoundSynth.Tone(220, 60, 0.16, WaveShape.Noise, 0.5),
            ["chain"] = SoundSynth.Tone(400, 900, 0.10, WaveShape.Triangle, 0.35),
            ["error"] = SoundSynth.Tone(150, 150, 0.15, WaveShape.Square, 0.3),
            ["win"] = SoundSynth.Arpeggio(new double[] { 261.6, 329.6, 392.0, 523.3 }, 0.11, WaveShape.Sine, 0.5),
            ["lose"] = SoundSynth.Arpeggio(new double[] { 349.2, 293.7, 246.9, 196.0 }, 0.15, WaveShape.Triangle, 0.45)
        };
    }

    /// <summary>Reproduce un efecto de sonido por clave logica. Claves invalidas se ignoran (defensivo, nunca interrumpe el juego).</summary>
    public void PlaySfx(string key)
    {
        if (_sfx.TryGetValue(key, out var effect))
            effect.Play(MasterVolume, 0f, 0f);
    }

    /// <summary>Reproduce/cambia la musica de fondo. No-op: ver el resumen de la clase.</summary>
    public void PlayMusic(string key)
    {
    }

    public void StopMusic()
    {
    }
}
