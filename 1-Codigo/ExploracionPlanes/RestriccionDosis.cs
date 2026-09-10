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
    public class RestriccionDosis : RestriccionBase
    {
        protected override int IndiceTipoRestriccion => 0;

        public override IRestriccion crear(Estructura _estructura, string _unidadValor, string _unidadCorrespondiente, bool _esMenorQue,
   double _valorEsperado, double _valorTolerado, double _valorCorrespondiente, string _nota, Condicion _condicion = null, string _prioridad = "", string _planMod="")

        {
            RestriccionDosis restriccion = new RestriccionDosis()
            {
                estructura = _estructura,
                unidadValor = _unidadValor,
                unidadCorrespondiente = _unidadCorrespondiente,
                esMenorQue = _esMenorQue,
                valorCorrespondiente = _valorCorrespondiente,
                valorEsperado = _valorEsperado,
                valorTolerado = _valorTolerado,
                nota = _nota,
                condicion = _condicion,
                prioridad = _prioridad,
                planMod = _planMod,

            };
            restriccion.crearEtiquetaInicio();
            restriccion.crearEtiqueta();
            return restriccion;
        }

        public override void crearEtiquetaInicio()
        {
            etiquetaInicio = estructura.nombre + ": D" + valorCorrespondiente.ToString() + unidadCorrespondiente;
        }

        private double dosisEnGy(PlanningItem plan, Structure estructura)
        {
            VolumePresentation volumePresentation;
            DoseValuePresentation doseValuePresentation = DoseValuePresentation.Absolute;
            if (unidadCorrespondiente == "%")
            {
                volumePresentation = VolumePresentation.Relative;
            }
            else
            {
                volumePresentation = VolumePresentation.AbsoluteCm3;
            }
            if (plan is PlanSetup)
            {
                if (valorCorrespondiente == 100 && unidadCorrespondiente == "%")
                {
                    double volumen = 100 * (1 - 0.035 / estructura.Volume);
                    return Math.Round(((PlanSetup)plan).GetDoseAtVolume(estructura, volumen, volumePresentation, doseValuePresentation).Dose / 100, 1);
                }
                else
                {
                    return Math.Round(((PlanSetup)plan).GetDoseAtVolume(estructura, valorCorrespondiente, volumePresentation, doseValuePresentation).Dose / 100, 1);
                }
            }
            else
            {
                DVHPoint[] curveData = ((PlanSum)plan).GetDVHCumulativeData(estructura, doseValuePresentation, volumePresentation, 0.01).CurveData;
                if (valorCorrespondiente==100 && unidadCorrespondiente=="%")
                {
                    double volumen = 100 * (1 - 0.035 / estructura.Volume);
                    return Math.Round(DVHDataExtensions_ESAPIX.GetDoseAtVolume(curveData, volumen).Dose / 100, 1);
                }
                else
                {
                    return Math.Round(DVHDataExtensions_ESAPIX.GetDoseAtVolume(curveData, valorCorrespondiente).Dose / 100, 1);
                }
            }
        }

        public override void analizarPlanEstructura(PlanningItem plan, Structure estructura)
        {
            valorMedido = dosisEnGy(plan, estructura);
            if (unidadValor == "%")
            {
                valorMedido = Math.Round(valorMedido / prescripcionEstructura * 100, 2); //extraigo en Gy y paso a porcentaje
            }
        }

        public override void analizarPlanEstructura(PlanningItem plan, Structure estructura, double alfaBeta, int numeroFracciones)
        {
            valorMedido = Math.Round(EQD2.Dosis2Gy(dosisEnGy(plan, estructura), alfaBeta, numeroFracciones), 1);
            if (unidadValor == "%")
            {
                double prescripcionEQD2 = EQD2.Dosis2Gy(prescripcionEstructura, alfaBeta, numeroFracciones);
                valorMedido = Math.Round(valorMedido / prescripcionEQD2 * 100, 2); //porcentaje relativo a la prescripción convertida a EQD2
            }
        }
    }
}
