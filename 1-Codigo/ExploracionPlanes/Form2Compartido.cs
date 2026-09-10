using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace ExploracionPlanes
{
    // Lógica y helpers de archivo que Form2 (análisis de 1 plan) y Form2_DosPlanes (comparación de 2
    // planes) tenían copiados dos veces. Las dos ventanas siguen siendo DialogoWpf por separado (los
    // flujos de llenarDGVAnalisis/analizarRestriccion son genuinamente distintos - Form2_DosPlanes
    // agrega columna de comparación y try/catch por restricción), pero todo lo que era texto idéntico
    // (I/O de memoria por plan, impresión, prescripcionPredefinida, etc.) queda acá una sola vez.
    public static class Form2Compartido
    {
        public static string pathParEstructuras => Properties.Settings.Default.Path + @"\paresEstructuras\";
        public static string pathPrescripciones => Properties.Settings.Default.Path + @"\prescripciones\";
        public static string pathDuplicados => Properties.Settings.Default.Path + @"\duplicadosEstructura\";
        public static string pathReportesJson => Properties.Settings.Default.Path + @"\Reportes\Json\";

        public static Course abrirCurso(Patient paciente, string nombreCurso)
        {
            return paciente.Courses.Where(c => c.Id == nombreCurso).FirstOrDefault();
        }

        public static PlanningItem abrirPlan(Course curso, string nombrePlan)
        {
            return curso.PlanSetups.Where(p => p.Id == nombrePlan).FirstOrDefault();
        }

        public static string equipo(PlanningItem plan)
        {
            string equipoID = "";
            if (plan is PlanSetup)
            {
                equipoID = ((PlanSetup)plan).Beams.First().TreatmentUnit.Id;
            }
            else if (plan is PlanSum)
            {
                equipoID = ((PlanSum)plan).PlanSetups.First().Beams.First().TreatmentUnit.Id;
            }
            return Equipos.diccionario()[equipoID];
        }

        public static List<Course> listaCursos(Patient paciente)
        {
            return paciente.Courses.ToList();
        }

        public static List<PlanningItem> listaPlanes(Course curso)
        {
            List<PlanningItem> lista = new List<PlanningItem>();
            foreach (PlanSetup planSetup in curso.PlanSetups)
            {
                lista.Add(planSetup);
            }
            foreach (PlanSum planSum in curso.PlanSums)
            {
                lista.Add(planSum);
            }
            return lista;
        }

        // Volumen a usar para una restricción condicionada por volumen de PTV: si la restricción es
        // sobre un PTV, su propio volumen (permite tener una restricción por cada PTV duplicado);
        // si es sobre otra estructura (ej. Lung), la suma de todos los PTVs matcheados.
        public static double volPTVParaCondicion(Structure estructuraRestriccion, List<Structure> ptvsMatcheados)
        {
            if (estructuraRestriccion != null && estructuraRestriccion.DicomType == "PTV")
            {
                return estructuraRestriccion.Volume;
            }
            return ptvsMatcheados.Sum(s => s.Volume);
        }

        public static System.Drawing.Color colorDrawing(System.Windows.Media.Brush brush)
        {
            if (brush is System.Windows.Media.SolidColorBrush solido && solido.Color.A != 0)
            {
                var c = solido.Color;
                return System.Drawing.Color.FromArgb(255, c.R, c.G, c.B);
            }
            return System.Drawing.Color.White;
        }

        public static void aplicarPrescripciones(Plantilla plantilla, IEnumerable<FilaPrescripcion> filasPrescripciones)
        {
            foreach (IRestriccion restriccion in plantilla.listaRestricciones)
            {
                if (restriccion.dosisEstaEnPorcentaje())
                {
                    foreach (var fila in filasPrescripciones)
                    {
                        if (restriccion.estructura.nombre.Equals(fila.Estructura))
                        {
                            restriccion.prescripcionEstructura = Metodos.validarYConvertirADouble(fila.Dosis);
                            break;
                        }
                    }
                }
            }
        }

        public static void cargarAlfaBetaDGVEstructuras(IEnumerable<FilaEstructura> filasEstructuras)
        {
            foreach (var fila in filasEstructuras)
            {
                fila.AlfaBeta = Estructura.AlfaBeta(fila.NombreSlot).ToString();
            }
        }

        // ponytail: una excepción no manejada acá tira abajo todo el proceso (comparte hilo de UI con
        // las demás ventanas, no solo esta) — Dispose() de la sesión de Eclipse es lo más propenso a
        // fallar (login ya inválido, etc.), no vale la pena arriesgar el resto de la app. No se llama
        // a ClosePatient() antes de Dispose() (dos cierres nativos seguidos contra la misma sesión de
        // Vision, sospecha de fallo - Dispose ya cierra el paciente solo).
        public static void cerrarSesion(bool hayContext, VMS.TPS.Common.Model.API.Application app, System.Windows.Controls.ItemCollection cursos, System.Windows.Controls.ItemCollection planes)
        {
            try
            {
                if (!hayContext)
                {
                    cursos.Clear();
                    planes.Clear();
                }
                if (app != null)
                {
                    app.Dispose();
                }
            }
            catch (Exception)
            {
            }
        }

        public static void imprimir(Document reporte)
        {
            var pd = new MigraDoc.Rendering.Printing.MigraDocPrintDocument();
            var rendered = new DocumentRenderer(reporte);
            rendered.PrepareDocument();
            pd.Renderer = rendered;
            var printDialog = new System.Windows.Forms.PrintDialog();
            if (printDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                pd.PrinterSettings = printDialog.PrinterSettings;
                pd.Print();
            }
        }

        public static void escribirArchivoParEstructuras(List<parEstructura> lista, string archivo)
        {
            try
            {
                using (StreamWriter file = new StreamWriter(archivo))
                {
                    foreach (parEstructura par in lista)
                    {
                        file.WriteLine(par.estructuraNombre + "," + par.structureID);
                    }
                }
            }
            catch (Exception exp)
            {
                MessageBox.Show("No se pudo guardar la memoria de estructuras:\n" + exp.Message);
            }
        }

        public static void escribirArchivoPrescripciones(List<prescripcion> lista, string archivo)
        {
            try
            {
                using (StreamWriter file = new StreamWriter(archivo))
                {
                    foreach (prescripcion presc in lista)
                    {
                        file.WriteLine(presc.estructura + "," + presc.dosis.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    }
                }
            }
            catch (Exception exp)
            {
                MessageBox.Show("No se pudo guardar la memoria de prescripciones:\n" + exp.Message);
            }
        }

        public static List<parEstructura> leerArchivoParEstructura(string archivo)
        {
            List<parEstructura> lista = new List<parEstructura>();
            try
            {
                foreach (string linea in File.ReadAllLines(archivo))
                {
                    string[] aux = linea.Split(',');
                    if (aux.Length < 2 || string.IsNullOrEmpty(aux[0]))
                    {
                        continue;
                    }
                    lista.Add(new parEstructura() { estructuraNombre = aux[0], structureID = aux[1] });
                }
            }
            catch (Exception exp)
            {
                MessageBox.Show("No se pudo leer la memoria de estructuras (" + Path.GetFileName(archivo) + "):\n" + exp.Message);
            }
            return lista;
        }

        public static List<prescripcion> leerArchivoPrescripcion(string archivo)
        {
            List<prescripcion> lista = new List<prescripcion>();
            try
            {
                foreach (string linea in File.ReadAllLines(archivo))
                {
                    string[] aux = linea.Split(',');
                    if (aux.Length < 2 || string.IsNullOrEmpty(aux[0]))
                    {
                        continue;
                    }
                    if (double.TryParse(aux[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double dosis))
                    {
                        lista.Add(new prescripcion() { estructura = aux[0], dosis = dosis });
                    }
                }
            }
            catch (Exception exp)
            {
                MessageBox.Show("No se pudo leer la memoria de prescripciones (" + Path.GetFileName(archivo) + "):\n" + exp.Message);
            }
            return lista;
        }

        public static string structureDeEstructura(string estructuraNombreBusca, List<parEstructura> lista)
        {
            return lista.Find(p => p.estructuraNombre == estructuraNombreBusca).structureID;
        }

        public static List<parEstructura> memoriaEstructuras(Patient paciente, PlanningItem plan)
        {
            string ruta = MemoriaPlan.rutaParaLeer(pathParEstructuras, paciente, plan);
            return ruta != null ? leerArchivoParEstructura(ruta) : new List<parEstructura>();
        }

        public static List<prescripcion> memoriaPrescripciones(Patient paciente, PlanningItem plan)
        {
            string ruta = MemoriaPlan.rutaParaLeer(pathPrescripciones, paciente, plan);
            return ruta != null ? leerArchivoPrescripcion(ruta) : new List<prescripcion>();
        }

        public static string nombreArchivoParEstructura(Patient paciente, PlanningItem plan)
        {
            return MemoriaPlan.rutaArchivo(pathParEstructuras, paciente, plan);
        }

        public static string nombreArchivoPrescripciones(Patient paciente, PlanningItem plan)
        {
            return MemoriaPlan.rutaArchivo(pathPrescripciones, paciente, plan);
        }

        public static double prescripcionPredefinida(Estructura estructura, Plantilla plantilla, double prescripcion, Patient paciente, PlanningItem planSeleccionado)
        {
            List<prescripcion> memoria = memoriaPrescripciones(paciente, planSeleccionado);
            if (memoria.Any(p => p.estructura == estructura.nombre))
            {
                return memoria.First(p => p.estructura == estructura.nombre).dosis;
            }
            if (plantilla.nombre.Contains("Cabeza"))
            {
                if (estructura.nombre.Contains("Mid"))
                {
                    return 59.4;
                }
                else if (estructura.nombre.Contains("Low"))
                {
                    return 54.45;
                }
            }
            else if (plantilla.nombre.Contains("Prostata") && estructura.nombre.Contains("Low"))
            {
                return 54;
            }
            else if (plantilla.nombre.Contains("Mama"))
            {
                if (prescripcion == 45 && estructura.nombre.Contains("WB"))
                {
                    return 40.05;
                }
                else if (prescripcion == 40.05 && estructura.nombre.Contains("Sb"))
                {
                    return 45;
                }
                else if (prescripcion == 60 && new[] { "WB", "CW", "IMN", "Ax", "Sclav" }.Any(c => estructura.nombre.Contains(c)))
                {
                    return 50;
                }
                if (prescripcion == 50 && estructura.nombre.Contains("Sb"))
                {
                    return 60;
                }
            }
            return prescripcion;
        }
    }
}
