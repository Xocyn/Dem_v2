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
            Cooldown
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

            WaveInEvent waveIn = new WaveInEvent();
            waveIn.DeviceNumber = device;
            waveIn.WaveFormat = new WaveFormat(44100, 16, 1);

            BFSKDemodulator demod = new BFSKDemodulator();

            StringBuilder syncBuffer = new StringBuilder();

            string startPattern = "0101010101";
            string endPattern = "11111110001111111000";

            int maxLen = Math.Max(startPattern.Length, endPattern.Length);

            Estado estado = Estado.EsperandoInicio;

            int cooldownMs = 2000;
            DateTime cooldownHasta = DateTime.MinValue;

            WaveFileWriter writer = null;

            waveIn.DataAvailable += (s, a) =>
            {
                if (estado == Estado.Cooldown)
                {
                    if (DateTime.Now > cooldownHasta)
                    {
                        Console.WriteLine("Cooldown terminado");
                        estado = Estado.EsperandoInicio;
                        ProcesarBits();

                        syncBuffer.Clear();
                    }

                    return;
                }

                string bits = demod.ProcessAudio(a.Buffer, a.BytesRecorded);

                foreach (char bit in bits)
                {
                    syncBuffer.Append(bit);

                    if (syncBuffer.Length > maxLen)
                        syncBuffer.Remove(0, 1);

                    if (estado == Estado.EsperandoInicio)
                    {
                        if (syncBuffer.ToString().EndsWith(startPattern))
                        {
                            Console.WriteLine("DOT PATTERN DETECTADO");

                            estado = Estado.Grabando;

                            syncBuffer.Clear();

                            string fileName = "debug_audio.wav";

                            writer = new WaveFileWriter(fileName, waveIn.WaveFormat);

                            Console.WriteLine($"Grabando audio en {fileName}");
                        }
                    }
                    else if (estado == Estado.Grabando)
                    {
                        if (syncBuffer.ToString().EndsWith(endPattern))
                        {
                            Console.WriteLine("PATRON FIN DETECTADO");

                            writer?.Dispose();
                            writer = null;

                            Console.WriteLine("Archivo WAV guardado.");

                            estado = Estado.Cooldown;

                            cooldownHasta = DateTime.Now.AddMilliseconds(cooldownMs);

                            syncBuffer.Clear();
                        }
                    }
                }

                if (estado == Estado.Grabando && writer != null)
                {
                    writer.Write(a.Buffer, 0, a.BytesRecorded);
                }
            };

            waveIn.RecordingStopped += (s, a) =>
            {
                writer?.Dispose();
                Console.WriteLine("Grabación detenida");
            };

            Console.WriteLine("\nEscuchando micrófono...");
            Console.WriteLine("Presione ENTER para detener el programa.\n");

            waveIn.StartRecording();

            Console.ReadLine();

            waveIn.StopRecording();

            
        }

        public static void ProcesarBits()

        {
            string pathx = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug_audio.wav");
            string input = BFSKDemodulator.DemodulateToString(pathx);

            List<(int Index, int Value)> encontrados = new List<(int, int)>();
            int i = 0;

            // Ventana deslizante: intenta decodificar 10 bits; si no es válido o no es caracter de phasing,
            // desplaza 1 bit; si es válido y es caracter de phasing, consume los 10 bits.

            // AGREGAR: si el mensaje no se sincroniza en N bits, DESCARTAR MENSAJE

            bool sincronizado = false; // usado en la phasing sequence

            while (!sincronizado)
            {
                string ventana = input.Substring(i, 10);
                int mensajeInt = Convert.ToInt32(ventana, 2);

                if (Decodificador.TryDecodificarMensaje(mensajeInt, out int valor))
                {
                    // mensaje de 10 bits válido según el control
                    if (PhasingSequence.TryCaracter(valor))
                    {
                        // Es un carácter válido de phasing: registrar y consumir los 10 bits
                        encontrados.Add((i, valor));
                        i += 10;

                        const int ventanaDetect = 3;
                        if (encontrados.Count >= ventanaDetect)
                        {
                            if (PhasingSequence.TryDetect(encontrados, out var pattern))
                            {
                                Console.WriteLine($"Patrón de phasing detectado: {pattern}");
                                sincronizado = true;
                            }
                        }
                    }
                    else
                    {
                        // Decodificable pero no es el carácter esperado: desplazar 1 bit
                        i += 1;
                    }

                }
                else
                {
                    // No decodificable: desplazar 1 bit
                    i += 1;
                }
            }


            if (encontrados.Count == 0)
            {
                Console.WriteLine("No se detectaron mensajes válidos en la secuencia.");
            }
            else
            {
                Console.WriteLine($"Phasing sequence encontrada:");
                foreach (var e in encontrados)
                {
                    //string printable = (e.Value >= 32 && e.Value <= 126) ? ((char)e.Value).ToString() : ".";
                    Console.WriteLine($"- Offset {e.Index}: valor numérico = {e.Value}");
                }
            }

            bool formatconfirmed = false;
            bool dxrxconfirmed = false;
            int form = 0;

            // Una vez hecha la sincronizacion, llega el format specifier
            // aca tengo que considerar los DX y RX en cada 4 posiciones
            // ME LOS ESTOY SALTENADO OLIMPICAMNTE, NOSE QUE ACCION TOMAR IS DX != RX

            while (sincronizado && !formatconfirmed)
            {
                string ventana = input.Substring(i, 10);
                int mensajeInt = Convert.ToInt32(ventana, 2);
                Decodificador.TryDecodificarMensaje(mensajeInt, out int valor);
                form = FormatSpecifier.Filtro(valor, out int j); // por ahora el filtrado tambien haria trabajo de format
                dxrxconfirmed = Decodificador.DxRx(input, i); // verifica si son iguales los DX y RX
                                                              // el problema surge sino necesito confirmarlos (solo caso socorro y allships)
                i = i + 10;
                if (j == 1 && dxrxconfirmed)
                {
                    formatconfirmed = true;
                    Console.WriteLine($"Format specifier confirmado: {form}");
                }

            }

            i = i - 10;  // retrocedo 10 para que el switch tome el format specifier correcto

            // SINO CONFIRMO FORMATO, DESCARTAR MENSAJE (AGREGAR)

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

                    //for (int k = 0; i+k < input.Length; k += 10)
                    //{
                    //    int valor;
                    //    string ventana = input.Substring(i + k, 10);
                    //    int mensajeInt = Convert.ToInt32(ventana, 2);
                    //    Decodificador.TryDecodificarMensaje(mensajeInt, out valor);
                    //    Console.WriteLine($"{valor}");
                    //}

                    string win = input.Substring(i, 10);
                    int ms = Convert.ToInt32(win, 2);
                    Decodificador.TryDecodificarMensaje(ms, out int val);
                    ECC.Add(val);
                    if (val == 127)
                    {
                        Console.WriteLine("EOS detectado");
                        Decodificador.checkecc(i, input, ECC);
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
                    string win_1 = input.Substring(i, 10);
                    int ms_1 = Convert.ToInt32(win_1, 2);
                    Decodificador.TryDecodificarMensaje(ms_1, out int val_1);
                    ECC.Add(val_1);
                    if (val_1 == 127)
                    {
                        Console.WriteLine("EOS detectado");
                        Decodificador.checkecc(i, input, ECC);
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
                    //metodo para formato no reconocido
                    break;
            }


        }
    }
}
