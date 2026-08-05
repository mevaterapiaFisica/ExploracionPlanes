using System;
using System.IO;
using System.Globalization;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Text.RegularExpressions;

namespace ExploracionPlanes
{
    public partial class Form1_prioridades : Form
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
            LB_listaRestricciones.DataSource = listaRestricciones;
            LB_listaRestricciones.DisplayMember = "etiqueta";
            editaPlantilla = _editaPlantilla;
            main = _main;
            if (_editaPlantilla)
            {
                main.plantillaSeleccionada().editar(TB_NombrePlantilla, CHB_esParaExtraccion, listaRestricciones, TB_NotaPlantilla);
            }


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
                return Double.NaN;
            }
        }

        private string nombrePlantilla()
        {
            return TB_NombrePlantilla.Text;
        }

        private bool esParaExtraccion()
        {
            return CHB_esParaExtraccion.Checked;
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
            if (!String.IsNullOrEmpty(TB_ValorEsperado.Text))
            {
                return Metodos.validarYConvertirADouble(TB_ValorEsperado.Text);
            }
            else
            {
                return Double.NaN;
            }
        }

        private double valorTolerado()
        {
            if (!String.IsNullOrEmpty(TB_ValorTolerado.Text))
            {
                return Metodos.validarYConvertirADouble(TB_ValorTolerado.Text);
            }
            else
            {
                return Double.NaN;
            }
        }
        private string unidadValor()
        {
            return CB_ValorEsperadoUnidades.Text;
        }

        private string unidadCorrespondiente()
        {
            return CB_CorrespAUnidades.Text;
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
        private void BT_AgregarALista_Click(object sender, EventArgs e)
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
                    L_Condicionada.Visible = false;
                }
                else
                {
                    listaRestricciones.Insert(ubicacion, restriccionActual());
                }
                editaRestriccion = false;
                LB_listaRestricciones.Enabled = true;
                LB_listaRestricciones.ClearSelected();
                LB_listaRestricciones.SelectedIndex = ubicacion;
            }
            else
            {
                if (restriccionCondicionada)
                {
                    restriccionActualConCondicion.agregarALista(listaRestricciones);
                    restriccionCondicionada = false;
                    L_Condicionada.Visible = false;
                }
                else
                {
                    restriccionActual().agregarALista(listaRestricciones);
                }
                LB_listaRestricciones.ClearSelected();
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
                L_CorrespA.Visible = true;
                TB_CorrespA.Visible = true;
                CB_CorrespAUnidades.Visible = true;
                cargarUnidadesDosis(CB_ValorEsperadoUnidades);
                cargarUnidadesDosis(CB_ValorToleradoUnidades);
                cargarUnidadesVolumen(CB_CorrespAUnidades);
                CB_ValorEsperadoUnidades.SelectedIndex = 0;
                CB_CorrespAUnidades.SelectedIndex = 0;
                CB_ValorEsperadoUnidades.Visible = true;
                CB_ValorToleradoUnidades.Visible = true;
            }
            else if (esRestriccionDmedia() || esRestriccionDmax())
            {
                L_CorrespA.Visible = false;
                TB_CorrespA.Visible = false;
                CB_CorrespAUnidades.Visible = false;
                cargarUnidadesDosis(CB_ValorEsperadoUnidades);
                CB_ValorEsperadoUnidades.SelectedIndex = 0;
                CB_ValorEsperadoUnidades.Visible = true;
                CB_ValorToleradoUnidades.Visible = true;
            }
            else if (esRestriccionVolumen())
            {
                L_CorrespA.Text = "correspondiente a \nuna dosis de: ";
                L_CorrespA.Visible = true;
                TB_CorrespA.Visible = true;
                CB_CorrespAUnidades.Visible = true;
                cargarUnidadesDosis(CB_CorrespAUnidades);
                cargarUnidadesVolumen(CB_ValorEsperadoUnidades);
                cargarUnidadesVolumen(CB_ValorToleradoUnidades);
                CB_ValorEsperadoUnidades.SelectedIndex = 0;
                CB_CorrespAUnidades.SelectedIndex = 0;
                CB_ValorEsperadoUnidades.Visible = true;
                CB_ValorToleradoUnidades.Visible = true;
            }

            else //esRestriccionIndiceConformidad
            {
                L_CorrespA.Text = "definido para \nla curva del: ";
                L_CorrespA.Visible = true;
                TB_CorrespA.Visible = true;
                CB_CorrespAUnidades.Visible = true;
                cargarUnidadesDosis(CB_CorrespAUnidades);
                CB_CorrespAUnidades.Enabled = false;
                CB_CorrespAUnidades.SelectedIndex = 1;
                CB_ValorEsperadoUnidades.Visible = false;
                CB_ValorToleradoUnidades.Visible = false;
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

        private void CB_TipoRestriccion_SelectedIndexChanged(object sender, EventArgs e)
        {
            actualizarPorRestriccion();
            CB_ValorEsperadoUnidades_SelectedIndexChanged(sender, e);
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
            LB_listaRestricciones.DataSource = listaRestricciones;
            TB_NombrePlantilla.Clear();
            fijarEsParaExtraccion();
            TB_NotaPlantilla.Clear();
        }
        private void BT_GuardarPlantilla_Click(object sender, EventArgs e)
        {
            plantillaActual().guardar(editaPlantilla, main.plantillaSeleccionada());
            limpiarPlantilla();
            main.leerPlantillas();
            editaPlantilla = false;
            Close();
        }

        private void CB_ValorEsperadoUnidades_SelectedIndexChanged(object sender, EventArgs e)
        {
            CB_ValorToleradoUnidades.SelectedIndex = CB_ValorEsperadoUnidades.SelectedIndex;
        }

        private void CB_ValorToleradoUnidades_SelectedIndexChanged(object sender, EventArgs e)
        {
            CB_ValorEsperadoUnidades.SelectedIndex = CB_ValorToleradoUnidades.SelectedIndex;
        }

        private void BT_EliminarRestriccion_Click(object sender, EventArgs e)
        {
            List<IRestriccion> listaAEliminar = LB_listaRestricciones.SelectedItems.OfType<IRestriccion>().ToList();
            foreach (IRestriccion item in listaAEliminar)
            {
                listaRestricciones.Remove(item);
            }
            fijarEsParaExtraccion();
        }

        private void CB_Estructura_TextChanged(object sender, EventArgs e)
        {
            TB_EstructuraNombresAlt.Clear();
            actualizarBotones(sender, e);
        }

        private void CHB_esParaExtraccion_CheckedChanged(object sender, EventArgs e)
        {
            if (esParaExtraccion())
            {
                Panel_esMenorque.Visible = false;
            }
            else
            {
                Panel_esMenorque.Visible = true;
            }
            actualizarBotones(sender, e);
        }

        private void fijarEsParaExtraccion()
        {
            if (LB_listaRestricciones.Items.Count > 0)
            {
                CHB_esParaExtraccion.Enabled = false;
            }
            else
            {
                CHB_esParaExtraccion.Enabled = true;
            }
        }

        private void BT_EditarRestriccion_Click(object sender, EventArgs e)
        {
            LB_listaRestricciones.Enabled = false;
            ((IRestriccion)(LB_listaRestricciones.SelectedItem)).editar(CB_Estructura, TB_EstructuraNombresAlt, CB_TipoRestriccion, TB_CorrespA,
                CB_CorrespAUnidades, CB_MenorOMayor, TB_ValorEsperado, TB_ValorTolerado, CB_ValorEsperadoUnidades, TB_NotaRestriccion, CB_prioridad);
            BT_AgregarALista.Text = "Guardar";
            editaRestriccion = true;
            if (((IRestriccion)(LB_listaRestricciones.SelectedItem)).condicion != null && ((IRestriccion)(LB_listaRestricciones.SelectedItem)).condicion.tipo == Tipo.CondicionadaPor)
            {
                L_Condicionada.Visible = true;
                L_Condicionada.Text = "Condicionada a\n" + ((IRestriccion)(LB_listaRestricciones.SelectedItem)).condicion.EtiquetaRestriccionAnidada;
                condicionActual = ((IRestriccion)(LB_listaRestricciones.SelectedItem)).condicion;
            }
            else
            {
                L_Condicionada.Visible = false;
                condicionActual = null;
            }
        }




        private void actualizarBotones(object sender, EventArgs e)
        {
            Metodos.habilitarBoton(LB_listaRestricciones.SelectedItems.Count > 0, BT_EliminarRestriccion);
            Metodos.habilitarBoton(LB_listaRestricciones.SelectedItems.Count > 0, BT_AplicarPrioridad);
            Metodos.habilitarBoton(LB_listaRestricciones.SelectedItems.Count > 0, BT_EvaluarEnPlanMod);
            Metodos.habilitarBoton(LB_listaRestricciones.SelectedItems.Count > 0, BT_AgregarNotaLote);
            Metodos.habilitarBoton(LB_listaRestricciones.SelectedItems.Count == 1, BT_EditarRestriccion);
            Metodos.habilitarBoton(estaParaGrabarRestriccion(), BT_AgregarALista);
            Metodos.habilitarBoton(!string.IsNullOrEmpty(TB_NombrePlantilla.Text) && LB_listaRestricciones.Items.Count > 0, BT_GuardarPlantilla);
            Metodos.habilitarBoton(LB_listaRestricciones.SelectedItems.Count == 1 && LB_listaRestricciones.SelectedIndex != 0, BT_RestriccionArriba);
            Metodos.habilitarBoton(LB_listaRestricciones.SelectedItems.Count == 1 && LB_listaRestricciones.SelectedIndex != LB_listaRestricciones.Items.Count - 1, BT_RestriccionAbajo);
        }

        private bool estaParaGrabarRestriccion()
        {
            return !string.IsNullOrEmpty(CB_Estructura.Text) && CB_TipoRestriccion.SelectedIndex != -1 &&
              (esParaExtraccion() || (CB_MenorOMayor.SelectedIndex != -1 && !string.IsNullOrEmpty(TB_ValorEsperado.Text)));
        }



        private void BT_RestriccionArriba_Click(object sender, EventArgs e)
        {
            int indice = LB_listaRestricciones.SelectedIndex;
            IRestriccion item = (IRestriccion)LB_listaRestricciones.SelectedItem;
            listaRestricciones.Remove(item);
            listaRestricciones.Insert(indice - 1, item);
            LB_listaRestricciones.ClearSelected();
            LB_listaRestricciones.SelectedIndex = indice - 1;
        }

        private void BT_RestriccionAbajo_Click(object sender, EventArgs e)
        {
            int indice = LB_listaRestricciones.SelectedIndex;
            IRestriccion item = (IRestriccion)LB_listaRestricciones.SelectedItem;
            listaRestricciones.Remove(item);
            listaRestricciones.Insert(indice + 1, item);
            LB_listaRestricciones.ClearSelected();
            LB_listaRestricciones.SelectedIndex = indice + 1;
        }

        private void Form1_prioridades_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (listaRestricciones.Count > 0 && MessageBox.Show("Hay restricciones que no han sido guardadas \n ¿Desea salir sin guardar?", "Salir", MessageBoxButtons.YesNo) == DialogResult.No)
            {
                e.Cancel = true;
            }
        }

        private void BT_CargarDesdePaciente_Click(object sender, EventArgs e)
        {
            ImportarNombresEstructuras importarNombresEstructuras = new ImportarNombresEstructuras();
            importarNombresEstructuras.ShowDialog();
            if (importarNombresEstructuras.DialogResult==true && importarNombresEstructuras.nombresEstructurasSeleccionadas != null && importarNombresEstructuras.nombresEstructurasSeleccionadas.Count > 0)
            {
                foreach (string nombre in importarNombresEstructuras.nombresEstructurasSeleccionadas)
                {
                    CB_Estructura.Items.Add(nombre);
                }
            }
            importarNombresEstructuras.cerrarPaciente();
        }




        private void BT_CondicionadaAOtraRestricción_Click(object sender, EventArgs e)
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
                L_Condicionada.Visible = true;
                L_Condicionada.Text = "Condicionada a\n" + restriccionActualCondicionante.etiqueta;

                //restriccionActualConCondicion.agregarALista(listaRestricciones);
                /*listaRestricciones.Add(restriccionActualConCondicion);

                limpiarPrescripcion();
                if (!CB_Estructura.Items.Contains(estructura().nombre))
                {
                    CB_Estructura.Items.Add(estructura().nombre);
                }
                fijarEsParaExtraccion();
                MessageBox.Show("Se agregó la restricción a la lista");*/
            }
        }

        private void BT_AplicarPrioridad_Click(object sender, EventArgs e)
        {
            FormTB formTB = new FormTB("", true,false,true);
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

        private void BT_EvaluarEnPlanMod_Click(object sender, EventArgs e)
        {
            FormTB formTB = new FormTB("mod",false,false,true);
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

        private void BT_AgregarNotaLote_Click(object sender, EventArgs e)
        {
            FormTB formTB = new FormTB("",false,false,true);
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
