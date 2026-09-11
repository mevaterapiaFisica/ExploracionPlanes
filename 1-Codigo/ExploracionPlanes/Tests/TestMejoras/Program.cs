// Test standalone de la lógica PURA (sin ESAPI) de los cambios:
// 1) Matcheo aproximado de estructuras (Damerau-Levenshtein) - Estructura.cs
// 2) Fallback de memoria por plan al plan más reciente del paciente - MemoriaPlan.cs
// 3) Reordenamiento de criterios de selección automática de plantilla - Plantillla.cs
// 4) Fix del bug de prescripcionPredefinida (memoria existente tapaba las heurísticas) - Form2.cs
// No se puede instanciar PlanSetup/Structure fuera de Eclipse, así que se reproduce cada pieza
// de lógica pura tal cual quedó en el código real, con datos inventados.

bool huboError = false;
void chequear(string nombre, bool condicion)
{
    if (condicion)
    {
        Console.WriteLine("OK   " + nombre);
    }
    else
    {
        Console.WriteLine("FAIL " + nombre);
        huboError = true;
    }
}

// ===== 1) Damerau-Levenshtein pesado (copia literal de Estructura.DistanciaDamerauLevenshtein) =====
// Sustitución pesa más que inserción/borrado (pedido del usuario): un nombre que es el slot buscado
// más caracteres agregados debe ganarle a uno de igual longitud pero con letras distintas.
const int CostoSustitucion = 2;
const int CostoInsercionOBorrado = 1;

int DistanciaDamerauLevenshtein(string a, string b)
{
    a = a.ToLowerInvariant();
    b = b.ToLowerInvariant();
    int[,] d = new int[a.Length + 1, b.Length + 1];
    for (int i = 0; i <= a.Length; i++) d[i, 0] = i * CostoInsercionOBorrado;
    for (int j = 0; j <= b.Length; j++) d[0, j] = j * CostoInsercionOBorrado;
    for (int i = 1; i <= a.Length; i++)
    {
        for (int j = 1; j <= b.Length; j++)
        {
            int costoSustitucion = a[i - 1] == b[j - 1] ? 0 : CostoSustitucion;
            d[i, j] = Math.Min(Math.Min(d[i - 1, j] + CostoInsercionOBorrado, d[i, j - 1] + CostoInsercionOBorrado), d[i - 1, j - 1] + costoSustitucion);
            if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
            {
                d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + CostoInsercionOBorrado);
            }
        }
    }
    return d[a.Length, b.Length];
}

Console.WriteLine("=== 1) Damerau-Levenshtein (pesado: sustitución cuesta más que inserción/borrado) ===");
chequear("Idénticas -> distancia 0", DistanciaDamerauLevenshtein("PTV", "PTV") == 0);
chequear("Case-insensitive -> distancia 0", DistanciaDamerauLevenshtein("ptv", "PTV") == 0);
chequear("Una sustitución -> distancia 2 (antes 1, ahora pesa el doble que un carácter agregado)", DistanciaDamerauLevenshtein("PTV", "PTB") == 2);
chequear("Transposición adyacente sigue costando 1 (no se encarece como una sustitución)", DistanciaDamerauLevenshtein("ab", "ba") == 1);
chequear("PTV_5400 vs PTV_5040 (transposición) distancia baja", DistanciaDamerauLevenshtein("PTV_5400", "PTV_5040") <= 2);
chequear("Nombres muy distintos -> distancia alta", DistanciaDamerauLevenshtein("PTV", "MEDULA") >= 5);

// ===== 1b) Núcleo del nombre (copia literal de Estructura.nucleoNombreClinico) =====
// Segundo pedido del usuario: quiere que "PTV_1mm"/"PTV05" le ganen a "GTV" - no solo que dejen de
// perder contra "Skin". Sufijos clínicos (margen "_PRV", tamaño "_1mm", numeración) no cambian de
// qué órgano/volumen se trata, así que se sacan antes de comparar: el "núcleo" manda en el orden,
// la distancia completa (de la sección 1) solo desempata entre candidatos de igual núcleo.
var sufijosClinicos = new System.Text.RegularExpressions.Regex(@"prv|mm|\d+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
string NucleoNombreClinico(string nombre) => sufijosClinicos.Replace(nombre ?? "", "").Replace("_", "").Trim();

Console.WriteLine();
Console.WriteLine("=== 1b) Núcleo del nombre: sufijos clínicos (PRV, mm, números) no cuentan ===");
chequear("PTV05 -> núcleo 'PTV'", NucleoNombreClinico("PTV05") == "PTV");
chequear("PTV_1mm -> núcleo 'PTV'", NucleoNombreClinico("PTV_1mm") == "PTV");
chequear("Bladder_PRV2 -> núcleo 'Bladder'", NucleoNombreClinico("Bladder_PRV2") == "Bladder");
chequear("GTV -> núcleo 'GTV' (no es lo mismo que PTV)", NucleoNombreClinico("GTV") == "GTV");

// Caso real reportado por el usuario: "PTV" (slot) con candidatos reales "GTV" (1 sustitución),
// "PTV_1mm" (mismo núcleo que PTV) y "Skin" (núcleo totalmente distinto). Quiere que PTV_1mm gane
// SIEMPRE, no solo que le gane a Skin.
Console.WriteLine();
Console.WriteLine("=== 1c) Caso real: PTV debe preferir PTV_1mm/PTV05 (mismo núcleo) sobre GTV y Skin ===");
int nGTV = DistanciaDamerauLevenshtein(NucleoNombreClinico("PTV"), NucleoNombreClinico("GTV"));
int nPTV1mm = DistanciaDamerauLevenshtein(NucleoNombreClinico("PTV"), NucleoNombreClinico("PTV_1mm"));
int nSkin = DistanciaDamerauLevenshtein(NucleoNombreClinico("PTV"), NucleoNombreClinico("Skin"));
Console.WriteLine($"  núcleo PTV vs GTV = {nGTV}, vs PTV_1mm = {nPTV1mm}, vs Skin = {nSkin}");
chequear("PTV_1mm primero (mismo núcleo 'PTV', distancia 0)", nPTV1mm == 0 && nPTV1mm < nGTV);
chequear("GTV segundo, le sigue ganando a Skin", nGTV < nSkin);

// candidatosPorDistancia: ordena estructuras reales por núcleo primero, distancia completa desempata.
List<(string Id, int DistanciaNucleo)> candidatosPorDistancia(List<string> nombresPosibles, List<string> estructurasPlan)
{
    return estructurasPlan
        .Select(s => new
        {
            Id = s,
            DistanciaNucleo = nombresPosibles.Min(n => DistanciaDamerauLevenshtein(NucleoNombreClinico(n), NucleoNombreClinico(s))),
            DistanciaCompleta = nombresPosibles.Min(n => DistanciaDamerauLevenshtein(n, s))
        })
        .OrderBy(x => x.DistanciaNucleo)
        .ThenBy(x => x.DistanciaCompleta)
        .Select(x => (x.Id, x.DistanciaNucleo))
        .ToList();
}

Console.WriteLine();
Console.WriteLine("=== 1d) candidatosPorDistancia con núcleo: PTV_1mm y PTV05 antes que GTV ===");
var candidatosPTV = candidatosPorDistancia(new List<string> { "PTV" }, new List<string> { "GTV", "Skin", "PTV_1mm", "PTV05" });
Console.WriteLine("  Orden real: " + string.Join(", ", candidatosPTV.Select(c => c.Id + "(" + c.DistanciaNucleo + ")")));
var nucleoCero = candidatosPTV.Where(c => c.DistanciaNucleo == 0).Select(c => c.Id).ToList();
chequear("PTV_1mm y PTV05 (núcleo 0) van antes que GTV", nucleoCero.Contains("PTV_1mm") && nucleoCero.Contains("PTV05")
    && candidatosPTV.FindIndex(c => c.Id == "PTV_1mm") < candidatosPTV.FindIndex(c => c.Id == "GTV")
    && candidatosPTV.FindIndex(c => c.Id == "PTV05") < candidatosPTV.FindIndex(c => c.Id == "GTV"));
chequear("GTV va antes que Skin", candidatosPTV.FindIndex(c => c.Id == "GTV") < candidatosPTV.FindIndex(c => c.Id == "Skin"));

var candidatos = candidatosPorDistancia(new List<string> { "PTV" }, new List<string> { "MEDULA", "PTV_2", "PTV" });
Console.WriteLine("Orden real: " + string.Join(", ", candidatos.Select(c => c.Id + "(" + c.DistanciaNucleo + ")")));
chequear("Exacto primero", candidatos[0].Id == "PTV" && candidatos[0].DistanciaNucleo == 0);
chequear("Aproximado (PTV_2) segundo, antes que MEDULA", candidatos[1].Id == "PTV_2" && candidatos[1].DistanciaNucleo < candidatos[2].DistanciaNucleo);

// ===== 2) Fallback de memoria por plan (misma lógica que MemoriaPlan.rutaArchivoFallbackPaciente) =====
Console.WriteLine();
Console.WriteLine("=== 2) Fallback de memoria por plan ===");
string carpeta = Path.Combine(Path.GetTempPath(), "TestMemoriaPlan_" + Guid.NewGuid());
Directory.CreateDirectory(carpeta);
string archivoPlan1 = Path.Combine(carpeta, "PAC1_Curso1_Plan1.txt");
string archivoPlan2 = Path.Combine(carpeta, "PAC1_Curso1_Plan2.txt");
File.WriteAllText(archivoPlan1, "PTV,PTV_1");
System.Threading.Thread.Sleep(20);
File.WriteAllText(archivoPlan2, "PTV,PTV_2");

string? rutaFallback(string carpetaBusqueda, string clavePaciente, string rutaActual)
{
    return Directory.GetFiles(carpetaBusqueda, clavePaciente + "_*.txt")
        .Where(f => !f.Equals(rutaActual, StringComparison.OrdinalIgnoreCase))
        .OrderByDescending(f => new FileInfo(f).LastWriteTime)
        .FirstOrDefault();
}

string archivoPlanNuevo = Path.Combine(carpeta, "PAC1_Curso1_PlanNuevo.txt");
string? fallback = rutaFallback(carpeta, "PAC1", archivoPlanNuevo);
chequear("Plan sin memoria propia cae al plan más reciente del paciente (Plan2, el último escrito)", fallback == archivoPlan2);

string? sinFallback = rutaFallback(carpeta, "PAC_INEXISTENTE", archivoPlanNuevo);
chequear("Paciente sin ningún plan con memoria -> null (no rompe, deja en blanco)", sinFallback == null);
Directory.Delete(carpeta, true);

// ===== 3) Reordenamiento de criterios de selección de plantilla =====
Console.WriteLine();
Console.WriteLine("=== 3) Orden de criterios en SeleccionarAutomaticamentePlantilla ===");

// Viejo: filtraba por fracciones ANTES de puntuar por estructuras -> podía descartar la plantilla
// que en realidad matchea mejor si no sigue la convención de nombre "_Nfx".
(string nombre, double score) ViejoOrden(List<(string nombre, double score)> plantillas, int numFx)
{
    var filtradas = plantillas.Where(p => p.nombre.Contains("_" + numFx + "fx")).ToList();
    var candidatas = filtradas.Count > 0 ? filtradas : plantillas;
    return candidatas.OrderByDescending(p => p.score).First();
}

// Nuevo: puntúa TODAS por estructuras primero; fracciones queda solo como desempate si hay empate de score.
(string nombre, double score) NuevoOrden(List<(string nombre, double score)> plantillas, int numFx)
{
    double mejorScore = plantillas.Max(p => p.score);
    var mejores = plantillas.Where(p => p.score == mejorScore).ToList();
    if (mejores.Count > 1)
    {
        var porFx = mejores.Where(p => p.nombre.Contains("_" + numFx + "fx")).ToList();
        if (porFx.Count > 0) return porFx.First();
    }
    return mejores.First();
}

var plantillasCaso1 = new List<(string, double)> { ("PlantillaA_25fx", 1), ("PlantillaB", 4) };
chequear("Viejo: el filtro por fracciones descarta a B aunque matchea mejor estructuras (bug reproducido)",
    ViejoOrden(plantillasCaso1, 25).nombre == "PlantillaA_25fx");
chequear("Nuevo: puntúa primero, elige B (mejor match de estructuras) sin importar el nombre",
    NuevoOrden(plantillasCaso1, 25).nombre == "PlantillaB");

var plantillasCaso2 = new List<(string, double)> { ("PlantillaC_15fx", 3), ("PlantillaD_20fx", 3) };
chequear("Nuevo: con empate de score, fracciones desempata correctamente (elige C, 15fx)",
    NuevoOrden(plantillasCaso2, 15).nombre == "PlantillaC_15fx");

// ===== 4) Fix de prescripcionPredefinida (memoria existente ya no tapa las heurísticas) =====
Console.WriteLine();
Console.WriteLine("=== 4) prescripcionPredefinida: memoria parcial no debe tapar las heurísticas ===");

double heuristicaMama(string nombreEstructura, double prescripcion)
{
    if (prescripcion == 45 && nombreEstructura.Contains("WB")) return 40.05;
    return prescripcion;
}

// Viejo: "if (existeArchivoDeMemoria) { buscar en memoria; si no está, NO se aplican heurísticas }"
double ViejoPrescripcionPredefinida(bool existeMemoria, Dictionary<string, double> memoria, string estructura, double prescripcion)
{
    if (existeMemoria)
    {
        if (memoria.TryGetValue(estructura, out double dosis)) return dosis;
        return prescripcion; // bug: nunca llega a la heurística aunque la memoria no tenga esta estructura
    }
    return heuristicaMama(estructura, prescripcion);
}

// Nuevo: primero busca la estructura puntual en la memoria (exista el archivo o no); si no está, heurística.
double NuevaPrescripcionPredefinida(Dictionary<string, double> memoria, string estructura, double prescripcion)
{
    if (memoria.TryGetValue(estructura, out double dosis)) return dosis;
    return heuristicaMama(estructura, prescripcion);
}

var memoriaParcial = new Dictionary<string, double> { { "Sb", 60 } }; // memoria tiene "Sb" pero no "WB"
chequear("Viejo: memoria existe pero no tiene 'WB' -> devuelve la prescripción física sin heurística (bug)",
    ViejoPrescripcionPredefinida(true, memoriaParcial, "WB", 45) == 45);
chequear("Nuevo: memoria no tiene 'WB' -> aplica la heurística de Mama (40.05)",
    NuevaPrescripcionPredefinida(memoriaParcial, "WB", 45) == 40.05);
chequear("Nuevo: memoria SÍ tiene 'Sb' -> usa la memoria (60), no la heurística",
    NuevaPrescripcionPredefinida(memoriaParcial, "Sb", 45) == 60);

// ===== 5) Plantillas ocultas: default Visible=true para plantillas viejas + filtro por checkbox =====
// Copia de la lógica de Main.leerPlantillas(): sin el checkbox tildado, solo se listan las Visible=true.
var plantillasFake = new List<(string nombre, bool Visible)>
{
    ("Vieja sin campo Visible en el JSON", true), // JSON.NET deja el default = true cuando el campo no está en el archivo
    ("Nueva oculta", false),
    ("Nueva visible", true),
};

List<(string nombre, bool Visible)> Filtrar(bool mostrarOcultas) =>
    mostrarOcultas ? plantillasFake : plantillasFake.Where(p => p.Visible).ToList();

chequear("Plantilla vieja (sin Visible en el JSON) se sigue mostrando por default",
    Filtrar(false).Any(p => p.nombre == "Vieja sin campo Visible en el JSON"));
chequear("Con 'Mostrar ocultas' destildado, la oculta no aparece",
    !Filtrar(false).Any(p => p.nombre == "Nueva oculta"));
chequear("Con 'Mostrar ocultas' tildado, aparecen las 3",
    Filtrar(true).Count == 3);

// ===== 6) Parseo de decimales bajo la cultura nativa es-AR sin forzar (copia de Metodos.validarYConvertirADouble) =====
// Bug real: Script.cs (entrada del plugin de Eclipse) no forzaba ninguna cultura, a diferencia de
// Program.cs (modo standalone). Bajo Windows en es-AR (decimal=",", miles="."), un TryParse sin
// NumberStyles explícito (que permite miles por default) leía "45.678" (dosis tipeada con punto,
// por hábito o copy-paste de otro sistema) como 45678 en vez de fallar o interpretarlo como 45,678.
Console.WriteLine();
Console.WriteLine("=== 6) validarYConvertirADouble: NumberStyles.Float sin AllowThousands ===");

var culturaEsArNativa = (System.Globalization.CultureInfo)System.Globalization.CultureInfo.InvariantCulture.Clone();
culturaEsArNativa.NumberFormat.NumberDecimalSeparator = ",";
culturaEsArNativa.NumberFormat.NumberGroupSeparator = ".";

double ViejoValidarYConvertirADouble(string entrada, System.Globalization.CultureInfo cultura)
{
    System.Threading.Thread.CurrentThread.CurrentCulture = cultura;
    double.TryParse(entrada, out double salida); // bug: sin NumberStyles explícito, permite miles
    return salida;
}

double NuevoValidarYConvertirADouble(string entrada, System.Globalization.CultureInfo cultura)
{
    var alternative = (System.Globalization.CultureInfo)cultura.Clone();
    alternative.NumberFormat.NumberDecimalSeparator = ",";
    bool esNumero = double.TryParse(entrada, System.Globalization.NumberStyles.Float, cultura, out double salida);
    if (!esNumero)
    {
        esNumero = double.TryParse(entrada, System.Globalization.NumberStyles.Float, alternative, out salida);
        if (!esNumero) salida = double.NaN; // igual que Metodos.validarYConvertirADouble tras el MessageBox
    }
    return salida;
}

var culturaOriginal = System.Threading.Thread.CurrentThread.CurrentCulture;
chequear("Antes: bajo es-AR nativa sin forzar (como quedaba Script.cs), \"45.678\" se leía como 45678 (bug reproducido)",
    ViejoValidarYConvertirADouble("45.678", culturaEsArNativa) == 45678);
System.Threading.Thread.CurrentThread.CurrentCulture = culturaOriginal;

chequear("Ahora: NumberStyles.Float (sin miles) ya no confunde el punto con separador de miles: \"45.678\" queda NaN (formato ambiguo, se pide reingresar) en vez de 45678",
    double.IsNaN(NuevoValidarYConvertirADouble("45.678", culturaEsArNativa)));
chequear("Ahora: \"45,0\" (coma nativa de esa cultura) se sigue leyendo bien como 45",
    NuevoValidarYConvertirADouble("45,0", culturaEsArNativa) == 45);
chequear("Ahora: bajo la cultura forzada por Program.cs/Script.cs (decimal '.'), \"45.0\" da 45 directamente, sin ambigüedad",
    NuevoValidarYConvertirADouble("45.0", System.Globalization.CultureInfo.InvariantCulture) == 45);

// ===== 7) Interpolación DVH sobre tramo plano (copia de DVHDataExtensions_ESAPIX.interpolar1D) =====
Console.WriteLine();
Console.WriteLine("=== 7) interpolar1D: tramo plano (x1==x2) no debe dar NaN/Infinity ===");

double Interpolar1D(double x1, double x2, double z1, double z2, double x)
{
    double z;
    if (x == x1) { z = z1; }
    else if (x == x2) { z = z2; }
    else if (x2 == x1) { z = z1; }
    else { z = z1 + (z2 - z1) / (x2 - x1) * (x - x1); }
    return z;
}

chequear("Antes hubiera dado NaN/Infinity (0/0); ahora devuelve z1 en el tramo plano",
    !double.IsNaN(Interpolar1D(10, 10, 5, 8, 20)) && Interpolar1D(10, 10, 5, 8, 20) == 5);
chequear("Caso normal (x1 != x2) sigue interpolando igual que antes",
    Interpolar1D(0, 10, 0, 100, 5) == 50);

// ===== 8) RestriccionBase: la lógica compartida (extraída de las 6 clases Restriccion*) da el mismo
// resultado que daba cada clase por separado antes del refactor. Copia fiel de
// RestriccionBase.crearEtiqueta/cumple (ExploracionPlanes no se puede referenciar acá porque arrastra
// las dependencias de ESAPI, que no están disponibles fuera de Eclipse - mismo criterio que el resto
// de este archivo: se reproduce la lógica pura tal cual quedó en el código real).
Console.WriteLine();
Console.WriteLine("=== 8) RestriccionBase: etiqueta/cumple/dosisEstaEnPorcentaje iguales a las 6 clases antes del refactor ===");

string CrearEtiquetaBase(string etiquetaInicio, string prioridad, double valorEsperado, bool esMenorQue, double valorTolerado,
    string unidadValor, bool incluirUnidadValorEnEtiqueta, string condicionTipo, string condicionId, string condicionEtiquetaAnidada, string planMod)
{
    string etiqueta = etiquetaInicio;
    if (!string.IsNullOrEmpty(prioridad)) etiqueta += " (p=" + prioridad + ") ";
    if (!double.IsNaN(valorEsperado))
    {
        etiqueta += esMenorQue ? " < " : " > ";
        etiqueta += valorEsperado.ToString();
        if (!double.IsNaN(valorTolerado)) etiqueta += " (" + valorTolerado.ToString() + ") ";
        if (incluirUnidadValorEnEtiqueta) etiqueta += unidadValor;
    }
    if (condicionTipo == "NumFx" || condicionTipo == "VolPTV") etiqueta += " (" + condicionId + ")";
    else if (condicionTipo == "CondicionadaPor") etiqueta += " (" + condicionEtiquetaAnidada + ") ";
    if (!string.IsNullOrEmpty(planMod)) etiqueta += "*";
    return etiqueta;
}

int CumpleBase(bool esMenorQue, double valorMedido, double valorEsperado, double valorTolerado)
{
    if (esMenorQue)
    {
        if (valorMedido <= valorEsperado) return 0;
        if (!double.IsNaN(valorTolerado) && valorMedido <= valorTolerado) return 1;
        return 2;
    }
    else
    {
        if (valorMedido >= valorEsperado) return 0;
        if (!double.IsNaN(valorTolerado) && valorMedido >= valorTolerado) return 1;
        return 2;
    }
}

// RestriccionDosis: etiqueta con unidad, sin condición, sin planMod (caso base, igual antes y después).
chequear("RestriccionDosis: etiqueta 'PTV: D95%: < 50 (45) Gy' (con unidad, sin condicion)",
    CrearEtiquetaBase("PTV: D95%", "", 50, true, 45, "Gy", true, "", "", "", "") == "PTV: D95% < 50 (45) Gy");
chequear("RestriccionDosis: cumple()=0 si valorMedido<=valorEsperado (esMenorQue)",
    CumpleBase(true, 40, 45, 50) == 0);
chequear("RestriccionDosis: cumple()=1 en tolerancia (entre esperado y tolerado), =2 fuera de tolerancia",
    CumpleBase(true, 47, 45, 50) == 1 && CumpleBase(true, 60, 45, 50) == 2);

// RestriccionIndiceConformidad: la única que NO agrega la unidad (IncluirUnidadValorEnEtiqueta=false) y con condición VolPTV.
chequear("RestriccionIndiceConformidad: etiqueta sin unidad ('IC (100%) < 1.2 (1.4)') aunque unidadValor no esté vacío",
    CrearEtiquetaBase("IC (100%)", "", 1.2, true, 1.4, "unidadQueNoDeberiaAparecer", false, "", "", "", "") == "IC (100%) < 1.2 (1.4) ");
chequear("RestriccionDosis (u otro tipo con unidad): la misma etiqueta SÍ lleva la unidad",
    CrearEtiquetaBase("IC (100%)", "", 1.2, true, 1.4, "Gy", true, "", "", "", "") == "IC (100%) < 1.2 (1.4) Gy");
chequear("Con condición VolPTV, se agrega '(id de la condicion)' al final",
    CrearEtiquetaBase("PTV: V95%", "", 95, false, 90, "%", true, "VolPTV", "VolPTV<10", "", "") == "PTV: V95% > 95 (90) % (VolPTV<10)");
chequear("Con planMod, se agrega '*' al final",
    CrearEtiquetaBase("PTV: V95%", "", 95, false, 90, "%", true, "", "", "", "PlanX_Mod").EndsWith("*"));

// dosisEstaEnPorcentaje: default (RestriccionDosis/DosisMax/DosisMedia) mira unidadValor;
// RestriccionVolumen/VolumenCritico miran unidadCorrespondiente; RestriccionIndiceConformidad siempre true.
bool DosisEstaEnPorcentajeDefault(string unidadValor) => unidadValor == "%";
bool DosisEstaEnPorcentajeVolumen(string unidadCorrespondiente) => unidadCorrespondiente == "%";
chequear("RestriccionDosis: dosisEstaEnPorcentaje mira unidadValor ('%'->true, 'Gy'->false)",
    DosisEstaEnPorcentajeDefault("%") && !DosisEstaEnPorcentajeDefault("Gy"));
chequear("RestriccionVolumen/VolumenCritico: dosisEstaEnPorcentaje mira unidadCorrespondiente, no unidadValor",
    DosisEstaEnPorcentajeVolumen("%") && !DosisEstaEnPorcentajeVolumen("Gy"));

// ===== 9) doseRate/coincidenciaCamillas: if/else largos -> lookup por tabla (Chequeos.cs) =====
// No se puede instanciar Beam/PlanSetup fuera de Eclipse, así que se reproduce cada lógica (vieja
// hardcodeada vs nueva por tabla) con los mismos strings/doubles que reciben los métodos reales.
Console.WriteLine("=== 9) doseRate/coincidenciaCamillas por tabla ===");

double DoseRateEsperadoViejo(string energyMode, string mlcPlanType, string treatmentUnitId)
{
    if (energyMode == "6X-SRS") return 1000;
    if (mlcPlanType == "VMAT") return 600;
    if (treatmentUnitId == "CRC_EQ1") return 320;
    if (treatmentUnitId == "Varian-600C") return 240;
    if (treatmentUnitId == "6oo C/D") return 300;
    return 400;
}

// Copia literal de Chequeos.doseRateEsperado, contra las líneas reales de doseRate.txt.
string[] lineasDoseRate = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "doseRate.txt"));
double DoseRateEsperadoNuevo(string clave, double valorPorDefecto)
{
    string coincidencia = lineasDoseRate.FirstOrDefault(s => s.Split('\t')[0] == clave);
    if (coincidencia == null || !double.TryParse(coincidencia.Split('\t')[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double valor))
    {
        return valorPorDefecto;
    }
    return valor;
}
double DoseRateEsperado(string energyMode, string mlcPlanType, string treatmentUnitId)
{
    if (energyMode == "6X-SRS") return DoseRateEsperadoNuevo("6X-SRS", 1000);
    if (mlcPlanType == "VMAT") return DoseRateEsperadoNuevo("VMAT", 600);
    return DoseRateEsperadoNuevo(treatmentUnitId, DoseRateEsperadoNuevo("DEFAULT", 400));
}

foreach (var caso in new[] {
    ("6X-SRS", "", "cualquiera"),
    ("", "VMAT", "cualquiera"),
    ("", "ARC", "CRC_EQ1"),
    ("", "ARC", "Varian-600C"),
    ("", "ARC", "6oo C/D"),
    ("", "ARC", "OtroEquipoNoListado"),
})
{
    double viejo = DoseRateEsperadoViejo(caso.Item1, caso.Item2, caso.Item3);
    double nuevo = DoseRateEsperado(caso.Item1, caso.Item2, caso.Item3);
    chequear($"doseRate esperado igual viejo vs nuevo para ({caso.Item1},{caso.Item2},{caso.Item3}): {viejo}", viejo == nuevo);
}

// Copia literal de la parte "lookup" de Chequeos.coincidenciaCamillas (sin el caso especial BrainLAB/RC).
string[] lineasCamillas = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "camillas.txt"));
bool CoincidenciaCamillasViejo(string camilla, string equipo)
{
    if (camilla.Contains("Unipanel, large") && equipo == "PBA_6EX_730") return true;
    if (camilla.Contains("Unipanel, large") && equipo == "6EX Viamonte") return true;
    if (camilla.Contains("Unipanel, large") && equipo == "CL21EX") return true;
    if (camilla.Contains("Unipanel, large") && equipo == "CRC_EQ1") return true;
    if (camilla.Contains("Unipanel, large") && equipo == "Varian-600C") return true;
    if (camilla.Contains("Unipanel, large") && equipo == "600 C / D") return true;
    if (camilla.Contains("Unipanel, large") && equipo == "Varian 21 EX") return true;
    if (camilla.Contains("IGRT") && equipo == "Equipo1") return true;
    if (camilla.Contains("IGRT") && equipo == "Equipo3") return true;
    if (camilla.Contains("IGRT") && equipo == "Equipo 2 6EX") return true;
    if (camilla.Contains("BL_ICT") && equipo == "D-2300CD") return true;
    if (camilla.Contains("QFix") && equipo == "EQ2_iX_827") return true;
    if (camilla.Contains("Unipanel") && equipo == "QBA_600CD_523") return true;
    return false;
}
bool CoincidenciaCamillasNuevo(string camilla, string equipo)
{
    foreach (string linea in lineasCamillas)
    {
        string[] campos = linea.Split('\t');
        if (campos.Length < 2) continue;
        if (camilla.Contains(campos[0]) && equipo == campos[1]) return true;
    }
    return false;
}
foreach (var caso in new[] {
    ("Unipanel, large", "PBA_6EX_730"),
    ("Unipanel, large", "Varian 21 EX"),
    ("IGRT", "Equipo 2 6EX"),
    ("BL_ICT", "D-2300CD"),
    ("QFix", "EQ2_iX_827"),
    ("Unipanel", "QBA_600CD_523"),
    ("CamillaQueNoExiste", "EquipoQueNoExiste"),
})
{
    bool viejo = CoincidenciaCamillasViejo(caso.Item1, caso.Item2);
    bool nuevo = CoincidenciaCamillasNuevo(caso.Item1, caso.Item2);
    chequear($"coincidenciaCamillas igual viejo vs nuevo para ({caso.Item1}, {caso.Item2}): {viejo}", viejo == nuevo);
}

// Caso especial BrainLAB/D-2300CD (depende de esRadioCirugia(plan), no de la tabla): sigue hardcodeado
// en Chequeos.coincidenciaCamillas. Se verifica la regla `esRadioCirugia == tieneExtensionHN`
// reproduce las 4 combinaciones del if/else original.
bool BrainLabValido(bool esRadioCirugia, bool tieneExtensionHN) => esRadioCirugia == tieneExtensionHN;
chequear("BrainLAB+D-2300CD: RC con extensión H&N -> válida", BrainLabValido(true, true));
chequear("BrainLAB+D-2300CD: RC sin extensión H&N -> inválida", !BrainLabValido(true, false));
chequear("BrainLAB+D-2300CD: no-RC con extensión H&N -> inválida", !BrainLabValido(false, true));
chequear("BrainLAB+D-2300CD: no-RC sin extensión H&N -> válida", BrainLabValido(false, false));

// ===== 10) CacheDVH: comparte DVHData entre restricciones de la misma estructura =====
// No se puede instanciar PlanningItem/Structure/DVHData reales de ESAPI fuera de Eclipse, así que se
// reproduce la misma lógica de clave/diccionario de CacheDVH.cs con objetos propios (fakes), contando
// cuántas veces se "pide a ESAPI" para verificar que se dedupliquen los pedidos.
Console.WriteLine("=== 10) CacheDVH: mismo DVHData reusado por (plan, estructura, volumePresentation) ===");

int pedidosAEsapi = 0;
var cacheFake = new Dictionary<(object plan, object estructura, string volumePresentation), object>();
object ObtenerFake(object plan, object estructura, string volumePresentation)
{
    var clave = (plan, estructura, volumePresentation);
    if (!cacheFake.TryGetValue(clave, out object dvhData))
    {
        pedidosAEsapi++;
        dvhData = new object();
        cacheFake[clave] = dvhData;
    }
    return dvhData;
}
void LimpiarFake() => cacheFake.Clear();

object planA = new object(), planB = new object();
object estrPTV = new object(), estrLung = new object();

pedidosAEsapi = 0;
var d1 = ObtenerFake(planA, estrPTV, "Relative");
var d2 = ObtenerFake(planA, estrPTV, "Relative"); // misma restricción tipo, otra fila -> mismo pedido
chequear("Dos pedidos iguales (mismo plan/estructura/presentación) -> 1 sola llamada a ESAPI", pedidosAEsapi == 1);
chequear("Devuelve el mismo objeto DVHData cacheado", ReferenceEquals(d1, d2));

var d3 = ObtenerFake(planA, estrPTV, "AbsoluteCm3"); // misma estructura, otra presentación (ej. RestriccionVolumen en cm3)
chequear("Misma estructura, distinta VolumePresentation -> pedido nuevo (no comparte curva con otras unidades)", pedidosAEsapi == 2 && !ReferenceEquals(d1, d3));

var d4 = ObtenerFake(planA, estrLung, "Relative"); // otra estructura del mismo plan
chequear("Otra estructura del mismo plan -> pedido nuevo", pedidosAEsapi == 3 && !ReferenceEquals(d1, d4));

var d5 = ObtenerFake(planB, estrPTV, "Relative"); // mismo nombre de estructura pero otro plan (ej. plan2 en comparación de 2 planes)
chequear("Misma estructura pero plan distinto (plan2) -> pedido nuevo, no se mezclan los dos planes", pedidosAEsapi == 4 && !ReferenceEquals(d1, d5));

LimpiarFake();
var d6 = ObtenerFake(planA, estrPTV, "Relative");
chequear("Limpiar() antes del próximo análisis fuerza a pedir de nuevo (no arrastra DVHData del plan/paciente anterior)", pedidosAEsapi == 5 && !ReferenceEquals(d1, d6));

Console.WriteLine();
if (huboError)
{
    Console.WriteLine("HAY CHEQUEOS QUE FALLARON");
    Environment.Exit(1);
}
else
{
    Console.WriteLine("TODOS LOS CHEQUEOS OK");
}
