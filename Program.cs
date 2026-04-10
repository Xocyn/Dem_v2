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
            int eosCount = 0;

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
                            ProcesarBits(bits);
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
                                    // Recorrer el buffer con ventana de 20 bits
                                    for (int w = 0; w <= decodeBuffer.Length - 20; w++)
                                    {
                                        // Extraer dos ventanas consecutivas de 10 bits
                                        string ventana1 = decodeBuffer.ToString(w, 10);
                                        string ventana2 = decodeBuffer.ToString(w + 10, 10);

                                        bool es127_1 = Decodificador.TryDeco(ventana1, out int val1) && val1 == 127;
                                        bool es127_2 = Decodificador.TryDeco(ventana2, out int val2) && val2 == 127;

                                        if (es127_1 && es127_2)
                                        {
                                            Console.WriteLine($"EOS CONSECUTIVO detectado en posición {w}: 127 + 127");
                                            FinalizarCaptura("EOS");
                                            debeFinalizarLoop = true;
                                            break;
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
                    eosCount = 0;
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
                    eosCount = 0;
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
                        eosCount = 0;
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
        public static bool ProcesarBits(string input)
        {
            List<(int Index, int Value)> encontrados = new List<(int, int)>();
            int i = 0;
            bool sincronizado = false;

            // Ventana deslizante: busca y consume caracteres de phasing
            while (!sincronizado)
            {
                if (i + 10 > input.Length) break;

                string ventana = input.Substring(i, 10);
                int mensajeInt = Convert.ToInt32(ventana, 2);

                if (Decodificador.TryDecodificarMensaje(mensajeInt, out int valor))
                {
                    if (PhasingSequence.TryCaracter(valor))
                    {
                        encontrados.Add((i, valor));
                        i += 10;

                        if (encontrados.Count >= 3 &&
                            PhasingSequence.TryDetect(encontrados, out var pattern))
                        {
                            Console.WriteLine($"Patrón de phasing detectado: {pattern}");
                            sincronizado = true;
                        }
                    }
                    else
                        i += 1;
                }
                else
                    i += 1;
            }

            if (encontrados.Count == 0)
            {
                Console.WriteLine("No se detectaron mensajes válidos en la secuencia.");
                return false;
            }

            Console.WriteLine("Phasing sequence encontrada:");
            foreach (var e in encontrados)
                Console.WriteLine($"  Offset {e.Index}: valor = {e.Value}");

            // ── Format specifier ─────────────────────────────────────────────────────
            bool formatConfirmed = false;
            bool dxrxConfirmed = false;
            int form = 0;

            while (sincronizado && !formatConfirmed)
            {
                if (i + 10 > input.Length) break;

                string ventana = input.Substring(i, 10);
                int mensajeInt = Convert.ToInt32(ventana, 2);
                Decodificador.TryDecodificarMensaje(mensajeInt, out int valor);

                form = FormatSpecifier.Filtro(valor, out int j);

                bool esBroadcast = (form == 112 || form == 116);
                dxrxConfirmed = esBroadcast || Decodificador.DxRx(input, i);

                i += 10;

                if (j == 1 && dxrxConfirmed)
                {
                    formatConfirmed = true;
                }
            }

            if (!formatConfirmed)
            {
                Console.WriteLine("Format specifier no confirmado. Descartando mensaje.");
                return false;
            }

            i -= 10; // Retroceder para que el switch lea el format specifier

            List<int> ECC = new List<int>();

            switch (form)
            {
                case 112:
                    // SOCORRO
                    ECC.Add(form);
                    i = Socorro.MMSI(i, form, input, ECC);
                    i = Socorro.NatureofDistress(i, input, ECC);
                    i = Geografica.PuntoGeografico(i, input, ECC, out bool valid);
                    if (valid)
                    {
                        i = Geografica.UTC(i, input, ECC);
                    }
                    else
                    {
                        Console.WriteLine($"Hora: 88 88");
                        i += 40;
                        ECC.Add(88); ECC.Add(88);
                    }
                    i = Socorro.FirstTelecommand(i, input, ECC);

                    //// Dump de todos los valores decodificados (debug)
                    //for (int k = i; k + 10 <= input.Length; k += 10)
                    //{
                    //    string v = input.Substring(k, 10);
                    //    int mi = Convert.ToInt32(v, 2);
                    //    Decodificador.TryDecodificarMensaje(mi, out int vv);
                    //    Console.Write($"{vv} ");
                    //}
                    //Console.WriteLine();

                    if (i + 10 > input.Length)
                    {
                        Console.WriteLine("Mensaje incompleto (112): stream demasiado corto.");
                        break;
                    }

                    {
                        string win = input.Substring(i, 10);
                        int ms = Convert.ToInt32(win, 2);
                        Decodificador.TryDecodificarMensaje(ms, out int val);
                        ECC.Add(val);
                        if (val == 127)
                        {
                            Console.WriteLine("EOS detectado");
                            if (i + 30 <= input.Length)
                                Decodificador.Mod2Sum7Bits(i, input, ECC);
                            else
                                Console.WriteLine("Stream demasiado corto para leer ECC.");
                        }
                    }
                    break;

                case 116:
                    // ALL SHIPS
                    ECC.Add(form);
                    i = General.Categoria(i, form, input, ECC, out bool socorro);

                    // AGREGAR: si socorro == true a donde demodulo

                    i = General.MMSI_2(i, input, ECC);

                    if (socorro)
                    {
                        // MENSAJE_1 puede ser utilizado
                    }
                    i = General.Mensaje_1(i, input, ECC);
                    byte h = 1;
                    i = General.Mensaje_2(i, input, ECC, h);
                    h++;
                    i = General.Mensaje_2(i, input, ECC, h);

                    if (i + 10 > input.Length)
                    {
                        Console.WriteLine("Mensaje incompleto (116): stream demasiado corto.");
                        break;
                    }

                    {
                        string win1 = input.Substring(i, 10);
                        int ms1 = Convert.ToInt32(win1, 2);
                        Decodificador.TryDecodificarMensaje(ms1, out int val1);
                        ECC.Add(val1);
                        if (val1 == 127)
                        {
                            Console.WriteLine("EOS detectado");
                            if (i + 30 <= input.Length)
                                Decodificador.Mod2Sum7Bits(i, input, ECC);
                            else
                                Console.WriteLine("Stream demasiado corto para leer ECC.");
                        }
                    }
                    break;

                case 114:
                    // TODO: formato grupo
                    break;
                case 120:
                    // TODO: formato individual
                    break;
                case 102:
                    // GEOGRAFICA
                    ECC.Add(form);
                    Socorro.TryLeer(input, i + 20, out int valor);
                    if (form == valor) // es el primer format recibido
                        i = i + 40;
                    else
                        i = i + 20;

                    i = Geografica.AreaGeografica(i, input, ECC);
                    i = General.Categoria2(i,input, ECC);
                    i = General.MMSI_2(i, input, ECC);
                    i = General.Mensaje_1(i, input, ECC);
                    byte h1 = 1;
                    i = General.Mensaje_2(i, input, ECC, h1);
                    h1++;
                    i = General.Mensaje_2(i, input, ECC, h1);

                    if (i + 10 > input.Length)
                    {
                        Console.WriteLine("Mensaje incompleto (102): stream demasiado corto.");
                        break;
                    }
                    {
                        string win1 = input.Substring(i, 10);
                        int ms1 = Convert.ToInt32(win1, 2);
                        Decodificador.TryDecodificarMensaje(ms1, out int val1);
                        ECC.Add(val1);
                        if (val1 == 127)
                        {
                            Console.WriteLine("EOS detectado");
                            if (i + 30 <= input.Length)
                                Decodificador.Mod2Sum7Bits(i, input, ECC);
                            else
                                Console.WriteLine("Stream demasiado corto para leer ECC.");
                        }
                    }
                    break;

                case 123:
                    // TODO: formato individual2
                    break;
                default:
                    Console.WriteLine("Formato no reconocido.");
                    return false;
            }

            return true;
        }
    }
}