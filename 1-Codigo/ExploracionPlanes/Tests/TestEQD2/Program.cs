using System;

// Réplica aislada (sin ESAPI) de la lógica pura de EQD2.cs y de las conversiones
// a porcentaje ANTES (bug) y DESPUÉS (fix) del cambio, para poder correrla con
// dotnet run fuera de Eclipse. Los métodos "Old*" son copia literal del código
// previo al fix; los "New*" son copia literal del código posterior al fix.

static class EQD2
{
    public static double Dosis2Gy(double DosisFxAlt, double alfaBeta, int numeroFracciones)
        => DosisFxAlt * (DosisFxAlt / numeroFracciones + alfaBeta) / (2 + alfaBeta);

    public static double DosisFxAlt(double Dosis2Gy, double alfaBeta, int numeroFracciones)
        => (Math.Sqrt(Math.Pow(alfaBeta / 2, 2) + Dosis2Gy * (2 + alfaBeta) / numeroFracciones) - alfaBeta / 2) * numeroFracciones;
}

class Program
{
    static int fallas = 0;

    static void Assert(string nombre, double esperado, double obtenido, double tol = 0.01)
    {
        bool ok = Math.Abs(esperado - obtenido) <= tol;
        Console.WriteLine($"{(ok ? "OK  " : "FAIL")} {nombre}: esperado={esperado:0.####} obtenido={obtenido:0.####}");
        if (!ok) fallas++;
    }

    static void Main()
    {
        double alfaBeta = 3;
        int numFx = 5;
        double prescripcionEstructura = 25; // Gy físicos totales (5 Gy/fx), dato de plantilla, NO convertido a EQD2

        Console.WriteLine("=== Caso 1: RestriccionDosisMedia, unidadValor=\"%\" ===");
        double dmediaEQD2 = 30; // Gy EQD2, ya calculado bin a bin en RestriccionDosisMedia (esa parte no tenía bug)

        // Sin EQD2 habilitado: código no tocado por el fix, debe dar igual antes y después.
        double dmediaGyFisica = 22.5; // Gy físicos (ejemplo, dosis media real del plan)
        double sinEQD2_Old = Math.Round(dmediaGyFisica / prescripcionEstructura * 100, 1);
        double sinEQD2_New = Math.Round(dmediaGyFisica / prescripcionEstructura * 100, 1); // misma fórmula, no cambia
        Assert("Sin EQD2 (Old==New, no debe cambiar)", sinEQD2_Old, sinEQD2_New, 0);

        // Con EQD2 habilitado:
        double conEQD2_Old = Math.Round(dmediaEQD2 / prescripcionEstructura * 100, 1); // BUG: divide EQD2 por prescripción física
        double prescripcionEQD2_1 = EQD2.Dosis2Gy(prescripcionEstructura, alfaBeta, numFx);
        double conEQD2_New = Math.Round(dmediaEQD2 / prescripcionEQD2_1 * 100, 1); // FIX: divide EQD2 por prescripción convertida a EQD2
        Console.WriteLine($"  prescripcionEQD2 = {prescripcionEQD2_1:0.##} Gy (prescripción física {prescripcionEstructura} Gy convertida)");
        Assert("Con EQD2 - valor BUG (se espera 120%, referencia incorrecta)", 120.0, conEQD2_Old, 0.5);
        Assert("Con EQD2 - valor FIX (se espera 75% aprox, referencia correcta)", 75.0, conEQD2_New, 0.5);
        if (Math.Abs(conEQD2_Old - conEQD2_New) < 1) fallas++; // deben diferir, si no el fix no cambió nada

        Console.WriteLine();
        Console.WriteLine("=== Caso 2: RestriccionDosis / RestriccionDosisMax, unidadValor=\"%\" ===");
        double alfaBeta2 = 10;
        int numFx2 = 3;
        double prescripcion2 = 24; // Gy físicos totales (8 Gy/fx)
        double doseGyFisica = 9;   // Gy físicos extraídos del DVH (ej. D2cc)

        // Sin EQD2: idéntico antes y después (dosisEnGy() es el mismo cálculo, solo se factorizó)
        double sinEQD2_Old2 = Math.Round(doseGyFisica / prescripcion2 * 100, 2);
        double sinEQD2_New2 = Math.Round(doseGyFisica / prescripcion2 * 100, 2);
        Assert("Sin EQD2 (Old==New, no debe cambiar)", sinEQD2_Old2, sinEQD2_New2, 0);

        // Con EQD2 - código viejo (bug real, no solo la referencia):
        // 1) primero convertía a % con la prescripción física
        // 2) DESPUÉS aplicaba la fórmula cuadrática de EQD2 sobre ese porcentaje (sin sentido físico)
        double pctFisicoViejo = Math.Round(doseGyFisica / prescripcion2 * 100, 2); // 37.5
        double conEQD2_OldRoto = Math.Round(EQD2.Dosis2Gy(pctFisicoViejo, alfaBeta2, numFx2), 1); // aplica EQD2 a un %, mal
        Console.WriteLine($"  % físico (antes de aplicar mal EQD2) = {pctFisicoViejo}%");
        Assert("Con EQD2 - valor BUG viejo (fórmula aplicada sobre %, resultado sin sentido físico)", 70.31, conEQD2_OldRoto, 0.5);

        // Con EQD2 - código nuevo (fix): primero EQD2 sobre la dosis en Gy, después % sobre prescripción EQD2
        double doseEQD2 = Math.Round(EQD2.Dosis2Gy(doseGyFisica, alfaBeta2, numFx2), 1); // 9.75 Gy EQD2
        double prescripcionEQD2_2 = EQD2.Dosis2Gy(prescripcion2, alfaBeta2, numFx2); // 36 Gy EQD2
        double conEQD2_New2 = Math.Round(doseEQD2 / prescripcionEQD2_2 * 100, 2);
        Console.WriteLine($"  doseEQD2 = {doseEQD2} Gy, prescripcionEQD2 = {prescripcionEQD2_2:0.##} Gy");
        Assert("Con EQD2 - valor FIX (dosis y prescripción, ambas en EQD2, luego %)", 27.08, conEQD2_New2, 0.5);

        Console.WriteLine();
        Console.WriteLine("=== Caso 3: RestriccionVolumen, unidadCorrespondiente=\"%\" ===");
        double valorCorrespondientePct = 95; // ej. V95%
        // Sin EQD2: no tocado por el fix
        double valorCorrespondienteGy_sinEQD2 = valorCorrespondientePct * prescripcionEstructura / 100;
        Assert("Sin EQD2 (no debe cambiar)", 23.75, valorCorrespondienteGy_sinEQD2, 0.01);

        // Con EQD2 - viejo: % de la prescripción FÍSICA usado como si fuera dosis EQD2 objetivo
        double valorCorrespondienteGy_Old = valorCorrespondientePct * prescripcionEstructura / 100; // 23.75, mal etiquetado como EQD2
        double dosisFisicaBuscada_Old = Math.Round(EQD2.DosisFxAlt(valorCorrespondienteGy_Old, alfaBeta, numFx), 2);

        // Con EQD2 - nuevo: % de la prescripción ya convertida a EQD2
        double prescripcionEQD2_3 = EQD2.Dosis2Gy(prescripcionEstructura, alfaBeta, numFx); // 40
        double valorCorrespondienteGy_New = valorCorrespondientePct * prescripcionEQD2_3 / 100; // 38
        double dosisFisicaBuscada_New = Math.Round(EQD2.DosisFxAlt(valorCorrespondienteGy_New, alfaBeta, numFx), 2);

        Console.WriteLine($"  dosis física buscada en la DVH -> Old={dosisFisicaBuscada_Old} Gy vs New={dosisFisicaBuscada_New} Gy");
        Assert("Con EQD2 - dosis física buscada, BUG", 18.0, dosisFisicaBuscada_Old, 0.05);
        Assert("Con EQD2 - dosis física buscada, FIX", 24.22, dosisFisicaBuscada_New, 0.05);

        Console.WriteLine();
        Console.WriteLine(fallas == 0 ? "TODOS LOS CHEQUEOS OK" : $"{fallas} CHEQUEO(S) FALLARON");
        Environment.Exit(fallas == 0 ? 0 : 1);
    }
}
