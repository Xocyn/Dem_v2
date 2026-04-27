using Dem_v2;
using NAudio.Wave;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Dem_v2
{
    internal class Program
    {
        enum Estado
        {
            EsperandoInicio,
            Grabando,
            Cooldown
        }

        // Cola thread-safe: el thread de audio deposita mensajes capturados aquí.
        // El thread de procesamiento los consume de forma independiente.
        // Así el callback DataAvailable nunca bloquea, sin importar cuánto tarde ProcesarBits.
        private static readonly ConcurrentQueue<string> _mensajesCapturados = new();

        // CancellationToken para detener el thread de procesamiento limpiamente al salir.
        private static readonly CancellationTokenSource _cts = new();

        // Lock para proteger las variables de estado compartidas entre el thread de audio
        // y el thread principal (cambio de modo con M).
        private static readonly object _lock = new();

        static void Main(string[] args)
        {
            Console.WriteLine("Dispositivos de entrada disponibles:\n");
            for (int i = 0; i < WaveInEvent.DeviceCount; i++)
            {
                var caps = WaveInEvent.GetCapabilities(i);
                Console.WriteLine($"{i}: {caps.ProductName}");
            }

            Console.WriteLine("\nSeleccione el numero del dispositivo:");
            int device = int.Parse(Console.ReadLine());

            Console.WriteLine("\nSeleccione el modo de demodulacion:");
            Console.WriteLine("0: HF  (100 bps  - 1615/1785 Hz)");
            Console.WriteLine("1: VHF (1200 bps - 1300/2100 Hz)");
            Console.Write("Modo: ");
            bool vhfMode = Console.ReadLine()?.Trim() == "1";
            Console.WriteLine(vhfMode ? "Modo VHF seleccionado." : "Modo HF seleccionado.");

            WaveInEvent waveIn = new WaveInEvent();
            waveIn.DeviceNumber = device;
            waveIn.WaveFormat = new WaveFormat(44100, 16, 1);

            BFSKDemodulator demod = new BFSKDemodulator(vhfMode);

            const int PhaseCount = 4;
            var syncBuffers = new StringBuilder[PhaseCount];
            for (int p = 0; p < PhaseCount; p++) syncBuffers[p] = new StringBuilder();

            int lockedPhase = -1;
            StringBuilder decodeBuffer = new StringBuilder();
            StringBuilder bitAccumulator = new StringBuilder();

            const string startPattern = "01010101010101010101"; // 20 bits
            int phasingStartOffset = 0;
            bool extensionDetected = false;

            Estado estado = Estado.EsperandoInicio;
            int cooldownMs = 1000;
            DateTime cooldownHasta = DateTime.MinValue;
            DateTime inicioGrabacion = DateTime.MinValue;

            // VHF: ~0.45 s por mensaje → timeout 2 s
            // HF:  ~5.4 s por mensaje → timeout 10 s
            int maxGrabacionSeg = vhfMode ? 2 : 10;

            // ── Thread de procesamiento ──────────────────────────────────────────────
            // Consume mensajes de la cola y llama a ProcesarBits sin tocar el thread de audio.
            Thread processingThread = new Thread(() =>
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    if (_mensajesCapturados.TryDequeue(out string bits))
                    {
                        try
                        {
                            Procesamiento.Procesar(bits, extensionDetected);
                            extensionDetected = false;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Error en ProcesarBits] {ex.Message}");
                        }
                    }
                    else
                    {
                        // Sin mensajes: dormir brevemente para no quemar CPU.
                        Thread.Sleep(10);
                    }
                }
            })
            {
                IsBackground = true,
                Name = "DSC-Processor"
            };
            processingThread.Start();

            // ── Callback de audio ────────────────────────────────────────────────────
            // REGLA: este handler solo acumula bits y cambia estado.
            // Nunca llama ProcesarBits directamente. Solo encola el resultado capturado.
            waveIn.DataAvailable += (s, a) =>
            {
                string[] bitsByPhase;
                lock (_lock)
                {
                    bitsByPhase = demod.ProcessAudio(a.Buffer, a.BytesRecorded);
                }

                // ── Cooldown ─────────────────────────────────────────────────────────
                lock (_lock)
                {
                    if (estado == Estado.Cooldown)
                    {
                        if (DateTime.Now > cooldownHasta)
                        {
                            Console.WriteLine("Cooldown terminado. Escuchando...");
                            estado = Estado.EsperandoInicio;
                            lockedPhase = -1;
                            demod.ResetTiming();
                            for (int p = 0; p < PhaseCount; p++) syncBuffers[p].Clear();
                        }
                        return; // Descartar bits durante cooldown
                    }
                }

                // ── Chequeo de timeout (independiente de bits) ────────────────────────────
                lock (_lock)
                {
                    if (estado == Estado.Grabando)
                    {
                        if ((DateTime.Now - inicioGrabacion).TotalSeconds > maxGrabacionSeg)
                        {
                            Console.WriteLine("Timeout de grabación");
                            FinalizarCaptura("TIMEOUT");
                        }
                    }
                }

                bool debeFinalizarLoop = false;

                int phaseStart, phaseEnd;
                lock (_lock) { phaseStart = (lockedPhase >= 0) ? lockedPhase : 0; }
                lock (_lock) { phaseEnd = (lockedPhase >= 0) ? lockedPhase + 1 : PhaseCount; }

                for (int ph = phaseStart; ph < phaseEnd && !debeFinalizarLoop; ph++)
                {
                    bool shouldProcess;
                    lock (_lock) { shouldProcess = (lockedPhase < 0 || ph == lockedPhase); }
                    if (!shouldProcess) continue;

                    foreach (char bit in bitsByPhase[ph])
                    {
                        Estado estadoActual;
                        lock (_lock) { estadoActual = estado; }

                        // ── ESTADO: EsperandoInicio ───────────────────────────────────
                        if (estadoActual == Estado.EsperandoInicio)
                        {
                            lock (_lock)
                            {
                                syncBuffers[ph].Append(bit);
                                if (syncBuffers[ph].Length > startPattern.Length)
                                    syncBuffers[ph].Remove(0, 1);

                                // Detección por DOT PATTERN (01010101...)
                                if (syncBuffers[ph].ToString().EndsWith(startPattern))
                                {
                                    Console.Clear();
                                    Console.WriteLine($"DOT PATTERN detectado (fase {ph})");
                                    IniciarGrabacion(ph);
                                }
                                // Detección alternativa: valor 125 alineado en posición 0..9
                                else if (syncBuffers[ph].Length >= 10)
                                {
                                    string sub = syncBuffers[ph].ToString().Substring(0, 10);
                                    if (Decodificador.TryDeco(sub, out int v) && v == 125)
                                    {
                                        Console.Clear();
                                        Console.WriteLine($"Valor 125 detectado sin DOT PATTERN (fase {ph})");
                                        IniciarGrabacion(ph);
                                    }
                                }
                            }
                        }

                        // ── ESTADO: Grabando ──────────────────────────────────────────
                        else if (estadoActual == Estado.Grabando)
                        {
                            lock (_lock)
                            {
                                bitAccumulator.Append(bit);
                                decodeBuffer.Append(bit);

                                // Detección de EOS CONSECUTIVOS (dos valores 127 seguidos)
                                // Buscar patrones de 20 bits que representen 127 + 127
                                if (decodeBuffer.Length >= 20)
                                {
                                    // Recorrer el buffer con ventana de 40 bits
                                    for (int w = 0; w <= decodeBuffer.Length - 60; w++)
                                    {
                                        // Extraer dos ventanas consecutivas de 10 bits
                                        string ventana1 = decodeBuffer.ToString(w, 10);
                                        string ventana2 = decodeBuffer.ToString(w + 10, 10);
                                        string ventana3 = decodeBuffer.ToString(w + 40, 10); // Para extensiones

                                        bool es127_1 = Decodificador.TryDeco(ventana1, out int val1) && (val1 == 127 || val1 == 117 || val1 == 122);
                                        bool es127_2 = Decodificador.TryDeco(ventana2, out int val2) && (val2 == 127 || val2 == 117 || val2 == 122);

                                        if (es127_1 && es127_2)
                                        {
                                            if (Decodificador.TryDeco(ventana3, out int val3) && (100 <= val3 && val3 <= 106))
                                            {
                                                extensionDetected = true;
                                                // Remover los bits ya procesados (dos EOS + extensión = 30 bits)
                                                // decodeBuffer.Remove(0, w);
                                                // Salir del bucle for para reintentar con el nuevo buffer
                                                break;
                                            }
                                            else
                                            {
                                                FinalizarCaptura("EOS");
                                                debeFinalizarLoop = true;
                                                break;
                                            }

                                            //FinalizarCaptura("EOS");
                                            //debeFinalizarLoop = true;
                                            //break;
                                        }
                                    }

                                    // Si no encontramos EOS y el buffer es muy largo, descartar el primer bit
                                    // para no acumular indefinidamente
                                    if (decodeBuffer.Length > 1000)
                                    {
                                        decodeBuffer.Remove(0, 1);
                                    }
                                }
                            }
                        }

                        // Si se finalizó por EOS, salir de este loop también
                        if (debeFinalizarLoop) break;
                    } // foreach bit
                } // for ph

                // ── Helpers locales (capturan variables del closure) ─────────────────

                void IniciarGrabacion(int ph)
                {
                    // Llamar solo dentro de lock(_lock)
                    lockedPhase = ph;
                    demod.LockPhase(ph);
                    inicioGrabacion = DateTime.Now;
                    estado = Estado.Grabando;
                    //eosCount = 0;
                    phasingStartOffset = 0;
                    decodeBuffer.Clear();
                    bitAccumulator.Clear();
                    Console.WriteLine($"[IniciarGrabacion] Fase {ph} bloqueada.");
                }

                void FinalizarCaptura(string motivo)
                {
                    // Llamar solo dentro de lock(_lock)
                    // Extraer bits acumulados desde el offset de inicio de phasing
                    int offset = Math.Max(0, Math.Min(phasingStartOffset, bitAccumulator.Length));
                    string capturado = bitAccumulator.ToString(offset, bitAccumulator.Length - offset);

                    Console.WriteLine($"[FinalizarCaptura - {motivo}] Bits acumulados: {bitAccumulator.Length}, offset: {offset}, capturado: {capturado.Length} bits");

                    // Encolar para procesamiento asíncrono — NO llamar ProcesarBits aquí
                    if (capturado.Length > 0)
                    {
                        _mensajesCapturados.Enqueue(capturado);
                    }
                    else
                    {
                        Console.WriteLine("[Advertencia] No se encoló mensaje: cadena vacía");
                    }

                    // Limpiar y entrar en cooldown
                    decodeBuffer.Clear();
                    bitAccumulator.Clear();
                    //eosCount = 0;
                    estado = Estado.Cooldown;
                    cooldownHasta = DateTime.Now.AddMilliseconds(cooldownMs);
                }
            };

            waveIn.RecordingStopped += (s, a) =>
            {
                Console.WriteLine("Grabación detenida.");
            };

            Console.WriteLine("\nEscuchando...");
            Console.WriteLine("ENTER para detener | M para cambiar modo HF/VHF\n");
            waveIn.StartRecording();

            // ── Bucle de teclado (thread principal) ─────────────────────────────────
            while (true)
            {
                var key = Console.ReadKey(intercept: true);

                if (key.Key == ConsoleKey.Enter)
                    break;

                if (key.Key == ConsoleKey.M)
                {
                    waveIn.StopRecording();

                    Console.WriteLine("\nSeleccione el modo:");
                    Console.WriteLine("0: HF  (100 bps  - 1615/1785 Hz)");
                    Console.WriteLine("1: VHF (1200 bps - 1300/2100 Hz)");
                    Console.Write("Modo: ");
                    vhfMode = Console.ReadLine()?.Trim() == "1";
                    Console.WriteLine(vhfMode ? "Modo VHF seleccionado." : "Modo HF seleccionado.");

                    lock (_lock)
                    {
                        demod = new BFSKDemodulator(vhfMode);
                        maxGrabacionSeg = vhfMode ? 2 : 10;
                        estado = Estado.EsperandoInicio;
                        lockedPhase = -1;
                        for (int p = 0; p < PhaseCount; p++) syncBuffers[p].Clear();
                        decodeBuffer.Clear();
                        bitAccumulator.Clear();
                        phasingStartOffset = 0;
                        //eosCount = 0;
                    }

                    Console.WriteLine("Escuchando...");
                    Console.WriteLine("ENTER para detener | M para cambiar modo HF/VHF\n");
                    waveIn.StartRecording();
                }
            }

            // ── Cierre limpio ────────────────────────────────────────────────────────
            waveIn.StopRecording();
            _cts.Cancel();
            processingThread.Join(2000); // Esperar que termine el thread de proc.
        }

        // ── ProcesarBits ─────────────────────────────────────────────────────────────
        // Este método corre SOLO en el thread de procesamiento. Puede tardar lo que quiera
        // sin afectar en absoluto la captura de audio.
    }
}