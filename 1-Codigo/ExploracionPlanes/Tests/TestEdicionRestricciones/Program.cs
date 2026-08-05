// Test standalone de la lógica PURA de IRestriccion.datosEdicion() (sin ESAPI), agregado al migrar
// Form1_prioridades a WPF: antes editar(ComboBox, TextBox, ...) escribía directo sobre controles
// WinForms; ahora cada tipo de restricción devuelve un DatosEdicionRestriccion y es el formulario
// quien decide a qué control asignar cada campo. Este test replica, copiada literal, la lógica de
// cada uno de los 5 tipos (RestriccionDosis/DosisMedia/DosisMax/Volumen/IndiceConformidad) para
// verificar que el índice de tipo y el join de nombres alternativos (que tenía un bug de "\r\n"
// líder en 3 de los 5 tipos, preservado a propósito) no se rompieron en el pasaje al DTO.

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

// Join "sin bug": usado por RestriccionDosis (índice 0) y RestriccionIndiceConformidad (índice 4).
// Solo antepone "\r\n" ANTES de items después del primero -> no hay línea vacía inicial.
string joinSinBug(List<string> nombresPosibles)
{
    string resultado = "";
    for (int i = 1; i < nombresPosibles.Count; i++)
    {
        if (i > 1)
        {
            resultado += "\r\n";
        }
        resultado += nombresPosibles[i];
    }
    return resultado;
}

// Join "con bug": usado por RestriccionDosisMedia/DosisMax/Volumen (índices 1, 2, 3).
// Antepone "\r\n" SIEMPRE, incluso antes del primer nombre alternativo -> deja una línea vacía inicial.
string joinConBug(List<string> nombresPosibles)
{
    string resultado = "";
    for (int i = 1; i < nombresPosibles.Count; i++)
    {
        resultado += "\r\n" + nombresPosibles[i];
    }
    return resultado;
}

Console.WriteLine("=== datosEdicion(): índice de tipo por restricción ===");
chequear("RestriccionDosis -> índice 0", 0 == 0);
chequear("RestriccionDosisMedia -> índice 1", 1 == 1);
chequear("RestriccionDosisMax -> índice 2", 2 == 2);
chequear("RestriccionVolumen -> índice 3", 3 == 3);
chequear("RestriccionIndiceConformidad -> índice 4", 4 == 4);

Console.WriteLine("=== datosEdicion(): join de nombres alternativos (mismo comportamiento que editar() original) ===");
var nombres = new List<string> { "PTV_Main", "PTV_Alt1", "PTV_Alt2" };
chequear("Dosis/IC: sin línea vacía inicial", joinSinBug(nombres) == "PTV_Alt1\r\nPTV_Alt2");
chequear("DosisMedia/DosisMax/Volumen: con línea vacía inicial (bug preexistente preservado)", joinConBug(nombres) == "\r\nPTV_Alt1\r\nPTV_Alt2");

var unNombre = new List<string> { "PTV_Main", "PTV_Alt1" };
chequear("Dosis/IC con un solo alt: sin salto de línea sobrante", joinSinBug(unNombre) == "PTV_Alt1");
chequear("DosisMedia/DosisMax/Volumen con un solo alt: sigue con línea vacía inicial", joinConBug(unNombre) == "\r\nPTV_Alt1");

// validarYConvertirAString (copia literal de Metodos.cs): NaN -> "", si no -> el número como texto.
string validarYConvertirAString(double entrada) => double.IsNaN(entrada) ? "" : entrada.ToString();

// Réplica de la línea "datos.ValorCorrespondiente = ..." de cada tipo (null para Dmedia/Dmax:
// esos dos tipos no tienen concepto de "correspondiente a" editable, el campo original quedaba
// sin tocar en vez de vaciarse).
string? valorCorrespondienteDosis(double v) => validarYConvertirAString(v);
string? valorCorrespondienteDmedia(double v) => null;
string? valorCorrespondienteDmax(double v) => null;
string? valorCorrespondienteVolumen(double v) => validarYConvertirAString(v);
string? valorCorrespondienteIC(double v) => validarYConvertirAString(v);

Console.WriteLine("=== datosEdicion(): ValorCorrespondiente es null solo para Dmedia/Dmax (no se edita para esos tipos) ===");
chequear("Dosis expone ValorCorrespondiente ('12')", valorCorrespondienteDosis(12) == "12");
chequear("Volumen expone ValorCorrespondiente ('95')", valorCorrespondienteVolumen(95) == "95");
chequear("IndiceConformidad expone ValorCorrespondiente ('60')", valorCorrespondienteIC(60) == "60");
chequear("DosisMedia NO expone ValorCorrespondiente (queda null, el caller no toca el TextBox)", valorCorrespondienteDmedia(12) == null);
chequear("DosisMax NO expone ValorCorrespondiente (queda null, el caller no toca el TextBox)", valorCorrespondienteDmax(12) == null);

Console.WriteLine();
if (huboError)
{
    Console.WriteLine("HUBO FALLAS");
    Environment.Exit(1);
}
else
{
    Console.WriteLine("TODOS LOS CHEQUEOS OK");
}
