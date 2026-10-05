using Godot;
using System;
using System.Collections.Generic;

namespace GodotGame.Audio;

/// <summary>
/// Musica de fondo generada por codigo (sin archivos, igual que
/// <see cref="SoundSynth"/> para los efectos): un secuenciador minimo que
/// arma un loop a partir de una progresion de acordes, con bajo, arpegio,
/// colchon armonico, una melodia opcional y bateria sintetizada. Cada pista
/// sale como un <see cref="AudioStreamWav"/> con loop, lista para reproducir.
/// Si hay un <c>.ogg</c> propio en <c>Data/Music</c>, <c>MusicManager</c>
/// lo prefiere a estas.
/// </summary>
public static class MusicSynth
{
    private const int SampleRate = 22050;

    /// <summary>Descripcion de una pista: tempo, acordes (notas MIDI, un acorde por compas) y que capas lleva.</summary>
    private sealed record Track(
        double Bpm,
        int[][] Chords,
        bool Drums,
        bool DrivingBass,
        bool Pad,
        float ArpVolume,
        int[]? Melody = null,          // una nota MIDI por corchea; -1 = silencio
        bool Loop = true,
        int Repeats = 1);

    public static AudioStreamWav Menu() => Render(new Track(
        Bpm: 84,
        Chords: new[] { Am, F, C, G, Am, F, Dm, E },
        Drums: false, DrivingBass: false, Pad: true, ArpVolume: 0.10f,
        Melody: new[]
        {
            76, -1, 74, 72, 71, -1, 72, -1,   77, -1, 76, 74, 72, -1, -1, -1,
            76, -1, 79, 77, 76, -1, 74, -1,   74, -1, 72, 71, 72, -1, -1, -1,
            76, -1, 74, 72, 71, -1, 72, -1,   77, -1, 76, 74, 72, -1, -1, -1,
            74, -1, 76, 77, 76, -1, 74, -1,   71, -1, 68, -1, 71, -1, -1, -1,
        }));

    public static AudioStreamWav Duel() => Render(new Track(
        Bpm: 132,
        Chords: new[] { Dm, Dm, Bb, Bb, C, C, A, A },
        Drums: true, DrivingBass: true, Pad: false, ArpVolume: 0.08f,
        Melody: new[]
        {
            74, -1, 74, 76, 77, -1, 76, 74,   72, -1, 69, -1, -1, -1, -1, -1,
            74, -1, 74, 76, 77, -1, 79, 81,   82, -1, 81, -1, 77, -1, -1, -1,
            79, -1, 79, 77, 76, -1, 74, 72,   76, -1, 77, -1, 79, -1, -1, -1,
            81, -1, 79, 77, 76, -1, 73, -1,   69, -1, 73, -1, 76, -1, -1, -1,
        }));

    public static AudioStreamWav Critical() => Render(new Track(
        Bpm: 150,
        Chords: new[] { Em, Em, C, C, B7, B7, Em, Edim },
        Drums: true, DrivingBass: true, Pad: true, ArpVolume: 0.09f,
        Melody: new[]
        {
            76, 75, 76, -1, 79, -1, 76, -1,   76, 75, 76, -1, 71, -1, -1, -1,
            72, 71, 72, -1, 76, -1, 72, -1,   79, -1, 77, -1, 76, -1, -1, -1,
            75, -1, 78, -1, 81, -1, 78, -1,   75, -1, 71, -1, 75, -1, -1, -1,
            76, -1, 79, -1, 76, -1, 71, -1,   70, -1, 67, -1, 64, -1, -1, -1,
        }));

    public static AudioStreamWav Victory() => Render(new Track(
        Bpm: 120,
        Chords: new[] { C, F, G, C },
        Drums: true, DrivingBass: false, Pad: true, ArpVolume: 0.12f,
        Melody: new[] { 72, 76, 79, 84, -1, 79, 84, -1,   81, -1, 77, -1, 81, 84, -1, -1,   79, -1, 83, -1, 86, -1, 83, -1,   84, -1, -1, -1, -1, -1, -1, -1 },
        Loop: false));

    public static AudioStreamWav Defeat() => Render(new Track(
        Bpm: 72,
        Chords: new[] { Am, Dm, E, Am },
        Drums: false, DrivingBass: false, Pad: true, ArpVolume: 0.08f,
        Melody: new[] { 76, -1, 74, -1, 72, -1, 71, -1,   69, -1, 65, -1, 62, -1, -1, -1,   68, -1, 71, -1, 74, -1, 71, -1,   69, -1, -1, -1, -1, -1, -1, -1 },
        Loop: false));

    // Acordes (notas MIDI, voicing en la octava central).
    private static readonly int[] Am = { 57, 60, 64 }, F = { 53, 57, 60 }, C = { 48, 52, 55 }, G = { 55, 59, 62 };
    private static readonly int[] Dm = { 50, 53, 57 }, E = { 52, 56, 59 }, Bb = { 46, 50, 53 }, A = { 45, 49, 52 };
    private static readonly int[] Em = { 52, 55, 59 }, B7 = { 47, 51, 54, 57 }, Edim = { 52, 55, 58 };

    private enum Wave { Sine, Triangle, Square, Pulse }

    private static AudioStreamWav Render(Track track)
    {
        double beat = 60.0 / track.Bpm;
        int samplesPerBeat = (int)(beat * SampleRate);
        int barSamples = samplesPerBeat * 4;
        int total = barSamples * track.Chords.Length * track.Repeats + (track.Loop ? 0 : samplesPerBeat * 2);
        var mix = new float[total];

        for (int rep = 0; rep < track.Repeats; rep++)
        for (int bar = 0; bar < track.Chords.Length; bar++)
        {
            int barStart = (rep * track.Chords.Length + bar) * barSamples;
            var chord = track.Chords[bar];
            int root = chord[0];

            // Colchon: el acorde sostenido todo el compas, suave.
            if (track.Pad)
                foreach (int note in chord)
                    AddNote(mix, barStart, barSamples, Freq(note + 12), Wave.Sine, 0.045f, attack: 0.15, release: 0.3);

            // Bajo: negras tranquilas, o corcheas insistentes (raiz/raiz/quinta/raiz...).
            if (track.DrivingBass)
            {
                for (int e = 0; e < 8; e++)
                {
                    int note = root - 12 + (e % 4 == 3 ? 7 : 0);
                    AddNote(mix, barStart + e * samplesPerBeat / 2, samplesPerBeat / 2, Freq(note), Wave.Triangle, 0.22f, attack: 0.005, release: 0.4);
                }
            }
            else
            {
                AddNote(mix, barStart, samplesPerBeat * 2, Freq(root - 12), Wave.Triangle, 0.2f, attack: 0.01, release: 0.5);
                AddNote(mix, barStart + samplesPerBeat * 2, samplesPerBeat * 2, Freq(root - 12 + 7), Wave.Triangle, 0.16f, attack: 0.01, release: 0.5);
            }

            // Arpegio en semicorcheas recorriendo el acorde en dos octavas.
            if (track.ArpVolume > 0)
            {
                var tones = new List<int>();
                foreach (int n in chord) tones.Add(n + 12);
                foreach (int n in chord) tones.Add(n + 24);
                for (int s = 0; s < 16; s++)
                {
                    int idx = s % (tones.Count * 2 - 2);
                    if (idx >= tones.Count) idx = tones.Count * 2 - 2 - idx; // sube y baja
                    AddNote(mix, barStart + s * samplesPerBeat / 4, samplesPerBeat / 4, Freq(tones[idx]), Wave.Pulse, track.ArpVolume, attack: 0.003, release: 0.6);
                }
            }

            // Bateria: bombo en 1 y 3, caja en 2 y 4, platillo en cada corchea.
            if (track.Drums)
            {
                for (int b = 0; b < 4; b++)
                {
                    int at = barStart + b * samplesPerBeat;
                    if (b % 2 == 0) AddKick(mix, at);
                    else AddNoise(mix, at, (int)(0.12 * SampleRate), 0.16f, decay: 18, seed: bar * 7 + b);
                    AddNoise(mix, at, (int)(0.03 * SampleRate), 0.05f, decay: 90, seed: bar * 13 + b);
                    AddNoise(mix, at + samplesPerBeat / 2, (int)(0.03 * SampleRate), 0.04f, decay: 90, seed: bar * 17 + b);
                }
            }
        }

        // Melodia: una nota por corchea, ligada si la siguiente es silencio.
        if (track.Melody != null)
        {
            int eighth = samplesPerBeat / 2;
            int loopLength = track.Chords.Length * barSamples;
            for (int rep = 0; rep < track.Repeats; rep++)
            for (int i = 0; i < track.Melody.Length; i++)
            {
                int note = track.Melody[i];
                if (note < 0) continue;
                int length = 1;
                while (i + length < track.Melody.Length && track.Melody[i + length] < 0 && length < 4) length++;
                int start = rep * loopLength + i * eighth;
                if (start >= total) break;
                AddNote(mix, start, Math.Min(length * eighth, total - start), Freq(note), Wave.Square, 0.075f, attack: 0.01, release: 0.35, vibrato: true);
            }
        }

        return Build(mix, track.Loop);
    }

    private static double Freq(int midi) => 440.0 * Math.Pow(2, (midi - 69) / 12.0);

    private static void AddNote(float[] mix, int start, int length, double freq, Wave wave, float volume,
        double attack, double release, bool vibrato = false)
    {
        int attackSamples = Math.Max(1, (int)(attack * SampleRate));
        int releaseSamples = Math.Max(1, (int)(length * release));
        double phase = 0;
        for (int i = 0; i < length && start + i < mix.Length; i++)
        {
            double t = i / (double)SampleRate;
            double f = vibrato && t > 0.15 ? freq * (1 + 0.004 * Math.Sin(2 * Math.PI * 5.5 * t)) : freq;
            phase += f / SampleRate;
            phase -= Math.Floor(phase);

            double raw = wave switch
            {
                Wave.Sine => Math.Sin(2 * Math.PI * phase),
                Wave.Triangle => 1 - 4 * Math.Abs(phase - 0.5),
                Wave.Square => phase < 0.5 ? 0.6 : -0.6,
                Wave.Pulse => phase < 0.25 ? 0.6 : -0.6,
                _ => 0
            };

            double env = 1;
            if (i < attackSamples) env = i / (double)attackSamples;
            else if (i > length - releaseSamples) env = Math.Max(0, (length - i) / (double)releaseSamples);
            mix[start + i] += (float)(raw * env * volume);
        }
    }

    private static void AddKick(float[] mix, int start)
    {
        int length = (int)(0.16 * SampleRate);
        double phase = 0;
        for (int i = 0; i < length && start + i < mix.Length; i++)
        {
            double p = i / (double)length;
            phase += (130 - 85 * p) / SampleRate;
            mix[start + i] += (float)(Math.Sin(2 * Math.PI * phase) * Math.Pow(1 - p, 2) * 0.32);
        }
    }

    private static void AddNoise(float[] mix, int start, int length, float volume, double decay, int seed)
    {
        var rng = new Random(seed);
        for (int i = 0; i < length && start + i < mix.Length; i++)
            mix[start + i] += (float)((rng.NextDouble() * 2 - 1) * Math.Exp(-decay * i / (double)SampleRate) * volume);
    }

    private static AudioStreamWav Build(float[] mix, bool loop)
    {
        float peak = 0.0001f;
        foreach (float s in mix) peak = Math.Max(peak, Math.Abs(s));
        float gain = Math.Min(1f, 0.85f / peak);

        var data = new byte[mix.Length * 2];
        for (int i = 0; i < mix.Length; i++)
        {
            short sample = (short)Math.Clamp(mix[i] * gain * short.MaxValue, short.MinValue, short.MaxValue);
            data[i * 2] = (byte)(sample & 0xFF);
            data[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }

        return new AudioStreamWav
        {
            Data = data,
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = SampleRate,
            Stereo = false,
            LoopMode = loop ? AudioStreamWav.LoopModeEnum.Forward : AudioStreamWav.LoopModeEnum.Disabled,
            LoopBegin = 0,
            LoopEnd = loop ? mix.Length : 0
        };
    }
}
