using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using VMS.TPS.Common.Model.API;

namespace ExploracionPlanes
{
    public partial class Main : DialogoWpf
    {
        public Form1_prioridades crearPlantilla;
        public Form1_ext crearPlantilla_ext;
        public Form2 aplicarPlantilla;
        public Form3 aplicarPorLote;
        public PlantillaBlanco plantillaBlanco;
        public PlanesParaComparar planesParaCompararForm;
        public Form2_DosPlanes Form2_DosPlanes;
        bool hayContext = false;
        bool editaPlantilla = false;
        Patient pacienteContext = null;
        PlanningItem planContext = null;
        PlanningItem planMod = null;
        PlanningItem planParaCompararMod = null;
        User usuarioContext = null;
        List<PlanningItem> planesParaComparar = new List<PlanningItem>();
        List<PlanSum> planSumsContext = new List<PlanSum>();
        string texto = "";

        public Main(bool _hayContext = false, Patient _pacienteContext = null, PlanningItem _planContext = null, User _usuarioContext = null, IEnumerable<PlanSum> _planSumsContext = null, IEnumerable<PlanSetup> _plansContext = null)
        {
            InitializeComponent();
            leerPlantillas();
            hayContext = _hayContext;
            pacienteContext = _pacienteContext;
            planContext = _planContext;
            usuarioContext = _usuarioContext;
            if (_plansContext != null && _plansContext.Count() > 0)
            {
                foreach (PlanSetup plan in _plansContext)
                {
                    planesParaComparar.Add(plan);
                }
                planesParaComparar.Remove(planContext);
            }
            if (_planSumsContext != null && _planSumsContext.Count() > 0)
            {
                planSumsContext = _planSumsContext.ToList();
                foreach (PlanSum plan in _planSumsContext)
                {
                    planesParaComparar.Add(plan);
                }
            }
            habilitarBotones();
            eliminarArchivosParesEstructura(1);
            if (hayContext && planContext != null)
            {
                texto += Chequeos.chequeos(planContext, false);
                if (texto != "")
                {
                    MessageBox.Show(texto, "Chequeos en plan actual");
                }
                else
                {
                    MessageBox.Show("Todo bien", "Chequeos en plan actual");
                }
                Plantilla plantilla = Plantilla.SeleccionarAutomaticamentePlantilla(planContext, pacienteContext);
                int indice = Plantilla.leerPlantillas().FindIndex(p => p.path == plantilla.path);
                LB_Plantillas.UnselectAll();
                LB_Plantillas.SelectedIndex = indice;
            }
            else if (hayContext && pacienteContext == null)
            {
                MessageBox.Show("Debe abrir un paciente");
                Close();
            }
            else if (hayContext && planContext == null)
            {
                if (_planSumsContext != null)
                {
                    PlanesSumaContext planesSumaContext = new PlanesSumaContext(planSumsContext);
                    planesSumaContext.ShowDialog();
                    planContext = planesSumaContext.PlanSuma;
                    texto += Chequeos.chequeos(planContext, true);
                    if (texto != "")
                    {
                        MessageBox.Show(texto, "Chequeos en plan actual");
                    }
                    else
                    {
                        MessageBox.Show("Todo bien", "Chequeos en plan actual");
                    }
                    Plantilla plantilla = Plantilla.SeleccionarAutomaticamentePlantilla(planContext, pacienteContext);
                    int indice = Plantilla.leerPlantillas().FindIndex(p => p.path == plantilla.path);
                    LB_Plantillas.UnselectAll();
                    LB_Plantillas.SelectedIndex = indice;
                }
                else
                {
                    MessageBox.Show("Debe seleccionar un plan");
                    Close();
                }
            }
        }

        private void BT_Nueva_Click(object sender, RoutedEventArgs e)
        {
            crearPlantilla = new Form1_prioridades(this, false);
            crearPlantilla.ShowDialog();
        }

        private void BT_Editar_Click(object sender, RoutedEventArgs e)
        {
            if (plantillaSeleccionada().tieneCondicionesTipo1())
            {
                crearPlantilla_ext = new Form1_ext(this, true);
                crearPlantilla_ext.ShowDialog();
            }
            else
            {
                crearPlantilla = new Form1_prioridades(this, true);
                crearPlantilla.ShowDialog();
            }
        }

        private void BT_AplicarAUnPlan_Click(object sender, RoutedEventArgs e)
        {
            planMod = null;
            if (hayContext)
            {
                if (plantillaSeleccionada().TieneRestriccionEnPlanMod())
                {
                    string nombrePlanMod = planContext.Id + plantillaSeleccionada().ExtensionPlanMod();
                    if (planesParaComparar.Any(p => p.Id == nombrePlanMod))
                    {
                        planMod = planesParaComparar.Where(p => p.Id == nombrePlanMod).First();
                        if (planMod is PlanSetup && ((PlanSetup)planMod).ApprovalStatus != VMS.TPS.Common.Model.Types.PlanSetupApprovalStatus.Rejected)
                        {
                            MessageBox.Show("El plan " + planMod.Id + " debe estar rechazado");
                        }
                        if (!PlanYModSonIguales((PlanSetup)planContext, (PlanSetup)planMod))
                        {
                            planMod = null;
                            Close();
                        }
                    }
                    else if (planesParaComparar.Any(p => p.Id.ToLower().Contains(plantillaSeleccionada().ExtensionPlanMod())))
                    {
                        MessageBox.Show("Se encontró un plan con la extensión " + plantillaSeleccionada().ExtensionPlanMod() + " pero cuyo nombre no coincide con el plan a analizar\nRevisar si se nombró de forma adecuada");
                    }
                }
            }
            if (hayContext && pacienteContext != null && planContext != null)
            {
                Plantilla.GuardarSeleccion(pacienteContext, planContext, plantillaSeleccionada().nombre);
            }
            aplicarPlantilla = new Form2(plantillaSeleccionada(), hayContext, pacienteContext, planContext, usuarioContext, planMod);
            aplicarPlantilla.ShowDialog();
        }

        private void BT_CompararPlanes_Click(object sender, RoutedEventArgs e)
        {
            planMod = null;
            planParaCompararMod = null;
            if (hayContext)
            {
                // planesParaComparar solo se llena con el contexto de Eclipse (Script.cs); en standalone
                // siempre está vacía, así que este diálogo (y el chequeo de plan Mod, que depende de
                // planContext) solo tiene sentido en modo plugin.
                planesParaCompararForm = new PlanesParaComparar(planesParaComparar);
                planesParaCompararForm.ShowDialog();

                if (plantillaSeleccionada().TieneRestriccionEnPlanMod())
                {
                    string nombrePlanMod = planContext.Id + plantillaSeleccionada().ExtensionPlanMod();
                    string nombrePlanParaCompararMod = planesParaCompararForm.planParaComparar + plantillaSeleccionada().ExtensionPlanMod();
                    if (planesParaComparar.Any(p => p.Id == nombrePlanMod))
                    {
                        planMod = planesParaComparar.Where(p => p.Id == nombrePlanMod).First();
                        if (planMod is PlanSetup && ((PlanSetup)planMod).ApprovalStatus != VMS.TPS.Common.Model.Types.PlanSetupApprovalStatus.Rejected)
                        {
                            MessageBox.Show("El plan " + planMod.Id + " debe estar rechazado");
                        }
                        if (!PlanYModSonIguales((PlanSetup)planContext, (PlanSetup)planMod))
                        {
                            return;
                        }
                    }
                    if (planesParaComparar.Any(p => p.Id == nombrePlanParaCompararMod))
                    {
                        planParaCompararMod = planesParaComparar.Where(p => p.Id == nombrePlanParaCompararMod).First();
                        if (planParaCompararMod is PlanSetup && ((PlanSetup)planParaCompararMod).ApprovalStatus != VMS.TPS.Common.Model.Types.PlanSetupApprovalStatus.Rejected)
                        {
                            MessageBox.Show("El plan " + planParaCompararMod.Id + " debe estar rechazado");
                        }
                        if (!PlanYModSonIguales((PlanSetup)planesParaCompararForm.planParaComparar, (PlanSetup)planParaCompararMod))
                        {
                            return;
                        }
                    }
                }
                Form2_DosPlanes = new Form2_DosPlanes(plantillaSeleccionada(), hayContext, pacienteContext, planContext, usuarioContext, planesParaCompararForm.planParaComparar, planMod, planParaCompararMod);
            }
            else
            {
                // Standalone: paciente/curso/los 2 planes se eligen dentro de Form2_DosPlanes (igual que Form2).
                Form2_DosPlanes = new Form2_DosPlanes(plantillaSeleccionada(), hayContext, pacienteContext, planContext, usuarioContext, null, null, null);
            }
            Form2_DosPlanes.ShowDialog();
        }

        public Plantilla plantillaSeleccionada()
        {
            return (Plantilla)LB_Plantillas.SelectedItem;
        }

        private void BT_AplicarPorLote_Click(object sender, RoutedEventArgs e)
        {
            aplicarPorLote = new Form3(plantillaSeleccionada());
            aplicarPorLote.ShowDialog();
        }

        private void BT_Ver_Click(object sender, RoutedEventArgs e)
        {
            plantillaBlanco = new PlantillaBlanco(plantillaSeleccionada());
            plantillaBlanco.ShowDialog();
        }

        public void leerPlantillas()
        {
            LB_Plantillas.ItemsSource = null;
            LB_Plantillas.ItemsSource = Plantilla.leerPlantillas();
        }

        private void LB_Plantillas_SelectedIndexChanged(object sender, SelectionChangedEventArgs e)
        {
            habilitarBotones();
        }

        private void BT_Eliminar_Click(object sender, RoutedEventArgs e)
        {
            plantillaSeleccionada().eliminar();
            leerPlantillas();
        }

        private void BT_Duplicar_Click(object sender, RoutedEventArgs e)
        {
            FormTB formTb = new FormTB();
            formTb.Title = "Nombre plantilla";
            formTb.L_Texto.Text = "Ingrese el nombre de la nueva plantilla";
            formTb.ShowDialog();
            if (formTb.DialogResult == true)
            {
                plantillaSeleccionada().duplicar(formTb.salida);
                leerPlantillas();
            }
        }

        private void habilitarBotones()
        {
            if (hayContext)
            {
                BT_Nueva.IsEnabled = false;
                BT_NuevaConCondiciones.IsEnabled = false;
                BT_Editar.IsEnabled = false;
                BT_Ver.IsEnabled = false;
                BT_Duplicar.IsEnabled = false;
                BT_Eliminar.IsEnabled = false;
                BT_AplicarAUnPlan.IsEnabled = true;
                BT_CompararPlanes.IsEnabled = true;
                BT_AplicarPorLote.IsEnabled = false;
                BT_ExtraerDePlantilla.IsEnabled = false;
            }
            else
            {
                BT_CompararPlanes.IsEnabled = LB_Plantillas.SelectedItems.Count == 1;
                BT_Nueva.IsEnabled = editaPlantilla;
                BT_NuevaConCondiciones.IsEnabled = editaPlantilla;
                BT_Editar.IsEnabled = LB_Plantillas.SelectedItems.Count == 1 && editaPlantilla;
                BT_Duplicar.IsEnabled = LB_Plantillas.SelectedItems.Count == 1 && editaPlantilla;
                BT_Ver.IsEnabled = LB_Plantillas.SelectedItems.Count == 1;
                BT_Eliminar.IsEnabled = LB_Plantillas.SelectedItems.Count > 0 && editaPlantilla;
                BT_AplicarAUnPlan.IsEnabled = LB_Plantillas.SelectedItems.Count == 1 && !((Plantilla)LB_Plantillas.SelectedItems[0]).esParaExtraccion;
                BT_AplicarPorLote.IsEnabled = LB_Plantillas.SelectedItems.Count == 1;
                BT_Configuracion.IsEnabled = editaPlantilla;
                BT_ExtraerDePlantilla.IsEnabled = LB_Plantillas.SelectedItems.Count == 1 && editaPlantilla;
            }
        }

        private void BT_HabilitarEdicion_Click(object sender, RoutedEventArgs e)
        {
            if (editaPlantilla == false)
            {
                FormTB formTb = new FormTB("", false, true);
                formTb.Title = "Edición de plantillas";
                formTb.L_Texto.Text = "Ingrese contraseña para edición de plantillas";
                formTb.ShowDialog();
                if (formTb.DialogResult == true)
                {
                    editaPlantilla = true;
                    L_Editando.Visibility = Visibility.Visible;
                    BT_HabilitarEdicion.Content = "Deshabilitar Edición";
                }
            }
            else
            {
                editaPlantilla = false;
                L_Editando.Visibility = Visibility.Collapsed;
                BT_HabilitarEdicion.Content = "Habilitar Edición";
            }
            habilitarBotones();
        }

        private void BT_Configuracion_Click(object sender, RoutedEventArgs e)
        {
            FormConfiguracion formConfiguracion = new FormConfiguracion();
            formConfiguracion.ShowDialog();
            leerPlantillas();
        }

        private void BT_NuevaConCondiciones_Click(object sender, RoutedEventArgs e)
        {
            crearPlantilla_ext = new Form1_ext(this, false);
            crearPlantilla_ext.ShowDialog();
        }

        public void eliminarArchivosParesEstructura(int meses)
        {
            string pathParEstructuras = Properties.Settings.Default.Path + @"\paresEstructuras\";
            string[] archivos = Directory.GetFiles(pathParEstructuras);
            foreach (string archivo in archivos)
            {
                FileInfo fi = new FileInfo(archivo);
                if (fi.CreationTime < DateTime.Now.AddMonths(-meses))
                {
                    File.Delete(archivo);
                }
            }
        }

        private void BT_ExtraerDePlantilla_Click(object sender, RoutedEventArgs e)
        {
            FormTB formTb = new FormTB(((Plantilla)(LB_Plantillas.SelectedItem)).etiqueta);
            formTb.Title = "Extraer de plantilla";
            formTb.L_Texto.Text = "Ingrese el nombre de la plantilla";
            formTb.CHB_Extra.Visibility = Visibility.Visible;
            formTb.CHB_Extra.Content = "Buscar solo planes aprobados";
            formTb.ShowDialog();
            if (formTb.DialogResult == true)
            {
                Mineria.escribirArchivo(Mineria.listaPlantillas(formTb.salida, formTb.CHB_Extra.IsChecked == true));
            }
        }

        private bool PlanYModSonIguales(PlanSetup plan, PlanSetup planMod)
        {
            if (plan.PlanNormalizationValue != planMod.PlanNormalizationValue)
            {
                MessageBox.Show("El plan " + plan.Id + " y el plan " + planMod.Id + " tienen diferente normalización.\nRevisar antes de continuar");
                return false;
            }
            else if (plan.Beams.Where(p => !p.IsSetupField).Count() != planMod.Beams.Where(p => !p.IsSetupField).Count())
            {
                MessageBox.Show("El plan " + plan.Id + " y el plan " + planMod.Id + " tienen diferente número de campos.\nRevisar antes de continuar");
                return false;
            }
            for (int i = 0; i < plan.Beams.Where(p => !p.IsSetupField).Count(); i++)
            {
                if (Math.Round(planMod.Beams.Where(p => !p.IsSetupField).ElementAt(i).Meterset.Value, 2) != 0 && Math.Round(plan.Beams.Where(p => !p.IsSetupField).ElementAt(i).Meterset.Value, 2) != Math.Round(planMod.Beams.Where(p => !p.IsSetupField).ElementAt(i).Meterset.Value, 2))
                {
                    MessageBox.Show("El plan " + plan.Id + " y el plan " + planMod.Id + " tienen diferentes UMs (y distintas de 0) en algunos de sus campos.\nRevisar antes de continuar");
                    return false;
                }
            }
            return true;
        }
    }
}
