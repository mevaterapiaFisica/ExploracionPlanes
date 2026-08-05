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
    public partial class Form2 : Form
    {

        Patient paciente;
        Course curso;
        PlanningItem plan;
        PlanningItem planMod = null;
        User usuario;
        Plantilla plantilla;
        Structure ptvCondicion;
        bool hayContext = false;
        PrintDialog printDialog1 = new PrintDialog();
        PrintPreviewDialog printPreviewDialog1 = new PrintPreviewDialog();
        VMS.TPS.Common.Model.API.Application app;
        static string pathParEstructuras => Properties.Settings.Default.Path + @"\paresEstructuras\";
        static string pathPrescripciones => Properties.Settings.Default.Path + @"\prescripciones\";
        static string pathDuplicados => Properties.Settings.Default.Path + @"\duplicadosEstructura\";
        public static string pathReportesJson => Properties.Settings.Default.Path + @"\Reportes\Json\";
        string plantillaNotaOriginal = "";


        public Form2(Plantilla _plantilla, bool _hayContext = false, Patient _pacienteContext = null, PlanningItem _planContext = null, User _usuarioContext = null, PlanningItem _planMod = null)
        {
            InitializeComponent();
            plantilla = _plantilla;
            this.Text = plantilla.nombre;
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
                BT_Analizar.Enabled = true;

                L_NombrePaciente.Text = paciente.LastName + ", " + paciente.FirstName;
                L_NombrePaciente.Visible = true;
                this.Text += " - " + paciente.LastName + ", " + paciente.FirstName;
                plantillaNotaOriginal = plantilla.nota;
            }
            else
            {
                try
                {
                    app = VMS.TPS.Common.Model.API.Application.CreateApplication(null,null);
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
            //DGV_Estructuras.Columns[2].ReadOnly = false;
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
                DGV_Prescripciones.Rows[DGV_Prescripciones.Rows.Count - 1].Cells[1].Value = prescripcionPredefinida(estructura, plantilla, Math.Round(prescripcion, 2),paciente,planSeleccionado());
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
            List<parEstructura> memoria = memoriaEstructuras(paciente, planSeleccionado());
            List<Structure> estructurasPlan = Estructura.listaEstructuras(planSeleccionado());
            for (int i = 0; i < DGV_Estructuras.Rows.Count; i++)
            {
                string nombreSlot = DGV_Estructuras.Rows[i].Cells[0].Value.ToString();
                List<string> nombresPosibles = plantilla.estructuras()[i].nombresPosibles;
                var candidatos = Estructura.candidatosPorDistancia(nombresPosibles, estructurasPlan);

                // El combo de esta fila se ordena de más a menos parecido (Damerau-Levenshtein) en vez del orden arbitrario del plan.
                var cell = (DataGridViewComboBoxCell)DGV_Estructuras.Rows[i].Cells[1];
                List<string> itemsOrdenados = candidatos.Select(c => c.Item1.Id).ToList();
                itemsOrdenados.Add("");
                cell.DataSource = itemsOrdenados;

                Structure estructuraExacta = Estructura.asociarConLista(nombresPosibles, estructurasPlan);
                if (estructuraExacta != null)
                {
                    cell.Value = estructuraExacta.Id;
                    continue;
                }
                string idMemoria = structureDeEstructura(nombreSlot, memoria);
                if (!string.IsNullOrEmpty(idMemoria) && itemsOrdenados.Contains(idMemoria))
                {
                    cell.Value = idMemoria;
                }
                else if (candidatos.Count > 0 && candidatos[0].Item2 <= Estructura.DistanciaMaximaSugerida)
                {
                    cell.Value = candidatos[0].Item1.Id;
                }
                else
                {
                    cell.Value = "";
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

        private void BT_DuplicarEstructura_Click(object sender, EventArgs e)
        {
            if (DGV_Estructuras.CurrentRow == null)
            {
                MessageBox.Show("Seleccione primero la fila de la estructura a duplicar.");
                return;
            }
            string nombreSlot = DGV_Estructuras.CurrentRow.Cells[0].Value.ToString();
            duplicarEstructura(nombreSlot);
            llenarDGVEstructuras();
            llenarDGVPrescripciones();
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

        private void CHB_OcultarNoAnalizadas_CheckedChanged(object sender, EventArgs e)
        {
            if (DGV_Análisis.Rows.Count > 0)
            {
                llenarDGVAnalisis();
            }
        }

        private bool estructurasSinAsociar()
        {
            bool aux = false;
            foreach (DataGridViewRow fila in DGV_Estructuras.Rows)
            {
                if (string.IsNullOrEmpty((string)fila.Cells[1].Value))
                {
                    aux = true;
                }
            }
            return aux;
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
            DGV_Análisis.ReadOnly = true;
            DGV_Análisis.Rows.Clear();

            DGV_Análisis.Columns[5].Width = 10;
            DGV_Análisis.Columns[7].DefaultCellStyle.Padding = new Padding(11);
            //DGV_Análisis.ColumnCount = 4;
            int j = 0;
            if (plantilla.tieneCondicionesTipo1())
            {
                SeleccionarPTV seleccionarPTV = new SeleccionarPTV(Estructura.ptvs(planSeleccionado()));
                seleccionarPTV.ShowDialog();
                ptvCondicion = seleccionarPTV.ptv;
                MessageBox.Show("PTV volumen: " + Math.Round(ptvCondicion.Volume, 1).ToString() + " [cm3]\nNumero de fracciones " + ((PlanSetup)planSeleccionado()).UniqueFractionation.NumberOfFractions.ToString());
                this.Text += " volPTV: " + Math.Round(ptvCondicion.Volume, 1).ToString() + "cm3 " + ((PlanSetup)planSeleccionado()).UniqueFractionation.NumberOfFractions.ToString() + " fx";
            }
            if (plantilla.tienePrioridades())
            {
                DGV_Análisis.Columns[1].Visible = true;
            }
            for (int i = 0; i < plantilla.listaRestricciones.Count; i++)
            {
                PlanningItem planRestriccion = null;
                IRestriccion restriccion = plantilla.listaRestricciones[i];
                if (!string.IsNullOrEmpty(restriccion.planMod) && planMod != null)
                {
                    planRestriccion = planMod;
                }
                else
                {
                    planRestriccion = plan;
                }

                if (restriccion.condicion == null || restriccion.condicion.CumpleCondicion(planSeleccionado(), ptvCondicion))
                {
                    Structure estructura = estructuraCorrespondiente(restriccion.estructura.nombre);
                    DGV_Análisis.Rows.Add();
                    if (estructura == null && CHB_OcultarNoAnalizadas.Checked)
                    {
                        DGV_Análisis.Rows[j].Visible = false;
                    }
                    DGV_Análisis.Rows[j].Cells[0].Value = Estructura.nombreEnDiccionario(restriccion.estructura);
                    DGV_Análisis.Rows[j].Cells[2].Value = restriccion.metrica();
                    if (restriccion.condicion != null && restriccion.condicion.tipo == Tipo.CondicionadaPor)
                    {
                        DGV_Análisis.Rows[j].Cells[0].Value = "(" + Estructura.nombreEnDiccionario(restriccion.estructura) + ")";
                        DGV_Análisis.Rows[j].Cells[2].Value = "(" + restriccion.metrica() + ")";
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
                    string valorEsperadoString;
                    if (Double.IsNaN(restriccion.valorEsperado))
                    {
                        valorEsperadoString = "Reportar";
                    }
                    else
                    {
                        valorEsperadoString = menorOmayor + restriccion.valorEsperado + restriccion.unidadValor;
                    }
                    
                    if (!Double.IsNaN(restriccion.valorTolerado))
                    {
                        valorEsperadoString += " (" + restriccion.valorTolerado + restriccion.unidadValor + ")";
                    }
                    DGV_Análisis.Rows[j].Cells[5].Value = valorEsperadoString;
                    DGV_Análisis.Rows[j].Cells[6].Value = restriccion.nota;
                    if (estructura != null)
                    {
                        if (!string.IsNullOrEmpty(restriccion.planMod) && planMod != null)
                        {
                            DGV_Análisis.Rows[j].Cells[6].Value += " *";
                        }
                        DGV_Análisis.Rows[j].Cells[3].Value = Math.Round(estructura.Volume, 2).ToString();
                        if (CHB_EvaluarConEQD2.Checked)
                        {
                            double alfaBeta = 3;
                            foreach (DataGridViewRow fila in DGV_Estructuras.Rows)
                            {
                                if (fila.Cells[1].Value != null && fila.Cells[1].Value.ToString() == estructura.Id)
                                {
                                    alfaBeta = Convert.ToDouble(fila.Cells[2].Value);
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
                            DGV_Análisis.Rows[j].Cells[4].Value = restriccion.valorMedido + restriccion.unidadValor;
                            if (restriccion.condicion != null && restriccion.condicion.tipo == Tipo.CondicionadaPor)
                            {
                                IRestriccion restriccionCondicionante = plantilla.listaRestricciones.Where(r => r.etiqueta == restriccion.condicion.EtiquetaRestriccionAnidada).First();
                                int filaCondicionante = plantilla.listaRestricciones.IndexOf(restriccionCondicionante);
                                colorCeldasAnidadas(restriccionCondicionante, DGV_Análisis.Rows[filaCondicionante].Cells[4], restriccion, DGV_Análisis.Rows[j].Cells[4]);
                            }
                            else
                            {
                                colorCelda(DGV_Análisis.Rows[j].Cells[4], restriccion);
                            }
                        }
                        if (restriccion.prioridad != null && restriccion.prioridad != "")
                        {
                            DGV_Análisis.Rows[j].Cells[1].Value = restriccion.prioridad;
                        }
                        if (restriccion.GetType() == typeof(RestriccionDosisMax))
                        {
                            DataGridViewButtonCell bt = (DataGridViewButtonCell)DGV_Análisis.Rows[j].Cells[7];
                            bt.FlatStyle = FlatStyle.System;
                            bt.Style.BackColor = System.Drawing.Color.LightGray;
                            bt.Style.ForeColor = System.Drawing.Color.Black;
                            bt.Style.SelectionBackColor = System.Drawing.Color.LightGray;
                            bt.Style.SelectionForeColor = System.Drawing.Color.Black;
                            bt.Value = RestriccionDosisMax.volumenDosisMaxima.ToString();
                            DGV_Análisis.Rows[j].Cells[7].Style.Padding = new Padding(0, 0, 0, 1);
                        }

                    }
                    //MessageBox.Show(DGV_Análisis.Rows[j].Cells[5].Value.ToString());
                    j++;
                }
                else
                {

                }
            }
            DGV_Análisis.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            DGV_Análisis.Columns[1].Width = 50;
            if (CHB_EvaluarConEQD2.Checked)
            {
                plantilla.nota += "\r\n" + notaEQD2;
            }
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

            }
            else
            {
                L_Advertencia.Visible = false;
            }

        }

        private Structure estructuraCorrespondiente(string nombreEstructura)
        {
            foreach (DataGridViewRow fila in DGV_Estructuras.Rows)
            {
                if (fila.Cells[0].Value.Equals(nombreEstructura))
                {
                    string estructuraID = (string)(fila.Cells[1].Value);
                    return Estructura.listaEstructuras(planSeleccionado()).Where(s => s.Id.Equals(estructuraID)).FirstOrDefault();
                }
            }
            return null;
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
            aplicarPrescripciones();
            llenarDGVAnalisis();
            escribirArchivoParEstructuras(listaParesEstructuras(), nombreArchivoParEstructura(paciente, planSeleccionado()));
            escribirArchivoPrescripciones(listaPrescripcion(), nombreArchivoPrescripciones(paciente, planSeleccionado()));
            guardarDuplicados();
            if (plantilla.nombre.Contains("SunRise"))
            {
                DGV_Análisis.Columns[0].HeaderText = "Structure";
                DGV_Análisis.Columns[1].HeaderText = "Priority";
                DGV_Análisis.Columns[2].HeaderText = "Metric";
                DGV_Análisis.Columns[4].HeaderText = "In plan";
                DGV_Análisis.Columns[5].HeaderText = "Expected";
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
            Metodos.habilitarBoton(DGV_Análisis.Rows.Count > 0, BT_GuardarReporte);
            Metodos.habilitarBoton(DGV_Análisis.Rows.Count > 0, BT_Imprimir);
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
                formTb.Title = "Volumen dosis maxima";
                formTb.L_Texto.Text = "Definir el tamaño del elemento de volumen para el \ncálculo de la dosis máxima [cm3]";
                formTb.ShowDialog();

                if (formTb.DialogResult == true)
                {
                    ((RestriccionDosisMax)(plantilla.listaRestricciones[e.RowIndex])).analizarPlanEstructura(planSeleccionado(), estructuraCorrespondiente(plantilla.listaRestricciones[e.RowIndex].estructura.nombre), Metodos.validarYConvertirADouble(formTb.salida));
                    DGV_Análisis.Rows[e.RowIndex].Cells[2].Value = plantilla.listaRestricciones[e.RowIndex].valorMedido + plantilla.listaRestricciones[e.RowIndex].unidadValor;
                    colorCelda(DGV_Análisis.Rows[e.RowIndex].Cells[2], plantilla.listaRestricciones[e.RowIndex]);
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

        private List<prescripcion> listaPrescripcion()
        {
            List<prescripcion> lista = new List<prescripcion>();
            foreach (DataGridViewRow fila in DGV_Prescripciones.Rows)
            {
                prescripcion presc = new prescripcion()
                {
                    estructura = fila.Cells[0].Value.ToString(),
                };
                if (fila.Cells[1].Value != null)
                {
                    presc.dosis = Convert.ToDouble(fila.Cells[1].Value);
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

            return Reporte.crearReporte(paciente.LastName, paciente.FirstName, paciente.Id, equipo(), plantilla.nombre, plantilla.nota, usuarioNombre, Convert.ToString(infoPlan()), Convert.ToString(prescripcion), DGV_Análisis);
        }
        private void BT_GuardarReporte_Click(object sender, EventArgs e)
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

        public void CHB_EvaluarConEQD2_CheckedChanged(object sender, EventArgs e)
        {
            if (CHB_EvaluarConEQD2.Checked)
            {
                if (planSeleccionado() is PlanSum)
                {
                    MessageBox.Show("No funciona para planes suma");
                    CHB_EvaluarConEQD2.Checked = false;
                }
                else if (((PlanSetup)planSeleccionado()).UniqueFractionation.DosePerFractionInPrimaryRefPoint.Dose == 200)
                {
                    MessageBox.Show("La dosis día es de 200cGy");
                    CHB_EvaluarConEQD2.Checked = false;
                }
                else
                {
                    DataGridViewTextBoxColumn columna = new DataGridViewTextBoxColumn();
                    columna.Width = 55;
                    columna.HeaderText = "α/β";
                    DGV_Estructuras.Columns.Add(columna);
                    //DGV_Estructuras.Columns[2].Visible = true;
                    DGV_Estructuras.Width = 314;
                    cargarAlfaBetaDGVEstructuras();
                }
            }
            else
            {
                if (DGV_Estructuras.Columns.Count == 3)
                {
                    DGV_Estructuras.Columns.RemoveAt(2);
                    //DGV_Estructuras.Columns[2].Visible = false;
                    DGV_Estructuras.Width = 254;
                }
            }
        }

        public void cargarAlfaBetaDGVEstructuras()
        {
            foreach (DataGridViewRow fila in DGV_Estructuras.Rows)
            {
                fila.Cells[2].Value = Estructura.AlfaBeta(fila.Cells[0].Value.ToString());
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



        /*public static bool habilitarAdvertenciaPrescripcion(PlanSetup plan, Plantilla plantilla)
        {
            double prescripcion = plan.TotalPrescribedDose.Dose / 100;
            if (plantilla.nombre.Contains("Mama") && plan.StructureSet.Structures.Any(s=>s.Id.Contains("PTV_Sb_Eval")) && plan.StructureSet.Structures.Any(s => s.Id.Contains("PTV_Sb_Eval")))
        }*/
    }
}