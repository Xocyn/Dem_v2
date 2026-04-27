using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Dem_v2
{
    internal class Socorro
    {
        public static string Peligro(int valor)
        {
            return valor switch
            {
                100 => "Incendio/Explosión",
                101 => "Inundación",
                102 => "Colision",
                103 => "Encallado",
                104 => "Peligro de zozobra",
                105 => "Naufragio",
                106 => "Deshabilitado y a la deriva",
                107 => "Socorro sin designar",
                108 => "Abandonando la nave",
                109 => "Pirateria/Robo a mano armada",
                110 => "Hombre al agua",
                112 => "EPIRB emitido",
                _ => "PELIGRO NO IDENTIFICADO" // Caso por defecto
            };
        }
        public static string PosteriorCom(int valor)
        {
            return valor switch
            {
                100 => "F3E/G3E ALL MODES TP",
                101 => "F3E/G3E DUPLEX TP",
                109 => "J3E TP",
                113 => "F1B/J2B TTY-FEC",
                115 => "F1B/J2B TTY-ARQ",
                126 => "Sin información",
                _ => "¿?" // Caso por defecto
            };
        }
    }
}