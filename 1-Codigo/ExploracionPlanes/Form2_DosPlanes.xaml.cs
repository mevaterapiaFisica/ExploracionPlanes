using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using MigraDoc.DocumentObjectModel;

namespace ExploracionPlanes
{
    public partial class Form2_DosPlanes : DialogoWpf
    {
        Patient paciente;
        PlanningItem plan;
        PlanningItem planMod;
        PlanningItem plan2;
        PlanningItem plan2Mod;
        User usuario;
        Plantilla plantilla;
        bool hayContext = false;
        string tituloBase;
        VMS.TPS.Common.Model.API.Application app;

        ObservableCollection<FilaEstructura> filasEstructuras = new ObservableCollection<FilaEstructura>();
        ObservableCollection<FilaPrescripcion> filasPrescripciones = new ObservableCollection<FilaPrescripcion>();
        ObservableCollection<FilaAnalisis> filasAnalisis = new ObservableCollection<FilaAnalisis>();

        public Form2_DosPlanes(Plantilla _plantilla, bool _hayContext = false, Patient _pacienteContext = null, PlanningItem _planContext = null, User _usuarioContext = null, PlanningItem _segundoPlan = null, PlanningItem _planMod = null, PlanningItem _segundoPlanMod = null)
        {
            InitializeComponent();
            DGV_Estructuras.ItemsSource = filasEstructuras;
            DGV_Prescripciones.ItemsSource = filasPrescripciones;
            DGV_Analisis.ItemsSource = filasAnalisis;

            plantilla = _plantilla;
            Title = plantilla.nombre;
            hayContext = _hayContext;
            if (_hayContext)
            {
                paciente = _pacienteContext;
                plan = _planContext;
                plan2 = _segundoPlan;
                planMod = _planMod;
                plan2Mod = _segundoPlanMod;
                usuario = _usuarioContext;
                prepararControlesContext();
                llenarDGVEstructuras();
                llenarDGVPrescripciones();
                BT_Analizar.IsEnabled = true;

                L_NombrePaciente.Text = paciente.LastName + ", " + paciente.FirstName;
                L_NombrePaciente.Visibility = Visibility.Visible;
                Title += " - " + paciente.LastName + ", " + paciente.FirstName;
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
            tituloBase = Title;
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

        public PlanningItem planSeleccionado()
        {
            if (hayContext)
            {
                return plan;
            }
            else if (LB_Planes.SelectedItems.Count == 2)
            {
                plan = (PlanningItem)LB_Planes.SelectedItems[0];
                plan2 = (PlanningItem)LB_Planes.SelectedItems[1];
                return plan;
            }
            else
            {
                return plan;
            }
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
                foreach (Course curso in Form2Compartido.listaCursos(paciente))
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
            foreach (PlanningItem plan in Form2Compartido.listaPlanes(cursoElegido))
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
            List<Structure> estructurasPlan = Estructura.listaEstructuras(planSeleccionado());
            foreach (Estructura estructura in plantilla.estructuras())
            {
                var fila = new FilaEstructura { NombreSlot = estructura.nombre };
                // Antes el combo listaba TODAS las estructuras del plan sin ordenar - con plantillas
                // largas obligaba a buscar a mano. Se ordena por parecido (mismo criterio que Form2),
                // "" primero para que el combo de una fila sin matchear arranque arriba.
                var candidatos = Estructura.candidatosPorDistancia(estructura.nombresPosibles, estructurasPlan);
                fila.Opciones.Add("");
                foreach (var candidato in candidatos)
                {
                    fila.Opciones.Add(candidato.Item1.Id);
                }
                filasEstructuras.Add(fila);
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
                    Dosis = Form2Compartido.prescripcionPredefinida(estructura, plantilla, Math.Round(prescripcion, 2), paciente, planSeleccionado()).ToString()
                });
            }
        }

        // El binding TwoWay de SelectedItem (ComboBox dentro de un DataGridTemplateColumn.CellTemplate
        // sin CellEditingTemplate) no empuja el valor elegido de vuelta a StructureId - queda solo
        // seleccionado visualmente. Se asigna a mano acá para que el match manual quede en el modelo.
        private void ComboBoxStructureId_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb && cb.DataContext is FilaEstructura fila)
            {
                fila.StructureId = cb.SelectedItem as string ?? "";
            }
        }

        private void asociarEstructuras()
        {
            bool existeArchivoPar = File.Exists(Form2Compartido.nombreArchivoParEstructura(paciente, planSeleccionado()));
            List<parEstructura> lista = existeArchivoPar
                ? Form2Compartido.leerArchivoParEstructura(Form2Compartido.nombreArchivoParEstructura(paciente, planSeleccionado()))
                : new List<parEstructura>();
            List<Structure> estructurasPlan = Estructura.listaEstructuras(planSeleccionado());
            for (int i = 0; i < filasEstructuras.Count; i++)
            {
                var fila = filasEstructuras[i];
                List<string> nombresPosibles = plantilla.estructuras()[i].nombresPosibles;
                string idExacto = Estructura.asociarConLista(nombresPosibles, estructurasPlan)?.Id;
                string idMemoria = existeArchivoPar ? Form2Compartido.structureDeEstructura(fila.NombreSlot, lista) : "";
                var candidatos = Estructura.candidatosPorDistancia(nombresPosibles, estructurasPlan);
                var mejorCandidato = candidatos.Count > 0 ? Tuple.Create(candidatos[0].Item1.Id, candidatos[0].Item2) : null;
                fila.StructureId = MatchingEstructuras.ElegirStructureId(idExacto, idMemoria, fila.Opciones, mejorCandidato);
            }
        }

        private void llenarDGVAnalisis()
        {
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
            if (plan2 is PlanSetup && ((PlanSetup)plan2).Dose == null)
            {
                MessageBox.Show("El segundo plan no está calculado");
                return;
            }
            else if (plan2 is PlanSum && ((PlanSum)plan2).Dose == null)
            {
                MessageBox.Show("El segundo plan no está calculado");
                return;
            }
            filasAnalisis.Clear();
            Col_EnPlan1.Header = planSeleccionado().Id;
            Col_EnPlan2.Header = plan2.Id;
            if (StructureSetUID(plan) != StructureSetUID(plan2))
            {
                MessageBox.Show("Los planes están calculados sobre diferentes Set de estructuras\nSe obtendrá información respetando el nombre de las estructuras");
            }
            Col_Prioridad.Visibility = plantilla.tienePrioridades() ? Visibility.Visible : Visibility.Collapsed;
            List<Structure> ptvsMatcheados = ptvsMatcheadosEnGrilla();
            if (plantilla.tieneCondicionesTipo1())
            {
                string extra = "";
                if (planSeleccionado() is PlanSetup planSetupTitulo)
                {
                    extra += " + " + (int)planSetupTitulo.UniqueFractionation.NumberOfFractions + " fx";
                }
                if (ptvsMatcheados.Count > 0)
                {
                    extra += " + PTV " + Math.Round(ptvsMatcheados.Sum(s => s.Volume), 1) + " cm3";
                    if (ptvsMatcheados.Count > 1)
                    {
                        extra += " (" + ptvsMatcheados.Count + " PTVs)";
                    }
                }
                Title = tituloBase + extra;
            }
            else
            {
                Title = tituloBase;
            }
            string notaEQD2 = "Se analizaron evaluando EQD2: ";
            List<string> estructurasConEQD2 = new List<string>();
            foreach (IRestriccion restriccion in plantilla.listaRestricciones)
            {
                if (restriccion.condicion != null)
                {
                    Structure estructuraCondicion = estructuraCorrespondiente(restriccion.estructura.nombre, plan);
                    if (!restriccion.condicion.CumpleCondicion(plan, Form2Compartido.volPTVParaCondicion(estructuraCondicion, ptvsMatcheados)))
                    {
                        continue;
                    }
                }
                try
                {
                    analizarRestriccion(restriccion, estructurasConEQD2, ref notaEQD2);
                }
                catch (Exception ex)
                {
                    logError($"llenarDGVAnalisis - restriccion '{restriccion.etiqueta}' paciente {paciente?.Id} plan {plan?.Id} vs {plan2?.Id}", ex);
                    var filaError = filasAnalisis.FirstOrDefault(f => f.Restriccion == restriccion);
                    if (filaError != null)
                    {
                        filaError.EnPlan = "ERROR";
                        filaError.FondoEnPlan = System.Windows.Media.Brushes.Red;
                    }
                    MessageBox.Show("Error al analizar la restricción \"" + restriccion.etiqueta + "\":\n" + ex.Message + "\n\nSe registró el detalle en log.txt");
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
                    L_Advertencia.Text = "⚠ Restricciones evaluadas en " + planMod.Id;
                    plantilla.nota += "\r\n* Restricciones evaluadas en " + planMod.Id;
                }
                else
                {
                    L_Advertencia.Text = "⚠ Restricciones evaluadas en " + plan.Id;
                }

                L_Advertencia2.Visibility = Visibility.Visible;
                if (plan2Mod != null)
                {
                    L_Advertencia2.Text = "⚠ Restricciones evaluadas en " + plan2Mod.Id;
                    plantilla.nota += "\r\n* Restricciones evaluadas en " + plan2Mod.Id;
                }
                else
                {
                    L_Advertencia2.Text = "⚠ Restricciones evaluadas en " + plan2.Id;
                }
            }
            else
            {
                L_Advertencia.Visibility = Visibility.Collapsed;
            }
            BT_GuardarReporte.IsEnabled = filasAnalisis.Count > 0;
            BT_Imprimir.IsEnabled = filasAnalisis.Count > 0;
        }

        private void analizarRestriccion(IRestriccion restriccion, List<string> estructurasConEQD2, ref string notaEQD2)
        {
            PlanningItem planRestriccion = (!string.IsNullOrEmpty(restriccion.planMod) && planMod != null) ? planMod : plan;
            Structure estructura = estructuraCorrespondiente(restriccion.estructura.nombre, plan);

            var fila = new FilaAnalisis { Restriccion = restriccion };
            if (estructura == null && CHB_OcultarNoAnalizadas.IsChecked == true)
            {
                fila.Oculta = true;
            }
            filasAnalisis.Add(fila);

            fila.Estructura = Estructura.nombreEnDiccionario(restriccion.estructura);
            fila.Metrica = restriccion.metrica();
            if (restriccion.condicion != null && restriccion.condicion.tipo == Tipo.CondicionadaPor)
            {
                fila.Estructura = "(" + Estructura.nombreEnDiccionario(restriccion.estructura) + ")";
                fila.Metrica = "(" + restriccion.metrica() + ")";
            }
            string menorOmayor = restriccion.esMenorQue ? "<" : ">";
            string valorEsperadoString = menorOmayor + restriccion.valorEsperado + restriccion.unidadValor;
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
                double alfaBeta = 3;
                if (CHB_EvaluarConEQD2.IsChecked == true)
                {
                    alfaBeta = alfaBetaDeEstructura(estructura.Id);
                    int numeroFraccionesPlan1 = (int)((PlanSetup)plan).UniqueFractionation.NumberOfFractions;
                    restriccion.analizarPlanEstructura(planRestriccion, estructura, alfaBeta, numeroFraccionesPlan1);
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
                        ColorearAnalisis.fondoAnidadasWpf(restriccionCondicionante, restriccion, out var fondoC, out var fondoD);
                        if (filaCondicionante != null)
                        {
                            filaCondicionante.FondoEnPlan = fondoC;
                        }
                        fila.FondoEnPlan = fondoD;
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

                PlanningItem plan2Restriccion = (!string.IsNullOrEmpty(restriccion.planMod) && plan2Mod != null) ? plan2Mod : plan2;
                Structure estructura2 = estructuraCorrespondiente2(restriccion.estructura, plan2);
                if (estructura2 == null)
                {
                    MessageBox.Show("No se encontró la estructura " + restriccion.estructura.nombre + " en el " + plan2.Id + ".\nNo se pude realizar el análisis");
                }
                else
                {
                    if (CHB_EvaluarConEQD2.IsChecked == true)
                    {
                        int numeroFraccionesPlan2 = (int)((PlanSetup)plan2).UniqueFractionation.NumberOfFractions;
                        restriccion.analizarPlanEstructura(plan2Restriccion, estructura2, alfaBeta, numeroFraccionesPlan2);
                    }
                    else
                    {
                        restriccion.analizarPlanEstructura(plan2Restriccion, estructura2);
                    }
                    if (restriccion.chequearSamplingCoverage(plan2Restriccion, estructura2))
                    {
                        MessageBox.Show("La estructura " + estructura2.Id + " no tiene el suficiente Sampling Coverage.\nNo se puede realizar el análisis");
                    }
                    else
                    {
                        fila.EnPlan2 = restriccion.valorMedido + restriccion.unidadValor;
                        if (restriccion.condicion != null && restriccion.condicion.tipo == Tipo.CondicionadaPor)
                        {
                            IRestriccion restriccionCondicionante = plantilla.listaRestricciones.Where(r => r.etiqueta == restriccion.condicion.EtiquetaRestriccionAnidada).First();
                            var filaCondicionante = filasAnalisis.FirstOrDefault(f => f.Restriccion == restriccionCondicionante);
                            ColorearAnalisis.fondoAnidadasWpf(restriccionCondicionante, restriccion, out var fondoC2, out var fondoD2);
                            if (filaCondicionante != null)
                            {
                                filaCondicionante.FondoEnPlan2 = fondoC2;
                            }
                            fila.FondoEnPlan2 = fondoD2;
                        }
                        else
                        {
                            fila.FondoEnPlan2 = ColorearAnalisis.fondoWpf(restriccion);
                        }
                    }
                }
            }
        }

        private Structure estructuraCorrespondiente(string nombreEstructura, PlanningItem plan)
        {
            foreach (var fila in filasEstructuras)
            {
                if (fila.NombreSlot.Equals(nombreEstructura))
                {
                    return Estructura.listaEstructuras(plan).Where(s => s.Id.Equals(fila.StructureId)).FirstOrDefault();
                }
            }
            return null;
        }

        // Todos los PTVs reales del matcheo en "plan" (esta ventana no tiene duplicado de filas como
        // Form2, pero puede haber más de un PTV en la plantilla). Reemplaza al diálogo "elegir PTV".
        private List<Structure> ptvsMatcheadosEnGrilla()
        {
            List<Structure> estructurasPlan = Estructura.listaEstructuras(plan);
            return filasEstructuras
                .Where(f => !string.IsNullOrEmpty(f.StructureId))
                .Select(f => estructurasPlan.FirstOrDefault(s => s.Id == f.StructureId))
                .Where(s => s != null && s.DicomType == "PTV")
                .GroupBy(s => s.Id)
                .Select(g => g.First())
                .ToList();
        }

        // El ID de estructura en DGV_Estructuras se asocia solo contra "plan" (asociarEstructuras()).
        // plan2 puede tener un structure set distinto (otro Id para la misma estructura), así que
        // para plan2 se re-asocia por nombre/alias directamente contra su propio structure set,
        // en vez de reusar el ID resuelto para plan. No hay combo manual para plan2 (a diferencia de
        // plan1) ni memoria en disco propia - por eso antes, si no había match exacto/alias, quedaba
        // sin asociar y el análisis fallaba con "no se encontró la estructura" aunque el plan2 sí la
        // tuviera con un nombre apenas distinto. Se agrega el mismo fallback por distancia que plan1.
        private Structure estructuraCorrespondiente2(Estructura estructuraTemplate, PlanningItem plan2)
        {
            List<Structure> estructurasPlan2 = Estructura.listaEstructuras(plan2);
            string idExacto = Estructura.asociarConLista(estructuraTemplate.nombresPosibles, estructurasPlan2)?.Id;
            var candidatos = Estructura.candidatosPorDistancia(estructuraTemplate.nombresPosibles, estructurasPlan2);
            var mejorCandidato = candidatos.Count > 0 ? Tuple.Create(candidatos[0].Item1.Id, candidatos[0].Item2) : null;
            string idElegido = MatchingEstructuras.ElegirStructureId(idExacto, "", new List<string>(), mejorCandidato);
            return string.IsNullOrEmpty(idElegido) ? null : estructurasPlan2.FirstOrDefault(s => s.Id == idElegido);
        }

        private string infoPlan()
        {
            return planSeleccionado().Id;
        }

        private void BT_Analizar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Form2Compartido.aplicarPrescripciones(plantilla, filasPrescripciones);
                llenarDGVAnalisis();
                Form2Compartido.escribirArchivoParEstructuras(listaParesEstructuras(), Form2Compartido.nombreArchivoParEstructura(paciente, planSeleccionado()));
            }
            catch (Exception ex)
            {
                logError($"BT_Analizar_Click paciente {paciente?.Id} plan {plan?.Id} vs {plan2?.Id}", ex);
                MessageBox.Show("Error al analizar la plantilla:\n" + ex.Message + "\n\nSe registró el detalle en log.txt");
            }
            // El foco se queda en el botón tras el click y el tema de Windows anima su highlight
            // (efecto "titilando" reportado por el usuario) - se saca el foco.
            Keyboard.ClearFocus();
        }

        private void BT_SeleccionarPlan_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                llenarDGVEstructuras();
                llenarDGVPrescripciones();
            }
            catch (Exception exp)
            {
                logError($"BT_SeleccionarPlan_Click paciente {paciente?.Id} plan {plan?.Id} vs {plan2?.Id}", exp);
                MessageBox.Show("Error al seleccionar el plan:\n" + exp.Message + "\n\nSe registró el detalle en log.txt");
            }
        }

        // ponytail: log a archivo plano, sin rotación; si el log crece mucho hay que pasarlo a algo con rotación
        private static void logError(string contexto, Exception ex)
        {
            try
            {
                File.AppendAllText("log.txt", $"\r\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {contexto}\r\n{ex}\r\n");
            }
            catch (Exception)
            {
            }
        }

        private void Form2_Closing(object sender, CancelEventArgs e)
        {
            Form2Compartido.cerrarSesion(hayContext, app, LB_Cursos.Items, LB_Planes.Items);
        }

        private void TB_ID_TextChanged(object sender, TextChangedEventArgs e)
        {
            BT_AbrirPaciente.IsEnabled = !string.IsNullOrEmpty(TB_ID.Text);
        }

        private void LB_Planes_SelectedIndexChanged(object sender, SelectionChangedEventArgs e)
        {
            BT_SeleccionarPlan.IsEnabled = LB_Planes.SelectedItems.Count == 2;
            actualizarBotonAnalizar();
        }

        private void actualizarBotonAnalizar()
        {
            BT_Analizar.IsEnabled = LB_Planes.SelectedItems.Count == 2 && filasEstructuras.Count > 0;
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

            // Desde contexto (Aria) paciente/curso/planes ya vienen fijados - ver mismo cambio y
            // comentario (bug de TextWrapping a ancho 0) en Form2.
            label4.Visibility = Visibility.Collapsed;
            GridColumnaPaciente.Visibility = Visibility.Collapsed;
            ColPaciente.Width = new GridLength(0);
            ColGapPaciente.Width = new GridLength(0);

            // Ver mismo cambio y comentario en Form2: con la columna 1 oculta, renumerar 4/5/6 -> 1/2/3.
            Label_PasoEstructuras.Text = "1. Asociar estructuras";
            Label_PasoPrescripciones.Text = "2. Ajustar prescripciones";
            Label_PasoAnalizar.Text = "3. Analizar";
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

        private string StructureSetUID(PlanningItem plan)
        {
            if (plan is PlanSetup)
            {
                return ((PlanSetup)plan).StructureSet.UID;
            }
            else
            {
                return ((PlanSum)plan).StructureSet.UID;
            }
        }

        #region Imprimir

        private List<ColumnaReporte> columnasReporte()
        {
            return new List<ColumnaReporte>
            {
                new ColumnaReporte { Encabezado = "Estructura", Ancho = 60 },
                new ColumnaReporte { Encabezado = "Prioridad", Ancho = 55 },
                new ColumnaReporte { Encabezado = "Métrica", Ancho = 60 },
                new ColumnaReporte { Encabezado = "Vol [cm3]", Ancho = 60 },
                new ColumnaReporte { Encabezado = planSeleccionado().Id, Ancho = 70 },
                new ColumnaReporte { Encabezado = plan2.Id, Ancho = 70 },
                new ColumnaReporte { Encabezado = "Esperado", Ancho = 70 },
                new ColumnaReporte { Encabezado = "Ref.", Ancho = 40 },
            };
        }

        private TablaReporte tablaReporte()
        {
            var tabla = new TablaReporte { Columnas = columnasReporte() };
            foreach (var fila in filasAnalisis)
            {
                var filaReporte = new FilaReporte();
                filaReporte.Valores.AddRange(new[] { fila.Estructura, fila.Prioridad, fila.Metrica, fila.Volumen, fila.EnPlan, fila.EnPlan2, fila.Esperado, fila.Referencia });
                filaReporte.Fondos.AddRange(new[] { Form2Compartido.colorDrawing(null), Form2Compartido.colorDrawing(null), Form2Compartido.colorDrawing(fila.FondoMetrica), Form2Compartido.colorDrawing(null), Form2Compartido.colorDrawing(fila.FondoEnPlan), Form2Compartido.colorDrawing(fila.FondoEnPlan2), Form2Compartido.colorDrawing(null), Form2Compartido.colorDrawing(null) });
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
            return Reporte.crearReporte(paciente.LastName, paciente.FirstName, paciente.Id, Form2Compartido.equipo(planSeleccionado()), plantilla.nombre, plantilla.nota, usuarioNombre, Convert.ToString(infoPlan()), Convert.ToString(prescripcion), tablaReporte());
        }

        private void BT_GuardarReporte_Click(object sender, RoutedEventArgs e)
        {
            Reporte.exportarAPdf(paciente.LastName, paciente.FirstName, paciente.Id, planSeleccionado().Id, plantilla.nombre, reporte());
        }

        private void BT_Imprimir_Click(object sender, RoutedEventArgs e)
        {
            Form2Compartido.imprimir(reporte());
        }

        #endregion

        private void CHB_OcultarNoAnalizadas_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (filasAnalisis.Count > 0)
            {
                llenarDGVAnalisis();
            }
        }

        public void CHB_EvaluarConEQD2_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (CHB_EvaluarConEQD2.IsChecked == true)
            {
                if (plan is PlanSum || plan2 is PlanSum)
                {
                    MessageBox.Show("No funciona para planes suma");
                    CHB_EvaluarConEQD2.IsChecked = false;
                }
                else if (((PlanSetup)plan).UniqueFractionation.DosePerFractionInPrimaryRefPoint.Dose == 200
                    || ((PlanSetup)plan2).UniqueFractionation.DosePerFractionInPrimaryRefPoint.Dose == 200)
                {
                    MessageBox.Show("La dosis día es de 200cGy en alguno de los dos planes");
                    CHB_EvaluarConEQD2.IsChecked = false;
                }
                else
                {
                    Col_AlfaBeta.Visibility = Visibility.Visible;
                    Form2Compartido.cargarAlfaBetaDGVEstructuras(filasEstructuras);
                }
            }
            else
            {
                Col_AlfaBeta.Visibility = Visibility.Collapsed;
            }
        }

        // El α/β en DGV_Estructuras está indexado por el structureID de "plan" (asociarEstructuras()
        // solo asocia contra plan, ver estructuraCorrespondiente2). Es una propiedad de la anatomía, no
        // del plan, así que el mismo valor se reusa para analizar plan y plan2.
        private double alfaBetaDeEstructura(string structureIdPlan1)
        {
            foreach (var fila in filasEstructuras)
            {
                if (fila.StructureId == structureIdPlan1)
                {
                    return Metodos.validarYConvertirADouble(fila.AlfaBeta);
                }
            }
            return 3;
        }
    }
}
