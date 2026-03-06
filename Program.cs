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

            // buffers de bits
            StringBuilder syncBuffer = new StringBuilder();
            StringBuilder frameBuffer = new StringBuilder();

            bool locked = false;

            // ejemplo de secuencia de sincronismo
            StringBuilder dot = new StringBuilder();
            for (int i = 0; i <= 100; i += 1)
            {
                dot.Append(i % 2 == 0 ? "0" : "1");
            }
            string syncPattern = dot.ToString();

            int frameLength = 600; // longitud estimada del mensaje

            waveIn.DataAvailable += (s, a) =>
            {
                string bits = demod.ProcessAudio(a.Buffer, a.BytesRecorded);

                foreach (char bit in bits)
                {
                    if (!locked)
                    {
                        syncBuffer.Append(bit);

                        if (syncBuffer.Length > syncPattern.Length)
                            syncBuffer.Remove(0, 1);

                        if (syncBuffer.ToString() == syncPattern)
                        {
                            Console.WriteLine("DOT PATTERN DETECTADO");

                            locked = true;
                            frameBuffer.Clear();
                        }
                    }
                    else
                    {
                        frameBuffer.Append(bit);

                        if (frameBuffer.Length >= frameLength)
                        {
                           
                            ProcesarBits(frameBuffer.ToString());

                            // reset
                            locked = false;
                            syncBuffer.Clear();
                            frameBuffer.Clear();
                        }
                    }
                }


            };

            waveIn.RecordingStopped += (s, a) =>
            {
                Console.WriteLine("Grabación detenida");
            };

            Console.WriteLine("\nGrabando... hable al micrófono.");
            Console.WriteLine("Presione ENTER para detener.\n");

            waveIn.StartRecording();

            Console.ReadLine();

            waveIn.StopRecording();
        }
        //static void Main(string[] args)
        //{
        //    Console.WriteLine("Dispositivos de entrada disponibles:\n");

        //    for (int i = 0; i < WaveInEvent.DeviceCount; i++)
        //    {
        //        var caps = WaveInEvent.GetCapabilities(i);
        //        Console.WriteLine($"{i}: {caps.ProductName}");
        //    }

        //    Console.WriteLine("\nSeleccione el numero del dispositivo:");
        //    int device = int.Parse(Console.ReadLine());

        //    WaveInEvent waveIn = new WaveInEvent();

        //    waveIn.DeviceNumber = device;
        //    waveIn.WaveFormat = new WaveFormat(44100, 16, 1);
        //    BFSKDemodulator demod = new BFSKDemodulator();

        //    waveIn.DataAvailable += (s, a) =>
        //    {
        //        short max = 0;
        //        string bits = demod.ProcessAudio(a.Buffer, a.BytesRecorded);

        //        Console.WriteLine(bits);

        //        for (int i = 0; i < a.BytesRecorded; i += 2)
        //        {
        //            short sample = BitConverter.ToInt16(a.Buffer, i);

        //            if (Math.Abs(sample) > max)
        //                max = Math.Abs(sample);
        //        }

        //        Console.WriteLine($"Nivel audio: {max}");  // ACA DEBERIA AGREGAR EL DEMODULADOR 
        //    };

        //      // medición de nivel de audio
        //      short max = 0;

        //      for (int i = 0; i < a.BytesRecorded; i += 2)
        //      {
        //          short sample = BitConverter.ToInt16(a.Buffer, i);

        //           if (Math.Abs(sample) > max)
        //              max = Math.Abs(sample);
        //      }

        //Console.WriteLine($"Nivel audio: {max}");

        //    waveIn.RecordingStopped += (s, a) =>
        //    {
        //        Console.WriteLine("Grabación detenida");
        //    };

        //    Console.WriteLine("\nGrabando... hable al micrófono.");
        //    Console.WriteLine("Presione ENTER para detener.\n");

        //    waveIn.StartRecording();

        //    Console.ReadLine();

        //    waveIn.StopRecording();
        //}


        //string input = BFSKDemodulator.DemodulateToString(pathx);

        public static void ProcesarBits(string input)
        {
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
