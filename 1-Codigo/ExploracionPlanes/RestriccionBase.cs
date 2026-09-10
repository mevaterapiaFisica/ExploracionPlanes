using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.ComponentModel;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace ExploracionPlanes
{
    // Base común a los 6 tipos de restricción (RestriccionDosis, RestriccionDosisMax,
    // RestriccionDosisMedia, RestriccionVolumen, RestriccionVolumenCritico,
    // RestriccionIndiceConformidad). Cada subclase solo implementa lo que de verdad cambia entre
    // tipos: el prefijo de etiqueta (crearEtiquetaInicio), cómo se mide contra el plan
    // (analizarPlanEstructura) y cómo arma sus campos propios (crear). Todo lo demás (evaluación de
    // tolerancia, sampling coverage, edición en grupo) era código idéntico copiado 6 veces.
    public abstract class RestriccionBase : IRestriccion
    {
        public Condicion condicion { get; set; }
        public Estructura estructura { get; set; }
        public string unidadValor { get; set; }
        public string unidadCorrespondiente { get; set; }
        public bool esMenorQue { get; set; }
        public double valorCorrespondiente { get; set; }
        public double valorMedido { get; set; }
        public double valorEsperado { get; set; }
        public double valorTolerado { get; set; }
        public double prescripcionEstructura { get; set; }
        public string etiquetaInicio { get; set; }
        public string etiqueta { get; set; }
        public string nota { get; set; }
        public string prioridad { get; set; }
        public string planMod { get; set; }

        // Índice de CB_TipoRestr/LB_TipoCondicion en el editor de plantillas (Form1_ext/Form1_prioridades) - uno fijo por tipo concreto.
        protected abstract int IndiceTipoRestriccion { get; }

        // RestriccionIndiceConformidad no agrega la unidad de valor a la etiqueta (su valorMedido ya es un índice adimensional, no una dosis/volumen con unidad propia).
        protected virtual bool IncluirUnidadValorEnEtiqueta => true;

        public abstract void crearEtiquetaInicio();
        public abstract void analizarPlanEstructura(PlanningItem plan, Structure estructura);
        public abstract void analizarPlanEstructura(PlanningItem plan, Structure estructura, double alfaBeta, int numeroFracciones);
        public abstract IRestriccion crear(Estructura _estructura, string _unidadValor, string _unidadCorrespondiente, bool _esMenorQue,
            double _valorEsperado, double _valorTolerado, double _valorCorrespondiente, string _nota, Condicion _condicion = null, string _prioridad = "", string _planMod = "");

        public int cumple()
        {
            if (esMenorQue)
            {
                if (valorMedido <= valorEsperado)
                {
                    return 0;
                }
                else if (!Double.IsNaN(valorTolerado) && valorMedido <= valorTolerado)
                {
                    return 1;
                }
                else
                {
                    return 2;
                }
            }
            else
            {
                if (valorMedido >= valorEsperado)
                {
                    return 0;
                }
                else if (!Double.IsNaN(valorTolerado) && valorMedido >= valorTolerado)
                {
                    return 1;
                }
                else
                {
                    return 2;
                }
            }
        }

        public virtual void crearEtiqueta()
        {
            etiqueta = etiquetaInicio;
            if (!string.IsNullOrEmpty(prioridad))
            {
                etiqueta += " (p=" + prioridad + ") ";
            }
            if (!Double.IsNaN(valorEsperado))
            {
                if (esMenorQue)
                {
                    etiqueta += " < ";
                }
                else
                {
                    etiqueta += " > ";
                }
                etiqueta += valorEsperado.ToString();
                if (!Double.IsNaN(valorTolerado))
                {
                    etiqueta += " (" + valorTolerado.ToString() + ") ";
                }
                if (IncluirUnidadValorEnEtiqueta)
                {
                    etiqueta += unidadValor;
                }
            }
            if (condicion != null && (condicion.tipo == Tipo.NumFx || condicion.tipo == Tipo.VolPTV))
            {
                etiqueta += " (" + condicion.id + ")";
            }
            else if (condicion != null && (condicion.tipo == Tipo.CondicionadaPor))
            {
                etiqueta += " (" + condicion.EtiquetaRestriccionAnidada + ") ";
            }
            if (!string.IsNullOrEmpty(planMod))
            {
                etiqueta += "*";
            }
        }

        public bool chequearSamplingCoverage(PlanningItem plan, Structure estructura)
        {
            if (Double.IsNaN(valorMedido))
            {
                if (plan.GetDVHCumulativeData(estructura, DoseValuePresentation.Absolute, VolumePresentation.Relative, 0.01).SamplingCoverage < 0.9)
                {
                    return true;
                }
            }
            return false;
        }

        public void agregarALista(BindingList<IRestriccion> lista)
        {
            lista.Add(this);
        }

        // Default: la mayoría de los tipos mide/compara en unidadValor. RestriccionVolumen y
        // RestriccionVolumenCritico lo overridean (miran unidadCorrespondiente) y
        // RestriccionIndiceConformidad también (siempre en %) - diferencia real ya existente, no
        // unificada acá para no cambiar comportamiento.
        public virtual bool dosisEstaEnPorcentaje()
        {
            return unidadValor == "%";
        }

        private string construirNombresAlt()
        {
            string nombresAlt = null;
            for (int i = 1; i < estructura.nombresPosibles.Count; i++)
            {
                if (i > 1)
                {
                    nombresAlt += "\r\n";
                }
                nombresAlt += estructura.nombresPosibles[i];
            }
            return nombresAlt;
        }

        // RestriccionDosisMax/RestriccionDosisMedia no tienen "valor correspondiente" (no están
        // parametrizadas por una dosis/volumen de referencia) y lo dejan en null.
        protected virtual string valorCorrespondienteParaEdicion()
        {
            return Metodos.validarYConvertirAString(valorCorrespondiente);
        }

        public DatosEdicionRestriccion datosEdicion()
        {
            var datos = new DatosEdicionRestriccion();
            datos.NombreEstructura = estructura.nombre;
            datos.NombresAlt = construirNombresAlt();
            datos.IndiceTipoRestriccion = IndiceTipoRestriccion;
            datos.Prioridad = prioridad;
            datos.ValorCorrespondiente = valorCorrespondienteParaEdicion();
            datos.EsMenorQue = esMenorQue;
            datos.ValorEsperado = Metodos.validarYConvertirAString(valorEsperado);
            datos.ValorTolerado = Metodos.validarYConvertirAString(valorTolerado);
            datos.UnidadValor = unidadValor;
            datos.UnidadCorrespondiente = unidadCorrespondiente;
            datos.Nota = nota;
            return datos;
        }

        public void editarGrupo(List<IRestriccion> lista, DataGridView tabla, ComboBox CB_Estructura, TextBox TB_nombresAlt, ComboBox CB_TipoRestr, TextBox TB_valorCorrespondiente,
            ComboBox CB_UnidadesCorresp, ComboBox CB_EsMenorQue, ComboBox CB_UnidadesValor, TextBox TB_nota, ListBox LB_TipoCondicion, ListBox LB_ListaCondiciones)
        {
            CB_Estructura.Text = estructura.nombre;
            for (int i = 1; i < estructura.nombresPosibles.Count; i++)
            {
                if (i > 1)
                {
                    TB_nombresAlt.Text += "\r\n";
                }
                TB_nombresAlt.Text += estructura.nombresPosibles[i];
            }
            CB_TipoRestr.SelectedIndex = IndiceTipoRestriccion;
            TB_valorCorrespondiente.Text = Metodos.validarYConvertirAString(valorCorrespondiente);
            if (esMenorQue)
            {
                CB_EsMenorQue.SelectedIndex = 0;
            }
            else
            {
                CB_EsMenorQue.SelectedIndex = 1;
            }
            foreach (IRestriccion restriccion in lista)
            {
                int indice = tabla.Columns.Add(restriccion.condicion.id, restriccion.condicion.id);
                tabla.Rows[0].Cells[indice].Value = Metodos.validarYConvertirAString(restriccion.valorEsperado);
                tabla.Rows[1].Cells[indice].Value = Metodos.validarYConvertirAString(restriccion.valorTolerado);
            }
            CB_UnidadesValor.SelectedItem = unidadValor;
            CB_UnidadesCorresp.SelectedItem = unidadCorrespondiente;
            TB_nota.Text = nota;
            LB_TipoCondicion.SelectedItem = condicion.tipo;
        }

        public string metrica()
        {
            return etiquetaInicio.Split(':')[1];
        }

        public bool cumpleCondicion(PlanningItem plan)
        {
            if (condicion == null)
            {
                return true;
            }
            else
            {
                return condicion.CumpleCondicion(plan);
            }
        }

        public override string ToString()
        {
            return etiqueta;
        }
    }
}
