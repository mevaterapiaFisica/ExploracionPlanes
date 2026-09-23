using System;
using System.Collections.Generic;
using System.Linq;

// Réplica aislada (sin WPF/ESAPI) de la preselección de plantilla en Main.xaml.cs cuando corre
// con contexto (context.PlanSetup desde Eclipse). Bug reportado: al correr desde script, a veces
// no preselecciona ninguna plantilla, o preselecciona una distinta a la que corresponde.
//
// Causa 1 (Main.xaml.cs): la plantilla "ganadora" se elegía sobre Plantilla.leerPlantillas() (lista
// completa, leída de disco de nuevo), pero el índice resultante se aplicaba como SelectedIndex sobre
// LB_Plantillas.ItemsSource, que es una lista FILTRADA (oculta las plantillas con Visible=false
// salvo que CHB_MostrarOcultas esté tildado). Si hay plantillas ocultas antes de la ganadora en la
// lista completa, el índice queda corrido: selecciona la plantilla equivocada, o ninguna si el
// índice cae fuera del rango de la lista filtrada (más corta).
//
// Causa 2 (Plantillla.cs, SeleccionarAutomaticamentePlantilla): buscaba la ganadora (por memoria
// recordada o por coincidencia de estructuras) sobre TODAS las plantillas, incluidas las ocultas. Una
// plantilla oculta podía "ganar" por mejor coincidencia de estructuras (no solo por memoria vieja) y
// nunca iba a encontrarse en LB_Plantillas.ItemsSource (que solo muestra visibles). Se filtra a
// visibles antes de buscar, así la ganadora siempre puede preseleccionarse.

class Plantilla
{
    public string Path = "";
    public bool Visible = true;
}

class Program
{
    static int fallas = 0;

    static void Assert(string nombre, bool ok)
    {
        Console.WriteLine($"{(ok ? "OK  " : "FAIL")} {nombre}");
        if (!ok) fallas++;
    }

    static List<Plantilla> ListaFiltrada(List<Plantilla> todas, bool mostrarOcultas)
    {
        return mostrarOcultas ? todas : todas.Where(p => p.Visible).ToList();
    }

    // Comportamiento VIEJO: indice sobre la lista completa aplicado a la lista filtrada.
    static Plantilla? SeleccionViejo(List<Plantilla> todas, Plantilla ganadora, bool mostrarOcultas)
    {
        List<Plantilla> filtrada = ListaFiltrada(todas, mostrarOcultas);
        int indice = todas.FindIndex(p => p.Path == ganadora.Path);
        if (indice < 0 || indice >= filtrada.Count) return null; // SelectedIndex fuera de rango
        return filtrada[indice];
    }

    // Comportamiento NUEVO: matchea por Path directo contra los items realmente listados.
    static Plantilla? SeleccionNuevo(List<Plantilla> todas, Plantilla ganadora, bool mostrarOcultas)
    {
        List<Plantilla> filtrada = ListaFiltrada(todas, mostrarOcultas);
        return filtrada.FirstOrDefault(p => p.Path == ganadora.Path);
    }

    static void Main()
    {
        var todas = new List<Plantilla>
        {
            new Plantilla { Path = "A_oculta", Visible = false },
            new Plantilla { Path = "B_oculta", Visible = false },
            new Plantilla { Path = "C_visible_ganadora", Visible = true },
            new Plantilla { Path = "D_visible", Visible = true },
        };
        var ganadora = todas.First(p => p.Path == "C_visible_ganadora"); // índice 2 en 'todas'

        // Viejo: índice 2 en la lista completa, pero la lista filtrada (sin ocultas) solo tiene
        // ["C_visible_ganadora", "D_visible"] -> índice 2 no existe -> no selecciona nada.
        var viejo = SeleccionViejo(todas, ganadora, mostrarOcultas: false);
        Assert("Viejo: no selecciona nada cuando hay ocultas antes de la ganadora (bug reportado)", viejo == null);

        // Nuevo: matchea por path, encuentra la ganadora sin importar cuántas ocultas la precedan.
        var nuevo = SeleccionNuevo(todas, ganadora, mostrarOcultas: false);
        Assert("Nuevo: selecciona la plantilla correcta pese a las ocultas", nuevo?.Path == "C_visible_ganadora");

        // Caso sin ocultas: viejo y nuevo coinciden (por eso el bug no era evidente en todos los casos).
        var todasSinOcultas = new List<Plantilla>
        {
            new Plantilla { Path = "X" },
            new Plantilla { Path = "Y_ganadora" },
        };
        var ganadoraSinOcultas = todasSinOcultas.First(p => p.Path == "Y_ganadora");
        var viejoSinOcultas = SeleccionViejo(todasSinOcultas, ganadoraSinOcultas, mostrarOcultas: false);
        var nuevoSinOcultas = SeleccionNuevo(todasSinOcultas, ganadoraSinOcultas, mostrarOcultas: false);
        Assert("Sin ocultas, viejo y nuevo coinciden", viejoSinOcultas?.Path == nuevoSinOcultas?.Path && nuevoSinOcultas?.Path == "Y_ganadora");

        // Caso "selecciona la equivocada" (no solo None): oculta antes + otra visible después de la ganadora.
        var todasCorrida = new List<Plantilla>
        {
            new Plantilla { Path = "P0_oculta", Visible = false },
            new Plantilla { Path = "P1_ganadora", Visible = true },
            new Plantilla { Path = "P2_visible", Visible = true },
        };
        var ganadoraCorrida = todasCorrida.First(p => p.Path == "P1_ganadora"); // índice 1 en 'todas'
        var viejoCorrida = SeleccionViejo(todasCorrida, ganadoraCorrida, mostrarOcultas: false);
        // filtrada = [P1_ganadora, P2_visible], índice 1 -> selecciona P2_visible (equivocada)
        Assert("Viejo: selecciona la plantilla equivocada por corrimiento de índice (bug reportado)", viejoCorrida?.Path == "P2_visible");
        var nuevoCorrida = SeleccionNuevo(todasCorrida, ganadoraCorrida, mostrarOcultas: false);
        Assert("Nuevo: selecciona la plantilla correcta (P1_ganadora)", nuevoCorrida?.Path == "P1_ganadora");

        // Causa 2: SeleccionarAutomaticamentePlantilla elegía entre TODAS (memoria o coincidencia de
        // estructuras podían devolver una oculta). Réplica minima: buscar solo por nombre en una lista
        // con ocultas (viejo) vs. filtrando a visibles antes de buscar (nuevo).
        var candidatas = new List<Plantilla>
        {
            new Plantilla { Path = "Recordada_oculta", Visible = false }, // se ocultó después de guardarse en memoria
            new Plantilla { Path = "MejorMatchEstructuras_oculta", Visible = false }, // gana por coincidencia aunque esté oculta
            new Plantilla { Path = "Visible1", Visible = true },
        };
        var ganadoraViejo = candidatas.FirstOrDefault(p => p.Path == "Recordada_oculta"); // memoria, sin filtrar
        Assert("Viejo: SeleccionarAutomaticamentePlantilla puede devolver una plantilla oculta", ganadoraViejo?.Visible == false);

        var visibles = candidatas.Where(p => p.Visible).ToList();
        var ganadoraNuevo = visibles.FirstOrDefault(p => p.Path == "Recordada_oculta"); // ya no está en el pool
        Assert("Nuevo: la recordada oculta no puede ganar (se filtra antes de buscar)", ganadoraNuevo == null);

        Console.WriteLine();
        Console.WriteLine(fallas == 0 ? "TODOS LOS CHEQUEOS OK" : $"{fallas} CHEQUEO(S) FALLARON");
        Environment.Exit(fallas == 0 ? 0 : 1);
    }
}
