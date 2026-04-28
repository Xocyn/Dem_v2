using System;
using System.Collections.Generic;
using System.Text;

namespace Dem_v2
{
    public class Respuesta
    {
        static StringBuilder rta = new StringBuilder();
        static List<int> ecc = new List<int>();
        static string MMSI = "889944123"; // MODIFICABLE SEGUN LA COSTERA
        static bool VHF = true;
        
        static public void RespuestaSocorro(List<int> datos_respuesta)
        {
            rta.Clear();
            ecc.Clear();

            Geografica.EliminarPosicionesImpares(datos_respuesta);

            Convertir.ConvertirNumero(116, rta); Convertir.ConvertirNumero(116, rta); ecc.Add(116);
            Convertir.ConvertirNumero(112, rta); ecc.Add(112);
            Convertir.MMSI(rta, ecc, MMSI);
            Convertir.ConvertirNumero(110, rta); ecc.Add(110);
            for (int i = 0; i < datos_respuesta.Count; i++)
            {
                Convertir.ConvertirNumero(datos_respuesta[i], rta); ecc.Add(datos_respuesta[i]);
            }
            Convertir.ConvertirNumero(127, rta); ecc.Add(127);
            Convertir.ConvertirNumero(Convertir.Mod2Sum7Bits(ecc), rta);
            Convertir.ConvertirNumero(127, rta); Convertir.ConvertirNumero(127, rta);
            EOS();

        }

        static public void EOS()
        {
            List<int> phasignseq = new List<int> { 125, 111, 125, 110, 125, 109, 125, 108, 125, 107, 125, 106 }; 
            StringBuilder pss = new StringBuilder();

            foreach (int ps in phasignseq)
            {
                Convertir.ConvertirNumero(ps, pss);
            }

            List<int> inicio_rx = new List<int> { 105, 104 }; // agrego los 105 y 104 al inicio del Rx
            StringBuilder rx = new StringBuilder();
            foreach (int pf in inicio_rx)
            {
                Convertir.ConvertirNumero(pf, rx);
            }
            rx.Append(rta); // armo los Rx 

            StringBuilder resultado = new StringBuilder();

            for (int i = 0; i < rta.Length; i += 10)
            {
                // Extraer 10 caracteres de resultadoConChequeo (o menos en la última iteración)
                int longitud = Math.Min(10, rta.Length - i);
                string aux = rta.ToString(i, longitud);
                resultado.Append(aux);

                // Extraer 10 caracteres de rx (o menos en la última iteración)
                longitud = Math.Min(10, rx.Length - i);
                string aux2 = rx.ToString(i, longitud);
                resultado.Append(aux2);
            }

            pss.Append(resultado);
            StringBuilder dot = new StringBuilder();

            for (int i = 0; i <= 20; i += 1)
            {
                dot.Append(i % 2 == 0 ? "0" : "1");
            }
            dot.Append(pss);

            string rutadesalida = AppDomain.CurrentDomain.BaseDirectory;

            string archivoFinal = Path.Combine(rutadesalida, "respuesta.txt");

            //File.WriteAllText(archivoFinal, pss.ToString().TrimEnd());
            // CON DOT
            File.WriteAllText(archivoFinal, dot.ToString().TrimEnd());

            // MODULACION Y REPRODUCCION DE AUDIO
            BFSKModulator.GenerateWav(archivoFinal, Path.Combine(rutadesalida, "respuesta.wav"), VHF);
            AudioPlayer.Play(Path.Combine(rutadesalida, "respuesta.wav"));

            ecc.Clear();
            rta.Clear();
            pss.Clear();
            rx.Clear();
            resultado.Clear();
            dot.Clear();
        }
        internal class Convertir
        {
            public static void ConvertirNumero(int leido, StringBuilder resultadoConChequeo)
            {

                if (leido >= 0 && leido <= 127)
                {
                    // Paso 1: Convertir a binario (7 bits)
                    string binario = Convert.ToString(leido, 2).PadLeft(7, '0');

                    // Paso 2: Invertir el orden de los bits (MSB ↔ LSB)
                    string binarioInvertido = InvertirBits(binario);

                    // Paso 3: Contar ceros en la secuencia invertida
                    int cantidadCeros = ContarCeros(binarioInvertido);

                    // Paso 4: Convertir cantidad de ceros a binario de 3 bits
                    string bitsChequeo = Convert.ToString(cantidadCeros, 2).PadLeft(3, '0');

                    // Paso 5: Agregar bits de chequeo al final (nuevo LSB)
                    string binarioFinal = binarioInvertido + bitsChequeo;


                    //resultadoConChequeo.Append(binarioFinal + " "); // Agregar espacio después de cada número convertido me mueve los indices
                    resultadoConChequeo.Append(binarioFinal);

                }
                else
                {
                    Console.WriteLine($"✗ Advertencia: {leido} está fuera del rango (0-127)");
                }

                // Función para invertir los bits
                static string InvertirBits(string binario)
                {
                    return new string(binario.Reverse().ToArray());
                }

                // Función para contar los ceros en una cadena binaria
                static int ContarCeros(string binario)
                {
                    return binario.Count(c => c == '0');
                }

            }

            static public void MMSI(StringBuilder rta, List<int> ECC, string numero)
            {
                List<int> mmsi = new List<int>();

                // Agrupar de 2 en 2
                List<string> grupos = AgruparDeDosEnDos(numero);
                foreach (string grupo in grupos)
                {
                    int value = Convert.ToInt32(grupo, 10);
                    mmsi.Add(value);
                }

                foreach (int mm in mmsi)
                {
                    ECC.Add(mm);
                    ConvertirNumero(mm, rta);
                }

            }

            static List<string> AgruparDeDosEnDos(string numero)
            {
                List<string> grupos = new List<string>();

                // Si es impar, agregar '0' al final
                if (numero.Length % 2 != 0)
                {
                    numero += "0"; // se le agrega un 0 al final para completar el ultimo grupo de 2 (NORMA)
                }

                // Agrupar de 2 en 2
                for (int i = 0; i < numero.Length; i += 2)
                {
                    string grupo = numero.Substring(i, 2);
                    grupos.Add(grupo);
                }

                return grupos;
            }

            public static int Mod2Sum7Bits(List<int> values)
            {
                if (values == null || values.Count == 0)
                    return 0;

                int result = 0;

                foreach (int v in values)
                {
                    result ^= v; // XOR acumulativo (suma módulo 2)
                }

                // Nos quedamos con los 7 bits menos significativos
                result &= 0x7F;

                return result;
            }

        }
    }
}
