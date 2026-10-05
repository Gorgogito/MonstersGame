using Godot;
using System;

namespace GodotGame.Audio;

/// <summary>Forma de onda base de un tono sintetizado.</summary>
public enum WaveShape
{
    Sine,
    Square,
    Triangle,
    Noise
}

/// <summary>
/// Sintetiza efectos de sonido cortos como PCM en tiempo de ejecucion, sin
/// archivos de audio externos -- misma filosofia que <c>Audio/SoundSynth.cs</c>
/// de MonoGame (mismas formulas, mismo muestreo), adaptada al tipo nativo de
/// Godot para audio pre-generado (<see cref="AudioStreamWav"/>). No se usa
/// <c>AudioStreamGenerator</c>: esa clase esta pensada para audio continuo
/// alimentado en vivo cuadro a cuadro, y un efecto corto de duracion fija
/// generado una sola vez encaja mejor como <see cref="AudioStreamWav"/> --
/// el equivalente exacto de <c>SoundEffect(buffer, sampleRate, channels)</c>.
/// No compone musica (mismo alcance que el original): sintetizar musica con
/// calidad razonable no es viable por codigo.
/// </summary>
public static class SoundSynth
{
    private const int SampleRate = 22050;

    /// <summary>Un tono con barrido de frecuencia (constante si <paramref name="startFreq"/> == <paramref name="endFreq"/>).</summary>
    public static AudioStreamWav Tone(double startFreq, double endFreq, double durationSeconds, WaveShape shape, double volume = 0.5)
    {
        var buffer = new byte[SampleCount(durationSeconds) * 2];
        WriteNote(buffer, 0, startFreq, endFreq, durationSeconds, shape, volume);
        return Build(buffer);
    }

    /// <summary>Varias notas consecutivas en un unico efecto (p. ej. un arpegio corto de victoria/derrota).</summary>
    public static AudioStreamWav Arpeggio(double[] frequencies, double noteDurationSeconds, WaveShape shape, double volume = 0.5)
    {
        int samplesPerNote = SampleCount(noteDurationSeconds);
        var buffer = new byte[samplesPerNote * frequencies.Length * 2];

        for (int n = 0; n < frequencies.Length; n++)
            WriteNote(buffer, n * samplesPerNote, frequencies[n], frequencies[n], noteDurationSeconds, shape, volume);

        return Build(buffer);
    }

    private static AudioStreamWav Build(byte[] buffer) => new()
    {
        Data = buffer,
        Format = AudioStreamWav.FormatEnum.Format16Bits,
        MixRate = SampleRate,
        Stereo = false
    };

    private static int SampleCount(double durationSeconds) => (int)(SampleRate * durationSeconds);

    private static void WriteNote(byte[] buffer, int sampleOffset, double startFreq, double endFreq,
        double durationSeconds, WaveShape shape, double volume)
    {
        int count = SampleCount(durationSeconds);
        var random = shape == WaveShape.Noise ? new Random(unchecked(startFreq.GetHashCode() * 31 + count)) : null;

        for (int i = 0; i < count; i++)
        {
            double t = i / (double)SampleRate;
            double progress = i / (double)count;

            // Fase integrada (no solo 2*pi*f*t) para que un barrido de
            // frecuencia no produzca un "click" por discontinuidad de fase.
            double phase = 2 * Math.PI * (startFreq * t + (endFreq - startFreq) * t * t / (2 * durationSeconds));

            double raw = shape switch
            {
                WaveShape.Sine => Math.Sin(phase),
                WaveShape.Square => Math.Sign(Math.Sin(phase)),
                WaveShape.Triangle => 2.0 / Math.PI * Math.Asin(Math.Sin(phase)),
                WaveShape.Noise => random!.NextDouble() * 2 - 1,
                _ => Math.Sin(phase)
            };

            double envelope = Envelope(progress);
            short sample = (short)Math.Clamp(raw * envelope * volume * short.MaxValue, short.MinValue, short.MaxValue);

            int byteIndex = (sampleOffset + i) * 2;
            buffer[byteIndex] = (byte)(sample & 0xFF);
            buffer[byteIndex + 1] = (byte)((sample >> 8) & 0xFF);
        }
    }

    /// <summary>Ataque rapido y caida suave, para que ninguna nota empiece/termine con un "click" audible.</summary>
    private static double Envelope(double progress)
    {
        const double attack = 0.06, release = 0.35;
        if (progress < attack) return progress / attack;
        if (progress > 1 - release) return Math.Max(0, (1 - progress) / release);
        return 1.0;
    }
}
