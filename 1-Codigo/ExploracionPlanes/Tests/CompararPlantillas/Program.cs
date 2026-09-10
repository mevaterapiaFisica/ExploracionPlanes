using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

// Compara las 2 plantillas unificadas REALES (generadas por Tests/GenerarPlantillasReales, que compila
// DesdeCSV.cs de produccion contra el stub de ESAPI) contra las 7 plantillas manuales existentes en
// \\ARIAMEVADB-SVR\va_data$\Plantillas (RC_1/3/5fx.txt, SBRT_1/3/5/15fx.txt).
//
// Antes este comparador reimplementaba el parseo de CSV por su cuenta -- se dejo de hacer: al ya existir
// un generador que corre el codigo real, comparar la reimplementacion contra si misma no prueba nada;
// ahora los 2 lados (generado y manual) se leen con el MISMO parser de JSON real.
//
// No escribe nada en ningun share: solo lee.

class Program
{
    class Restriccion
    {
        public string Tipo = "";
        public string Estructura = "";
        public List<string> Alias = new();
        public double ValorEsperado = double.NaN;
        public double ValorTolerado = double.NaN;
        public double ValorCorrespondiente = double.NaN;
        public int? NumFx = null; // null = sin Condicion de fx (aplica siempre)
    }

    static double LeerNum(JsonElement obj, string prop)
    {
        if (!obj.TryGetProperty(prop, out var el)) return double.NaN;
        if (el.ValueKind == JsonValueKind.Number) return el.GetDouble();
        if (el.ValueKind == JsonValueKind.String)
        {
            string s = el.GetString() ?? "";
            return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double d) ? d : double.NaN;
        }
        return double.NaN;
    }

    static List<Restriccion> LeerPlantilla(string path)
    {
        var resultado = new List<Restriccion>();
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var r in doc.RootElement.GetProperty("listaRestricciones").EnumerateArray())
        {
            string tipoFull = r.GetProperty("$type").GetString() ?? "";
            string tipo = tipoFull.Split(',')[0].Replace("ExploracionPlanes.", "").Trim();
            var estructuraEl = r.GetProperty("estructura");
            string nombre = estructuraEl.GetProperty("nombre").GetString() ?? "";
            var alias = estructuraEl.TryGetProperty("nombresPosibles", out var al) && al.ValueKind == JsonValueKind.Array
                ? al.EnumerateArray().Select(x => x.GetString() ?? "").ToList()
                : new List<string> { nombre };

            int? numFx = null;
            if (r.TryGetProperty("condicion", out var cond) && cond.ValueKind == JsonValueKind.Object
                && cond.TryGetProperty("tipo", out var tipoCond) && tipoCond.GetInt32() == 1 /* Tipo.NumFx */)
            {
                numFx = (int)LeerNum(cond, "ValorEsperado");
            }

            resultado.Add(new Restriccion
            {
                Tipo = tipo,
                Estructura = nombre,
                Alias = alias,
                ValorEsperado = LeerNum(r, "valorEsperado"),
                ValorTolerado = LeerNum(r, "valorTolerado"),
                ValorCorrespondiente = LeerNum(r, "valorCorrespondiente"),
                NumFx = numFx,
            });
        }
        return resultado;
    }

    // ---------- Comparacion ----------

    static bool Cerca(double a, double b)
    {
        // "correspondiente" en Dmax es un campo sin uso real: unas veces se serializa 0.0, otras NaN.
        if (double.IsNaN(a)) a = 0;
        if (double.IsNaN(b)) b = 0;
        return Math.Abs(a - b) < 0.05;
    }

    // Reglas de match acordadas: (1a) ignorar prosa/descriptores extra -> corta en "(" o "," y se queda
    // con lo de antes; (1b) "<Nombre>" y "<Nombre>_PRV<#>" se consideran el mismo -> saca el sufijo _PRV#.
    // El resto (espacios/guiones/mayusculas) tambien se colapsa para no generar ruido cosmetico.
    // Nota: los mapeos "duros" (Renal cortex->Kidneys_PRV, Rib->Ribs, etc.) ya se aplican en DesdeCSV.cs
    // (produccion) antes de generar el archivo -- esto de aca es solo tolerancia cosmetica de comparacion.
    static string Normalizar(string nombre)
    {
        string s = nombre;
        int idxParen = s.IndexOf('(');
        if (idxParen >= 0) s = s.Substring(0, idxParen);
        int idxComa = s.IndexOf(',');
        if (idxComa >= 0) s = s.Substring(0, idxComa);
        s = Regex.Replace(s, @"_PRV\d*", "", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"[^a-zA-Z0-9]", "");
        return s.ToLowerInvariant();
    }

    static string CanonicalEstructura(Restriccion g, List<Restriccion> manual)
    {
        string normG = Normalizar(g.Estructura);
        var m = manual.FirstOrDefault(x => x.Alias.Any(a => Normalizar(a) == normG));
        return m != null ? m.Estructura : g.Estructura;
    }

    // RestriccionVolumen(esperado=V0,correspondiente=D0) y RestriccionDosis(esperado=D0,correspondiente=V0)
    // son la MISMA restriccion de DVH, solo parametrizada al reves ("el volumen que recibe D0 es < V0" vs
    // "la dosis en V0 es < D0") -- se agrupan bajo la misma clave de comparacion para no generar ruido.
    static string TipoBucket(string tipo)
    {
        return (tipo == "RestriccionDosis" || tipo == "RestriccionVolumen") ? "Dosis|Volumen" : tipo;
    }

    // Ademas de un match directo (mismos esperado/tolerado/correspondiente), si un lado es Dosis y el
    // otro Volumen se prueba tambien el match "cruzado" (esperado<->correspondiente invertidos). El
    // tolerado no se invierte -- es una segunda cota sobre el MISMO eje que esperado, no tiene un
    // equivalente cruzado limpio, asi que solo cuenta si coincide tal cual (incluido NaN==NaN).
    static bool Equivalentes(Restriccion a, Restriccion b)
    {
        if (Cerca(a.ValorEsperado, b.ValorEsperado) && Cerca(a.ValorTolerado, b.ValorTolerado) && Cerca(a.ValorCorrespondiente, b.ValorCorrespondiente))
        {
            return true;
        }
        bool esParDosisVolumenCruzado = a.Tipo != b.Tipo && TipoBucket(a.Tipo) == "Dosis|Volumen" && TipoBucket(b.Tipo) == "Dosis|Volumen";
        if (esParDosisVolumenCruzado && Cerca(a.ValorEsperado, b.ValorCorrespondiente) && Cerca(a.ValorCorrespondiente, b.ValorEsperado) && Cerca(a.ValorTolerado, b.ValorTolerado))
        {
            return true;
        }
        return false;
    }

    static void Comparar(string nombreArchivo, string pathManual, List<Restriccion> generadas, int fx)
    {
        var manual = LeerPlantilla(pathManual);
        var gen = generadas.Where(r => r.NumFx == fx || r.NumFx == null).ToList();

        var claves = manual.Select(m => (m.Estructura, Tipo: TipoBucket(m.Tipo)))
            .Union(gen.Select(g => (Estructura: CanonicalEstructura(g, manual), Tipo: TipoBucket(g.Tipo))))
            .Distinct().ToList();

        int coincide = 0, valorDistinto = 0, soloManual = 0, soloGenerado = 0;
        var detalleDiff = new List<string>();

        foreach (var clave in claves)
        {
            var vm = manual.Where(m => m.Estructura == clave.Estructura && TipoBucket(m.Tipo) == clave.Tipo).ToList();
            var vg = gen.Where(g => CanonicalEstructura(g, manual) == clave.Estructura && TipoBucket(g.Tipo) == clave.Tipo).ToList();

            if (vm.Count == 0)
            {
                soloGenerado += vg.Count;
                foreach (var g in vg) detalleDiff.Add($"  SOLO-GENERADO  {clave.Estructura} [{g.Tipo}]: esperado={g.ValorEsperado:0.##} tolerado={g.ValorTolerado:0.##} correspondiente={g.ValorCorrespondiente:0.##}");
                continue;
            }
            if (vg.Count == 0)
            {
                soloManual += vm.Count;
                foreach (var m in vm) detalleDiff.Add($"  SOLO-MANUAL    {clave.Estructura} [{m.Tipo}]: esperado={m.ValorEsperado:0.##} tolerado={m.ValorTolerado:0.##} correspondiente={m.ValorCorrespondiente:0.##}");
                continue;
            }

            // Multiset match: cada manual busca un generado equivalente (directo o cruzado Dosis/Volumen).
            var vgRestante = new List<Restriccion>(vg);
            foreach (var m in vm)
            {
                var match = vgRestante.FirstOrDefault(g => Equivalentes(m, g));
                if (match != null)
                {
                    coincide++;
                    vgRestante.Remove(match);
                }
                else
                {
                    valorDistinto++;
                    detalleDiff.Add($"  VALOR-DISTINTO {clave.Estructura} [{m.Tipo}]: manual(esp={m.ValorEsperado:0.##},tol={m.ValorTolerado:0.##},corr={m.ValorCorrespondiente:0.##}) vs generado(candidatos: {string.Join(" | ", vg.Select(g => $"[{g.Tipo}] esp={g.ValorEsperado:0.##},tol={g.ValorTolerado:0.##},corr={g.ValorCorrespondiente:0.##}"))})");
                }
            }
            foreach (var g in vgRestante)
            {
                soloGenerado++;
                detalleDiff.Add($"  SOLO-GENERADO  {clave.Estructura} [{g.Tipo}]: esperado={g.ValorEsperado:0.##} tolerado={g.ValorTolerado:0.##} correspondiente={g.ValorCorrespondiente:0.##}");
            }
        }

        Console.WriteLine($"\n=== {nombreArchivo} (manual: {manual.Count} restricciones, generado fx={fx}: {gen.Count} restricciones) ===");
        Console.WriteLine($"  Coinciden (estructura+tipo+valores): {coincide}");
        Console.WriteLine($"  Misma estructura+tipo, valores distintos: {valorDistinto}");
        Console.WriteLine($"  Solo en manual (no generado): {soloManual}");
        Console.WriteLine($"  Solo en generado (no manual): {soloGenerado}");
        foreach (var d in detalleDiff) Console.WriteLine(d);
    }

    static void Main()
    {
        string carpetaSalida = @"\\fisica0\centro_de_datos2018\000_Centro de Datos 2021\12-Software propio\2-En uso clínico\ExploracionPlanes\1-Codigo\ExploracionPlanes\Tests\CompararPlantillas\salida";
        string carpetaManual = @"\\ARIAMEVADB-SVR\va_data$\Plantillas";

        var restriccionesRC = LeerPlantilla(Path.Combine(carpetaSalida, "RC_real.txt"));
        var restriccionesSBRT = LeerPlantilla(Path.Combine(carpetaSalida, "SBRT_real.txt"));

        Console.WriteLine($"Generado (real, DesdeCSV.cs de produccion): RC={restriccionesRC.Count} restricciones, SBRT={restriccionesSBRT.Count} restricciones");

        Console.WriteLine("\n########## RC ##########");
        foreach (int fx in new[] { 1, 3, 5 })
        {
            Comparar($"RC_{fx}fx.txt", Path.Combine(carpetaManual, $"RC_{fx}fx.txt"), restriccionesRC, fx);
        }

        Console.WriteLine("\n########## SBRT ##########");
        foreach (int fx in new[] { 1, 3, 5, 15 })
        {
            Comparar($"SBRT_{fx}fx.txt", Path.Combine(carpetaManual, $"SBRT_{fx}fx.txt"), restriccionesSBRT, fx);
        }
    }
}
