using System;
using System.IO;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace ExploracionPlanes
{
    public class Mineria
    {
        public struct Caso
        {
            public string pacienteID;
            public Patient paciente;
            public string planID;
            public PlanSetup plan;
            public string nombrePlantilla;
            public Plantilla plantilla;
        }
        public static List<string> leerArchivos()
        {
            return Directory.GetFiles(@"C:\Users\Varian\Desktop\rep pros").ToList();
        }
        public static Caso leerCaso(string nombreArchivo)
        {
            string[] aux = (Path.GetFileName(nombreArchivo)).Split('_');
            Caso caso = new Caso()
            {
                pacienteID = aux[0],
                planID = aux[2],
                nombrePlantilla = aux[3],
            };
            return caso;
        }

        public static List<Caso> buscarPlantilla(Plantilla plantilla)
        {
            List<Caso> casos = new List<Caso>();
            List<string> archivos = leerArchivos();
            foreach (string archivo in archivos)
            {
                string[] aux = archivo.Split('_');
                if (aux[3].Equals(plantilla.nombre))
                {
                    Caso caso = leerCaso(archivo);
                    caso.plantilla = plantilla;
                    casos.Add(caso);
                }
            }
            return casos;
        }

        public static void aplicarPlantilla(Caso caso)
        {
            foreach (IRestriccion restriccion in caso.plantilla.listaRestricciones)
            {
                Structure estructuraEc = Estructura.asociarConLista(restriccion.estructura.nombresPosibles, Estructura.listaEstructuras(caso.plan));
                if (restriccion.dosisEstaEnPorcentaje())
                {
                    restriccion.prescripcionEstructura = caso.plan.TotalPrescribedDose.Dose;
                }
                if (estructuraEc != null)
                {
                    restriccion.analizarPlanEstructura(caso.plan, estructuraEc);
                }

            }
        }

        public static void extraerDePaciente(Caso caso)
        {

        }

        public static List<Plantilla> listaPlantillas(string nombrePlantilla, bool soloPlanesAprobados)
        {
            List<string> archivos = Directory.GetFiles(Form2.pathReportesJson).Where(f => f.Contains(nombrePlantilla)).ToList();
            List<Plantilla> plantillas = new List<Plantilla>();
            List<Plantilla> plantillasFiltradas = new List<Plantilla>();
            foreach (string archivo in archivos)
            {
                plantillas.Add(IO.readJson<Plantilla>(archivo));
            }
            if (plantillas.Count == 0)
            {
                MessageBox.Show("No se encontraron plantillas con el nombre: " + nombrePlantilla);
                return plantillasFiltradas;
            }
            plantillasFiltradas.Add(plantillas[0]);
            foreach (Plantilla plantilla in plantillas.Skip(1))
            {
                if (plantilla.sonMismaPlantilla(plantillas[0]))
                {
                    plantillasFiltradas.Add(plantilla);
                }
            }
            if (plantillasFiltradas.Count < plantillas.Count)
            {
                MessageBox.Show("Se encontraron " + plantillas.Count.ToString() + " plantillas, pero no resultaron todas iguales.\nSe preservaron las " + plantillasFiltradas.Count.ToString() + " iguales a la primera de ellas.");
            }
            if (soloPlanesAprobados)
            {
                List<Plantilla> plantillasFiltradasAprobados = new List<Plantilla>();
                using (VMS.TPS.Common.Model.API.Application app = VMS.TPS.Common.Model.API.Application.CreateApplication(null, null))
                {
                    foreach (Plantilla plantilla in plantillasFiltradas)
                    {
                        Patient paciente = app.OpenPatientById(plantilla.IDpaciente);
                        foreach (Course curso in paciente.Courses.Where(c => !c.Id.Contains("QA")))
                        {
                            if (curso.PlanSetups.Any(p => p.Id == plantilla.plan && (p.ApprovalStatus == PlanSetupApprovalStatus.PlanningApproved || p.ApprovalStatus == PlanSetupApprovalStatus.TreatmentApproved)))
                            {
                                plantillasFiltradasAprobados.Add(plantilla);
                                break;
                            }
                        }
                        app.ClosePatient();
                    }
                }

                MessageBox.Show("Se encontraron " + plantillasFiltradasAprobados.Count.ToString() + " plantillas de planes aprobados");
                return plantillasFiltradasAprobados;
            }
            return plantillasFiltradas;
        }
        public static void escribirArchivo(List<Plantilla> plantillas)
        {
            List<string> output = new List<string>();
            string header = "ID;Plan;";
            string esperados = "Esperado;;";
            string tolerados = "Tolerado;;";
            foreach (IRestriccion restriccion in plantillas[0].listaRestricciones)
            {
                header += restriccion.etiquetaInicio;
                if (restriccion.esMenorQue)
                {
                    header += "<;";
                }
                else
                {
                    header += ">;";
                }
                esperados += restriccion.valorEsperado + ";";
                tolerados += restriccion.valorTolerado + ";";

            }
            output.Add(header);
            output.Add(esperados);
            output.Add(tolerados);

            foreach (Plantilla plantilla in plantillas)
            {
                string linea = plantilla.IDpaciente + ";" + plantilla.plan + ";";
                foreach (IRestriccion restriccion in plantilla.listaRestricciones)
                {
                    linea += restriccion.valorMedido + ";";
                }
                output.Add(linea);
            }
            string path = Form2.pathReportesJson + @"Analisis\" + plantillas[0].nombre + "_" + DateTime.Today.Date.ToString("dd-MM-yyyy") + ".txt";
            File.WriteAllLines(path, output);
            MessageBox.Show("Se analizaron " + plantillas.Count.ToString() + " plantillas\nSe escribieron los resultados en el archivo " + path);
        }
    }
}
