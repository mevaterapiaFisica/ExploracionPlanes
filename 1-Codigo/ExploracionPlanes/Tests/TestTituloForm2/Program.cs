using System;

// Réplica aislada (sin ESAPI/WPF) de la lógica de título de Form2.xaml.cs / Form2_DosPlanes.xaml.cs
// tras el cambio: para plantillas condicionadas por fx/VolPTV, el título pasa de
// "<Plantilla> volPTV: <vol>cm3 <fx> fx" a "<Plantilla> + <fx> fx + PTV <vol> cm3", reconstruido desde
// un "tituloBase" guardado una sola vez (antes el codigo hacia Title += ..., que se iba acumulando en
// cada click de "Analizar" si el usuario reanalizaba el mismo plan mas de una vez).

class Program
{
    static int fallas = 0;

    static void Assert(string nombre, bool ok)
    {
        Console.WriteLine($"{(ok ? "OK  " : "FAIL")} {nombre}");
        if (!ok) fallas++;
    }

    // Misma logica que Form2.xaml.cs/Form2_DosPlanes.xaml.cs: Title siempre se reconstruye desde
    // tituloBase (nombre de plantilla + paciente si hay contexto), nunca se acumula con "+=".
    static string ConstruirTitulo(string tituloBase, bool tieneCondicionesTipo1, int numFx, double volPTV)
    {
        if (tieneCondicionesTipo1)
        {
            return tituloBase + " + " + numFx.ToString() + " fx + PTV " + volPTV.ToString() + " cm3";
        }
        return tituloBase;
    }

    static void Main()
    {
        // Caso 1: plantilla condicionada, sin contexto de paciente (tituloBase = solo nombre)
        string t1 = ConstruirTitulo("SBRT", true, 3, 45.0);
        Assert("Formato pedido: '<Plantilla> + <fx> fx + PTV <vol> cm3'", t1 == "SBRT + 3 fx + PTV 45 cm3");

        // Caso 2: plantilla condicionada, CON contexto de paciente (tituloBase incluye " - Apellido, Nombre")
        string tituloBaseConPaciente = "RC" + " - " + "Perez, Juan";
        string t2 = ConstruirTitulo(tituloBaseConPaciente, true, 1, 12.3);
        Assert("Con paciente en contexto, el sufijo fx/PTV se agrega despues del nombre del paciente", t2 == "RC - Perez, Juan + 1 fx + PTV 12.3 cm3");

        // Caso 3: plantilla NO condicionada -> titulo queda igual al de siempre (sin sufijo)
        string t3 = ConstruirTitulo("Gliomas_NormoFx_Adultos", false, 5, 30.0);
        Assert("Plantilla sin condiciones de fx/VolPTV -> titulo sin sufijo", t3 == "Gliomas_NormoFx_Adultos");

        // Caso 4 (regresion del bug de "+="): si se re-analiza el mismo plan 2 veces seguidas,
        // el titulo NO debe acumular el sufijo 2 veces (antes "Title += ..." si lo hacia).
        string tituloBase4 = "SBRT";
        string analisis1 = ConstruirTitulo(tituloBase4, true, 3, 45.0);
        string analisis2 = ConstruirTitulo(tituloBase4, true, 3, 45.0); // 2do click en "Analizar"
        Assert("Reanalizar 2 veces no acumula el sufijo (se reconstruye desde tituloBase, no += )", analisis1 == analisis2 && analisis2 == "SBRT + 3 fx + PTV 45 cm3");

        Console.WriteLine();
        Console.WriteLine(fallas == 0 ? "TODOS LOS CHEQUEOS OK" : $"{fallas} CHEQUEO(S) FALLARON");
        Environment.Exit(fallas == 0 ? 0 : 1);
    }
}
