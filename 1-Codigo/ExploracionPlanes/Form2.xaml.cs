using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;

namespace ExploracionPlanes
{
    public partial class Form2 : DialogoWpf
    {
        Patient paciente;
        Course curso;
        PlanningItem plan;
        PlanningItem planMod = null;
        User usuario;
        Plantilla plantilla;
        Structure ptvCondicion;
        bool hayContext = false;
        VMS.TPS.Common.Model.API.Application app;
        static string pathParEstructuras => Properties.Settings.Default.Path + @"\paresEstructuras\";
        static string pathPrescripciones => Properties.Settings.Default.Path + @"\prescripciones\";
        static string pathDuplicados => Properties.Settings.Default.Path + @"\duplicadosEstructura\";
        public static string pathReportesJson => Properties.Settings.Default.Path + @"\Reportes\Json\";
        string plantillaNotaOriginal = "";

        ObservableCollection<FilaEstructura> filasEstructuras = new ObservableCollection<FilaEstructura>();
        ObservableCollection<FilaPrescripcion> filasPrescripciones = new ObservableCollection<FilaPrescripcion>();
        ObservableCollection<FilaAnalisis> filasAnalisis = new ObservableCollection<FilaAnalisis>();

        public Form2(Plantilla _plantilla, bool _hayContext = false, Patient _pacienteContext = null, PlanningItem _planContext = null, User _usuarioContext = null, PlanningItem _planMod = null)
        {
            InitializeComponent();
            DGV_Estructuras.ItemsSource = filasEstructuras;
            DGV_Prescripciones.ItemsSource = filasPrescripciones;
            DGV_Análisis.ItemsSource = filasAnalisis;

            plantilla = _plantilla;
            Title = plantilla.nombre;
            hayContext = _hayContext;
            if (_hayContext)
            {
                paciente = _pacienteContext;
                plan = _planContext;
                planMod = _planMod;
                usuario = _usuarioContext;
                prepararControlesContext();
                aplicarDuplicadosGuardados();
                llenarDGVEstructuras();
                llenarDGVPrescripciones();
                BT_Analizar.IsEnabled = true;

                L_NombrePaciente.Text = paciente.LastName + ", " + paciente.FirstName;
                L_NombrePaciente.Visibility = Visibility.Visible;
                Title += " - " + paciente.LastName + ", " + paciente.FirstName;
                plantillaNotaOriginal = plantilla.nota;
            }
            else
            {
                try
                {
                    app = VMS.TPS.Common.Model.API.Application.CreateApplication(null, null);
                }
                catch (Exception)
                {
                    MessageBox.Show("No se puede acceder a Eclipse.\n Compruebe que está en una PC con acceso al TPS");
                }
            }
        }

        public bool abrirPaciente(string ID)
        {
            if (paciente != null)
            {
                cerrarPaciente();
            }
            if (app.PatientSummaries.Any(p => p.Id == ID))
            {
                paciente = app.OpenPatientById(ID);
                L_NombrePaciente.Text = paciente.LastName + ", " + paciente.FirstName;
                L_NombrePaciente.Visibility = Visibility.Visible;
                Title += " - " + paciente.LastName + ", " + paciente.FirstName;
                return true;
            }
            else
            {
                MessageBox.Show("El paciente no existe");
                L_NombrePaciente.Visibility = Visibility.Collapsed;
                return false;
            }
        }

        public void cerrarPaciente()
        {
            app.ClosePatient();
        }

        public Course abrirCurso(Patient paciente, string nombreCurso)
        {
            return paciente.Courses.Where(c => c.Id == nombreCurso).FirstOrDefault();
        }

        public PlanningItem abrirPlan(Course curso, string nombrePlan)
        {
            return curso.PlanSetups.Where(p => p.Id == nombrePlan).FirstOrDefault();
        }

        public Course cursoSeleccionado()
        {
            if (LB_Cursos.SelectedItems.Count == 1)
            {
                return (Course)LB_Cursos.SelectedItems[0];
            }
            else
            {
                return curso;
            }
        }

        public PlanningItem planSeleccionado()
        {
            if (hayContext)
            {
                return plan;
            }
            else if (LB_Planes.SelectedItems.Count == 1)
            {
                return (PlanningItem)LB_Planes.SelectedItems[0];
            }
            else
            {
                return plan;
            }
        }

        public string equipo()
        {
            string equipoID = "";
            if (planSeleccionado() is PlanSetup)
            {
                equipoID = ((PlanSetup)planSeleccionado()).Beams.First().TreatmentUnit.Id;
            }
            else if (planSeleccionado() is PlanSum)
            {
                equipoID = ((PlanSum)planSeleccionado()).PlanSetups.First().Beams.First().TreatmentUnit.Id;
            }
            return Equipos.diccionario()[equipoID];
        }

        public List<Course> listaCursos(Patient paciente)
        {
            return paciente.Courses.ToList<Course>();
        }

        public List<PlanningItem> listaPlanes(Course curso)
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

        private void BT_AbrirPaciente_Click(object sender, RoutedEventArgs e)
        {
            // Limpiar ANTES de abrirPaciente(): ese método cierra el paciente anterior (dispose de
            // sus Course), y si la lista todavía los referencia en ese momento, el Clear() de más
            // abajo dispararía SelectionChanged apuntando a un Course ya disposed -> crash.
            LB_Cursos.Items.Clear();
            LB_Planes.Items.Clear();
            if (abrirPaciente(TB_ID.Text))
            {
                foreach (Course curso in listaCursos(paciente))
                {
                    LB_Cursos.Items.Add(curso);
                }
                if (LB_Cursos.Items.Count > 0)
                {
                    LB_Cursos.SelectedIndex = 0;
                }
            }
        }

        private void LB_Cursos_SelectedIndexChanged(object sender, SelectionChangedEventArgs e)
        {
            LB_Planes.Items.Clear();
            Course cursoElegido = LB_Cursos.SelectedItem as Course;
            if (cursoElegido == null)
            {
                return;
            }
            foreach (PlanningItem plan in listaPlanes(cursoElegido))
            {
                LB_Planes.Items.Add(plan);
            }
            if (LB_Planes.Items.Count > 0)
            {
                LB_Planes.SelectedIndex = 0;
            }
        }

        private void llenarDGVEstructuras()
        {
            filasEstructuras.Clear();
            foreach (Estructura estructura in plantilla.estructuras())
            {
                filasEstructuras.Add(new FilaEstructura { NombreSlot = estructura.nombre });
            }
            asociarEstructuras();
            actualizarBotonAnalizar();
        }

        private void llenarDGVPrescripciones()
        {
            filasPrescripciones.Clear();
            double prescripcion = 0;
            if (planSeleccionado() is PlanSetup)
            {
                prescripcion = ((PlanSetup)planSeleccionado()).TotalPrescribedDose.Dose / 100;
            }
            else
            {
                foreach (PlanSetup planS in ((PlanSum)planSeleccionado()).PlanSetups)
                {
                    prescripcion += planS.TotalPrescribedDose.Dose / 100;
                }
            }
            foreach (Estructura estructura in plantilla.estructurasParaPrescribir())
            {
                filasPrescripciones.Add(new FilaPrescripcion
                {
                    Estructura = estructura.nombre,
                    Dosis = prescripcionPredefinida(estructura, plantilla, Math.Round(prescripcion, 2), paciente, planSeleccionado()).ToString()
                });
            }
        }

        private void aplicarPrescripciones()
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

        private void asociarEstructuras()
        {
            List<parEstructura> memoria = memoriaEstructuras(paciente, planSeleccionado());
            List<Structure> estructurasPlan = Estructura.listaEstructuras(planSeleccionado());
            for (int i = 0; i < filasEstructuras.Count; i++)
            {
                var fila = filasEstructuras[i];
                string nombreSlot = fila.NombreSlot;
                List<string> nombresPosibles = plantilla.estructuras()[i].nombresPosibles;
                var candidatos = Estructura.candidatosPorDistancia(nombresPosibles, estructurasPlan);

                // "" va primero (no al final) para que al abrir el combo de una fila sin matchear
                // el desplegable arranque arriba en vez de saltar directo al final de la lista.
                List<string> itemsOrdenados = new List<string> { "" };
                itemsOrdenados.AddRange(candidatos.Select(c => c.Item1.Id));
                fila.Opciones.Clear();
                foreach (string item in itemsOrdenados)
                {
                    fila.Opciones.Add(item);
                }

                Structure estructuraExacta = Estructura.asociarConLista(nombresPosibles, estructurasPlan);
                if (estructuraExacta != null)
                {
                    fila.StructureId = estructuraExacta.Id;
                    continue;
                }
                string idMemoria = structureDeEstructura(nombreSlot, memoria);
                if (!string.IsNullOrEmpty(idMemoria) && itemsOrdenados.Contains(idMemoria))
                {
                    fila.StructureId = idMemoria;
                }
                else if (candidatos.Count > 0 && candidatos[0].Item2 <= Estructura.DistanciaMaximaSugerida)
                {
                    fila.StructureId = candidatos[0].Item1.Id;
                }
                else
                {
                    fila.StructureId = "";
                }
            }
        }

        // Clona todas las restricciones de nombreSlot bajo un nuevo slot "nombreSlot (n)", para poder
        // matchear un mismo tipo de restricción (ej. PTV) con una segunda estructura real del plan.
        private void duplicarEstructura(string nombreSlot)
        {
            List<IRestriccion> originales = plantilla.listaRestricciones.Where(r => r.estructura.nombre == nombreSlot).ToList();
            if (originales.Count == 0)
            {
                return;
            }
            int copia = 2;
            while (plantilla.listaRestricciones.Any(r => r.estructura.nombre == nombreSlot + " (" + copia + ")"))
            {
                copia++;
            }
            Estructura estructuraNueva = Estructura.crear(nombreSlot + " (" + copia + ")", new List<string>(originales[0].estructura.nombresPosibles));
            int indiceInsercion = plantilla.listaRestricciones.IndexOf(originales.Last()) + 1;
            foreach (IRestriccion original in originales)
            {
                IRestriccion clon = original.crear(estructuraNueva, original.unidadValor, original.unidadCorrespondiente, original.esMenorQue,
                    original.valorEsperado, original.valorTolerado, original.valorCorrespondiente, original.nota, original.condicion, original.prioridad, original.planMod);
                plantilla.listaRestricciones.Insert(indiceInsercion, clon);
                indiceInsercion++;
            }
        }

        private void BT_DuplicarEstructura_Click(object sender, RoutedEventArgs e)
        {
            if (!(DGV_Estructuras.SelectedItem is FilaEstructura filaActual))
            {
                MessageBox.Show("Seleccione primero la fila de la estructura a duplicar.");
                return;
            }
            duplicarEstructura(filaActual.NombreSlot);
            llenarDGVEstructuras();
            llenarDGVPrescripciones();
        }

        private static bool esSlotDuplicado(string nombreSlot)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(nombreSlot, @" \(\d+\)$");
        }

        private void eliminarDuplicado(string nombreSlot)
        {
            foreach (IRestriccion restriccion in plantilla.listaRestricciones.Where(r => r.estructura.nombre == nombreSlot).ToList())
            {
                plantilla.listaRestricciones.Remove(restriccion);
            }
        }

        private void BT_EliminarDuplicado_Click(object sender, RoutedEventArgs e)
        {
            if (!(DGV_Estructuras.SelectedItem is FilaEstructura filaActual) || !esSlotDuplicado(filaActual.NombreSlot))
            {
                MessageBox.Show("Seleccione primero la fila de la estructura duplicada a eliminar.");
                return;
            }
            eliminarDuplicado(filaActual.NombreSlot);
            llenarDGVEstructuras();
            llenarDGVPrescripciones();
            guardarDuplicados();
        }

        // Guarda en memoria (por plan) cuántas copias tiene cada slot duplicado, derivándolo de los
        // nombres de estructura actuales (sufijo " (n)"), para volver a aplicarlos al reabrir el plan.
        private void guardarDuplicados()
        {
            var duplicados = plantilla.listaRestricciones
                .Select(r => System.Text.RegularExpressions.Regex.Match(r.estructura.nombre, @"^(.*) \((\d+)\)$"))
                .Where(m => m.Success)
                .GroupBy(m => m.Groups[1].Value)
                .Select(g => new { Base = g.Key, Max = g.Max(m => int.Parse(m.Groups[2].Value)) });
            string ruta = nombreArchivoDuplicados(paciente, planSeleccionado());
            try
            {
                using (StreamWriter file = new StreamWriter(ruta))
                {
                    foreach (var d in duplicados)
                    {
                        file.WriteLine(d.Base + "," + d.Max);
                    }
                }
            }
            catch (Exception exp)
            {
                MessageBox.Show("No se pudo guardar la memoria de estructuras duplicadas:\n" + exp.Message);
            }
        }

        private void aplicarDuplicadosGuardados()
        {
            string ruta = MemoriaPlan.rutaParaLeer(pathDuplicados, paciente, planSeleccionado());
            if (ruta == null)
            {
                return;
            }
            try
            {
                foreach (string linea in File.ReadAllLines(ruta))
                {
                    string[] aux = linea.Split(',');
                    if (aux.Length < 2 || !int.TryParse(aux[1], out int cantidad))
                    {
                        continue;
                    }
                    for (int copia = 2; copia <= cantidad; copia++)
                    {
                        if (!plantilla.listaRestricciones.Any(r => r.estructura.nombre == aux[0] + " (" + copia + ")"))
                        {
                            duplicarEstructura(aux[0]);
                        }
                    }
                }
            }
            catch (Exception exp)
            {
                MessageBox.Show("No se pudo leer la memoria de estructuras duplicadas:\n" + exp.Message);
            }
        }

        public static string nombreArchivoDuplicados(Patient paciente, PlanningItem plan)
        {
            return MemoriaPlan.rutaArchivo(pathDuplicados, paciente, plan);
        }

        private void CHB_OcultarNoAnalizadas_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (filasAnalisis.Count > 0)
            {
                llenarDGVAnalisis();
            }
        }

        private bool estructurasSinAsociar()
        {
            return filasEstructuras.Any(f => string.IsNullOrEmpty(f.StructureId));
        }

        private void llenarDGVAnalisis()
        {
            plantilla.nota = plantillaNotaOriginal;
            if (plan == null)
            {
                plan = planSeleccionado();
            }
            if (plan is PlanSetup && ((PlanSetup)plan).Dose == null)
            {
                MessageBox.Show("El plan no está calculado");
                return;
            }
            else if (plan is PlanSum && ((PlanSum)plan).Dose == null)
            {
                MessageBox.Show("El plan no está calculado");
                return;
            }
            string notaEQD2 = "Se analizaron evaluando EQD2: ";
            List<string> estructurasConEQD2 = new List<string>();
            filasAnalisis.Clear();

            if (plantilla.tieneCondicionesTipo1())
            {
                SeleccionarPTV seleccionarPTV = new SeleccionarPTV(Estructura.ptvs(planSeleccionado()));
                seleccionarPTV.ShowDialog();
                ptvCondicion = seleccionarPTV.ptv;
                MessageBox.Show("PTV volumen: " + Math.Round(ptvCondicion.Volume, 1).ToString() + " [cm3]\nNumero de fracciones " + ((PlanSetup)planSeleccionado()).UniqueFractionation.NumberOfFractions.ToString());
                Title += " volPTV: " + Math.Round(ptvCondicion.Volume, 1).ToString() + "cm3 " + ((PlanSetup)planSeleccionado()).UniqueFractionation.NumberOfFractions.ToString() + " fx";
            }
            Col_Prioridad.Visibility = plantilla.tienePrioridades() ? Visibility.Visible : Visibility.Collapsed;

            foreach (IRestriccion restriccion in plantilla.listaRestricciones)
            {
                PlanningItem planRestriccion = (!string.IsNullOrEmpty(restriccion.planMod) && planMod != null) ? planMod : plan;

                if (restriccion.condicion == null || restriccion.condicion.CumpleCondicion(planSeleccionado(), ptvCondicion))
                {
                    Structure estructura = estructuraCorrespondiente(restriccion.estructura.nombre);
                    var fila = new FilaAnalisis { Restriccion = restriccion };
                    if (estructura == null && CHB_OcultarNoAnalizadas.IsChecked == true)
                    {
                        fila.Oculta = true;
                    }
                    fila.Estructura = Estructura.nombreEnDiccionario(restriccion.estructura);
                    fila.Metrica = restriccion.metrica();
                    if (restriccion.condicion != null && restriccion.condicion.tipo == Tipo.CondicionadaPor)
                    {
                        fila.Estructura = "(" + Estructura.nombreEnDiccionario(restriccion.estructura) + ")";
                        fila.Metrica = "(" + restriccion.metrica() + ")";
                    }
                    string menorOmayor = restriccion.esMenorQue ? "<" : ">";
                    string valorEsperadoString;
                    if (double.IsNaN(restriccion.valorEsperado))
                    {
                        valorEsperadoString = "Reportar";
                    }
                    else
                    {
                        valorEsperadoString = menorOmayor + restriccion.valorEsperado + restriccion.unidadValor;
                    }
                    if (!double.IsNaN(restriccion.valorTolerado))
                    {
                        valorEsperadoString += " (" + restriccion.valorTolerado + restriccion.unidadValor + ")";
                    }
                    fila.Esperado = valorEsperadoString;
                    fila.Referencia = restriccion.nota;
                    if (estructura != null)
                    {
                        if (!string.IsNullOrEmpty(restriccion.planMod) && planMod != null)
                        {
                            fila.Referencia += " *";
                        }
                        fila.Volumen = Math.Round(estructura.Volume, 2).ToString();
                        if (CHB_EvaluarConEQD2.IsChecked == true)
                        {
                            double alfaBeta = 3;
                            foreach (var filaEst in filasEstructuras)
                            {
                                if (filaEst.StructureId == estructura.Id)
                                {
                                    alfaBeta = Metodos.validarYConvertirADouble(filaEst.AlfaBeta);
                                    break;
                                }
                            }
                            int numeroFracciones = (int)((PlanSetup)planSeleccionado()).UniqueFractionation.NumberOfFractions;
                            restriccion.analizarPlanEstructura(planRestriccion, estructura, alfaBeta, numeroFracciones);
                            if (!estructurasConEQD2.Contains(estructura.Id))
                            {
                                estructurasConEQD2.Add(estructura.Id);
                                notaEQD2 += "\r\n" + estructura.Id + " α/β=" + alfaBeta.ToString();
                            }
                        }
                        else
                        {
                            restriccion.analizarPlanEstructura(planRestriccion, estructura);
                        }

                        if (restriccion.chequearSamplingCoverage(planRestriccion, estructura))
                        {
                            MessageBox.Show("La estructura " + estructura.Id + " no tiene el suficiente Sampling Coverage.\nNo se puede realizar el análisis");
                        }
                        else
                        {
                            fila.EnPlan = restriccion.valorMedido + restriccion.unidadValor;
                            if (restriccion.condicion != null && restriccion.condicion.tipo == Tipo.CondicionadaPor)
                            {
                                IRestriccion restriccionCondicionante = plantilla.listaRestricciones.Where(r => r.etiqueta == restriccion.condicion.EtiquetaRestriccionAnidada).First();
                                var filaCondicionante = filasAnalisis.FirstOrDefault(f => f.Restriccion == restriccionCondicionante);
                                ColorearAnalisis.fondoAnidadasWpf(restriccionCondicionante, restriccion, out var fondoCondicionante, out var fondoCondicionada);
                                if (filaCondicionante != null)
                                {
                                    filaCondicionante.FondoEnPlan = fondoCondicionante;
                                }
                                fila.FondoEnPlan = fondoCondicionada;
                            }
                            else
                            {
                                fila.FondoEnPlan = ColorearAnalisis.fondoWpf(restriccion);
                            }
                        }
                        if (!string.IsNullOrEmpty(restriccion.prioridad))
                        {
                            fila.Prioridad = restriccion.prioridad;
                        }
                        if (restriccion.GetType() == typeof(RestriccionDosisMax))
                        {
                            fila.EsDmax = true;
                            fila.VolumenDmaxTexto = RestriccionDosisMax.volumenDosisMaxima.ToString();
                        }
                    }
                    filasAnalisis.Add(fila);
                }
            }
            if (CHB_EvaluarConEQD2.IsChecked == true)
            {
                plantilla.nota += "\r\n" + notaEQD2;
            }
            if (plantilla.TieneRestriccionEnPlanMod())
            {
                L_Advertencia.Visibility = Visibility.Visible;
                if (planMod != null)
                {
                    L_Advertencia.Text = "* Restricciones evaluadas en " + planMod.Id;
                    plantilla.nota += "\r\n* Restricciones evaluadas en " + planMod.Id;
                }
                else
                {
                    L_Advertencia.Text = "* Restricciones evaluadas en " + plan.Id;
                }
            }
            else
            {
                L_Advertencia.Visibility = Visibility.Collapsed;
            }
            BT_GuardarReporte.IsEnabled = filasAnalisis.Count > 0;
            BT_Imprimir.IsEnabled = filasAnalisis.Count > 0;
        }

        private Structure estructuraCorrespondiente(string nombreEstructura)
        {
            foreach (var fila in filasEstructuras)
            {
                if (fila.NombreSlot.Equals(nombreEstructura))
                {
                    return Estructura.listaEstructuras(planSeleccionado()).Where(s => s.Id.Equals(fila.StructureId)).FirstOrDefault();
                }
            }
            return null;
        }

        private string infoPlan()
        {
            return planSeleccionado().Id;
        }

        private void BT_Analizar_Click(object sender, RoutedEventArgs e)
        {
            aplicarPrescripciones();
            llenarDGVAnalisis();
            escribirArchivoParEstructuras(listaParesEstructuras(), nombreArchivoParEstructura(paciente, planSeleccionado()));
            escribirArchivoPrescripciones(listaPrescripcion(), nombreArchivoPrescripciones(paciente, planSeleccionado()));
            guardarDuplicados();
            if (plantilla.nombre.Contains("SunRise"))
            {
                Col_Estructura.Header = "Structure";
                Col_Prioridad.Header = "Priority";
                Col_Metrica.Header = "Metric";
                Col_EnPlan.Header = "In plan";
                Col_Esperado.Header = "Expected";
            }
        }

        private void BT_SeleccionarPlan_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var plantilla = Plantilla.SeleccionarAutomaticamentePlantilla(planSeleccionado(), paciente);
                aplicarDuplicadosGuardados();
                llenarDGVEstructuras();
                planSeleccionado();
                llenarDGVPrescripciones();
            }
            catch (Exception exp)
            {
                File.WriteAllText("log.txt", exp.ToString());
            }
        }

        private void Form2_Closing(object sender, CancelEventArgs e)
        {
            if (hayContext)
            {
            }
            else if (paciente != null)
            {
                LB_Cursos.Items.Clear();
                LB_Planes.Items.Clear();
                cerrarPaciente();
            }
            if (app != null)
            {
                app.Dispose();
            }
        }

        private void TB_ID_TextChanged(object sender, TextChangedEventArgs e)
        {
            BT_AbrirPaciente.IsEnabled = !string.IsNullOrEmpty(TB_ID.Text);
        }

        private void LB_Planes_SelectedIndexChanged(object sender, SelectionChangedEventArgs e)
        {
            BT_SeleccionarPlan.IsEnabled = LB_Planes.SelectedItems.Count == 1;
            actualizarBotonAnalizar();
        }

        private void actualizarBotonAnalizar()
        {
            BT_Analizar.IsEnabled = LB_Planes.SelectedItems.Count == 1 && filasEstructuras.Count > 0;
        }

        private void prepararControlesContext()
        {
            label4.IsEnabled = false;
            TB_ID.IsEnabled = false;
            BT_AbrirPaciente.IsEnabled = false;
            label2.IsEnabled = false;
            LB_Cursos.IsEnabled = false;
            Label3.IsEnabled = false;
            LB_Planes.IsEnabled = false;
            BT_SeleccionarPlan.IsEnabled = false;
        }

        private void BT_VolumenDmax_Click(object sender, RoutedEventArgs e)
        {
            var fila = (FilaAnalisis)((Button)sender).DataContext;
            var restriccion = (RestriccionDosisMax)fila.Restriccion;
            FormTB formTb = new FormTB(fila.VolumenDmaxTexto, true);
            formTb.Title = "Volumen dosis maxima";
            formTb.L_Texto.Text = "Definir el tamaño del elemento de volumen para el \ncálculo de la dosis máxima [cm3]";
            formTb.ShowDialog();

            if (formTb.DialogResult == true)
            {
                Structure estructura = estructuraCorrespondiente(restriccion.estructura.nombre);
                restriccion.analizarPlanEstructura(planSeleccionado(), estructura, Metodos.validarYConvertirADouble(formTb.salida));
                fila.Metrica = restriccion.valorMedido + restriccion.unidadValor;
                fila.FondoMetrica = ColorearAnalisis.fondoWpf(restriccion);
                fila.VolumenDmaxTexto = formTb.salida;
            }
        }

        private List<parEstructura> listaParesEstructuras()
        {
            List<parEstructura> lista = new List<parEstructura>();
            foreach (var fila in filasEstructuras)
            {
                lista.Add(new parEstructura() { estructuraNombre = fila.NombreSlot, structureID = fila.StructureId });
            }
            return lista;
        }

        private List<prescripcion> listaPrescripcion()
        {
            List<prescripcion> lista = new List<prescripcion>();
            foreach (var fila in filasPrescripciones)
            {
                var presc = new prescripcion() { estructura = fila.Estructura };
                if (!string.IsNullOrEmpty(fila.Dosis))
                {
                    presc.dosis = Metodos.validarYConvertirADouble(fila.Dosis);
                }
                lista.Add(presc);
            }
            return lista;
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

        private static List<parEstructura> memoriaEstructuras(Patient paciente, PlanningItem plan)
        {
            string ruta = MemoriaPlan.rutaParaLeer(pathParEstructuras, paciente, plan);
            return ruta != null ? leerArchivoParEstructura(ruta) : new List<parEstructura>();
        }

        private static List<prescripcion> memoriaPrescripciones(Patient paciente, PlanningItem plan)
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

        #region Imprimir

        private List<ColumnaReporte> columnasReporte()
        {
            var esSunRise = plantilla.nombre.Contains("SunRise");
            return new List<ColumnaReporte>
            {
                new ColumnaReporte { Encabezado = esSunRise ? "Structure" : "Estructura", Ancho = 60 },
                new ColumnaReporte { Encabezado = "Priority", Ancho = 50 },
                new ColumnaReporte { Encabezado = esSunRise ? "Metric" : "Métrica", Ancho = 60 },
                new ColumnaReporte { Encabezado = "Vol [cm3]", Ancho = 60 },
                new ColumnaReporte { Encabezado = esSunRise ? "In plan" : "En Plan", Ancho = 70 },
                new ColumnaReporte { Encabezado = esSunRise ? "Expected" : "Esperado", Ancho = 70 },
                new ColumnaReporte { Encabezado = "Ref.", Ancho = 60 },
            };
        }

        private static System.Drawing.Color colorDrawing(System.Windows.Media.Brush brush)
        {
            if (brush is System.Windows.Media.SolidColorBrush solido && solido.Color.A != 0)
            {
                var c = solido.Color;
                return System.Drawing.Color.FromArgb(255, c.R, c.G, c.B);
            }
            return System.Drawing.Color.White;
        }

        private TablaReporte tablaReporte()
        {
            var tabla = new TablaReporte { Columnas = columnasReporte() };
            foreach (var fila in filasAnalisis.Where(f => !f.Oculta))
            {
                var filaReporte = new FilaReporte();
                filaReporte.Valores.AddRange(new[] { fila.Estructura, fila.Prioridad, fila.Metrica, fila.Volumen, fila.EnPlan, fila.Esperado, fila.Referencia });
                filaReporte.Fondos.AddRange(new[] { colorDrawing(null), colorDrawing(null), colorDrawing(fila.FondoMetrica), colorDrawing(null), colorDrawing(fila.FondoEnPlan), colorDrawing(null), colorDrawing(null) });
                tabla.Filas.Add(filaReporte);
            }
            return tabla;
        }

        private Document reporte()
        {
            string usuarioNombre = hayContext ? usuario.Name : app.CurrentUser.Name;
            double prescripcion = 0;
            if (planSeleccionado() is PlanSetup)
            {
                prescripcion = ((PlanSetup)planSeleccionado()).TotalPrescribedDose.Dose / 100;
            }
            else if (planSeleccionado() is PlanSum)
            {
                foreach (PlanSetup plan in ((PlanSum)planSeleccionado()).PlanSetups)
                {
                    prescripcion += plan.TotalPrescribedDose.Dose / 100;
                }
            }
            return Reporte.crearReporte(paciente.LastName, paciente.FirstName, paciente.Id, equipo(), plantilla.nombre, plantilla.nota, usuarioNombre, Convert.ToString(infoPlan()), Convert.ToString(prescripcion), tablaReporte());
        }

        private void BT_GuardarReporte_Click(object sender, RoutedEventArgs e)
        {
            Reporte.exportarAPdf(paciente.LastName, paciente.FirstName, paciente.Id, planSeleccionado().Id, plantilla.nombre, reporte());
            guardarPlantillaComoJson();
        }

        private void guardarPlantillaComoJson()
        {
            plantilla.IDpaciente = paciente.Id;
            plantilla.plan = planSeleccionado().Id;
            string pacienteS = "";
            string planS = "";
            if (paciente.LastName != "" || paciente.FirstName != "")
            {
                pacienteS = paciente.Id + "_" + paciente.LastName + ", " + paciente.FirstName + "_";
            }
            if (planSeleccionado().Id != "")
            {
                planS = planSeleccionado().Id + "_";
            }

            string nombre = pacienteS + planS + plantilla.nombre;
            if (!Directory.Exists(pathReportesJson))
            {
                Directory.CreateDirectory(pathReportesJson);
            }
            string path = IO.GetUniqueFilename(pathReportesJson, nombre, "txt");
            IO.writeObjectAsJson(path, plantilla);
        }

        private void BT_Imprimir_Click(object sender, RoutedEventArgs e)
        {
            var pd = new MigraDoc.Rendering.Printing.MigraDocPrintDocument();
            var rendered = new DocumentRenderer(reporte());
            rendered.PrepareDocument();
            pd.Renderer = rendered;
            var printDialog = new System.Windows.Forms.PrintDialog();
            if (printDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                pd.PrinterSettings = printDialog.PrinterSettings;
                pd.Print();
            }
        }

        #endregion

        public void CHB_EvaluarConEQD2_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (CHB_EvaluarConEQD2.IsChecked == true)
            {
                if (planSeleccionado() is PlanSum)
                {
                    MessageBox.Show("No funciona para planes suma");
                    CHB_EvaluarConEQD2.IsChecked = false;
                }
                else if (((PlanSetup)planSeleccionado()).UniqueFractionation.DosePerFractionInPrimaryRefPoint.Dose == 200)
                {
                    MessageBox.Show("La dosis día es de 200cGy");
                    CHB_EvaluarConEQD2.IsChecked = false;
                }
                else
                {
                    Col_AlfaBeta.Visibility = Visibility.Visible;
                    cargarAlfaBetaDGVEstructuras();
                }
            }
            else
            {
                Col_AlfaBeta.Visibility = Visibility.Collapsed;
            }
        }

        public void cargarAlfaBetaDGVEstructuras()
        {
            foreach (var fila in filasEstructuras)
            {
                fila.AlfaBeta = Estructura.AlfaBeta(fila.NombreSlot).ToString();
            }
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
