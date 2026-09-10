using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using ExploracionPlanes;

class Program
{
    static void Main()
    {
        string carpetaTablas = @"\\fisica0\centro_de_datos2018\000_Centro de Datos 2021\12-Software propio\2-En uso clínico\ExploracionPlanes\1-Codigo\ExploracionPlanes\tablas";
        Console.WriteLine("Armando las plantillas con el parseo REAL de produccion (DesdeCSV.cs, sin reimplementar).");
        Console.WriteLine("No se llama a Plantilla.guardar() (hace MessageBox.Show, cuelga sin desktop interactivo) -- se");
        Console.WriteLine("invocan por reflection los mismos metodos privados y se serializa directo con IO.writeObjectAsJson.");

        Type tipoDesdeCSV = typeof(DesdeCSV);
        MethodInfo metodoImportarArchivoFx = tipoDesdeCSV.GetMethod("ImportarArchivoFx", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new Exception("No se encontro DesdeCSV.ImportarArchivoFx (privado) -- cambio el nombre en produccion?");
        MethodInfo metodoImportarCoberturaPTV = tipoDesdeCSV.GetMethod("ImportarCoberturaPTV", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new Exception("No se encontro DesdeCSV.ImportarCoberturaPTV (privado) -- cambio el nombre en produccion?");
        MethodInfo metodoExtrasRC = tipoDesdeCSV.GetMethod("AgregarExtrasCuradosRC", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new Exception("No se encontro DesdeCSV.AgregarExtrasCuradosRC (privado) -- cambio el nombre en produccion?");
        MethodInfo metodoExtrasSBRT = tipoDesdeCSV.GetMethod("AgregarExtrasCuradosSBRT", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new Exception("No se encontro DesdeCSV.AgregarExtrasCuradosSBRT (privado) -- cambio el nombre en produccion?");
        MethodInfo metodoMapearSBRT = tipoDesdeCSV.GetMethod("MapearEstructurasSBRT", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new Exception("No se encontro DesdeCSV.MapearEstructurasSBRT (privado) -- cambio el nombre en produccion?");
        MethodInfo metodoFusionar = tipoDesdeCSV.GetMethod("FusionarNombresDuplicados", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new Exception("No se encontro DesdeCSV.FusionarNombresDuplicados (privado) -- cambio el nombre en produccion?");
        MethodInfo metodoUnificarPedidos = tipoDesdeCSV.GetMethod("UnificarNombresPedidosSBRT", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new Exception("No se encontro DesdeCSV.UnificarNombresPedidosSBRT (privado) -- cambio el nombre en produccion?");
        MethodInfo metodoOrdenarPTV = tipoDesdeCSV.GetMethod("OrdenarPTVPrimero", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new Exception("No se encontro DesdeCSV.OrdenarPTVPrimero (privado) -- cambio el nombre en produccion?");
        MethodInfo metodoSepararSBRT = tipoDesdeCSV.GetMethod("SepararSBRT", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new Exception("No se encontro DesdeCSV.SepararSBRT (privado) -- cambio el nombre en produccion?");

        var restriccionesSBRT = new BindingList<IRestriccion>();
        var restriccionesRC = new BindingList<IRestriccion>();

        foreach (string path in Directory.GetFiles(carpetaTablas, "*FX*.csv"))
        {
            metodoImportarArchivoFx.Invoke(null, new object[] { path, restriccionesSBRT, restriccionesRC });
        }

        string pathVolPTV = Path.Combine(carpetaTablas, "Constrains consortium vs volPTV.txt");
        if (File.Exists(pathVolPTV))
        {
            metodoImportarCoberturaPTV.Invoke(null, new object[] { pathVolPTV, restriccionesSBRT });
        }

        metodoExtrasRC.Invoke(null, new object[] { restriccionesRC });
        metodoExtrasSBRT.Invoke(null, new object[] { restriccionesSBRT });
        metodoMapearSBRT.Invoke(null, new object[] { restriccionesSBRT });
        metodoFusionar.Invoke(null, new object[] { restriccionesRC });
        metodoFusionar.Invoke(null, new object[] { restriccionesSBRT });
        metodoUnificarPedidos.Invoke(null, new object[] { restriccionesSBRT });
        metodoOrdenarPTV.Invoke(null, new object[] { restriccionesRC });

        string carpetaSalida = Path.Combine(ExploracionPlanes.Properties.Settings.Default.Path, "Plantillas");
        Directory.CreateDirectory(carpetaSalida);

        string notaRC = "[1] Timmerman\r\n[2] UK Consortium\r\n[H] Hombre\r\n[M] Mujer\r\n* Optimal";
        Plantilla plantillaRC = Plantilla.crear("RC", false, restriccionesRC, notaRC);
        plantillaRC.path = Path.Combine(carpetaSalida, "RC.txt");
        IO.writeObjectAsJson(plantillaRC.path, plantillaRC);
        Console.WriteLine($"RC: {restriccionesRC.Count} restricciones. Escrito: " + plantillaRC.path);

        string notaSBRT = "[1] Timmerman\r\n[2] UK Consortium\r\n[H] Hombre\r\n[M] Mujer\r\n* Optimal\r\nCoverage PTV: ver nota de cada restriccion (Pulmon / No pulmon)";
        var separadas = (System.Collections.IEnumerable)metodoSepararSBRT.Invoke(null, new object[] { restriccionesSBRT })!;
        foreach (object item in separadas)
        {
            // Tupla (string, BindingList<IRestriccion>) devuelta por reflection -> se lee por los campos
            // publicos Item1/Item2 de ValueTuple en vez de castear al tipo generico directamente.
            Type tipoTupla = item.GetType();
            string nombrePlantilla = (string)tipoTupla.GetField("Item1")!.GetValue(item)!;
            var restricciones = (BindingList<IRestriccion>)tipoTupla.GetField("Item2")!.GetValue(item)!;

            Plantilla plantilla = Plantilla.crear(nombrePlantilla, false, restricciones, notaSBRT);
            plantilla.path = Path.Combine(carpetaSalida, nombrePlantilla + ".txt");
            IO.writeObjectAsJson(plantilla.path, plantilla);
            Console.WriteLine($"{nombrePlantilla}: {restricciones.Count} restricciones. Escrito: " + plantilla.path);
        }
    }
}
