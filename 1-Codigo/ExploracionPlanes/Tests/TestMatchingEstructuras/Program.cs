// Aisla la lógica pura de prioridad que ahora usa Form2_DosPlanes.xaml.cs para matchear
// estructuras (plan1 vía asociarEstructuras(), plan2 vía estructuraCorrespondiente2()):
// exacto/alias -> memoria guardada -> mejor candidato por distancia (si entra en el umbral) -> "".
// Mismo orden que ya usaba Form2.asociarFila; acá se prueba MatchingEstructuras.ElegirStructureId,
// la función que ambos plan1 y plan2 de Form2_DosPlanes llaman ahora (antes plan2 no tenía fallback
// por distancia y fallaba seguido con "no se encontró la estructura").

using ExploracionPlanes;

int fallas = 0;

void Check(string nombre, string esperado, string obtenido)
{
    string estado = obtenido == esperado ? "OK  " : "FAIL";
    if (obtenido != esperado) fallas++;
    Console.WriteLine($"{estado} {nombre}: esperado='{esperado}' obtenido='{obtenido}'");
}

var opciones = new List<string> { "", "Brainstem_PRV02", "Cochlea_L" };

// 1) Match exacto/alias disponible: gana siempre, sin importar memoria ni distancia.
Check("Exacto gana sobre memoria y distancia",
    "Brainstem_PRV02",
    MatchingEstructuras.ElegirStructureId("Brainstem_PRV02", "Cochlea_L", opciones, Tuple.Create("Cochlea_L", 1)));

// 2) Sin exacto, memoria válida (sigue siendo una opción real del plan actual): gana sobre distancia.
Check("Sin exacto, memoria válida gana sobre distancia",
    "Cochlea_L",
    MatchingEstructuras.ElegirStructureId("", "Cochlea_L", opciones, Tuple.Create("Brainstem_PRV02", 2)));

// 3) Memoria apunta a una estructura que ya no existe en este plan (no es una opción válida): se ignora, cae a distancia.
Check("Memoria inválida (estructura borrada) cae a distancia",
    "Brainstem_PRV02",
    MatchingEstructuras.ElegirStructureId("", "Estructura_Borrada", opciones, Tuple.Create("Brainstem_PRV02", 2)));

// 4) Sin exacto ni memoria, mejor candidato por distancia DENTRO del umbral (4): se autoasocia.
//    Este es el caso real reportado: plan2 con nombres apenas distintos al plan1 (ej. "BrainStem_PRV_02"
//    vs "Brainstem_PRV02") no matcheaba exacto y antes no tenía ningún fallback -> quedaba sin asociar.
Check("Sin exacto/memoria, candidato dentro del umbral se autoasocia",
    "Brainstem_PRV02",
    MatchingEstructuras.ElegirStructureId(null, null, opciones, Tuple.Create("Brainstem_PRV02", 4)));

// 5) Candidato FUERA del umbral: no se autoasocia (evita matchear estructuras no relacionadas).
Check("Candidato fuera del umbral no se autoasocia",
    "",
    MatchingEstructuras.ElegirStructureId(null, null, opciones, Tuple.Create("Cochlea_L", 5)));

// 6) Sin exacto, sin memoria, sin ningún candidato (plan sin ninguna estructura remotamente parecida).
Check("Sin ningún candidato, sin match",
    "",
    MatchingEstructuras.ElegirStructureId(null, null, opciones, null));

Console.WriteLine();
Console.WriteLine(fallas == 0 ? "TODOS LOS CHEQUEOS OK" : $"{fallas} CHEQUEO(S) FALLARON");
return fallas == 0 ? 0 : 1;
