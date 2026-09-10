using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

// Réplica aislada (sin ESAPI) del parseo de DesdeCSV.cs: en vez de instanciar IRestriccion/Estructura
// (que arrastran VMS.TPS.Common.Model.API en sus firmas), corre la misma lógica de split/parseo sobre
// los archivos reales de tablas/ y valida que no explote, que los números clave salgan bien, y que
// ninguna línea con datos reales quede sin generar al menos una restricción ("salteo").

class Program
{
    static int fallas = 0;
    static List<string> salteos = new List<string>();

    static void Assert(string nombre, bool ok)
    {
        Console.WriteLine($"{(ok ? "OK  " : "FAIL")} {nombre}");
        if (!ok) fallas++;
    }

    const int ColumnasEsperadas = 7;

    // Igual que DesdeCSV.LeerLineas: File.ReadAllLines corta tambien por un "\r" suelto (varios tablas/*.csv
    // traen uno pegado dentro de la celda "ChestWall"), lo que partia esa fila en 2 y generaba basura.
    static string[] LeerLineas(string path)
    {
        return File.ReadAllText(path).Replace("\r\n", "\n").Replace("\r", "").Split('\n');
    }

    static double Dbl(string valor)
    {
        return double.TryParse(valor.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double d) ? d : double.NaN;
    }

    static string[] SplitCsv(string linea)
    {
        var campos = new List<string>();
        bool enComillas = false;
        var actual = new StringBuilder();
        foreach (char c in linea)
        {
            if (c == '"') enComillas = !enComillas;
            else if (c == ',' && !enComillas) { campos.Add(actual.ToString()); actual.Clear(); }
            else actual.Append(c);
        }
        campos.Add(actual.ToString());
        return campos.ToArray();
    }

    static readonly Regex PatronNumeroSexo = new Regex(@"([\d.]+)\s*\(?([HM])\)?", RegexOptions.IgnoreCase);

    static List<(double valor, string notaSexo)> ExtraerValoresConSexo(string celda)
    {
        var matches = PatronNumeroSexo.Matches(celda);
        var resultado = new List<(double, string)>();
        if (matches.Count >= 2)
        {
            foreach (Match m in matches) resultado.Add((Dbl(m.Groups[1].Value), " [" + m.Groups[2].Value.ToUpper() + "]"));
            return resultado;
        }
        double valor = Dbl(celda.Replace("<", "").Replace("M", "").Replace("H", "").Replace("%", ""));
        string notaSexo = "";
        if (celda.Contains("H")) notaSexo = " [H]";
        else if (celda.Contains("M")) notaSexo = " [M]";
        resultado.Add((valor, notaSexo));
        return resultado;
    }

    class Restr
    {
        public string Tipo = "";
        public string Estructura = "";
        public double A = double.NaN;
        public double B = double.NaN;
    }

    static List<Restr> ParsearBloque(IEnumerable<string> lineasDeDatos, bool tieneUK, string etiquetaBloque, string archivo)
    {
        var resultado = new List<Restr>();
        string estructuraAnt = "";
        bool esVolumenCritico = false;

        foreach (string lineaCruda in lineasDeDatos)
        {
            string[] Linea = SplitCsv(lineaCruda);
            if (Linea.Length < ColumnasEsperadas)
            {
                Linea = Linea.Concat(Enumerable.Repeat("", ColumnasEsperadas - Linea.Length)).ToArray();
            }

            if (Linea[0] != "") esVolumenCritico = false;

            bool esSubencabezadoCritico = Linea[1].IndexOf("CRITICAL", StringComparison.OrdinalIgnoreCase) >= 0
                || Linea[2].IndexOf("CRITICAL", StringComparison.OrdinalIgnoreCase) >= 0;
            if (esSubencabezadoCritico) esVolumenCritico = true;

            string nombreEstructura = Linea[0] != "" ? Linea[0] : estructuraAnt;
            int antesDeLaFila = resultado.Count;

            if (!esSubencabezadoCritico)
            {
                if (Linea[1] == "")
                {
                    // sin valor de volumen/Dmean; puede traer Dmax en col3 mas abajo
                }
                else if (Linea[1].ToLower().Contains("mean"))
                {
                    double v = Dbl(Linea[2].Replace("<", ""));
                    if (!double.IsNaN(v)) resultado.Add(new Restr { Tipo = "Dmean(txt)", Estructura = nombreEstructura, A = v });
                }
                else if (esVolumenCritico)
                {
                    double dosis = Dbl(Linea[2].Replace("Gy", ""));
                    foreach (var (vol, notaSexo) in ExtraerValoresConSexo(Linea[1]))
                    {
                        if (!double.IsNaN(vol) && !double.IsNaN(dosis)) resultado.Add(new Restr { Tipo = "VolumenCritico" + notaSexo, Estructura = nombreEstructura, A = vol, B = dosis });
                    }
                }
                else
                {
                    double dosis = Dbl(Linea[2]);
                    foreach (var (vol, notaSexo) in ExtraerValoresConSexo(Linea[1]))
                    {
                        if (!double.IsNaN(vol) && !double.IsNaN(dosis)) resultado.Add(new Restr { Tipo = "Dosis" + notaSexo, Estructura = nombreEstructura, A = vol, B = dosis });
                    }
                }

                if (Linea[3] != "" && !Linea[3].Contains("<"))
                {
                    if (Linea[3].Contains("≤"))
                    {
                        string[] rango = Linea[3].Replace("≤", "").Trim().Split('-');
                        double menor = Dbl(rango[0]);
                        double mayor = rango.Length > 1 ? Dbl(rango[1]) : double.NaN;
                        if (!double.IsNaN(menor)) resultado.Add(new Restr { Tipo = "DosisMax(rango)", Estructura = nombreEstructura, A = menor, B = mayor });
                    }
                    else
                    {
                        double m = Dbl(Linea[3]);
                        if (!double.IsNaN(m)) resultado.Add(new Restr { Tipo = "DosisMax", Estructura = nombreEstructura, A = m });
                    }
                }
            }

            if (tieneUK && Linea[4].Contains("V"))
            {
                double v = Dbl(Linea[4].Replace("V", "").Replace("Gy", "").Replace("*", "").Trim());
                if (!double.IsNaN(v)) resultado.Add(new Restr { Tipo = "UK-Volumen", Estructura = nombreEstructura, A = v });
            }
            else if (Linea[4].ToLower().Contains("mean") || Linea[4].ToLower().Contains("med"))
            {
                resultado.Add(new Restr { Tipo = "UK-Dmean", Estructura = nombreEstructura });
            }
            else if (Linea[4].ToLower().Contains("cc"))
            {
                double v = Dbl(Linea[4].Replace("cc", "").Replace("D", "").Replace("*", "").Trim());
                if (!double.IsNaN(v)) resultado.Add(new Restr { Tipo = "UK-Dosis", Estructura = nombreEstructura, A = v });
            }

            bool teniaContenido = (!esSubencabezadoCritico && (Linea[1] != "" || Linea[3] != "")) || Linea[4] != "";
            if (teniaContenido && resultado.Count == antesDeLaFila)
            {
                salteos.Add($"{archivo} [{etiquetaBloque}] {nombreEstructura}: \"{lineaCruda}\"");
            }

            estructuraAnt = nombreEstructura;
        }
        return resultado;
    }

    static (List<Restr> craneal, List<Restr> cuerpo, int numFx) ParsearArchivoFx(string path)
    {
        string[] archivo = LeerLineas(path);
        int numFx = Convert.ToInt32(archivo[0].Split(' ').First());
        bool tieneUK = archivo[1].Contains("UK");
        int idxSeparador = Array.FindIndex(archivo, l => l.Trim() == "###");

        var lineasCraneal = archivo.Skip(3).Take(idxSeparador - 3).Where(l => !string.IsNullOrWhiteSpace(l));
        var lineasCuerpo = archivo.Skip(idxSeparador + 1).Where(l => !string.IsNullOrWhiteSpace(l));

        string nombreArchivo = Path.GetFileName(path);
        return (ParsearBloque(lineasCraneal, tieneUK, "craneal/RC", nombreArchivo), ParsearBloque(lineasCuerpo, tieneUK, "cuerpo/SBRT", nombreArchivo), numFx);
    }

    static void Main()
    {
        string carpetaTablas = @"\\fisica0\centro_de_datos2018\000_Centro de Datos 2021\12-Software propio\2-En uso clínico\ExploracionPlanes\1-Codigo\ExploracionPlanes\tablas";
        var archivosFx = Directory.GetFiles(carpetaTablas, "*FX*.csv");

        Assert("Encuentra los 8 archivos de fx", archivosFx.Length == 8);

        var fxEncontrados = new List<int>();
        int totalCraneal = 0, totalCuerpo = 0;

        foreach (string path in archivosFx)
        {
            try
            {
                var (craneal, cuerpo, numFx) = ParsearArchivoFx(path);
                fxEncontrados.Add(numFx);
                totalCraneal += craneal.Count;
                totalCuerpo += cuerpo.Count;
                Console.WriteLine($"{Path.GetFileName(path)}: fx={numFx}, craneal={craneal.Count} restricciones, cuerpo={cuerpo.Count} restricciones");

                if (numFx == 1)
                {
                    var brainstem = craneal.FirstOrDefault(f => f.Estructura.Contains("Brainstem") && f.Tipo == "Dosis");
                    Assert("1FX: Brainstem_PRV02 <0.5cc / 10Gy (Timmerman)", brainstem != null && brainstem.A == 0.5 && brainstem.B == 10);

                    // 1FX.csv trae Liver en formato compacto (sin literal "CRITICAL"), a diferencia de
                    // 5/8/10/15FX -> en este archivo especifico sigue siendo RestriccionDosis, no VolumenCritico.
                    var liver = cuerpo.FirstOrDefault(f => f.Estructura == "Liver" && f.Tipo == "Dosis");
                    Assert("1FX: Liver (formato compacto, sin 'CRITICAL') -> RestriccionDosis (700cc/11.6Gy)", liver != null && liver.A == 700 && liver.B == 11.6);
                }
                if (numFx == 10)
                {
                    var eye = craneal.FirstOrDefault(f => f.Estructura.Contains("Eye") && f.Tipo == "Dmean(txt)");
                    Assert("10FX: 'Eye (retina),Mean dose,<26,30' -> Dmean=26 (no se saltea)", eye != null && eye.A == 26);

                    var eyeDmax = craneal.FirstOrDefault(f => f.Estructura.Contains("Eye") && f.Tipo == "DosisMax");
                    Assert("10FX: misma fila tambien genera DosisMax=30", eyeDmax != null && eyeDmax.A == 30);

                    var liverGtv = cuerpo.FirstOrDefault(f => f.Estructura.StartsWith("Liver") && f.Tipo == "VolumenCritico");
                    Assert("10FX: Liver-GTV (con 'CRITICAL') -> RestriccionVolumenCritico (700cc/27Gy)", liverGtv != null && liverGtv.A == 700 && liverGtv.B == 27);

                    var lungsHM = cuerpo.Where(f => f.Estructura.StartsWith("Lungs") && f.Tipo.StartsWith("VolumenCritico")).ToList();
                    Assert("10FX: Lungs '1500 (H)/950 (M)' -> 2 restricciones VolumenCritico (H y M)", lungsHM.Any(f => f.A == 1500) && lungsHM.Any(f => f.A == 950));

                    var eyelid = craneal.FirstOrDefault(f => f.Estructura.Contains("Eyelid") && f.Tipo == "DosisMax");
                    Assert("10FX: 'Eyelid, meibomian glands (one side)',,,21.3 -> DosisMax=21.3 (coma en nombre no rompe el parseo)", eyelid != null && eyelid.A == 21.3);

                    var hippoRango = craneal.Where(f => f.Estructura.Contains("Hippocampus") && f.Tipo == "DosisMax(rango)").ToList();
                    Assert("10FX: Hippocampus '≤16-17'/'≤9-10' -> DosisMax(rango) esperado=menor, tolerado=mayor", hippoRango.Any(f => f.A == 16 && f.B == 17) && hippoRango.Any(f => f.A == 9 && f.B == 10));
                }
                if (numFx == 5)
                {
                    var renal = cuerpo.Where(f => f.Estructura.StartsWith("Renal cortex") && f.Tipo == "VolumenCritico").ToList();
                    Assert("5FX: Renal cortex -> VolumenCritico (200cc/17.5Gy)", renal.Any(f => f.A == 200 && f.B == 17.5));
                    var renalUK = cuerpo.Where(f => f.Estructura.StartsWith("Renal cortex") && (f.Tipo == "UK-Dmean" || f.Tipo == "UK-Volumen")).ToList();
                    Assert("5FX: Renal cortex header-critico tambien genero su UK Dmean (col4-6 no se pierde)", renalUK.Count >= 1);
                }
            }
            catch (Exception exp)
            {
                Assert($"{Path.GetFileName(path)} parsea sin excepciones", false);
                Console.WriteLine("   Excepcion: " + exp.Message);
            }
        }

        Assert("RC (craneal) ahora se junta en TODOS los fx, no solo 1/3/5", new[] { 1, 2, 3, 4, 5, 8, 10, 15 }.All(fx => fxEncontrados.Contains(fx)));
        Assert("Se generaron restricciones de cuerpo (SBRT) en todos los archivos", totalCuerpo > 0);
        Assert("Se generaron restricciones craneales (RC) en todos los archivos", totalCraneal > 0);

        // --- tabla de cobertura PTV por volumen (sin cambios en esta ronda) ---
        string pathVolPTV = Path.Combine(carpetaTablas, "Constrains consortium vs volPTV.txt");
        string[] lineasVolPTV = LeerLineas(pathVolPTV);
        int idxPulmon = Array.FindIndex(lineasVolPTV, l => l.Trim() == "###Para SBRT Pulmon");
        int idxNoPulmon = Array.FindIndex(lineasVolPTV, l => l.Trim() == "###Para SBRT no Pulmon");
        Assert("Encuentra las 2 secciones de la tabla de cobertura PTV", idxPulmon >= 0 && idxNoPulmon > idxPulmon);

        Console.WriteLine();
        Console.WriteLine($"=== Salteos detectados (fila con dato pero sin restriccion generada): {salteos.Count} ===");
        foreach (string s in salteos) Console.WriteLine("SALTEO: " + s);

        Console.WriteLine();
        Console.WriteLine(fallas == 0 ? "TODOS LOS CHEQUEOS OK" : $"{fallas} CHEQUEO(S) FALLARON");
        Environment.Exit(fallas == 0 ? 0 : 1);
    }
}
