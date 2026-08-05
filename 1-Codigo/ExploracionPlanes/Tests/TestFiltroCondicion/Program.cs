using System;
using System.Collections.Generic;
using System.Linq;

// Réplica aislada (sin ESAPI) del patrón de loop de Form2_DosPlanes.llenarDGVAnalisis:
// recorrer restricciones de la plantilla, saltar las que no cumplen su Condicion,
// y agregar una fila por cada restricción que sí aplica. Antes del fix no había
// filtro (bug #4) y el índice de fila para el botón de RestriccionDosisMax usaba
// el índice del loop de plantilla ("i") en vez del índice de fila real ("j") (bug #5).
// Acá se simula ambos loops con datos inventados y se compara.

class Restriccion
{
    public string Id;
    public bool Aplica; // simula restriccion.condicion == null || CumpleCondicion(...)
}

class Program
{
    static int fallas = 0;

    static void Assert(string nombre, bool ok)
    {
        Console.WriteLine($"{(ok ? "OK  " : "FAIL")} {nombre}");
        if (!ok) fallas++;
    }

    // Comportamiento VIEJO: sin filtro de Condicion, fila = i siempre (bug #4, y por eso nunca diverge de j -> bug #5 no se manifestaba)
    static List<string> LoopViejo(List<Restriccion> restricciones)
    {
        var filas = new List<string>();
        for (int i = 0; i < restricciones.Count; i++)
        {
            filas.Add(restricciones[i].Id); // se analiza SIEMPRE, aplique o no la condición
        }
        return filas;
    }

    // Comportamiento NUEVO: filtra por Condicion (fix #4), índice de fila = j (fix #5)
    static List<string> LoopNuevo(List<Restriccion> restricciones)
    {
        var filas = new List<string>();
        int j = 0;
        for (int i = 0; i < restricciones.Count; i++)
        {
            var restriccion = restricciones[i];
            if (!restriccion.Aplica)
            {
                continue; // no agrega fila, j no avanza
            }
            filas.Add(restriccion.Id); // fila filas[j]
            j++;
        }
        return filas;
    }

    static void Main()
    {
        var restricciones = new List<Restriccion>
        {
            new Restriccion { Id = "R0_PTV_D95", Aplica = true },
            new Restriccion { Id = "R1_MEDULA_5fx", Aplica = false },  // ej: condición NumFx=5, plan tiene 3 fx
            new Restriccion { Id = "R2_PULMON", Aplica = true },
            new Restriccion { Id = "R3_RIÑON_5fx", Aplica = false },
            new Restriccion { Id = "R4_HIGADO", Aplica = true },
        };

        var filasViejo = LoopViejo(restricciones);
        var filasNuevo = LoopNuevo(restricciones);

        Console.WriteLine("Filas (viejo, sin filtro): " + string.Join(", ", filasViejo));
        Console.WriteLine("Filas (nuevo, con filtro): " + string.Join(", ", filasNuevo));

        // Viejo: bug #4 confirmado -> analiza TODAS, incluso las que no aplican (R1, R3 aparecen)
        Assert("Viejo analiza restricciones que no aplican (bug #4 reproducido)", filasViejo.Contains("R1_MEDULA_5fx") && filasViejo.Contains("R3_RIÑON_5fx"));
        Assert("Viejo agrega una fila por cada restricción de la plantilla, sin filtrar", filasViejo.Count == restricciones.Count);

        // Nuevo: fix #4 -> solo aparecen las que aplican, en orden
        var esperadoNuevo = new List<string> { "R0_PTV_D95", "R2_PULMON", "R4_HIGADO" };
        Assert("Nuevo filtra las que no aplican (fix #4)", filasNuevo.SequenceEqual(esperadoNuevo));

        // fix #5: el índice de fila para post-proceso (ej. botón de RestriccionDosisMax) debe ser
        // la posición REAL en la grilla (j), no la posición en la plantilla (i). Si R1 se salta,
        // R2 debe caer en la fila 1 de la grilla, no en la fila 2.
        int indiceFilaR2_esperado = 1;
        int indiceFilaR2_real = filasNuevo.IndexOf("R2_PULMON");
        Assert("fix #5: R2 cae en la fila j=1 (no en i=2) tras saltear R1", indiceFilaR2_real == indiceFilaR2_esperado);

        int indiceFilaR4_esperado = 2;
        int indiceFilaR4_real = filasNuevo.IndexOf("R4_HIGADO");
        Assert("fix #5: R4 cae en la fila j=2 (no en i=4) tras saltear R1 y R3", indiceFilaR4_real == indiceFilaR4_esperado);

        Console.WriteLine();
        Console.WriteLine(fallas == 0 ? "TODOS LOS CHEQUEOS OK" : $"{fallas} CHEQUEO(S) FALLARON");
        Environment.Exit(fallas == 0 ? 0 : 1);
    }
}
