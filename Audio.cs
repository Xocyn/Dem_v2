using System;
using System.Collections.Generic;
using System.Text;

public class BFSKDemodulator
{
    const int SampleRate = 44100;
    //const int BaudRate = 1200; // VHF
    const int BaudRate = 100; // HF

    // VHF
    //const double FreqBit1 = 1300.0;
    //const double FreqBit0 = 2100.0;

    // HF
    const double FreqBit1 = 1615.0;
    const double FreqBit0 = 1785.0;

    int samplesPerSymbol = (int)Math.Round((double)SampleRate / BaudRate);

    List<short> sampleBuffer = new List<short>();

    StringBuilder bits = new StringBuilder();

    public string ProcessAudio(byte[] buffer, int bytesRecorded)
    {
        int samples = bytesRecorded / 2;

        for (int i = 0; i < samples; i++)
        {
            short sample = BitConverter.ToInt16(buffer, i * 2);
            sampleBuffer.Add(sample);
        }

        while (sampleBuffer.Count >= samplesPerSymbol)
        {
            short[] symbol = sampleBuffer.GetRange(0, samplesPerSymbol).ToArray();

            double e0 = EnergyIQ(symbol, samplesPerSymbol, FreqBit0);
            double e1 = EnergyIQ(symbol, samplesPerSymbol, FreqBit1);

            bits.Append(e1 > e0 ? '1' : '0');

            sampleBuffer.RemoveRange(0, samplesPerSymbol);
        }

        return bits.ToString();
    }
    //public string ProcessAudio(byte[] buffer, int bytesRecorded)
    //{
    //    StringBuilder newBits = new StringBuilder();

    //    int samples = bytesRecorded / 2;

    //    for (int i = 0; i < samples; i++)
    //    {
    //        short sample = BitConverter.ToInt16(buffer, i * 2);
    //        sampleBuffer.Add(sample);
    //    }

    //    while (sampleBuffer.Count >= samplesPerSymbol)
    //    {
    //        short[] symbol = sampleBuffer.GetRange(0, samplesPerSymbol).ToArray();

    //        double e0 = EnergyIQ(symbol, samplesPerSymbol, FreqBit0);
    //        double e1 = EnergyIQ(symbol, samplesPerSymbol, FreqBit1);

    //        newBits.Append(e1 > e0 ? '1' : '0');

    //        sampleBuffer.RemoveRange(0, samplesPerSymbol);
    //    }

    //    return newBits.ToString();
    //}

    private double EnergyIQ(short[] samples, int length, double freq)
    {
        double I = 0;
        double Q = 0;

        for (int n = 0; n < length; n++)
        {
            double t = (double)n / SampleRate;
            double sample = samples[n];

            double cos = Math.Cos(2 * Math.PI * freq * t);
            double sin = Math.Sin(2 * Math.PI * freq * t);

            I += sample * cos;
            Q += sample * sin;
        }

        return I * I + Q * Q;
    }
}