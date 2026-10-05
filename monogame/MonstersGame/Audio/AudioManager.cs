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
            ["lose"] = SoundSynth.Arpeggio(new double[] { 349.2, 293.7, 246.9, 196.0 }, 0.15, WaveShape.Triangle, 0.45),

            // Fase 7 del plan de mejoras visuales: solo momentos que hoy no
            // tenian ningun sonido propio (destruccion en batalla, Fusion,
            // Ritual, impacto del choque) -- las claves de arriba ya suenan
            // desde el mecanismo viejo de diffing y no se tocan, para no
            // duplicar sonido.
            ["destroy_battle"] = SoundSynth.Tone(180, 40, 0.18, WaveShape.Noise, 0.45),
            ["fusion"] = SoundSynth.Arpeggio(new double[] { 392.0, 523.3, 659.3 }, 0.09, WaveShape.Sine, 0.4),
            ["ritual"] = SoundSynth.Arpeggio(new double[] { 220.0, 277.2, 329.6, 440.0 }, 0.11, WaveShape.Triangle, 0.45),
            ["impact"] = SoundSynth.Tone(90, 40, 0.08, WaveShape.Square, 0.5)
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
