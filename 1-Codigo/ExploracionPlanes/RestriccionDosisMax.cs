using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace ExploracionPlanes
{
    public class RestriccionDosisMax : IRestriccion
    {
        public Condicion condicion { get; set; }
        public Estructura estructura { get; set; }
        public string unidadValor { get; set; }
        public string unidadCorrespondiente { get; set; }
        public bool esMenorQue { get; set; }
        public double valorMedido { get; set; }
        public double valorEsperado { get; set; }
        public double valorTolerado { get; set; }
        public double valorCorrespondiente { get; set; }
        public static double volumenDosisMaxima = Properties.Settings.Default.VolDosisMax;
        public double prescripcionEstructura { get; set; }
        public string etiquetaInicio { get; set; }
        public string etiqueta { get; set; }
        public string nota { get; set; }
        public string prioridad { get; set; }
        public string planMod { get; set; }

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

        

        public IRestriccion crear(Estructura _estructura, string _unidadValor, string _unidadCorrespondiente, bool _esMenorQue,
    double _valorEsperado, double _valorTolerable, double _valorCorrespondiente, string _nota, Condicion _condicion = null, string _prioridad = "", string _planMod="")
        {
            RestriccionDosisMax restriccion = new RestriccionDosisMax()
            {
                estructura = _estructura,
                unidadValor = _unidadValor,
                esMenorQue = _esMenorQue,
                valorEsperado = _valorEsperado,
                valorTolerado = _valorTolerable,
                nota = _nota,
                condicion = _condicion,
                prioridad = _prioridad,
                planMod = _planMod,
            };
            restriccion.crearEtiquetaInicio();
            restriccion.crearEtiqueta();
            return restriccion;
        }

        public void crearEtiquetaInicio()
        {
            etiquetaInicio = estructura.nombre + ": Dmax";
        }
        public void crearEtiqueta()
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
                etiqueta += unidadValor;
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





        private double dosisEnGy(PlanningItem plan, Structure estructura)
        {
            DoseValuePresentation doseValuePresentation = DoseValuePresentation.Absolute;
            if (plan is PlanSetup)
            {
                return Math.Round(((PlanSetup)plan).GetDoseAtVolume(estructura, volumenDosisMaxima, VolumePresentation.AbsoluteCm3, doseValuePresentation).Dose / 100, 2);
            }
            else
            {
                DVHPoint[] curveData = ((PlanSum)plan).GetDVHCumulativeData(estructura, doseValuePresentation, VolumePresentation.AbsoluteCm3, 0.01).CurveData;
                return Math.Round(DVHDataExtensions_ESAPIX.GetDoseAtVolume(curveData, volumenDosisMaxima).Dose / 100, 2);
            }
        }

        public void analizarPlanEstructura(PlanningItem plan, Structure estructura)
        {
            valorMedido = dosisEnGy(plan, estructura);
            if (unidadValor == "%")
            {
                valorMedido = Math.Round(valorMedido / prescripcionEstructura * 100,2); //extraigo en Gy y paso a porcentaje
            }
        }

        public void analizarPlanEstructura(PlanningItem plan, Structure estructura, double volumenDosisMaximaOVR) //Ver cuál sirve
        {
            DoseValuePresentation doseValuePresentation = DoseValuePresentation.Absolute;
            if (plan is PlanSetup)
            {
                valorMedido = Math.Round(((PlanSetup)plan).GetDoseAtVolume(estructura, volumenDosisMaximaOVR, VolumePresentation.AbsoluteCm3, doseValuePresentation).Dose / 100, 1);
            }
            else
            {
                DVHPoint[] curveData = ((PlanSum)plan).GetDVHCumulativeData(estructura, doseValuePresentation, VolumePresentation.AbsoluteCm3, 0.01).CurveData;
                valorMedido = Math.Round(DVHDataExtensions_ESAPIX.GetDoseAtVolume(curveData, volumenDosisMaximaOVR).Dose / 100, 1);
            }
            if (unidadValor == "%")
            {
                valorMedido = Math.Round(valorMedido / prescripcionEstructura * 100, 2); //extraigo en Gy y paso a porcentaje
            }
        }

        public void analizarPlanEstructura(PlanningItem plan, Structure estructura, double alfaBeta, int numeroFracciones)
        {
            valorMedido = Math.Round(EQD2.Dosis2Gy(dosisEnGy(plan, estructura), alfaBeta, numeroFracciones), 1);
            if (unidadValor == "%")
            {
                double prescripcionEQD2 = EQD2.Dosis2Gy(prescripcionEstructura, alfaBeta, numeroFracciones);
                valorMedido = Math.Round(valorMedido / prescripcionEQD2 * 100, 2); //porcentaje relativo a la prescripción convertida a EQD2
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

        public bool dosisEstaEnPorcentaje()
        {
            if (unidadValor == "%")
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public DatosEdicionRestriccion datosEdicion()
        {
            var datos = new DatosEdicionRestriccion();
            datos.NombreEstructura = estructura.nombre;
            for (int i = 1; i < estructura.nombresPosibles.Count; i++)
            {
                datos.NombresAlt += "\r\n" + estructura.nombresPosibles[i];
            }
            datos.IndiceTipoRestriccion = 2; //cambiar en cada restriccion
            datos.Prioridad = prioridad;
            datos.ValorCorrespondiente = null;
            datos.EsMenorQue = esMenorQue;
            datos.ValorEsperado = Metodos.validarYConvertirAString(valorEsperado);
            datos.ValorTolerado = Metodos.validarYConvertirAString(valorTolerado);
            datos.UnidadValor = unidadValor;
            datos.UnidadCorrespondiente = unidadCorrespondiente;
            datos.Nota = nota;
            return datos;
        }

        public void editarGrupo(List<IRestriccion> lista, DataGridView tabla, ComboBox CB_Estructura, TextBox TB_nombresAlt, ComboBox CB_TipoRestr, TextBox TB_valorCorrespondiente,
ComboBox CB_UnidadesCorresp, ComboBox CB_EsMenorQue, ComboBox CB_UnidadesValor, TextBox TB_nota,ListBox LB_TipoCondicion, ListBox LB_ListaCondiciones)
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
            CB_TipoRestr.SelectedIndex = 2; //cambiar en cada restriccion
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


