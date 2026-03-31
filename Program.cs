using Dem_v2;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;



namespace Dem_v2
{
    internal class Program
    {
        enum Estado
        {
            EsperandoInicio,
            Grabando,
            Cooldown,
            ValidandoCaracter
        }

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
            bool vhfMode = Console.ReadLine()?.Trim() == "1"; // var para poder reasignar con M
            Console.WriteLine(vhfMode ? "Modo VHF seleccionado." : "Modo HF seleccionado.");

            WaveInEvent waveIn = new WaveInEvent();
            waveIn.DeviceNumber = device;
            waveIn.WaveFormat = new WaveFormat(44100, 16, 1);

            BFSKDemodulator demod = new BFSKDemodulator(vhfMode); // reasignable con M

            // 4 syncBuffers — uno por fase del demodulador.
            // Garantiza detección independientemente del offset de timing.
            const int PhaseCount = 4;
            var syncBuffers = new StringBuilder[PhaseCount];
            for (int p = 0; p < PhaseCount; p++) syncBuffers[p] = new StringBuilder();
            int lockedPhase = -1;

            StringBuilder decodeBuffer = new StringBuilder();
            Random rnd = new Random();

            string startPattern = "01010101010101010101"; // 20 bits

            // phasingConsecutivos: cuántos phasing chars alineados y consecutivos
            // se detectaron desde el dot pattern.
            // Se necesitan >= 3 para pasar a Grabando.
            // Con 1 solo char la prob de falso positivo es 81.5%;
            // con 3 consecutivos baja a 0.0007%.
            int phasingConsecutivos = 0;
            int phasingStartOffset = 0;

            // eosCount: EOS (127) consecutivos con paridad OK para detectar fin de trama.
            // Reemplaza endbuffer/endPattern para evitar falsos fin en VHF.
            int eosCount = 0;


            Estado estado = Estado.EsperandoInicio;

            int cooldownMs = 2000;
            DateTime cooldownHasta = DateTime.MinValue;


            DateTime inicioGrabacion = DateTime.MinValue;
            // VHF: mensaje dura ~0.45s → timeout 1s
            // HF:  mensaje dura ~5.4s → timeout 10s
            int maxGrabacionSeg = vhfMode ? 1 : 10;

            int bitsValidacion = 0;
            int maxBitsValidacion = 50; // 200 inicialmente
            StringBuilder bitAccumulator = new StringBuilder();

            waveIn.DataAvailable += (s, a) =>
            {
                // Siempre llamar ProcessAudio para mantener las fases alineadas.
                // En Cooldown descartamos los bits pero seguimos actualizando el demodulador.
                string[] bitsByPhase = demod.ProcessAudio(a.Buffer, a.BytesRecorded);

                if (estado == Estado.Cooldown)
                {
                    if (DateTime.Now > cooldownHasta)
                    {
                        Console.WriteLine("Cooldown terminado");
                        estado = Estado.EsperandoInicio;
                        lockedPhase = -1;
                        demod.ResetTiming();
                        for (int p = 0; p < PhaseCount; p++) syncBuffers[p].Clear();
                    }

                    // Escritura de audio aunque sea en cooldown no aplica,
                    // pero mantenemos el return solo del procesado de bits
                    goto WriteAudio;
                }

                {
                    int phaseStart = (lockedPhase >= 0) ? lockedPhase : 0;
                    int phaseEnd = (lockedPhase >= 0) ? lockedPhase + 1 : PhaseCount;

                    for (int ph = phaseStart; ph < phaseEnd; ph++)
                    {
                        // Si ya bloqueamos y este no es el fase bloqueada, saltar
                        if (lockedPhase >= 0 && ph != lockedPhase) continue;

                        foreach (char bit in bitsByPhase[ph])
                        {
                            //----------------------------------------
                            // ESTADO 1: ESPERANDO INICIO
                            //----------------------------------------
                            if (estado == Estado.EsperandoInicio)
                            {
                                syncBuffers[ph].Append(bit);
                                if (syncBuffers[ph].Length > startPattern.Length)
                                    syncBuffers[ph].Remove(0, 1);


                                if (syncBuffers[ph].ToString().EndsWith(startPattern))
                                {
                                    Console.Clear();
                                    Console.WriteLine($"DOT PATTERN DETECTADO (fase {ph})");
                                    lockedPhase = ph;
                                    demod.LockPhase(ph);
                                    estado = Estado.ValidandoCaracter;
                                    decodeBuffer.Clear();
                                    bitsValidacion = 0;
                                    phasingConsecutivos = 0;
                                    phasingStartOffset = 0;
                                    bitAccumulator.Clear();
                                    // NO hacer break: seguimos procesando bits restantes
                                    // del chunk en estado ValidandoCaracter
                                }
                                // Lógica alternativa: detectar valor 125 sin DOT PATTERN
                                else if (syncBuffers[ph].Length >= 10)
                                {
                                    string substring = syncBuffers[ph].ToString().Substring(0, 10);
                                    if (Decodificador.TryDeco(substring, out int valor) && valor == 125)
                                    {
                                        Console.Clear();
                                        Console.WriteLine($"Valor 125 detectado sin DOT PATTERN (fase {ph})");
                                        lockedPhase = ph;
                                        demod.LockPhase(ph);
                                        estado = Estado.ValidandoCaracter;
                                        decodeBuffer.Clear();
                                        bitsValidacion = 0;
                                        phasingConsecutivos = 0;
                                        phasingStartOffset = 0;
                                        bitAccumulator.Clear();
                                    }
                                }
                            }

                            //----------------------------------------
                            // ESTADO 2: VALIDANDO CARACTER
                            //----------------------------------------

                            else if (estado == Estado.ValidandoCaracter)
                            {
                                bitAccumulator.Append(bit);
                                decodeBuffer.Append(bit);
                                bitsValidacion++;

                                if (decodeBuffer.Length == 10)
                                {
                                    if (Decodificador.TryDeco(decodeBuffer.ToString(), out int valor)
                                        && PhasingSequence.TryCaracter(valor))
                                    {
                                        if (phasingConsecutivos == 0)
                                        {
                                            phasingStartOffset = bitAccumulator.Length - 10;
                                            bitsValidacion = 0;  // RESETEA
                                        }
                                        phasingConsecutivos++;
                                        Console.WriteLine($"Phasing char #{phasingConsecutivos}: {valor}");
                                        decodeBuffer.Clear();

                                        if (phasingConsecutivos >= 3)
                                        {
                                            Console.WriteLine("Phasing confirmado. Grabando...");
                                            inicioGrabacion = DateTime.Now;
                                            estado = Estado.Grabando;
                                            eosCount = 0;
                                            decodeBuffer.Clear();
                                        }
                                    }
                                    else
                                    {
                                        // Falló: resetea contador si no hay progreso
                                        phasingConsecutivos = 0;
                                        decodeBuffer.Remove(0, 1);
                                    }
                                }

                                // Sale si pasan N bits sin detectar NINGÚN phasing
                                if (bitsValidacion > maxBitsValidacion)
                                {
                                    Console.WriteLine("Validacion fallida, volviendo a espera");
                                    estado = Estado.EsperandoInicio;
                                    demod.ResetTiming();
                                    lockedPhase = -1;
                                    for (int p = 0; p < PhaseCount; p++) syncBuffers[p].Clear();
                                    decodeBuffer.Clear();
                                    bitsValidacion = 0;
                                    phasingConsecutivos = 0;
                                }
                            }

                            //----------------------------------------
                            // ESTADO 3: GRABANDO
                            //----------------------------------------
                            else if (estado == Estado.Grabando)
                            {
                                if ((DateTime.Now - inicioGrabacion).TotalSeconds > maxGrabacionSeg)
                                {
                                    Console.WriteLine("Timeout de grabación alcanzado");
                                    estado = Estado.Cooldown;
                                    cooldownHasta = DateTime.Now.AddMilliseconds(cooldownMs);
                                    decodeBuffer.Clear();
                                    eosCount = 0;
                                    break;
                                }

                                bitAccumulator.Append(bit);
                                decodeBuffer.Append(bit);

                                if (decodeBuffer.Length == 10)
                                {
                                    if (Decodificador.TryDeco(decodeBuffer.ToString(), out int simVal))
                                    {
                                        // solo valido si 127
                                        // AGREGAR: que lea ECC
                                        if (simVal == 127)
                                        {
                                            eosCount++;
                                            if (eosCount >= 2)
                                            {
                                                Console.WriteLine("FIN DE TRAMA");
                                                estado = Estado.Cooldown;
                                                cooldownHasta = DateTime.Now.AddMilliseconds(cooldownMs);
                                                string capturedBits = bitAccumulator.ToString(
                                                    phasingStartOffset,
                                                    bitAccumulator.Length - phasingStartOffset);
                                                decodeBuffer.Clear();
                                                bitAccumulator.Clear();
                                                eosCount = 0;
                                                ProcesarBits(capturedBits);
                                                cooldownHasta = DateTime.Now.AddMilliseconds(cooldownMs);
                                            }
                                        }
                                        else
                                        {
                                            eosCount = 0;
                                        }
                                    }
                                    // Paridad incorrecta: no resetear eosCount
                                    decodeBuffer.Clear();
                                }
                            }
                        } // foreach bit
                    } // for ph
                }

            WriteAudio:;
            };

            waveIn.RecordingStopped += (s, a) =>
            {
                Console.WriteLine("Grabación detenida");
            };

            Console.WriteLine("\nEscuchando micrófono...");
            Console.WriteLine("Presione ENTER para detener | M para cambiar modo HF/VHF\n");

            waveIn.StartRecording();

            while (true)
            {
                var key = Console.ReadKey(intercept: true);

                if (key.Key == ConsoleKey.Enter)
                {
                    // Salir del programa
                    break;
                }
                else if (key.Key == ConsoleKey.M)
                {
                    // Pausar la grabación mientras el usuario elige
                    waveIn.StopRecording();

                    Console.WriteLine("\nSeleccione el modo de demodulacion:");
                    Console.WriteLine("0: HF  (100 bps  - 1615/1785 Hz)");
                    Console.WriteLine("1: VHF (1200 bps - 1300/2100 Hz)");
                    Console.Write("Modo: ");
                    vhfMode = Console.ReadLine()?.Trim() == "1";
                    Console.WriteLine(vhfMode ? "Modo VHF seleccionado." : "Modo HF seleccionado.");

                    // Reiniciar demodulador con el nuevo modo
                    demod = new BFSKDemodulator(vhfMode);
                    maxGrabacionSeg = vhfMode ? 2 : 15;

                    // Resetear toda la máquina de estados
                    estado = Estado.EsperandoInicio;
                    lockedPhase = -1;
                    for (int p = 0; p < PhaseCount; p++) syncBuffers[p].Clear();
                    decodeBuffer.Clear();
                    bitAccumulator.Clear();
                    bitsValidacion = 0;
                    phasingConsecutivos = 0;
                    phasingStartOffset = 0;
                    eosCount = 0;

                    // El mismo waveIn y su DataAvailable handler siguen válidos
                    // porque capturan demod/vhfMode por referencia (closures)
                    Console.WriteLine("Escuchando...\n");
                    waveIn.StartRecording();
                    Console.WriteLine("Presione ENTER para detener | M para cambiar modo HF/VHF\n");
                }
            }

            waveIn.StopRecording();
        }

        public static bool ProcesarBits(string input)

        {

            List<(int Index, int Value)> encontrados = new List<(int, int)>();
            int i = 0;

            // Ventana deslizante: intenta decodificar 10 bits; si no es válido o no es caracter de phasing,
            // desplaza 1 bit; si es válido y es caracter de phasing, consume los 10 bits.

            // AGREGAR: si el mensaje no se sincroniza en N bits, DESCARTAR MENSAJE

            bool sincronizado = true; // usado en la phasing sequence

            //while (!sincronizado)
            //{
            //    if (i + 10 <= input.Length)
            //    {
            //        string ventana = input.Substring(i, 10);
            //        int mensajeInt = Convert.ToInt32(ventana, 2);

            //        if (Decodificador.TryDecodificarMensaje(mensajeInt, out int valor))
            //        {
            //            // mensaje de 10 bits válido según el control
            //            if (PhasingSequence.TryCaracter(valor))
            //            {
            //                // Es un carácter válido de phasing: registrar y consumir los 10 bits
            //                encontrados.Add((i, valor));
            //                i += 10;

            //                const int ventanaDetect = 3;
            //                if (encontrados.Count >= ventanaDetect)
            //                {
            //                    if (PhasingSequence.TryDetect(encontrados, out var pattern))
            //                    {
            //                        Console.WriteLine($"Patrón de phasing detectado: {pattern}");
            //                        sincronizado = true;
            //                    }
            //                }
            //            }
            //            else
            //            {
            //                // Decodificable pero no es el carácter esperado: desplazar 1 bit
            //                i += 1;
            //            }

            //        }
            //        else
            //        {
            //            // No decodificable: desplazar 1 bit
            //            i += 1;
            //        }
            //    }
            //    else
            //    {
            //        break; // o manejar el caso donde no hay suficientes caracteres
            //    }
            //}


            //if (encontrados.Count == 0)
            //{
            //    Console.WriteLine("No se detectaron mensajes válidos en la secuencia.");
            //    return false;
            //}
            //else
            //{
            //    Console.WriteLine($"Phasing sequence encontrada:");
            //    foreach (var e in encontrados)
            //    {
            //        //string printable = (e.Value >= 32 && e.Value <= 126) ? ((char)e.Value).ToString() : ".";
            //        Console.WriteLine($"- Offset {e.Index}: valor numérico = {e.Value}");
            //    }
            //}

            bool formatconfirmed = false;
            bool dxrxconfirmed = false;
            int form = 0;

            // Una vez hecha la sincronizacion, llega el format specifier
            // aca tengo que considerar los DX y RX en cada 4 posiciones
            // ME LOS ESTOY SALTENADO OLIMPICAMNTE, NOSE QUE ACCION TOMAR IS DX != RX

            while (sincronizado && !formatconfirmed)
            {
                if (i + 10 <= input.Length)
                {
                    string ventana = input.Substring(i, 10);
                    int mensajeInt = Convert.ToInt32(ventana, 2);
                    Decodificador.TryDecodificarMensaje(mensajeInt, out int valor);
                    form = FormatSpecifier.Filtro(valor, out int j);
                    // Socorro (112) y All Ships (116) son broadcast — no tienen receptor
                    // específico, DxRx no aplica. Para el resto, verificar DX==RX.
                    bool esBroadcast = (form == 112 || form == 116);
                    dxrxconfirmed = esBroadcast || Decodificador.DxRx(input, i);
                    i = i + 10;
                    if (j == 1 && dxrxconfirmed)
                    {
                        formatconfirmed = true;
                        Console.WriteLine($"Format specifier confirmado: {form}");
                    }
                }
                else
                {
                    break;
                }
            }

            i = i - 10;  // retrocedo 10 para que el switch tome el format specifier correcto

            if (!formatconfirmed)
            {
                Console.WriteLine("Format specifier no confirmado. Descartando mensaje.");
                return false;
            }

            List<int> ECC = new List<int>();

            switch (form)
            {
                case 112:
                    //metodo para formato socorro
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
                        Console.WriteLine($"Hora: {8 | 8 | 8 | 8}");
                        i = i + 40;
                        ECC.Add(08); ECC.Add(08);

                    }
                    i = Socorro.FirstTelecommand(i, input, ECC);

                    for (int k = 0; k < input.Length; k += 10)
                    {
                        int valor;
                        string ventana = input.Substring(k, 10);
                        int mensajeInt = Convert.ToInt32(ventana, 2);
                        Decodificador.TryDecodificarMensaje(mensajeInt, out valor);
                        Console.Write($"{valor} ");
                    }

                    if (i + 10 > input.Length)
                    {
                        Console.WriteLine("Mensaje incompleto (caso 112): stream demasiado corto.");
                        break;
                    }
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

                    break;

                case 116:
                    //metodo para formato all ships
                    ECC.Add(form);
                    i = General.Categoria(i, form, input, ECC);
                    i = General.MMSI_2(i, input, ECC);
                    // leer el primer y el segundo telecomando (MENSAJE 1)
                    i = General.Mensaje_1(i, input, ECC);  // En el primer telecomando decidiría si 3 caracteres o 4 de frecuencia (CASO ESPECIAL)
                                                           // ver que frecuencia/ canal/ etc (MENSAJE 2)
                    byte h = 1;
                    i = General.Mensaje_2(i, input, ECC, h);
                    h++;
                    i = General.Mensaje_2(i, input, ECC, h);

                    // EOS/ECC
                    if (i + 10 > input.Length)
                    {
                        Console.WriteLine("Mensaje incompleto (caso 116): stream demasiado corto.");
                        break;
                    }
                    string win_1 = input.Substring(i, 10);
                    int ms_1 = Convert.ToInt32(win_1, 2);
                    Decodificador.TryDecodificarMensaje(ms_1, out int val_1);
                    ECC.Add(val_1);
                    if (val_1 == 127)
                    {
                        Console.WriteLine("EOS detectado");
                        if (i + 30 <= input.Length)
                            Decodificador.checkecc(i, input, ECC);
                        else
                            Console.WriteLine("Stream demasiado corto para leer ECC.");
                    }

                    break;
                case 114:
                    //metodo para formato grupo
                    break;
                case 120:
                    //metodo para formato individual
                    break;
                case 102:
                    //metodo para formato geografica
                    break;
                case 123:
                    //metodo para formato individual2
                    break;
                default:
                    Console.WriteLine("Formato no reconocido.");
                    return false;
            }
            return false;

        }
    }
}