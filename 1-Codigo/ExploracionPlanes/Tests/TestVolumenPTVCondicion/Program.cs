using System;
using System.Collections.Generic;
using System.Linq;

// Réplica aislada (sin ESAPI) de Form2.ptvsMatcheadosEnGrilla / volPTVParaCondicion: ya no se
// pregunta "¿cuál es el PTV?" por diálogo. En su lugar, se identifican los PTV ya matcheados en la
// grilla de estructuras (uno o más si se duplicó la fila), y para cada restricción condicionada por
// volumen de PTV:
//   - si la restricción es sobre un PTV, se usa el volumen de ESE PTV (permite una restricción
//     distinta por cada PTV duplicado).
//   - si es sobre otra estructura (ej. Lung), se usa la suma de los volumenes de todos los PTVs
//     matcheados.

class Estructura
{
    public string Id = "";
    public string DicomType = "";
    public double Volume;
}

class Program
{
    static int fallas = 0;

    static void Assert(string nombre, bool ok)
    {
        Console.WriteLine($"{(ok ? "OK  " : "FAIL")} {nombre}");
        if (!ok) fallas++;
    }

    static List<Estructura> PtvsMatcheados(List<Estructura?> resueltas)
    {
        return resueltas
            .Where(s => s != null && s.DicomType == "PTV")
            .Select(s => s!)
            .GroupBy(s => s.Id)
            .Select(g => g.First())
            .ToList();
    }

    static double VolPTVParaCondicion(Estructura? estructuraRestriccion, List<Estructura> ptvsMatcheados)
    {
        if (estructuraRestriccion != null && estructuraRestriccion.DicomType == "PTV")
        {
            return estructuraRestriccion.Volume;
        }
        return ptvsMatcheados.Sum(s => s.Volume);
    }

    static void Main()
    {
        var ptv1 = new Estructura { Id = "PTV", DicomType = "PTV", Volume = 120 };
        var ptv2 = new Estructura { Id = "PTV_Low (2)_real", DicomType = "PTV", Volume = 400 };
        var lung = new Estructura { Id = "Lung", DicomType = "ORGAN", Volume = 1500 };

        // Caso: sin duplicar, un solo PTV matcheado.
        var matcheados1 = PtvsMatcheados(new List<Estructura?> { ptv1, lung });
        Assert("Un solo PTV matcheado", matcheados1.Count == 1 && matcheados1[0].Id == "PTV");
        Assert("Restricción de PTV usa su propio volumen (no el de otro PTV)", VolPTVParaCondicion(ptv1, matcheados1) == 120);
        Assert("Restricción de Lung usa el volumen del único PTV matcheado", VolPTVParaCondicion(lung, matcheados1) == 120);

        // Caso: PTV duplicado (dos PTVs reales matcheados a dos filas distintas).
        var matcheados2 = PtvsMatcheados(new List<Estructura?> { ptv1, ptv2, lung });
        Assert("Dos PTVs matcheados tras duplicar", matcheados2.Count == 2);
        Assert("Restricción del primer PTV usa SU volumen, no la suma", VolPTVParaCondicion(ptv1, matcheados2) == 120);
        Assert("Restricción del segundo PTV (duplicado) usa SU volumen, no la suma", VolPTVParaCondicion(ptv2, matcheados2) == 400);
        Assert("Restricción de Lung usa la SUMA de ambos PTVs", VolPTVParaCondicion(lung, matcheados2) == 520);

        // Caso: ninguna estructura matcheada es PTV -> no hace falta preguntar nada, suma vacía = 0.
        var matcheados0 = PtvsMatcheados(new List<Estructura?> { lung });
        Assert("Sin PTV matcheado, la lista queda vacía", matcheados0.Count == 0);
        Assert("Sin PTV matcheado, Lung condicionado por volumen de PTV da 0 (no hay PTV para sumar)", VolPTVParaCondicion(lung, matcheados0) == 0);

        Console.WriteLine();
        Console.WriteLine(fallas == 0 ? "TODOS LOS CHEQUEOS OK" : $"{fallas} CHEQUEO(S) FALLARON");
        Environment.Exit(fallas == 0 ? 0 : 1);
    }
}
