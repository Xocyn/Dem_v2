using System;
using System.Collections.Generic;
using System.Text;

public class BFSKDemodulator
{
    const int SampleRate = 44100;

    private readonly int _baudRate;
    private readonly double _freqBit0;
    private readonly double _freqBit1;
    private readonly double _samplesPerSymbol;

    // Umbral de energía: por debajo se considera silencio (sin señal FSK).
    // Evita falsas detecciones cuando no hay transmisión.
    // Se calibra como fracción del rango dinámico del ADC (short.MaxValue = 32767).
    // Con amplitude = 0.25 * 32767 ≈ 8192, una señal real tiene RMS ≈ 5792.
    // Se pone el umbral en ~1% de ese valor para tolerar señales débiles.
    private readonly double _energyThreshold;

    private readonly List<short> _sampleBuffer = new List<short>();
    private double _symbolAccumulator = 0.0;

    public BFSKDemodulator(bool vhf = false)
    {
        if (vhf)
        {
            _baudRate = 1200;
            _freqBit0 = 2100.0;
            _freqBit1 = 1300.0;
        }
        else
        {
            _baudRate = 100;
            _freqBit0 = 1785.0;
            _freqBit1 = 1615.0;
        }

        // Mantener como double — 44100/1200 = 36.75, NO se redondea
        _samplesPerSymbol = (double)SampleRate / _baudRate;

        // Umbral de RMS cuadrado: (short.MaxValue * 0.25 * 0.01)^2 * samplesPerSymbol
        // Se escala con samplesPerSymbol porque la energía IQ suma N términos
        double minRms = short.MaxValue * 0.01;  // 1% del rango → ~327
        _energyThreshold = minRms * minRms * _samplesPerSymbol;
    }

    // ────────────────────────────────────────────────────────────────────
    // ProcessAudio — demodulación en tiempo real
    // Retorna bits válidos solamente cuando hay señal por encima del umbral.
    // ────────────────────────────────────────────────────────────────────
    public string ProcessAudio(byte[] buffer, int bytesRecorded)
    {
        var newBits = new StringBuilder();

        int samples = bytesRecorded / 2;
        for (int i = 0; i < samples; i++)
            _sampleBuffer.Add(BitConverter.ToInt16(buffer, i * 2));

        while (_symbolAccumulator + _samplesPerSymbol <= _sampleBuffer.Count)
        {
            int start = (int)Math.Round(_symbolAccumulator);
            int end = (int)Math.Round(_symbolAccumulator + _samplesPerSymbol);
            int length = end - start;

            if (start + length > _sampleBuffer.Count)
                break;

            short[] symbol = _sampleBuffer.GetRange(start, length).ToArray();

            // Verificar energía: si la señal es ruido/silencio, no emitir bits
            double rawEnergy = RawEnergy(symbol, length);
            if (rawEnergy >= _energyThreshold)
            {
                double e0 = EnergyIQ(symbol, length, _freqBit0);
                double e1 = EnergyIQ(symbol, length, _freqBit1);
                newBits.Append(e1 > e0 ? '1' : '0');
            }

            _symbolAccumulator += _samplesPerSymbol;
        }

        // Purgar muestras consumidas y ajustar acumulador
        int consumed = (int)Math.Floor(_symbolAccumulator);
        if (consumed > 0 && consumed <= _sampleBuffer.Count)
        {
            _sampleBuffer.RemoveRange(0, consumed);
            _symbolAccumulator -= consumed;
        }

        return newBits.ToString();
    }

    // ────────────────────────────────────────────────────────────────────
    // DemodulateToString — demodulación desde archivo WAV
    // ────────────────────────────────────────────────────────────────────
    public static string DemodulateToString(string wavPath, bool vhf = false)
    {
        double freqBit0 = vhf ? 2100.0 : 1785.0;
        double freqBit1 = vhf ? 1300.0 : 1615.0;
        int baudRate = vhf ? 1200 : 100;

        double samplesPerSymbol = (double)SampleRate / baudRate;
        double minRms = short.MaxValue * 0.01;
        double energyThreshold = minRms * minRms * samplesPerSymbol;

        short[] samples = ReadWav16BitMono(wavPath);
        var bits = new StringBuilder();

        double pos = 0.0;
        while (pos + samplesPerSymbol <= samples.Length)
        {
            int start = (int)Math.Round(pos);
            int end = (int)Math.Round(pos + samplesPerSymbol);
            int length = end - start;

            if (start + length > samples.Length)
                break;

            double rawEnergy = RawEnergy2(samples, start, length);
            if (rawEnergy >= energyThreshold)
            {
                double e0 = EnergyIQ2(samples, start, length, freqBit0);
                double e1 = EnergyIQ2(samples, start, length, freqBit1);
                bits.Append(e1 > e0 ? '1' : '0');
            }

            pos += samplesPerSymbol;
        }

        return bits.ToString();
    }

    // ────────────────────────────────────────────────────────────────────
    // Energía bruta (sum of squares) — para detectar silencio
    // ────────────────────────────────────────────────────────────────────
    private static double RawEnergy(short[] samples, int length)
    {
        double e = 0;
        for (int n = 0; n < length; n++)
            e += (double)samples[n] * samples[n];
        return e;
    }

    private static double RawEnergy2(short[] samples, int start, int length)
    {
        double e = 0;
        for (int n = 0; n < length; n++)
            e += (double)samples[start + n] * samples[start + n];
        return e;
    }

    // ────────────────────────────────────────────────────────────────────
    // Correladores IQ (non-coherent — la fase absoluta cancela en I²+Q²)
    // ────────────────────────────────────────────────────────────────────
    private static double EnergyIQ(short[] samples, int length, double freq)
    {
        double I = 0, Q = 0;
        for (int n = 0; n < length; n++)
        {
            double t = (double)n / SampleRate;
            I += samples[n] * Math.Cos(2 * Math.PI * freq * t);
            Q += samples[n] * Math.Sin(2 * Math.PI * freq * t);
        }
        return I * I + Q * Q;
    }

    private static double EnergyIQ2(short[] samples, int start, int length, double freq)
    {
        double I = 0, Q = 0;
        for (int n = 0; n < length; n++)
        {
            double t = (double)n / SampleRate;
            I += samples[start + n] * Math.Cos(2 * Math.PI * freq * t);
            Q += samples[start + n] * Math.Sin(2 * Math.PI * freq * t);
        }
        return I * I + Q * Q;
    }

    // ────────────────────────────────────────────────────────────────────
    // Lector WAV 16-bit mono
    // ────────────────────────────────────────────────────────────────────
    private static short[] ReadWav16BitMono(string path)
    {
        using var reader = new System.IO.BinaryReader(
            System.IO.File.Open(path, System.IO.FileMode.Open));
        reader.ReadBytes(44);
        int sampleCount = (int)((reader.BaseStream.Length - 44) / 2);
        short[] data = new short[sampleCount];
        for (int i = 0; i < sampleCount; i++)
            data[i] = reader.ReadInt16();
        return data;
    }
}