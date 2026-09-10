using System;
using System.Collections.Generic;
using System.Linq;

namespace ExploracionPlanes
{
    // Lógica pura (sin ESAPI) de prioridad para elegir el StructureId auto-matcheado de una fila,
    // compartida por Form2_DosPlanes (plan1 y plan2). Mismo orden de prioridad que ya usaba
    // Form2.asociarFila: exacto/alias -> memoria guardada en disco -> mejor candidato por distancia
    // (si entra dentro del umbral) -> sin match. Aislada acá para poder testearla sin instanciar ESAPI.
    public static class MatchingEstructuras
    {
        // distanciaMaxima default = Estructura.DistanciaMaximaSugerida (4), repetido como literal (no
        // referenciado directo) para que esta clase no dependa de Estructura/ESAPI y sea testeable sola.
        public static string ElegirStructureId(string idExacto, string idMemoria, IEnumerable<string> opcionesValidas, Tuple<string, int> mejorCandidatoPorDistancia, int distanciaMaxima = 4)
        {
            if (!string.IsNullOrEmpty(idExacto))
            {
                return idExacto;
            }
            if (!string.IsNullOrEmpty(idMemoria) && opcionesValidas.Contains(idMemoria))
            {
                return idMemoria;
            }
            if (mejorCandidatoPorDistancia != null && mejorCandidatoPorDistancia.Item2 <= distanciaMaxima)
            {
                return mejorCandidatoPorDistancia.Item1;
            }
            return "";
        }
    }
}
