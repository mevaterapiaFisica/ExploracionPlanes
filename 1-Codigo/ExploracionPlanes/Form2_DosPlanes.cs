using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using MigraDoc.Rendering.Forms;


namespace ExploracionPlanes
{
    public partial class Form2_DosPlanes : Form
    {

        Patient paciente;
        Course curso;
        PlanningItem plan;
        PlanningItem planMod;
        PlanningItem plan2;
        PlanningItem plan2Mod;
        User usuario;
        Plantilla plantilla;
        bool hayContext = false;
        Structure ptvCondicion;
        PrintDialog printDialog1 = new PrintDialog();
        PrintPreviewDialog printPreviewDialog1 = new PrintPreviewDialog();
        VMS.TPS.Common.Model.API.Application app;


        public Form2_DosPlanes(Plantilla _plantilla, bool _hayContext = false, Patient _pacienteContext = null, PlanningItem _planContext = null, User _usuarioContext = null, PlanningItem _segundoPlan = null, PlanningItem _planMod = null, PlanningItem _segundoPlanMod = null)
        {
            InitializeComponent();
            plantilla = _plantilla;
            this.Text = plantilla.nombre;
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
                BT_Analizar.Enabled = true;

                L_NombrePaciente.Text = paciente.LastName + ", " + paciente.FirstName;
                L_NombrePaciente.Visible = true;
                this.Text += " - " + paciente.LastName + ", " + paciente.FirstName;
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
                L_NombrePaciente.Visible = true;
                this.Text += " - " + paciente.LastName + ", " + paciente.FirstName;
                return true;
            }
            else
            {
                MessageBox.Show("El paciente no existe");
                L_NombrePaciente.Visible = false;
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
                plan = (PlanningItem)LB_Planes.SelectedItems[0];
                plan2 = cursoSeleccionado().PlanSetups.FirstOrDefault(p => p.Id.ToLower().Contains("cam"));
                if (plan2 == null)
                {
                    MessageBox.Show("No se encontró en el curso un plan cuyo nombre contenga \"cam\" para comparar.");
                }
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


        private void BT_AbrirPaciente_Click(object sender, EventArgs e)
        {
            if (abrirPaciente(TB_ID.Text))
            {
                LB_Cursos.Items.Clear();
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

        private void LB_Cursos_SelectedIndexChanged(object sender, EventArgs e)
        {
            LB_Planes.Items.Clear();
            foreach (PlanningItem plan in listaPlanes(cursoSeleccionado()))
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
            DGV_Estructuras.Rows.Clear();
            DGV_Estructuras.ColumnCount = 2;
            foreach (Estructura estructura in plantilla.estructuras())
            {
                DGV_Estructuras.Rows.Add();
                DGV_Estructuras.Rows[DGV_Estructuras.Rows.Count - 1].Cells[0].Value = estructura.nombre;
            }

            DataGridViewComboBoxColumn dgvCBCol = (DataGridViewComboBoxColumn)DGV_Estructuras.Columns[1];
            dgvCBCol.DataSource = Estructura.listaEstructurasID(Estructura.listaEstructuras(planSeleccionado()));

            asociarEstructuras();
            DGV_Estructuras.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            DGV_Estructuras.Columns[0].ReadOnly = true;
            DGV_Estructuras.Columns[1].ReadOnly = false;
        }

        private void llenarDGVPrescripciones()
        {
            DGV_Prescripciones.Rows.Clear();
            DGV_Prescripciones.ColumnCount = 2;
            double prescripcion = 0;
            if (planSeleccionado() is PlanSetup)
            {
                prescripcion = ((PlanSetup)planSeleccionado()).TotalPrescribedDose.Dose / 100;
            }
            else
            {
                foreach (PlanSetup planS in ((PlanSum)planSeleccionado()).PlanSetups) //asumo que todos los planes suman con peso 1. Más adelante se puede mejorar con PlanSumComponents
                {
                    prescripcion += planS.TotalPrescribedDose.Dose / 100;
                }
            }

            foreach (Estructura estructura in plantilla.estructurasParaPrescribir())
            {
                DGV_Prescripciones.Rows.Add();
                DGV_Prescripciones.Rows[DGV_Prescripciones.Rows.Count - 1].Cells[0].Value = estructura.nombre;
                DGV_Prescripciones.Rows[DGV_Prescripciones.Rows.Count - 1].Cells[1].Value = Form2.prescripcionPredefinida(estructura, plantilla, Math.Round(prescripcion, 2),paciente,planSeleccionado());
            }
            DGV_Prescripciones.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            DGV_Prescripciones.Columns[0].ReadOnly = true;
            DGV_Prescripciones.Columns[1].ReadOnly = false;
        }

        private void aplicarPrescripciones()
        {
            foreach (IRestriccion restriccion in plantilla.listaRestricciones)
            {
                if (restriccion.dosisEstaEnPorcentaje())
                {
                    foreach (DataGridViewRow fila in DGV_Prescripciones.Rows)
                    {
                        if (restriccion.estructura.nombre.Equals(fila.Cells[0].Value))
                        {
                            restriccion.prescripcionEstructura = Convert.ToDouble(fila.Cells[1].Value);
                            break;
                        }
                    }
                }
            }

        }

        private void asociarEstructuras()
        {
            bool existeArchivoPar = File.Exists(Form2.nombreArchivoParEstructura(paciente, planSeleccionado()));
            List<parEstructura> lista = new List<parEstructura>();
            if (existeArchivoPar)
            {
                lista = Form2.leerArchivoParEstructura(Form2.nombreArchivoParEstructura(paciente, planSeleccionado()));
            }
            for (int i = 0; i < DGV_Estructuras.Rows.Count; i++)
            {
                Structure estructura = Estructura.asociarConLista(plantilla.estructuras()[i].nombresPosibles, Estructura.listaEstructuras(planSeleccionado()));
                if (estructura != null)
                {
                    (DGV_Estructuras.Rows[i].Cells[1]).Value = estructura.Id;
                }
                else
                {
                    if (existeArchivoPar)
                    {
                        string structureID = Form2.structureDeEstructura(DGV_Estructuras.Rows[i].Cells[0].Value.ToString(), lista);
                        if (((DataGridViewComboBoxCell)DGV_Estructuras.Rows[i].Cells[1]).Items.Contains(structureID))
                        {
                            DGV_Estructuras.Rows[i].Cells[1].Value = structureID;
                        }
                        else
                        {
                            (DGV_Estructuras.Rows[i].Cells[1]).Value = "";
                        }
                    }
                    else
                    {
                        (DGV_Estructuras.Rows[i].Cells[1]).Value = "";
                    }

                }
            }
        }

        private bool estructurasSinAsociar()
        {
            foreach (DataGridViewRow fila in DGV_Estructuras.Rows)
            {
                if (string.IsNullOrEmpty((string)fila.Cells[1].Value))
                {
                    return true;
                }
            }
            return false;
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
            DGV_Analisis.ReadOnly = true;
            DGV_Analisis.Rows.Clear();

            DGV_Analisis.Columns[6].Width = 10;
            DGV_Analisis.Columns[7].DefaultCellStyle.Padding = new Padding(11);
            DGV_Analisis.Columns[4].HeaderText = planSeleccionado().Id;
            DGV_Analisis.Columns[5].HeaderText = plan2.Id;
            if (StructureSetUID(plan) != StructureSetUID(plan2))
            {
                MessageBox.Show("Los planes están calculados sobre diferentes Set de estructuras\nSe obtendrá información respetando el nombre de las estructuras");
            }
            if (plantilla.tienePrioridades())
            {
                DGV_Analisis.Columns[1].Visible = true;
            }
            if (plantilla.tieneCondicionesTipo1())
            {
                SeleccionarPTV seleccionarPTV = new SeleccionarPTV(Estructura.ptvs(plan));
                seleccionarPTV.ShowDialog();
                ptvCondicion = seleccionarPTV.ptv;
            }
            int j = 0;
            for (int i = 0; i < plantilla.listaRestricciones.Count; i++)
            {
                IRestriccion restriccion = plantilla.listaRestricciones[i];
                if (restriccion.condicion != null && !restriccion.condicion.CumpleCondicion(plan, ptvCondicion))
                {
                    continue;
                }
                try
                {
                    analizarRestriccion(restriccion, j);
                }
                catch (Exception ex)
                {
                    logError($"llenarDGVAnalisis - restriccion '{restriccion.etiqueta}' paciente {paciente?.Id} plan {plan?.Id} vs {plan2?.Id}", ex);
                    DGV_Analisis.Rows[j].Cells[4].Value = "ERROR";
                    DGV_Analisis.Rows[j].Cells[4].Style.BackColor = System.Drawing.Color.Red;
                    MessageBox.Show("Error al analizar la restricción \"" + restriccion.etiqueta + "\":\n" + ex.Message + "\n\nSe registró el detalle en log.txt");
                }
                j++;
            }
            DGV_Analisis.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            if (plantilla.TieneRestriccionEnPlanMod())
            {
                L_Advertencia.Visible = true;
                if (planMod != null)
                {
                    L_Advertencia.Text = "* Restricciones evaluadas en " + planMod.Id;
                    plantilla.nota += "\r\n* Restricciones evaluadas en " + planMod.Id;
                }
                else
                {
                    L_Advertencia.Text = "* Restricciones evaluadas en " + plan.Id;
                }

                L_Advertencia2.Visible = true;
                if (plan2Mod != null)
                {
                    L_Advertencia2.Text = "* Restricciones evaluadas en " + plan2Mod.Id;
                    plantilla.nota += "\r\n* Restricciones evaluadas en " + plan2Mod.Id;
                }
                else
                {
                    L_Advertencia2.Text = "* Restricciones evaluadas en " + plan2.Id;
                }
            }
            else
            {
                L_Advertencia.Visible = false;
            }
        }

        private void analizarRestriccion(IRestriccion restriccion, int j)
        {
            PlanningItem planRestriccion;
            if (!string.IsNullOrEmpty(restriccion.planMod) && planMod != null)
            {
                planRestriccion = planMod;
            }
            else
            {
                planRestriccion = plan;
            }
            Structure estructura = estructuraCorrespondiente(restriccion.estructura.nombre, plan);
            DGV_Analisis.Rows.Add();
                DGV_Analisis.Rows[j].Cells[0].Value = Estructura.nombreEnDiccionario(restriccion.estructura);
                DGV_Analisis.Rows[j].Cells[2].Value = restriccion.metrica();
                if (restriccion.condicion != null && restriccion.condicion.tipo == Tipo.CondicionadaPor)
                {
                    DGV_Analisis.Rows[j].Cells[0].Value = "(" + Estructura.nombreEnDiccionario(restriccion.estructura) + ")";
                    DGV_Analisis.Rows[j].Cells[2].Value = "(" + restriccion.metrica() + ")";
                }
                string menorOmayor;
                if (restriccion.esMenorQue)
                {
                    menorOmayor = "<";
                }
                else
                {
                    menorOmayor = ">";
                }
                string valorEsperadoString = menorOmayor + restriccion.valorEsperado + restriccion.unidadValor;
                if (!Double.IsNaN(restriccion.valorTolerado))
                {
                    valorEsperadoString += " (" + restriccion.valorTolerado + restriccion.unidadValor + ")";
                }

                DGV_Analisis.Rows[j].Cells[6].Value = valorEsperadoString;
                DGV_Analisis.Rows[j].Cells[7].Value = restriccion.nota;
                if (estructura != null)
                {
                    if (!string.IsNullOrEmpty(restriccion.planMod) && planMod != null)
                    {
                        DGV_Analisis.Rows[j].Cells[6].Value += " *";
                    }
                    DGV_Analisis.Rows[j].Cells[3].Value = Math.Round(estructura.Volume, 2).ToString();
                    restriccion.analizarPlanEstructura(planRestriccion, estructura);
                    if (restriccion.chequearSamplingCoverage(planRestriccion, estructura))
                    {
                        MessageBox.Show("La estructura " + estructura.Id + " no tiene el suficiente Sampling Coverage.\nNo se puede realizar el análisis");
                    }
                    else
                    {
                        DGV_Analisis.Rows[j].Cells[4].Value = restriccion.valorMedido + restriccion.unidadValor;
                        if (restriccion.condicion != null && restriccion.condicion.tipo == Tipo.CondicionadaPor)
                        {
                            IRestriccion restriccionCondicionante = plantilla.listaRestricciones.Where(r => r.etiqueta == restriccion.condicion.EtiquetaRestriccionAnidada).First();
                            int filaCondicionante = plantilla.listaRestricciones.IndexOf(restriccionCondicionante);
                            colorCeldasAnidadas(restriccionCondicionante, DGV_Analisis.Rows[filaCondicionante].Cells[4], restriccion, DGV_Analisis.Rows[j].Cells[4]);
                        }
                        else
                        {
                            colorCelda(DGV_Analisis.Rows[j].Cells[4], restriccion);
                        }
                    }
                    if (restriccion.prioridad != null && restriccion.prioridad != "")
                    {
                        DGV_Analisis.Rows[j].Cells[1].Value = restriccion.prioridad;
                    }
                    //para el segundo Plan
                    PlanningItem plan2Restriccion = null;
                    if (!string.IsNullOrEmpty(restriccion.planMod) && plan2Mod != null)
                    {
                        plan2Restriccion = plan2Mod;
                    }
                    else
                    {
                        plan2Restriccion = plan2;
                    }
                    estructura = estructuraCorrespondiente2(restriccion.estructura, plan2);
                    if (estructura == null)
                    {
                        MessageBox.Show("No se encontró la estructura " + restriccion.estructura.nombre + " en el " + plan2.Id + ".\nNo se pude realizar el análisis");
                    }
                    else
                    {
                        restriccion.analizarPlanEstructura(plan2Restriccion, estructura);
                        if (restriccion.chequearSamplingCoverage(plan2Restriccion, estructura))
                        {
                            MessageBox.Show("La estructura " + estructura.Id + " no tiene el suficiente Sampling Coverage.\nNo se puede realizar el análisis");
                        }
                        else
                        {
                            DGV_Analisis.Rows[j].Cells[5].Value = restriccion.valorMedido + restriccion.unidadValor;
                            if (restriccion.condicion != null && restriccion.condicion.tipo == Tipo.CondicionadaPor)
                            {
                                IRestriccion restriccionCondicionante = plantilla.listaRestricciones.Where(r => r.etiqueta == restriccion.condicion.EtiquetaRestriccionAnidada).First();
                                int filaCondicionante = plantilla.listaRestricciones.IndexOf(restriccionCondicionante);
                                colorCeldasAnidadas(restriccionCondicionante, DGV_Analisis.Rows[filaCondicionante].Cells[5], restriccion, DGV_Analisis.Rows[j].Cells[5]);
                            }
                            else
                            {
                                colorCelda(DGV_Analisis.Rows[j].Cells[5], restriccion);
                            }
                        }

                        if (restriccion.GetType() == typeof(RestriccionDosisMax))
                        {
                            DataGridViewButtonCell bt = (DataGridViewButtonCell)DGV_Analisis.Rows[j].Cells[8];
                            bt.FlatStyle = FlatStyle.System;
                            bt.Style.BackColor = System.Drawing.Color.LightGray;
                            bt.Style.ForeColor = System.Drawing.Color.Black;
                            bt.Style.SelectionBackColor = System.Drawing.Color.LightGray;
                            bt.Style.SelectionForeColor = System.Drawing.Color.Black;
                            bt.Value = RestriccionDosisMax.volumenDosisMaxima.ToString();
                            DGV_Analisis.Rows[j].Cells[6].Style.Padding = new Padding(0, 0, 0, 1);
                        }
                    }

                }
        }

        private Structure estructuraCorrespondiente(string nombreEstructura, PlanningItem plan)
        {
            foreach (DataGridViewRow fila in DGV_Estructuras.Rows)
            {
                if (fila.Cells[0].Value.Equals(nombreEstructura))
                {
                    string estructuraID = (string)(fila.Cells[1].Value);
                    return Estructura.listaEstructuras(plan).Where(s => s.Id.Equals(estructuraID)).FirstOrDefault();
                }
            }
            return null;
        }

        // El ID de estructura en DGV_Estructuras se asocia solo contra "plan" (asociarEstructuras()).
        // plan2 puede tener un structure set distinto (otro Id para la misma estructura), así que
        // para plan2 se re-asocia por nombre/alias directamente contra su propio structure set,
        // en vez de reusar el ID resuelto para plan.
        private Structure estructuraCorrespondiente2(Estructura estructuraTemplate, PlanningItem plan2)
        {
            return Estructura.asociarConLista(estructuraTemplate.nombresPosibles, Estructura.listaEstructuras(plan2));
        }

        private string infoPlan()
        {
            string infoPlan = planSeleccionado().Id;
            /*   if (planSeleccionado().ApprovalStatus == PlanSetupApprovalStatus.PlanningApproved || planSeleccionado().ApprovalStatus == PlanSetupApprovalStatus.TreatmentApproved)
               {
                   infoPlan += " Aprobado por: " + planSeleccionado().PlanningApprover;
               }*/
            return infoPlan;
        }

        private void BT_Analizar_Click(object sender, EventArgs e)
        {
            try
            {
                aplicarPrescripciones();
                llenarDGVAnalisis();
                Form2.escribirArchivoParEstructuras(listaParesEstructuras(), Form2.nombreArchivoParEstructura(paciente, planSeleccionado()));
            }
            catch (Exception ex)
            {
                logError($"BT_Analizar_Click paciente {paciente?.Id} plan {plan?.Id} vs {plan2?.Id}", ex);
                MessageBox.Show("Error al analizar la plantilla:\n" + ex.Message + "\n\nSe registró el detalle en log.txt");
            }
        }

        private void colorCelda(DataGridViewCell celda, IRestriccion restriccion)
        {
            ColorearAnalisis.colorCelda(celda, restriccion);
        }
        private void colorCeldasAnidadas(IRestriccion restriccionCondicionante, DataGridViewCell celdaCondicionante, IRestriccion restriccionCondicionada, DataGridViewCell celdaCondicionada)
        {
            ColorearAnalisis.colorCeldasAnidadas(restriccionCondicionante, celdaCondicionante, restriccionCondicionada, celdaCondicionada);
        }

        private void BT_SeleccionarPlan_Click(object sender, EventArgs e)
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

        private void Form2_FormClosing(object sender, FormClosingEventArgs e)
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



        private void TB_ID_TextChanged(object sender, EventArgs e)
        {
            Metodos.habilitarBoton(!string.IsNullOrEmpty(TB_ID.Text), BT_AbrirPaciente);
        }


        private void LB_Planes_SelectedIndexChanged(object sender, EventArgs e)
        {
            Metodos.habilitarBoton(LB_Planes.SelectedItems.Count == 1, BT_SeleccionarPlan);
        }

        private void DGV_Análisis_RowsAdded(object sender, DataGridViewRowsAddedEventArgs e)
        {
            Metodos.habilitarBoton(DGV_Analisis.Rows.Count > 0, BT_GuardarReporte);
            Metodos.habilitarBoton(DGV_Analisis.Rows.Count > 0, BT_Imprimir);
        }

        private void DGV_Estructuras_RowsAdded(object sender, DataGridViewRowsAddedEventArgs e)
        {
            Metodos.habilitarBoton(LB_Planes.SelectedItems.Count == 1 && DGV_Estructuras.RowCount > 0, BT_Analizar);
        }

        private void prepararControlesContext()
        {
            label4.Enabled = false;
            TB_ID.Enabled = false;
            BT_AbrirPaciente.Enabled = false;
            label2.Enabled = false;
            LB_Cursos.Enabled = false;
            Label3.Enabled = false;
            LB_Planes.Enabled = false;
            BT_SeleccionarPlan.Enabled = false;
        }

        private void DGV_Análisis_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            var senderGrid = (DataGridView)sender;

            if (senderGrid.Columns[e.ColumnIndex] is DataGridViewButtonColumn &&
                e.RowIndex >= 0)
            {
                FormTB formTb = new FormTB((senderGrid.Rows[e.RowIndex].Cells[e.ColumnIndex]).Value.ToString(), true);
                formTb.Text = "Volumen dosis maxima";
                formTb.Controls.OfType<Label>().FirstOrDefault().Text = "Definir el tamaño del elemento de volumen para el \ncálculo de la dosis máxima [cm3]";
                formTb.ShowDialog();

                if (formTb.DialogResult == DialogResult.OK)
                {
                    ((RestriccionDosisMax)(plantilla.listaRestricciones[e.RowIndex])).analizarPlanEstructura(planSeleccionado(), estructuraCorrespondiente(plantilla.listaRestricciones[e.RowIndex].estructura.nombre, plan), Metodos.validarYConvertirADouble(formTb.salida));
                    DGV_Analisis.Rows[e.RowIndex].Cells[2].Value = plantilla.listaRestricciones[e.RowIndex].valorMedido + plantilla.listaRestricciones[e.RowIndex].unidadValor;
                    colorCelda(DGV_Analisis.Rows[e.RowIndex].Cells[2], plantilla.listaRestricciones[e.RowIndex]);
                    (senderGrid.Rows[e.RowIndex].Cells[e.ColumnIndex]).Value = formTb.salida;
                }
            }
        }

        private List<parEstructura> listaParesEstructuras()
        {
            List<parEstructura> lista = new List<parEstructura>();
            foreach (DataGridViewRow fila in DGV_Estructuras.Rows)
            {
                parEstructura par = new parEstructura()
                {
                    estructuraNombre = fila.Cells[0].Value.ToString(),
                };
                if (fila.Cells[1].Value != null)
                {
                    par.structureID = fila.Cells[1].Value.ToString();
                }

                lista.Add(par);
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
        private Document reporte()
        {
            string usuarioNombre;
            double prescripcion = 0;
            if (hayContext)
            {
                usuarioNombre = usuario.Name;
            }
            else
            {
                usuarioNombre = app.CurrentUser.Name;
            }
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

            return Reporte.crearReporte(paciente.LastName, paciente.FirstName, paciente.Id, equipo(), plantilla.nombre, plantilla.nota, usuarioNombre, Convert.ToString(infoPlan()), Convert.ToString(prescripcion), DGV_Analisis);
        }
        private void BT_GuardarReporte_Click(object sender, EventArgs e)
        {
            Reporte.exportarAPdf(paciente.LastName, paciente.FirstName, paciente.Id, planSeleccionado().Id, plantilla.nombre, reporte());
        }

        private void BT_Imprimir_Click(object sender, EventArgs e)
        {
            MigraDoc.Rendering.Printing.MigraDocPrintDocument pd = new MigraDoc.Rendering.Printing.MigraDocPrintDocument();
            var rendered = new DocumentRenderer(reporte());
            rendered.PrepareDocument();
            pd.Renderer = rendered;
            if (printDialog1.ShowDialog() == DialogResult.OK)
            {
                pd.PrinterSettings = printDialog1.PrinterSettings;
                pd.Print();
            }

        }



        #endregion

        private void DGV_Prescripciones_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }
    }
}