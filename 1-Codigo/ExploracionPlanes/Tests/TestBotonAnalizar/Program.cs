// Aisla la lógica de actualizarBotonAnalizar() (Form2.xaml.cs) sin WPF/ESAPI.
// Bug: en modo contexto (hayContext=true, invocado desde script en Eclipse) LB_Planes
// nunca se puebla, así que la fórmula vieja siempre daba false al recalcular tras
// duplicar/eliminar una estructura duplicada.

bool viejo(bool hayContext, int planesSeleccionados, int filasEstructuras) =>
    planesSeleccionados == 1 && filasEstructuras > 0;

bool nuevo(bool hayContext, int planesSeleccionados, int filasEstructuras) =>
    (hayContext || planesSeleccionados == 1) && filasEstructuras > 0;

int fallas = 0;

void Check(string nombre, bool esperado, bool obtenido)
{
    string estado = obtenido == esperado ? "OK  " : "FAIL";
    if (obtenido != esperado) fallas++;
    Console.WriteLine($"{estado} {nombre}: esperado={esperado} obtenido={obtenido}");
}

// Modo contexto (script Eclipse): LB_Planes vacío (0 seleccionados), plan viene por contexto.
Check("Contexto, con estructuras: viejo queda deshabilitado (el bug)", false, viejo(hayContext: true, planesSeleccionados: 0, filasEstructuras: 3));
Check("Contexto, con estructuras: nuevo queda habilitado (fix)", true, nuevo(hayContext: true, planesSeleccionados: 0, filasEstructuras: 3));
Check("Contexto, sin estructuras (plantilla vacía): nuevo sigue deshabilitado", false, nuevo(hayContext: true, planesSeleccionados: 0, filasEstructuras: 0));

// Modo standalone (sin contexto): comportamiento no debe cambiar.
Check("Standalone, un plan seleccionado, con estructuras: viejo habilitado", true, viejo(hayContext: false, planesSeleccionados: 1, filasEstructuras: 2));
Check("Standalone, un plan seleccionado, con estructuras: nuevo igual", true, nuevo(hayContext: false, planesSeleccionados: 1, filasEstructuras: 2));
Check("Standalone, ningún plan seleccionado: nuevo sigue deshabilitado", false, nuevo(hayContext: false, planesSeleccionados: 0, filasEstructuras: 2));

Console.WriteLine();
Console.WriteLine(fallas == 0 ? "TODOS LOS CHEQUEOS OK" : $"{fallas} CHEQUEO(S) FALLARON");
return fallas == 0 ? 0 : 1;
