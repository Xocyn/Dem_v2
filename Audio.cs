using System;
using System.Collections.Generic;
using System.Text;

public class BFSKDemodulator
{
    const int SampleRate = 44100;

    private readonly double _freqBit0;
    private readonly double _freqBit1;
    private readonly double _samplesPerSymbol;
    private readonly double _energyThreshold;

    // Buffer compartido de muestras
    private readonly List<short> _sampleBuffer = new List<short>();

    // ── 4 fases en paralelo ───────────────────────────────────────────────────
    // offsets: 0, spb/4, spb/2, 3*spb/4
    // Garantiza que siempre al menos una fase tenga offset < 32% del símbolo
    // (zona donde los bits en transición son correctos).
    private const int PhaseCount = 4;
    private readonly double[] _accumulators = new double[PhaseCount];
    private int _activePhase = -1; // -1 = modo detección (todas activas)
    private bool _phaseLocked = false;

    public BFSKDemodulator(bool vhf = false)
    {
        if (vhf)
        {
            _freqBit0 = 2100.0;
            _freqBit1 = 1300.0;
        }
        else
        {
            _freqBit0 = 1785.0;
            _freqBit1 = 1615.0;
        }
        _samplesPerSymbol = (double)SampleRate / (vhf ? 1200 : 100);

        double minRms = short.MaxValue * 0.01;
        _energyThreshold = minRms * minRms * _samplesPerSymbol;

        // Inicializar los 4 acumuladores con offsets distribuidos uniformemente
        for (int p = 0; p < PhaseCount; p++)
            _accumulators[p] = p * (_samplesPerSymbol / PhaseCount);
    }

    // ── ResetTiming: volver a modo detección con 4 fases ─────────────────────
    public void ResetTiming()
    {
        _phaseLocked = false;
        _activePhase = -1;
        _sampleBuffer.Clear();
        for (int p = 0; p < PhaseCount; p++)
            _accumulators[p] = p * (_samplesPerSymbol / PhaseCount);
    }

    // ── LockPhase: llamar cuando una fase detectó el dot pattern ─────────────
    public void LockPhase(int phaseIndex)
    {
        _activePhase = phaseIndex;
        _phaseLocked = true;
    }

    // ── ProcessAudio ─────────────────────────────────────────────────────────
    // En modo detección: retorna bits de las 4 fases → string[4]
    // En modo bloqueado: retorna bits solo de la fase activa → string[0] con contenido
    // Program.cs usa el índice 'activePhase' para separar los bits.
    public string[] ProcessAudio(byte[] buffer, int bytesRecorded)
    {
        // Agregar muestras al buffer compartido
        int samples = bytesRecorded / 2;
        for (int i = 0; i < samples; i++)
            _sampleBuffer.Add(BitConverter.ToInt16(buffer, i * 2));

        var results = new StringBuilder[PhaseCount];
        for (int p = 0; p < PhaseCount; p++)
            results[p] = new StringBuilder();

        int pStart = _phaseLocked ? _activePhase : 0;
        int pEnd = _phaseLocked ? _activePhase + 1 : PhaseCount;

        for (int p = pStart; p < pEnd; p++)
        {
            while (_accumulators[p] + _samplesPerSymbol <= _sampleBuffer.Count)
            {
                int start = (int)Math.Round(_accumulators[p]);
                int end = (int)Math.Round(_accumulators[p] + _samplesPerSymbol);
                int length = end - start;

                if (start < 0 || start + length > _sampleBuffer.Count) break;

                double rawE = 0;
                for (int n = 0; n < length; n++)
                    rawE += (double)_sampleBuffer[start + n] * _sampleBuffer[start + n];

                if (rawE >= _energyThreshold)
                {
                    double e0 = EnergyIQ(_sampleBuffer, start, length, _freqBit0);
                    double e1 = EnergyIQ(_sampleBuffer, start, length, _freqBit1);
                    results[p].Append(e1 > e0 ? '1' : '0');
                }

                _accumulators[p] += _samplesPerSymbol;
            }
        }

        // Purgar muestras ya consumidas por TODAS las fases activas
        double minAcc = double.MaxValue;
        for (int p = pStart; p < pEnd; p++)
            if (_accumulators[p] < minAcc) minAcc = _accumulators[p];

        int consumed = (int)Math.Floor(minAcc);
        if (consumed > 0 && consumed <= _sampleBuffer.Count)
        {
            _sampleBuffer.RemoveRange(0, consumed);
            for (int p = 0; p < PhaseCount; p++)
                _accumulators[p] -= consumed;
        }

        return results.Select(sb => sb.ToString()).ToArray();
    }

    // ── DemodulateToString: desde archivo WAV (timing recovery por scan) ──────
    public static string DemodulateToString(string wavPath, bool vhf = false)
    {
        double f0 = vhf ? 2100.0 : 1785.0;
        double f1 = vhf ? 1300.0 : 1615.0;
        int br = vhf ? 1200 : 100;
        double spb = (double)SampleRate / br;

        double minRms = short.MaxValue * 0.01;
        double ethr = minRms * minRms * spb;

        short[] samples = ReadWav16BitMono(wavPath);

        // Probar los 4 offsets, elegir el mejor por score×alternación
        int steps = (int)Math.Round(spb);
        double best = -1;
        double bestOff = 0;

        for (int step = 0; step < steps; step++)
        {
            double score = 0; int count = 0, alt = 0; char prev = ' ';
            for (int sym = 0; sym < 60 && step + sym * spb + spb <= samples.Length; sym++)
            {
                int s = (int)Math.Round(step + sym * spb);
                int l = (int)Math.Round(step + sym * spb + spb) - s;
                if (s + l > samples.Length) break;
                double raw = 0;
                for (int n = 0; n < l; n++) raw += (double)samples[s + n] * samples[s + n];
                if (raw < ethr) { prev = ' '; continue; }
                var (e0, e1) = EnergyIQStatic(samples, s, l, f0, f1);
                double d = e0 + e1; if (d <= 0) continue;
                score += Math.Abs(e0 - e1) / d; count++;
                char b = e1 > e0 ? '1' : '0';
                if (prev != ' ' && prev != b) alt++;
                prev = b;
            }
            if (count < 3) continue;
            double c = (score / count) * ((count > 1) ? (double)alt / (count - 1) : 0);
            if (c > best) { best = c; bestOff = step; }
        }

        var bits = new StringBuilder();
        double pos = bestOff;
        while (pos + spb <= samples.Length)
        {
            int s = (int)Math.Round(pos), l = (int)Math.Round(pos + spb) - s;
            if (s + l > samples.Length) break;
            double raw = 0; for (int n = 0; n < l; n++) raw += (double)samples[s + n] * samples[s + n];
            if (raw >= ethr)
            {
                var (e0, e1) = EnergyIQStatic(samples, s, l, f0, f1);
                bits.Append(e1 > e0 ? '1' : '0');
            }
            pos += spb;
        }
        return bits.ToString();
    }

    // ── Correladores ─────────────────────────────────────────────────────────
    private static double EnergyIQ(List<short> s, int start, int length, double freq)
    {
        double I = 0, Q = 0;
        for (int n = 0; n < length; n++)
        {
            double t = (double)n / SampleRate;
            I += s[start + n] * Math.Cos(2 * Math.PI * freq * t);
            Q += s[start + n] * Math.Sin(2 * Math.PI * freq * t);
        }
        return I * I + Q * Q;
    }

    private static (double e0, double e1) EnergyIQStatic(short[] s, int start, int length, double f0, double f1)
    {
        double I0 = 0, Q0 = 0, I1 = 0, Q1 = 0;
        for (int n = 0; n < length; n++)
        {
            double t = (double)n / SampleRate, v = s[start + n];
            I0 += v * Math.Cos(2 * Math.PI * f0 * t); Q0 += v * Math.Sin(2 * Math.PI * f0 * t);
            I1 += v * Math.Cos(2 * Math.PI * f1 * t); Q1 += v * Math.Sin(2 * Math.PI * f1 * t);
        }
        return (I0 * I0 + Q0 * Q0, I1 * I1 + Q1 * Q1);
    }

    private static short[] ReadWav16BitMono(string path)
    {
        using var r = new System.IO.BinaryReader(System.IO.File.Open(path, System.IO.FileMode.Open));
        r.ReadBytes(44);
        int n = (int)((r.BaseStream.Length - 44) / 2);
        var d = new short[n]; for (int i = 0; i < n; i++) d[i] = r.ReadInt16();
        return d;
    }
}