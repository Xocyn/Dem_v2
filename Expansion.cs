using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace Dem_v2
{
    internal class Expansion
    {
        public static int Especificador(int i, string input)
        {
            //List<int> Expansion = new List<int>();
            //for (int k = i; k < input.Length; k += 10) 
            //{
            //    string ventana = input.Substring(i + k, 10);
            //    int mensajeInt = Convert.ToInt32(ventana, 2);
            //    Decodificador.TryDecodificarMensaje(mensajeInt, out int valor);
            //    Expansion.Add(valor);
            //}

            //Geografica.EliminarPosicionesImpares(Expansion); // Solo me quedo con los DX, no verifica RX
            List<int> ECC = new List<int>();

            string ventana = input.Substring(i, 10);
            int mensajeInt = Convert.ToInt32(ventana, 2);
            Decodificador.TryDecodificarMensaje(mensajeInt, out int valor); ECC.Add(valor);
            i = i + 20;
            Console.WriteLine($"Formato de Extension ({valor})");

            switch (valor)
            {
                case 100:
                    //  Resolusion mejorada de la posicion
                    i = res_mejorada(i, input, ECC);
                    break;
                case 101:
                    // Origen y punto de referencia de posicion 
                    i = origen_punto_ref(i, input, ECC);
                    break;
                case 102:
                    // Velocidad actual del barco
                    i = velocidad_actual(i, input, ECC);
                    break;
                case 103:
                    // Ruta actual del barco
                    i = ruta_actual(i, input, ECC);
                    break;
                case 104:
                    // Identificador adicional de la estacion
                    i = identificador_adicional(i, input, ECC);
                    break;
                case 105:
                    // Zona geofrafica ampliada
                    i = zona_geografica_ampliada(i, input, ECC);
                    break;
                case 106:
                    // Numero de personas a bordo
                    i = numero_personas_a_bordo(i, input, ECC);
                    break;
                default:
                    // No identificado
                    Console.WriteLine("Caracter no identificado");
                    break;
            }

            // leer el caracter y si es EOS se va al final y sino es el Mensaje 2
            string ventana2 = input.Substring(i, 10);
            int mensajeInt2 = Convert.ToInt32(ventana2, 2);
            Decodificador.TryDecodificarMensaje(mensajeInt2, out int valor2); ECC.Add(valor2);

            if (valor2 == 127 || valor2 == 122 || valor2 == 117)
            {
                // EOS
                Console.WriteLine("EOS detectado");
                Decodificador.Mod2Sum7Bits(i, input, ECC);
                return i;
            }
            else
            {
                i = i + 20;
                switch (valor2)
                {
                    case 100:
                        //  Resolusion mejorada de la posicion
                        i = res_mejorada(i, input, ECC);
                        break;
                    case 101:
                        // Origen y punto de referencia de posicion 
                        i = origen_punto_ref(i, input, ECC);
                        break;
                    case 102:
                        // Velocidad actual del barco
                        i = velocidad_actual(i, input, ECC);
                        break;
                    case 103:
                        // Ruta actual del barco
                        i = ruta_actual(i, input, ECC);
                        break;
                    case 104:
                        // Identificador adicional de la estacion
                        i = identificador_adicional(i, input, ECC);
                        break;
                    case 105:
                        // Zona geofrafica ampliada
                        i = zona_geografica_ampliada(i, input, ECC);
                        break;
                    case 106:
                        // Numero de personas a bordo
                        i = numero_personas_a_bordo(i, input, ECC);
                        break;
                    default:
                        // No identificado
                        Console.WriteLine("Caracter no identificado");
                        break;
                }
            }

            string ventana3 = input.Substring(i, 10);
            int mensajeInt3 = Convert.ToInt32(ventana3, 2);
            Decodificador.TryDecodificarMensaje(mensajeInt3, out int valor3); ECC.Add(valor3);
            // EOS
            if (valor2 == 127 || valor2 == 122 || valor2 == 117)
            {
                // EOS
                Console.WriteLine("EOS detectado");
                Decodificador.Mod2Sum7Bits(i, input, ECC);
                return i;
            }
            else
            {
                Console.WriteLine("Caracter no identificado");
            }

            return i;
        }

        private static int res_mejorada(int i, string input, List<int> ECC)
        {
            // ACA ME FALTA SABER SI ES PETICION O SI NO HAY DATOS //
            // PUEDE SER 1 SOLO CARACTER //
            // Se leen 4 caracteres (40 bits) y se decodifican a una resolucion mejorada de la posicion
            List<int> res = new List<int>();
            for (int k = 0; k < 80; k += 10) // 80 porque son 4 caracteres
            {
                string ventana = input.Substring(i + k, 10);
                int mensajeInt = Convert.ToInt32(ventana, 2);
                Decodificador.TryDecodificarMensaje(mensajeInt, out int valor);
                res.Add(valor);
            }

            Geografica.EliminarPosicionesImpares(res); // Solo me quedo con los DX
            foreach (int val in res)
            {
                ECC.Add(val);
            }

            if (res[0] == 110)
            {
                Console.WriteLine("Peticion de datos");
                i = i + 20;
                return i;
            }
            else if (res[0] == 126)
            {
                Console.WriteLine("Ningun dato disponible");
                i = i + 20;
                return i;
            }

            List<string> res_I = res
            .Select(x => x.ToString("D2"))
            .ToList();

            List<int> res_D = General.SplitDigits2(res_I);

            Console.WriteLine($"Mejora de Latitud {res_D[0]}{res_D[1]}{res_D[2]}{res_D[3]}'' ");
            Console.WriteLine($"Mejora de Longitud {res_D[4]}{res_D[5]}{res_D[6]}{res_D[7]}'' ");

            return i + 80;
        }

        private static int origen_punto_ref(int i, string input, List<int> ECC)
        {
            // Se leen 3 caracteres (30 bits) y se decodifican a un origen y punto de referencia de posicion
            List<int> og = new List<int>();
            for (int k = 0; k < 60; k += 10) // 60 porque son 3 caracteres
            {
                string ventana = input.Substring(i + k, 10);
                int mensajeInt = Convert.ToInt32(ventana, 2);
                Decodificador.TryDecodificarMensaje(mensajeInt, out int valor);
                og.Add(valor);
            }

            Geografica.EliminarPosicionesImpares(og); // Solo me quedo con los DX
            foreach (int val in og)
            {
                ECC.Add(val);
            }

            if (og[0] == 110)
            {
                Console.WriteLine("Peticion de datos");
                i = i + 20;
                return i;
            }
            else if (og[0] == 126)
            {
                Console.WriteLine("Ningun dato disponible");
                i = i + 20;
                return i;
            }

            string dispositivo = "";
            string punto_ref = "";

            switch (og[0])
            {
                case 0:
                    dispositivo = "NO VALIDO";
                    break;
                case 1:
                    dispositivo = "GPS diferencial";
                    break;
                case 2:
                    dispositivo = "GPS sin corregir";
                    break;
                case 3:
                    dispositivo = "LORAN-C diferencial";
                    break;
                case 4:
                    dispositivo = "LORAN-C sin dorregir";
                    break;
                case 5:
                    dispositivo = "GLONASS";
                    break;
                case 6:
                    dispositivo = "PUNTO DE REFERENCIA DE RADAR";
                    break;
                case 7:
                    dispositivo = "DECCA";
                    break;
                case 8:
                    dispositivo = "OTRA REFERENCIA";
                    break;
                default:
                    dispositivo = "¿¿??";
                    break;
            }

            switch (og[2])
            {
                case 0:
                    punto_ref = "WGS-84";
                    break;
                case 1:
                    punto_ref = "WGS-72";
                    break;
                case 2:
                    punto_ref = "OTRO";
                    break;
                default:
                    break;
            }

            Console.WriteLine($"Dato de posicion procedentes de: {dispositivo}");
            Console.WriteLine($"Presicion del punto de referencia: {og[1]}");
            Console.WriteLine($"Punto de referencia: {punto_ref}");

            return i + 60;

        }

        private static int velocidad_actual(int i, string input, List<int> ECC)
        {
            // Se leen 2 caracteres (20 bits) y se decodifican a la velocidad actual del barco
            List<int> vel = new List<int>();
            for (int k = 0; k < 40; k += 10) // 40 porque son 2 caracteres
            {
                string ventana = input.Substring(i + k, 10);
                int mensajeInt = Convert.ToInt32(ventana, 2);
                Decodificador.TryDecodificarMensaje(mensajeInt, out int valor);
                vel.Add(valor);
            }
            Geografica.EliminarPosicionesImpares(vel); // Solo me quedo con los DX
            foreach (int val in vel)
            {
                ECC.Add(val);
            }

            if (vel[0] == 110)
            {
                Console.WriteLine("Peticion de datos");
                i = i + 20;
                return i;
            }
            else if (vel[0] == 126)
            {
                Console.WriteLine("Ningun dato disponible");
                i = i + 20;
                return i;
            }

            List<string> vel_i = vel
            .Select(x => x.ToString("D2"))
            .ToList();

            List<int> vel_d = General.SplitDigits2(vel_i);

            Console.WriteLine($"Velocidad actual del barco: {vel_d[0]}{vel_d[1]}{vel_d[2]},{vel_d[3]} nudos");
            return i + 40;
        }

        private static int ruta_actual(int i, string input, List<int> ECC)
        {
            // Se leen 2 caracteres (20 bits) y se decodifica la ruta actual del barco
            List<int> ruta = new List<int>();
            for (int k = 0; k < 40; k += 10) // 40 porque son 2 caracteres
            {
                string ventana = input.Substring(i + k, 10);
                int mensajeInt = Convert.ToInt32(ventana, 2);
                Decodificador.TryDecodificarMensaje(mensajeInt, out int valor);
                ruta.Add(valor);
            }
            Geografica.EliminarPosicionesImpares(ruta); // Solo me quedo con los DX
            foreach (int val in ruta)
            {
                ECC.Add(val);
            }

            if (ruta[0] == 110)
            {
                Console.WriteLine("Peticion de datos");
                i = i + 20;
                return i;
            }
            else if (ruta[0] == 126)
            {
                Console.WriteLine("Ningun dato disponible");
                i = i + 20;
                return i;
            }

            List<string> ruta_i = ruta
            .Select(x => x.ToString("D2"))
            .ToList();

            List<int> ruta_d = General.SplitDigits2(ruta_i);
            Console.WriteLine($"Ruta actual del barco: {ruta_d[0]}{ruta_d[1]}{ruta_d[2]},{ruta_d[3]} grados");
            return i + 40;
        }

        private static int identificador_adicional(int i, string input, List<int> ECC)
        {
            // Se leen 10 caracteres (100 bits) y se decodifica un identificador adicional de la estacion
            List<int> id = new List<int>();
            for (int k = 0; k < 200; k += 10) // 200 porque son 10 caracteres
            {
                string ventana = input.Substring(i + k, 10);
                int mensajeInt = Convert.ToInt32(ventana, 2);
                Decodificador.TryDecodificarMensaje(mensajeInt, out int valor);
                id.Add(valor);
            }
            Geografica.EliminarPosicionesImpares(id); // Solo me quedo con los DX
            foreach (int val in id)
            {
                ECC.Add(val);
            }

            if (id[0] == 110)
            {
                Console.WriteLine("Peticion de datos");
                i = i + 20;
                return i;
            }
            else if (id[0] == 126)
            {
                Console.WriteLine("Ningun dato disponible");
                i = i + 20;
                return i;
            }

            Console.WriteLine("Identificador adicional de la estacion: " + string.Join("", id.Select(x => Caracter(x))));

            return i + 200;
        }

        private static int zona_geografica_ampliada(int i, string input, List<int> ECC)
        {
            // se leen 12 caracteres (120 bits)
            List<int> zona = new List<int>();
            for (int k = 0; k < 240; k += 10) // 240 porque son 12 caracteres
            {
                string ventana = input.Substring(i + k, 10);
                int mensajeInt = Convert.ToInt32(ventana, 2);
                Decodificador.TryDecodificarMensaje(mensajeInt, out int valor);
                zona.Add(valor);
            }

            Geografica.EliminarPosicionesImpares(zona);
            foreach (int val in zona)
            {
                ECC.Add(val);
            }

            List<string> zona_i = zona
            .Select(x => x.ToString("D2"))
            .ToList();

            // PUEDEN EXISTIR CARACTERES 126
            // SI LEE 126 OCUPA MAS DE 2 DIGITOS

            List<int> zona_d = General.SplitDigits2(zona_i);

            Console.WriteLine($"Mejora de Latitud ,{zona_d[0]}{zona_d[1]}{zona_d[2]}{zona_d[3]}'' ");
            Console.WriteLine($"Mejora de Longitud ,{zona_d[4]}{zona_d[5]}{zona_d[6]}{zona_d[7]}'' ");

            Console.WriteLine($"Resolucion adicional ventana vertical: {zona[8]}{zona[9]}{zona[10]}{zona[11]}");
            Console.WriteLine($"Resolucion adicional ventana horizontal: {zona[12]}{zona[13]}{zona[14]}{zona[15]}");

            if (zona[8] == 126 || zona[9] == 126)
            {
                Console.WriteLine("No se dispone estimacion de velocidad");
            }
            else
            {
                Console.WriteLine($"Velocidad actual del barco: {zona[16]}{zona[17]}{zona[18]},{zona[19]} nudos");
            }

            if (zona[10] == 126 || zona[11] == 126)
            {
                Console.WriteLine("No se dispone estimacion de trayectoria");
            }
            else
            {
                Console.WriteLine($"Trayectoria actual del barco: {zona[20]}{zona[21]}{zona[22]},{zona[23]} grados");
            }

            return i + 240;
        }

        private static int numero_personas_a_bordo(int i, string input, List<int> ECC)
        {
            // Se leen 2 caracteres (20 bits) y se decodifica el numero de personas a bordo
            List<int> personas = new List<int>();
            for (int k = 0; k < 40; k += 10) // 40 porque son 2 caracteres
            {
                string ventana = input.Substring(i + k, 10);
                int mensajeInt = Convert.ToInt32(ventana, 2);
                Decodificador.TryDecodificarMensaje(mensajeInt, out int valor);
                personas.Add(valor);
            }
            Geografica.EliminarPosicionesImpares(personas); // Solo me quedo con los DX

            foreach (int val in personas)
            {
                ECC.Add(val);
            }

            if (personas[0] == 110)
            {
                Console.WriteLine("Peticion de datos");
                i = i + 20;
                return i;
            }
            else if (personas[0] == 126)
            {
                Console.WriteLine("Ningun dato disponible");
                i = i + 20;
                return i;
            }

            //List<string> personas_i = personas
            //.Select(x => x.ToString("D2"))
            //.ToList();
            //List<int> personas_d = General.SplitDigits2(personas_i);
            //Console.WriteLine($"Numero de personas a bordo: {personas_d[0]}{personas_d[1]}{personas_d[2]}{personas_d[3]}");

            string ppol = string.Join("", personas.Select(x => x.ToString("D2")));
            Console.WriteLine($"Numero de personas a bordo: {ppol}");


            return i + 40;
        }

        private static string Caracter(int h)
        {
            return h switch
            {
                0 => "0",
                1 => "1",
                2 => "2",
                3 => "3",
                4 => "4",
                5 => "5",
                6 => "6",
                7 => "7",
                8 => "8",
                9 => "9",
                10 => "Sin utilizar",
                11 => "A",
                12 => "B",
                13 => "C",
                14 => "D",
                15 => "E",
                16 => "F",
                17 => "G",
                18 => "H",
                19 => "I",
                20 => "J",
                21 => "K",
                22 => "L",
                23 => "M",
                24 => "N",
                25 => "O",
                26 => "P",
                27 => "Q",
                28 => "R",
                29 => "S",
                30 => "T",
                31 => "U",
                32 => "V",
                33 => "W",
                34 => "X",
                35 => "Y",
                36 => "Z",
                37 => ".",
                38 => ",",
                39 => "-",
                40 => "/",
                41 => " ",
                _ => "¿¿??"
            };
        }
    }
}
