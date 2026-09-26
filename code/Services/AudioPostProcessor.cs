// code/Services/AudioPostProcessor.cs
using System;
using System.IO;
namespace Leron.Audio.Services;

/// Постобработка WAV записи (16 кГц моно 16 бит PCM):
/// high-pass ~80 Гц, мягкий noise-gate, peak-нормализация к -1 dBFS.
/// Чужой/битый формат → false, вызывающий пропускает обработку.
public static class AudioPostProcessor
{
    private const int SampleRate = 16000;

    /// Пик ниже этого уровня (~-42 dBFS) — цифровая тишина или очень тихий шум.
    private const float SilentPeak = 0.008f;
    /// Меньше этой доли кадров над шумовым полом — речи в записи нет.
    private const float SilentVoicedRatio = 0.05f;

    /// Метрики уровня записи для проверки тишины перед распознаванием.
    public sealed record AudioLevel(float Peak, float Rms, float VoicedRatio);

    /// true — в записи нет речи: тишина или ровный фоновый гул.
    /// Пик ловит цифровую тишину, VoicedRatio — ровный шум (вентилятор),
    /// который пиковым порогом не берётся.
    public static bool IsSilent(AudioLevel level) =>
        level.Peak < SilentPeak || level.VoicedRatio < SilentVoicedRatio;

    /// Замер пика/RMS/доли голосовых кадров WAV ДО любой обработки
    /// (нормализация вытянула бы тишину к -1 dBFS и исказила картину).
    /// Чужой/битый формат → null, вызывающий пропускает guard.
    public static AudioLevel? Measure(string wavPath)
    {
        try
        {
            var all = File.ReadAllBytes(wavPath);
            if (!TryLocatePcm(all, out int dataOffset, out int dataLength)) return null;
            int count = dataLength / 2;
            var samples = new float[count];
            float peak = 0f;
            double sum = 0;
            for (int i = 0; i < count; i++)
            {
                float v = BitConverter.ToInt16(all, dataOffset + i * 2) / 32768f;
                samples[i] = v;
                float a = MathF.Abs(v);
                if (a > peak) peak = a;
                sum += (double)v * v;
            }
            float rms = (float)Math.Sqrt(sum / Math.Max(1, count));
            return new AudioLevel(peak, rms, ComputeVoicedRatio(samples));
        }
        catch
        {
            return null;
        }
    }

    public static bool TryProcess(string wavPath, bool noiseGate, bool normalize)
    {
        if (!noiseGate && !normalize) return false;
        try
        {
            var all = File.ReadAllBytes(wavPath);
            if (!TryLocatePcm(all, out int dataOffset, out int dataLength)) return false;
            int count = dataLength / 2;
            var samples = new float[count];
            for (int i = 0; i < count; i++)
                samples[i] = BitConverter.ToInt16(all, dataOffset + i * 2) / 32768f;
            ApplyHighPass(samples);
            if (noiseGate) ApplyGate(samples);
            if (normalize) ApplyPeakNormalize(samples);
            for (int i = 0; i < count; i++)
            {
                short v = (short)Math.Clamp((int)MathF.Round(samples[i] * 32767f), short.MinValue, short.MaxValue);
                all[dataOffset + i * 2] = (byte)(v & 0xFF);
                all[dataOffset + i * 2 + 1] = (byte)((v >> 8) & 0xFF);
            }
            File.WriteAllBytes(wavPath, all);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// Разбор RIFF/WAVE: находит data-чанк PCM 16 бит моно 16 кГц. Чужой формат → false.
    private static bool TryLocatePcm(byte[] all, out int dataOffset, out int dataLength)
    {
        dataOffset = -1;
        dataLength = 0;
        if (all.Length < 44) return false;
        if (ReadU32(all, 0) != 0x46464952u) return false; // "RIFF"
        if (ReadU32(all, 8) != 0x45564157u) return false; // "WAVE"
        int pos = 12;
        short channels = 0, bits = 0;
        int rate = 0;
        while (pos + 8 <= all.Length)
        {
            uint id = ReadU32(all, pos);
            int size = (int)ReadU32(all, pos + 4);
            int body = pos + 8;
            if (id == 0x20746D66u) // "fmt "
            {
                if (body + 16 > all.Length) return false;
                short format = (short)ReadU16(all, body);
                channels = (short)ReadU16(all, body + 2);
                rate = (int)ReadU32(all, body + 4);
                bits = (short)ReadU16(all, body + 14);
                if (format != 1) return false; // только PCM
            }
            else if (id == 0x61746164u) // "data"
            {
                dataOffset = body;
                dataLength = size;
                break;
            }
            pos = body + size + (size & 1);
        }
        if (dataOffset < 0 || channels != 1 || bits != 16 || rate != SampleRate) return false;
        if (dataLength < 4 || dataOffset + dataLength > all.Length) return false;
        return true;
    }

    /// Доля 20 мс-кадров с RMS выше порога от шумового пола (нижние 10% кадров).
    /// Та же логика порога, что в ApplyGate: речь даёт десятки процентов
    /// голосовых кадров, тишина и ровный гул — единицы.
    private static float ComputeVoicedRatio(float[] samples)
    {
        const int frame = 320; // 20 мс при 16 кГц
        int frames = samples.Length / frame;
        if (frames < 4) return 0f;
        var rms = new float[frames];
        for (int f = 0; f < frames; f++)
        {
            double sum = 0;
            for (int i = 0; i < frame; i++)
            {
                float v = samples[f * frame + i];
                sum += v * v;
            }
            rms[f] = MathF.Sqrt((float)(sum / frame));
        }
        var sorted = (float[])rms.Clone();
        Array.Sort(sorted);
        float noiseFloor = sorted[Math.Max(0, (int)(sorted.Length * 0.10) - 1)];
        float thr = Math.Max(0.004f, noiseFloor * 2.5f);
        int voiced = 0;
        for (int f = 0; f < frames; f++)
        {
            if (rms[f] > thr) voiced++;
        }
        return (float)voiced / frames;
    }

    /// One-pole high-pass ~80 Гц: срезаёт гул/дыхание, речь не трогает.
    private static void ApplyHighPass(float[] samples)
    {
        const float fc = 80f;
        float alpha = 1f / (1f + 2f * MathF.PI * fc / SampleRate);
        float prevX = 0f, prevY = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float x = samples[i];
            float y = alpha * (prevY + x - prevX);
            prevX = x;
            prevY = y;
            samples[i] = y;
        }
    }

    /// Мягкий гейт: порог от шумового пола (нижние 10% RMS-кадров), атака/отпуск сглажены.
    private static void ApplyGate(float[] samples)
    {
        const int frame = 320; // 20 мс при 16 кГц
        int frames = samples.Length / frame;
        if (frames < 4) return;
        var rms = new float[frames];
        for (int f = 0; f < frames; f++)
        {
            double sum = 0;
            for (int i = 0; i < frame; i++)
            {
                float v = samples[f * frame + i];
                sum += v * v;
            }
            rms[f] = MathF.Sqrt((float)(sum / frame));
        }
        var sorted = (float[])rms.Clone();
        Array.Sort(sorted);
        float noiseFloor = sorted[Math.Max(0, (int)(sorted.Length * 0.10) - 1)];
        float thr = Math.Max(0.004f, noiseFloor * 2.5f);
        float gain = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float target = MathF.Abs(samples[i]) > thr ? 1f : 0.02f;
            gain += 0.02f * (target - gain);
            samples[i] *= gain;
        }
    }

    /// Peak-нормализация к -1 dBFS (0.891): тихие диктовки вытягивает, громкие прижимает.
    private static void ApplyPeakNormalize(float[] samples)
    {
        float peak = 0f;
        for (int i = 0; i < samples.Length; i++) peak = Math.Max(peak, MathF.Abs(samples[i]));
        if (peak < 0.001f) return;
        float factor = 0.891f / peak;
        for (int i = 0; i < samples.Length; i++)
            samples[i] = Math.Clamp(samples[i] * factor, -1f, 1f);
    }

    private static uint ReadU32(byte[] b, int p) => BitConverter.ToUInt32(b, p);
    private static ushort ReadU16(byte[] b, int p) => BitConverter.ToUInt16(b, p);
}