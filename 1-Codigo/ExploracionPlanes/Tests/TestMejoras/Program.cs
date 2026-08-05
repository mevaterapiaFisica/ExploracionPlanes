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

// ===== 1) Damerau-Levenshtein (copia literal de Estructura.DistanciaDamerauLevenshtein) =====
int DistanciaDamerauLevenshtein(string a, string b)
{
    a = a.ToLowerInvariant();
    b = b.ToLowerInvariant();
    int[,] d = new int[a.Length + 1, b.Length + 1];
    for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
    for (int j = 0; j <= b.Length; j++) d[0, j] = j;
    for (int i = 1; i <= a.Length; i++)
    {
        for (int j = 1; j <= b.Length; j++)
        {
            int costo = a[i - 1] == b[j - 1] ? 0 : 1;
            d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + costo);
            if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
            {
                d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + costo);
            }
        }
    }
    return d[a.Length, b.Length];
}

Console.WriteLine("=== 1) Damerau-Levenshtein ===");
chequear("Idénticas -> distancia 0", DistanciaDamerauLevenshtein("PTV", "PTV") == 0);
chequear("Case-insensitive -> distancia 0", DistanciaDamerauLevenshtein("ptv", "PTV") == 0);
chequear("Una sustitución -> distancia 1", DistanciaDamerauLevenshtein("PTV", "PTB") == 1);
chequear("Transposición adyacente cuenta como 1 (Damerau, no Levenshtein simple)", DistanciaDamerauLevenshtein("ab", "ba") == 1);
chequear("PTV_5400 vs PTV_5040 (transposición) distancia baja", DistanciaDamerauLevenshtein("PTV_5400", "PTV_5040") <= 2);
chequear("Nombres muy distintos -> distancia alta", DistanciaDamerauLevenshtein("PTV", "MEDULA") >= 5);

// candidatosPorDistancia: ordena estructuras reales por distancia mínima a cualquiera de los nombresPosibles
List<(string Id, int Distancia)> candidatosPorDistancia(List<string> nombresPosibles, List<string> estructurasPlan)
{
    return estructurasPlan
        .Select(s => (Id: s, Distancia: nombresPosibles.Min(n => DistanciaDamerauLevenshtein(n, s))))
        .OrderBy(t => t.Distancia)
        .ToList();
}

var candidatos = candidatosPorDistancia(new List<string> { "PTV" }, new List<string> { "MEDULA", "PTV_2", "PTV" });
Console.WriteLine("Orden real: " + string.Join(", ", candidatos.Select(c => c.Id + "(" + c.Distancia + ")")));
chequear("Exacto primero", candidatos[0].Id == "PTV" && candidatos[0].Distancia == 0);
chequear("Aproximado (PTV_2) segundo, antes que MEDULA", candidatos[1].Id == "PTV_2" && candidatos[1].Distancia < candidatos[2].Distancia);

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
