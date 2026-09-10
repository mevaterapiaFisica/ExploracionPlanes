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
    public class RestriccionDosisMax : RestriccionBase
    {
        // ponytail: propiedad calculada (no campo cacheado) para reflejar cambios de Configuración sin reiniciar la app.
        public static double volumenDosisMaxima => Configuracion.volDosisMaxima();

        protected override int IndiceTipoRestriccion => 2;
        protected override string valorCorrespondienteParaEdicion() => null;

        public override IRestriccion crear(Estructura _estructura, string _unidadValor, string _unidadCorrespondiente, bool _esMenorQue,
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

        public override void crearEtiquetaInicio()
        {
            etiquetaInicio = estructura.nombre + ": Dmax";
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

        public override void analizarPlanEstructura(PlanningItem plan, Structure estructura)
        {
            valorMedido = dosisEnGy(plan, estructura);
            if (unidadValor == "%")
            {
                valorMedido = Math.Round(valorMedido / prescripcionEstructura * 100,2); //extraigo en Gy y paso a porcentaje
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
