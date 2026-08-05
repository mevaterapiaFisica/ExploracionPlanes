using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ExploracionPlanes
{
    public partial class Form1_prioridades : DialogoWpf
    {
        BindingList<IRestriccion> listaRestricciones = new BindingList<IRestriccion>();
        bool editaRestriccion = false;
        bool editaPlantilla = false;
        bool restriccionCondicionada = false;
        IRestriccion restriccionActualConCondicion;
        IRestriccion restriccionActualCondicionante;
        Condicion condicionActual = null;
        Main main = new Main();

        public Form1_prioridades(Main _main, bool _editaPlantilla)
        {
            InitializeComponent();
            CB_MenorOMayor.SelectedIndex = 0;
            CB_TipoRestriccion.SelectedIndex = 0;
            foreach (string opcion in new[] { "", "1", "2", "3", "4", "5" })
            {
                CB_prioridad.Items.Add(opcion);
            }
            LB_listaRestricciones.ItemsSource = listaRestricciones;
            listaRestricciones.ListChanged += (s, e) => RefrescarLista();
            CB_Estructura.AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler(CB_Estructura_TextChanged));
            editaPlantilla = _editaPlantilla;
            main = _main;
            if (_editaPlantilla)
            {
                var plantilla = main.plantillaSeleccionada();
                TB_NombrePlantilla.Text = plantilla.nombre;
                if (plantilla.esParaExtraccion)
                {
                    CHB_esParaExtraccion.IsChecked = true;
                }
                foreach (IRestriccion restriccion in plantilla.listaRestricciones)
                {
                    listaRestricciones.Add(restriccion);
                }
                TB_NotaPlantilla.Text = plantilla.nota;
            }
        }

        private void RefrescarLista()
        {
            CollectionViewSource.GetDefaultView(LB_listaRestricciones.ItemsSource)?.Refresh();
        }

        private Estructura estructura()
        {
            return Estructura.crear(CB_Estructura.Text, estructuraNombresAlt());
        }

        private string notaRestriccion()
        {
            return TB_NotaRestriccion.Text;
        }

        private List<string> estructuraNombresAlt()
        {
            if (!string.IsNullOrEmpty(TB_EstructuraNombresAlt.Text))
            {
                return Regex.Split(TB_EstructuraNombresAlt.Text, "\r\n").ToList<string>();
            }
            else
            {
                return new List<string>();
            }
        }
        private double valorCorrespondiente()
        {
            if (!string.IsNullOrEmpty(TB_CorrespA.Text))
            {
                return Metodos.validarYConvertirADouble(TB_CorrespA.Text);
            }
            else
            {
                return double.NaN;
            }
        }

        private string nombrePlantilla()
        {
            return TB_NombrePlantilla.Text;
        }

        private bool esParaExtraccion()
        {
            return CHB_esParaExtraccion.IsChecked == true;
        }

        private string notaPlantilla()
        {
            return TB_NotaPlantilla.Text;
        }

        private Plantilla plantillaActual()
        {
            return Plantilla.crear(nombrePlantilla(), esParaExtraccion(), listaRestricciones, notaPlantilla());
        }

        private double valorEsperado()
        {
            if (!string.IsNullOrEmpty(TB_ValorEsperado.Text))
            {
                return Metodos.validarYConvertirADouble(TB_ValorEsperado.Text);
            }
            else
            {
                return double.NaN;
            }
        }

        private double valorTolerado()
        {
            if (!string.IsNullOrEmpty(TB_ValorTolerado.Text))
            {
                return Metodos.validarYConvertirADouble(TB_ValorTolerado.Text);
            }
            else
            {
                return double.NaN;
            }
        }
        private string unidadValor()
        {
            return textoSeleccionado(CB_ValorEsperadoUnidades);
        }

        private string unidadCorrespondiente()
        {
            return textoSeleccionado(CB_CorrespAUnidades);
        }

        private string textoSeleccionado(ComboBox cb)
        {
            return cb.SelectedItem as string ?? (cb.SelectedItem as ComboBoxItem)?.Content as string ?? "";
        }

        private bool esRestriccionDosis()
        {
            return CB_TipoRestriccion.SelectedIndex == 0;
        }

        private bool esRestriccionDmedia()
        {
            return CB_TipoRestriccion.SelectedIndex == 1;
        }

        private bool esRestriccionDmax()
        {
            return CB_TipoRestriccion.SelectedIndex == 2;
        }

        private bool esRestriccionVolumen()
        {
            return CB_TipoRestriccion.SelectedIndex == 3;
        }

        private bool esRestriccionIndiceConformidad()
        {
            return CB_TipoRestriccion.SelectedIndex == 4;
        }

        private bool esMenorQue()
        {
            return CB_MenorOMayor.SelectedIndex == 0;
        }

        private string prioridad()
        {
            if (CB_prioridad.Text == "1" || CB_prioridad.Text == "2" || CB_prioridad.Text == "3" || CB_prioridad.Text == "4")
            {
                return CB_prioridad.Text;
            }
            else
            {
                return "";
            }
        }

        private void cargarUnidadesDosis(ComboBox cb)
        {
            cb.Items.Clear();
            cb.Items.Add("Gy");
            cb.Items.Add("%");
        }

        private void cargarUnidadesVolumen(ComboBox cb)
        {
            cb.Items.Clear();
            cb.Items.Add("%");
            cb.Items.Add("cm3");
        }

        private void BT_AgregarALista_Click(object sender, RoutedEventArgs e)
        {
            if (editaRestriccion)
            {
                int ubicacion = LB_listaRestricciones.SelectedIndex;
                listaRestricciones.Remove((IRestriccion)LB_listaRestricciones.SelectedItem);
                if (restriccionActual().condicion != null && (restriccionActual().condicion.tipo == Tipo.CondicionadaPor || restriccionActual().condicion.tipo == Tipo.CondicionaA) && !restriccionCondicionada)
                {
                    condicionActual = restriccionActual().condicion;
                }
                if (restriccionCondicionada)
                {
                    listaRestricciones.Insert(ubicacion, restriccionActualConCondicion);
                    restriccionCondicionada = false;
                    L_Condicionada.Visibility = Visibility.Collapsed;
                }
                else
                {
                    listaRestricciones.Insert(ubicacion, restriccionActual());
                }
                editaRestriccion = false;
                LB_listaRestricciones.IsEnabled = true;
                LB_listaRestricciones.UnselectAll();
                LB_listaRestricciones.SelectedIndex = ubicacion;
            }
            else
            {
                if (restriccionCondicionada)
                {
                    restriccionActualConCondicion.agregarALista(listaRestricciones);
                    restriccionCondicionada = false;
                    L_Condicionada.Visibility = Visibility.Collapsed;
                }
                else
                {
                    restriccionActual().agregarALista(listaRestricciones);
                }
                LB_listaRestricciones.UnselectAll();
            }
            limpiarPrescripcion();
            if (!CB_Estructura.Items.Contains(estructura().nombre))
            {
                CB_Estructura.Items.Add(estructura().nombre);
            }
            fijarEsParaExtraccion();
        }

        private void actualizarPorRestriccion()
        {
            if (esRestriccionDosis())
            {
                L_CorrespA.Text = "correspondiente a \nun volumen de: ";
                L_CorrespA.Visibility = Visibility.Visible;
                TB_CorrespA.Visibility = Visibility.Visible;
                CB_CorrespAUnidades.Visibility = Visibility.Visible;
                cargarUnidadesDosis(CB_ValorEsperadoUnidades);
                cargarUnidadesDosis(CB_ValorToleradoUnidades);
                cargarUnidadesVolumen(CB_CorrespAUnidades);
                CB_ValorEsperadoUnidades.SelectedIndex = 0;
                CB_CorrespAUnidades.SelectedIndex = 0;
                CB_ValorEsperadoUnidades.Visibility = Visibility.Visible;
                CB_ValorToleradoUnidades.Visibility = Visibility.Visible;
            }
            else if (esRestriccionDmedia() || esRestriccionDmax())
            {
                L_CorrespA.Visibility = Visibility.Collapsed;
                TB_CorrespA.Visibility = Visibility.Collapsed;
                CB_CorrespAUnidades.Visibility = Visibility.Collapsed;
                cargarUnidadesDosis(CB_ValorEsperadoUnidades);
                CB_ValorEsperadoUnidades.SelectedIndex = 0;
                CB_ValorEsperadoUnidades.Visibility = Visibility.Visible;
                CB_ValorToleradoUnidades.Visibility = Visibility.Visible;
            }
            else if (esRestriccionVolumen())
            {
                L_CorrespA.Text = "correspondiente a \nuna dosis de: ";
                L_CorrespA.Visibility = Visibility.Visible;
                TB_CorrespA.Visibility = Visibility.Visible;
                CB_CorrespAUnidades.Visibility = Visibility.Visible;
                cargarUnidadesDosis(CB_CorrespAUnidades);
                cargarUnidadesVolumen(CB_ValorEsperadoUnidades);
                cargarUnidadesVolumen(CB_ValorToleradoUnidades);
                CB_ValorEsperadoUnidades.SelectedIndex = 0;
                CB_CorrespAUnidades.SelectedIndex = 0;
                CB_ValorEsperadoUnidades.Visibility = Visibility.Visible;
                CB_ValorToleradoUnidades.Visibility = Visibility.Visible;
            }
            else //esRestriccionIndiceConformidad
            {
                L_CorrespA.Text = "definido para \nla curva del: ";
                L_CorrespA.Visibility = Visibility.Visible;
                TB_CorrespA.Visibility = Visibility.Visible;
                CB_CorrespAUnidades.Visibility = Visibility.Visible;
                cargarUnidadesDosis(CB_CorrespAUnidades);
                CB_CorrespAUnidades.IsEnabled = false;
                CB_CorrespAUnidades.SelectedIndex = 1;
                CB_ValorEsperadoUnidades.Visibility = Visibility.Collapsed;
                CB_ValorToleradoUnidades.Visibility = Visibility.Collapsed;
            }
        }

        private IRestriccion restriccionActual()
        {
            if (esRestriccionDosis())
            {
                return new RestriccionDosis().crear(estructura(), unidadValor(), unidadCorrespondiente(), esMenorQue(), valorEsperado(), valorTolerado(), valorCorrespondiente(), notaRestriccion(), condicionActual, prioridad());
            }
            else if (esRestriccionDmedia())
            {
                return new RestriccionDosisMedia().crear(estructura(), unidadValor(), unidadCorrespondiente(), esMenorQue(), valorEsperado(), valorTolerado(), valorCorrespondiente(), notaRestriccion(), condicionActual, prioridad());
            }
            else if (esRestriccionDmax())
            {
                return new RestriccionDosisMax().crear(estructura(), unidadValor(), unidadCorrespondiente(), esMenorQue(), valorEsperado(), valorTolerado(), valorCorrespondiente(), notaRestriccion(), condicionActual, prioridad());
            }
            else if (esRestriccionVolumen())
            {
                return new RestriccionVolumen().crear(estructura(), unidadValor(), unidadCorrespondiente(), esMenorQue(), valorEsperado(), valorTolerado(), valorCorrespondiente(), notaRestriccion(), condicionActual, prioridad());
            }
            else //esRestriccionIndiceConformidad
            {
                return new RestriccionIndiceConformidad().crear(estructura(), unidadValor(), unidadCorrespondiente(), esMenorQue(), valorEsperado(), valorTolerado(), valorCorrespondiente(), notaRestriccion(), condicionActual, prioridad());
            }
        }

        private void CB_TipoRestriccion_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            actualizarPorRestriccion();
            CB_ValorEsperadoUnidades_SelectionChanged(sender, e);
        }

        private void limpiarPrescripcion()
        {
            TB_CorrespA.Clear();
            TB_ValorEsperado.Clear();
            TB_ValorTolerado.Clear();
            CB_MenorOMayor.SelectedIndex = 0;
            CB_TipoRestriccion.SelectedIndex = 0;
            CB_CorrespAUnidades.SelectedIndex = 0;
            CB_ValorEsperadoUnidades.SelectedIndex = 0;
            TB_NotaRestriccion.Clear();
            CB_prioridad.Text = "";
        }

        private void limpiarPlantilla()
        {
            limpiarPrescripcion();
            CB_Estructura.Items.Clear();
            listaRestricciones.Clear();
            TB_NombrePlantilla.Clear();
            fijarEsParaExtraccion();
            TB_NotaPlantilla.Clear();
        }

        private void BT_GuardarPlantilla_Click(object sender, RoutedEventArgs e)
        {
            plantillaActual().guardar(editaPlantilla, main.plantillaSeleccionada());
            limpiarPlantilla();
            main.leerPlantillas();
            editaPlantilla = false;
            Close();
        }

        private void CB_ValorEsperadoUnidades_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CB_ValorToleradoUnidades.SelectedIndex = CB_ValorEsperadoUnidades.SelectedIndex;
        }

        private void CB_ValorToleradoUnidades_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CB_ValorEsperadoUnidades.SelectedIndex = CB_ValorToleradoUnidades.SelectedIndex;
        }

        private void BT_EliminarRestriccion_Click(object sender, RoutedEventArgs e)
        {
            List<IRestriccion> listaAEliminar = LB_listaRestricciones.SelectedItems.OfType<IRestriccion>().ToList();
            foreach (IRestriccion item in listaAEliminar)
            {
                listaRestricciones.Remove(item);
            }
            fijarEsParaExtraccion();
        }

        private void CB_Estructura_TextChanged(object sender, TextChangedEventArgs e)
        {
            TB_EstructuraNombresAlt.Clear();
            actualizarBotones();
        }

        private void CHB_esParaExtraccion_CheckedChanged(object sender, RoutedEventArgs e)
        {
            Panel_esMenorque.Visibility = esParaExtraccion() ? Visibility.Collapsed : Visibility.Visible;
            actualizarBotones();
        }

        private void fijarEsParaExtraccion()
        {
            CHB_esParaExtraccion.IsEnabled = LB_listaRestricciones.Items.Count == 0;
        }

        private void BT_EditarRestriccion_Click(object sender, RoutedEventArgs e)
        {
            LB_listaRestricciones.IsEnabled = false;
            var restriccion = (IRestriccion)LB_listaRestricciones.SelectedItem;
            var datos = restriccion.datosEdicion();
            CB_Estructura.Text = datos.NombreEstructura;
            TB_EstructuraNombresAlt.Text = datos.NombresAlt;
            CB_TipoRestriccion.SelectedIndex = datos.IndiceTipoRestriccion;
            CB_prioridad.Text = datos.Prioridad;
            if (datos.ValorCorrespondiente != null)
            {
                TB_CorrespA.Text = datos.ValorCorrespondiente;
            }
            CB_MenorOMayor.SelectedIndex = datos.EsMenorQue ? 0 : 1;
            TB_ValorEsperado.Text = datos.ValorEsperado;
            TB_ValorTolerado.Text = datos.ValorTolerado;
            CB_ValorEsperadoUnidades.SelectedItem = datos.UnidadValor;
            CB_CorrespAUnidades.SelectedItem = datos.UnidadCorrespondiente;
            TB_NotaRestriccion.Text = datos.Nota;

            BT_AgregarALista.Content = "Guardar";
            editaRestriccion = true;
            if (restriccion.condicion != null && restriccion.condicion.tipo == Tipo.CondicionadaPor)
            {
                L_Condicionada.Visibility = Visibility.Visible;
                L_Condicionada.Text = "Condicionada a\n" + restriccion.condicion.EtiquetaRestriccionAnidada;
                condicionActual = restriccion.condicion;
            }
            else
            {
                L_Condicionada.Visibility = Visibility.Collapsed;
                condicionActual = null;
            }
        }

        private void actualizarBotones(object sender, TextChangedEventArgs e) => actualizarBotones();
        private void actualizarBotones(object sender, SelectionChangedEventArgs e) => actualizarBotones();

        private void actualizarBotones()
        {
            BT_EliminarRestriccion.IsEnabled = LB_listaRestricciones.SelectedItems.Count > 0;
            BT_AplicarPrioridad.IsEnabled = LB_listaRestricciones.SelectedItems.Count > 0;
            BT_EvaluarEnPlanMod.IsEnabled = LB_listaRestricciones.SelectedItems.Count > 0;
            BT_AgregarNotaLote.IsEnabled = LB_listaRestricciones.SelectedItems.Count > 0;
            BT_EditarRestriccion.IsEnabled = LB_listaRestricciones.SelectedItems.Count == 1;
            BT_AgregarALista.IsEnabled = estaParaGrabarRestriccion();
            BT_GuardarPlantilla.IsEnabled = !string.IsNullOrEmpty(TB_NombrePlantilla.Text) && LB_listaRestricciones.Items.Count > 0;
            BT_RestriccionArriba.IsEnabled = LB_listaRestricciones.SelectedItems.Count == 1 && LB_listaRestricciones.SelectedIndex != 0;
            BT_RestriccionAbajo.IsEnabled = LB_listaRestricciones.SelectedItems.Count == 1 && LB_listaRestricciones.SelectedIndex != LB_listaRestricciones.Items.Count - 1;
        }

        private bool estaParaGrabarRestriccion()
        {
            return !string.IsNullOrEmpty(CB_Estructura.Text) && CB_TipoRestriccion.SelectedIndex != -1 &&
              (esParaExtraccion() || (CB_MenorOMayor.SelectedIndex != -1 && !string.IsNullOrEmpty(TB_ValorEsperado.Text)));
        }

        private void BT_RestriccionArriba_Click(object sender, RoutedEventArgs e)
        {
            int indice = LB_listaRestricciones.SelectedIndex;
            IRestriccion item = (IRestriccion)LB_listaRestricciones.SelectedItem;
            listaRestricciones.Remove(item);
            listaRestricciones.Insert(indice - 1, item);
            LB_listaRestricciones.UnselectAll();
            LB_listaRestricciones.SelectedIndex = indice - 1;
        }

        private void BT_RestriccionAbajo_Click(object sender, RoutedEventArgs e)
        {
            int indice = LB_listaRestricciones.SelectedIndex;
            IRestriccion item = (IRestriccion)LB_listaRestricciones.SelectedItem;
            listaRestricciones.Remove(item);
            listaRestricciones.Insert(indice + 1, item);
            LB_listaRestricciones.UnselectAll();
            LB_listaRestricciones.SelectedIndex = indice + 1;
        }

        private void Form1_prioridades_Closing(object sender, CancelEventArgs e)
        {
            if (listaRestricciones.Count > 0 && MessageBox.Show("Hay restricciones que no han sido guardadas \n ¿Desea salir sin guardar?", "Salir", MessageBoxButton.YesNo) == MessageBoxResult.No)
            {
                e.Cancel = true;
            }
        }

        private void BT_CargarDesdePaciente_Click(object sender, RoutedEventArgs e)
        {
            ImportarNombresEstructuras importarNombresEstructuras = new ImportarNombresEstructuras();
            importarNombresEstructuras.ShowDialog();
            if (importarNombresEstructuras.DialogResult == true && importarNombresEstructuras.nombresEstructurasSeleccionadas != null && importarNombresEstructuras.nombresEstructurasSeleccionadas.Count > 0)
            {
                foreach (string nombre in importarNombresEstructuras.nombresEstructurasSeleccionadas)
                {
                    CB_Estructura.Items.Add(nombre);
                }
            }
            importarNombresEstructuras.cerrarPaciente();
        }

        private void BT_CondicionadaAOtraRestricción_Click(object sender, RoutedEventArgs e)
        {
            Form_ListaRestricciones form_ListaRestricciones = new Form_ListaRestricciones(listaRestricciones);
            if (form_ListaRestricciones.ShowDialog() == true)
            {
                restriccionCondicionada = true;
                restriccionActualConCondicion = restriccionActual();
                restriccionActualConCondicion.condicion = new Condicion();
                restriccionActualConCondicion.condicion.tipo = Tipo.CondicionadaPor;
                restriccionActualConCondicion.condicion.EtiquetaRestriccionAnidada = form_ListaRestricciones.restriccionElegida.etiqueta;
                restriccionActualCondicionante = listaRestricciones.Where(r => r.etiqueta == form_ListaRestricciones.restriccionElegida.etiqueta).First();
                restriccionActualCondicionante.condicion = new Condicion();
                restriccionActualCondicionante.condicion.tipo = Tipo.CondicionaA;
                restriccionActualConCondicion.crearEtiqueta();
                restriccionActualCondicionante.condicion.EtiquetaRestriccionAnidada = restriccionActualConCondicion.etiqueta;
                L_Condicionada.Visibility = Visibility.Visible;
                L_Condicionada.Text = "Condicionada a\n" + restriccionActualCondicionante.etiqueta;
            }
        }

        private void BT_AplicarPrioridad_Click(object sender, RoutedEventArgs e)
        {
            FormTB formTB = new FormTB("", true, false, true);
            formTB.Title = "Definición de prioridades";
            formTB.L_Texto.Text = "Defina las prioridades";
            formTB.ShowDialog();
            List<IRestriccion> restriccionesSeleccionadas = LB_listaRestricciones.SelectedItems.Cast<IRestriccion>().ToList();
            foreach (IRestriccion restriccion in restriccionesSeleccionadas)
            {
                restriccion.prioridad = formTB.salida;
                restriccion.crearEtiqueta();
                int ubicacion = listaRestricciones.IndexOf(restriccion);
                listaRestricciones.Remove(restriccion);
                listaRestricciones.Insert(ubicacion, restriccion);
            }
        }

        private void BT_EvaluarEnPlanMod_Click(object sender, RoutedEventArgs e)
        {
            FormTB formTB = new FormTB("mod", false, false, true);
            formTB.Title = "Plan modificado";
            formTB.L_Texto.Text = "Sufijo del plan modificado\n(Dejar vacío para eliminar)";
            formTB.ShowDialog();
            List<IRestriccion> restriccionesSeleccionadas = LB_listaRestricciones.SelectedItems.Cast<IRestriccion>().ToList();
            foreach (IRestriccion restriccion in restriccionesSeleccionadas)
            {
                restriccion.planMod = formTB.salida;
                restriccion.crearEtiqueta();
                int ubicacion = listaRestricciones.IndexOf(restriccion);
                listaRestricciones.Remove(restriccion);
                listaRestricciones.Insert(ubicacion, restriccion);
            }
        }

        private void BT_AgregarNotaLote_Click(object sender, RoutedEventArgs e)
        {
            FormTB formTB = new FormTB("", false, false, true);
            formTB.Title = "Agregar nota";
            formTB.L_Texto.Text = "Nota para las estructuras seleccionadas\n(Dejar vacío para eliminar)";
            formTB.ShowDialog();
            List<IRestriccion> restriccionesSeleccionadas = LB_listaRestricciones.SelectedItems.Cast<IRestriccion>().ToList();
            foreach (IRestriccion restriccion in restriccionesSeleccionadas)
            {
                restriccion.nota = formTB.salida;
                restriccion.crearEtiqueta();
                int ubicacion = listaRestricciones.IndexOf(restriccion);
                listaRestricciones.Remove(restriccion);
                listaRestricciones.Insert(ubicacion, restriccion);
            }
        }
    }
}
