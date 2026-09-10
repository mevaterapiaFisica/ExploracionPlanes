// Aisla 2 fixes de Form2.xaml.cs relacionados (mismo bug reportado dos veces: el primer fix no
// alcanzaba porque tocaba solo la mitad del problema):
//
// 1. asociarEstructuras(): si una fila ya tiene un StructureId válido, no se recalcula.
// 2. llenarDGVEstructuras(): reconstruye TODAS las FilaEstructura de cero (Clear() + new) cada vez
//    que se duplica/elimina una fila -> el fix (1) no alcanzaba porque para cuando corre, las filas
//    ya son objetos nuevos con StructureId vacío. Hace falta preservar el StructureId por
//    NombreSlot ANTES del Clear() y aplicarlo a las filas nuevas.
//
// El match manual recién se guarda en disco al apretar "Analizar" (escribirArchivoParEstructuras),
// así que cualquier match manual hecho antes de eso vivía solo en el FilaEstructura en memoria.

// Viejo: siempre recalcula, ignora lo que ya estaba puesto.
string viejo(string? structureIdActual, List<string> itemsOrdenados, string candidatoAutomatico) =>
    candidatoAutomatico;

// Nuevo: si el valor actual sigue siendo una opción válida, se mantiene.
string nuevo(string? structureIdActual, List<string> itemsOrdenados, string candidatoAutomatico) =>
    !string.IsNullOrEmpty(structureIdActual) && itemsOrdenados.Contains(structureIdActual)
        ? structureIdActual
        : candidatoAutomatico;

int fallas = 0;

void Check(string nombre, string esperado, string obtenido)
{
    string estado = obtenido == esperado ? "OK  " : "FAIL";
    if (obtenido != esperado) fallas++;
    Console.WriteLine($"{estado} {nombre}: esperado='{esperado}' obtenido='{obtenido}'");
}

var opciones = new List<string> { "", "GTV01", "PTV_1mm" };

// Caso del bug real: usuario matcheó "PTV" a mano con "PTV_1mm" (no es el candidato automático,
// que sería GTV01 por distancia). Duplica otra fila -> se recalculan todas.
Check("Viejo pisa el match manual con el candidato automático (el bug)", "GTV01", viejo("PTV_1mm", opciones, "GTV01"));
Check("Nuevo preserva el match manual", "PTV_1mm", nuevo("PTV_1mm", opciones, "GTV01"));

// Fila nunca matcheada (StructureId vacío): debe seguir yendo al candidato automático.
Check("Nuevo, fila sin match previo, usa el candidato automático", "GTV01", nuevo("", opciones, "GTV01"));
Check("Nuevo, fila sin match previo (null), usa el candidato automático", "GTV01", nuevo(null, opciones, "GTV01"));

// La estructura que tenía asignada ya no existe en el plan (no está en itemsOrdenados) -> recalcular.
Check("Nuevo, match previo ya no es una opción válida, recalcula", "GTV01", nuevo("Estructura_Borrada", opciones, "GTV01"));

// --- Simulación de llenarDGVEstructuras(): reconstruye las filas por NombreSlot, preservando ---
// --- el StructureId previo (fix 2), y ahí sí asociarEstructuras (fix 1) lo respeta.           ---

Dictionary<string, string> filasViejoBug(Dictionary<string, string> filasActuales, List<string> nombresSlot)
{
    // Viejo: filas nuevas siempre arrancan con StructureId vacío, sin importar lo que había antes.
    return nombresSlot.ToDictionary(n => n, n => "");
}

Dictionary<string, string> filasNuevoFix(Dictionary<string, string> filasActuales, List<string> nombresSlot)
{
    return nombresSlot.ToDictionary(n => n, n => filasActuales.TryGetValue(n, out var id) ? id : "");
}

var filasAntesDeDuplicar = new Dictionary<string, string> { ["PTV"] = "PTV_1mm", ["PTV_0933"] = "" };
var slotsDespuesDeDuplicar = new List<string> { "PTV", "PTV_0933", "PTV (2)" };

var reconstruidoViejo = filasViejoBug(filasAntesDeDuplicar, slotsDespuesDeDuplicar);
Check("Viejo: al reconstruir filas para agregar 'PTV (2)', el match de PTV se pierde (el bug real)", "", reconstruidoViejo["PTV"]);

var reconstruidoNuevo = filasNuevoFix(filasAntesDeDuplicar, slotsDespuesDeDuplicar);
Check("Nuevo: al reconstruir filas para agregar 'PTV (2)', el match de PTV se preserva", "PTV_1mm", reconstruidoNuevo["PTV"]);
Check("Nuevo: la fila nueva 'PTV (2)' arranca sin match (se auto-matchea después)", "", reconstruidoNuevo["PTV (2)"]);

Console.WriteLine();
Console.WriteLine(fallas == 0 ? "TODOS LOS CHEQUEOS OK" : $"{fallas} CHEQUEO(S) FALLARON");
return fallas == 0 ? 0 : 1;
