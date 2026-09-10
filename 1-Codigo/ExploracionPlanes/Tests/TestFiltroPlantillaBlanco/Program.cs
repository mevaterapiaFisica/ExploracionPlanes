using System;
using System.Collections.Generic;
using System.Linq;

// Réplica aislada (sin WPF/ESAPI) del filtro agregado en PlantillaBlanco.xaml.cs:
// antes del cambio, llenarAnalisis() mostraba TODAS las restricciones de la
// plantilla sin importar su Condicion (NumFx / VolPTV). Ahora hay listboxes
// que filtran por el id de condición seleccionado (o "(Todas)").

class Restriccion
{
    public string Id = "";
    public string? Tipo; // "NumFx", "VolPTV" o null (sin condición)
    public string? CondicionId;
    public double ValorCondicion; // valor numérico de la condición, para el orden del combo
}

class Program
{
    const string TODAS = "(Todas)";
    static int fallas = 0;

    static void Assert(string nombre, bool ok)
    {
        Console.WriteLine($"{(ok ? "OK  " : "FAIL")} {nombre}");
        if (!ok) fallas++;
    }

    // Comportamiento VIEJO: sin filtro, todas las restricciones se muestran siempre.
    static List<string> FilasViejo(List<Restriccion> restricciones)
    {
        return restricciones.Select(r => r.Id).ToList();
    }

    // Comportamiento NUEVO: filtra por el id de condición seleccionado en cada combo.
    static List<string> FilasNuevo(List<Restriccion> restricciones, string filtroNumFx, string filtroVolPTV)
    {
        var filas = new List<string>();
        foreach (var r in restricciones)
        {
            if (r.Tipo == "NumFx" && filtroNumFx != TODAS && r.CondicionId != filtroNumFx) continue;
            if (r.Tipo == "VolPTV" && filtroVolPTV != TODAS && r.CondicionId != filtroVolPTV) continue;
            filas.Add(r.Id);
        }
        return filas;
    }

    // Orden del combo: antes del fix se ordenaba alfabéticamente por el id ("NumFx=1", "NumFx=10", "NumFx=2", ...);
    // ahora se ordena por el valor numérico de la condición.
    static List<string> OpcionesComboViejo(List<Restriccion> restricciones, string tipo)
    {
        return restricciones.Where(r => r.Tipo == tipo).Select(r => r.CondicionId!).Distinct().OrderBy(id => id).ToList();
    }

    static List<string> OpcionesComboNuevo(List<Restriccion> restricciones, string tipo)
    {
        return restricciones.Where(r => r.Tipo == tipo)
            .GroupBy(r => r.CondicionId)
            .Select(g => g.First())
            .OrderBy(r => r.ValorCondicion)
            .Select(r => r.CondicionId!)
            .ToList();
    }

    static void Main()
    {
        var restricciones = new List<Restriccion>
        {
            new Restriccion { Id = "R0_PTV_D95", Tipo = null },
            new Restriccion { Id = "R1_MEDULA_5fx", Tipo = "NumFx", CondicionId = "NumFx=5", ValorCondicion = 5 },
            new Restriccion { Id = "R2_MEDULA_10fx", Tipo = "NumFx", CondicionId = "NumFx=10", ValorCondicion = 10 },
            new Restriccion { Id = "R3_PULMON", Tipo = null },
            new Restriccion { Id = "R4_RIÑON_chico", Tipo = "VolPTV", CondicionId = "VolPTV<50", ValorCondicion = 50 },
            new Restriccion { Id = "R5_RIÑON_grande", Tipo = "VolPTV", CondicionId = "VolPTV>50", ValorCondicion = 50 },
            new Restriccion { Id = "R6_MEDULA_1fx", Tipo = "NumFx", CondicionId = "NumFx=1", ValorCondicion = 1 },
            new Restriccion { Id = "R7_MEDULA_2fx", Tipo = "NumFx", CondicionId = "NumFx=2", ValorCondicion = 2 },
            new Restriccion { Id = "R8_MEDULA_3fx", Tipo = "NumFx", CondicionId = "NumFx=3", ValorCondicion = 3 },
        };

        // Viejo: siempre aparecen todas, mezclando condiciones incompatibles entre sí (bug reportado).
        var filasViejo = FilasViejo(restricciones);
        Assert("Viejo muestra todas las restricciones sin filtrar (comportamiento reportado)", filasViejo.Count == restricciones.Count);

        // Nuevo, sin filtro seleccionado ("(Todas)" en ambos listbox): igual que el viejo.
        var filasNuevoSinFiltro = FilasNuevo(restricciones, TODAS, TODAS);
        Assert("Nuevo sin filtro reproduce el comportamiento viejo", filasNuevoSinFiltro.SequenceEqual(filasViejo));

        // Nuevo, filtrando por NumFx=5: sin condición siempre entran, NumFx=10 se excluye, VolPTV no se toca.
        var filasNuevoFx5 = FilasNuevo(restricciones, "NumFx=5", TODAS);
        var esperadoFx5 = new List<string> { "R0_PTV_D95", "R1_MEDULA_5fx", "R3_PULMON", "R4_RIÑON_chico", "R5_RIÑON_grande" };
        Assert("Nuevo filtra por NumFx=5 (excluye NumFx=10, respeta el resto)", filasNuevoFx5.SequenceEqual(esperadoFx5));

        // Nuevo, combinando ambos filtros a la vez.
        var filasNuevoCombinado = FilasNuevo(restricciones, "NumFx=10", "VolPTV<50");
        var esperadoCombinado = new List<string> { "R0_PTV_D95", "R2_MEDULA_10fx", "R3_PULMON", "R4_RIÑON_chico" };
        Assert("Nuevo combina ambos filtros (NumFx=10 + VolPTV<50)", filasNuevoCombinado.SequenceEqual(esperadoCombinado));

        // Orden del combo de NumFx: viejo ordena alfabético por id (1, 10, 2, 3, 5), nuevo por valor numérico (1, 2, 3, 5, 10).
        var opcionesViejo = OpcionesComboViejo(restricciones, "NumFx");
        var opcionesNuevo = OpcionesComboNuevo(restricciones, "NumFx");
        Console.WriteLine("Combo NumFx (viejo, alfabético): " + string.Join(", ", opcionesViejo));
        Console.WriteLine("Combo NumFx (nuevo, numérico):   " + string.Join(", ", opcionesNuevo));
        var esperadoViejoAlfabetico = new List<string> { "NumFx=1", "NumFx=10", "NumFx=2", "NumFx=3", "NumFx=5" };
        Assert("Viejo ordena el combo alfabéticamente (1,10,2,3,5 - bug reportado)", opcionesViejo.SequenceEqual(esperadoViejoAlfabetico));
        var esperadoNuevoNumerico = new List<string> { "NumFx=1", "NumFx=2", "NumFx=3", "NumFx=5", "NumFx=10" };
        Assert("Nuevo ordena el combo numéricamente (1,2,3,5,10)", opcionesNuevo.SequenceEqual(esperadoNuevoNumerico));

        Console.WriteLine();
        Console.WriteLine(fallas == 0 ? "TODOS LOS CHEQUEOS OK" : $"{fallas} CHEQUEO(S) FALLARON");
        Environment.Exit(fallas == 0 ? 0 : 1);
    }
}
