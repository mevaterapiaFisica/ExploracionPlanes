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
    // Volumen sano mínimo para una dosis dada: V_sano(D) = V_total - V(D). El constraint (esMenorQue/valorEsperado/valorTolerado)
    // se evalúa igual que RestriccionVolumen, pero contra V_sano en vez de contra V(D).
    public class RestriccionVolumenCritico : RestriccionBase
    {
        protected override int IndiceTipoRestriccion => 5;

        public override bool dosisEstaEnPorcentaje() => unidadCorrespondiente == "%";

        public override IRestriccion crear(Estructura _estructura, string _unidadValor, string _unidadCorrespondiente, bool _esMenorQue,
            double _valorEsperado, double _valorTolerado, double _valorCorrespondiente, string _nota, Condicion _condicion = null, string _prioridad = "", string _planMod = "")
        {
            RestriccionVolumenCritico restriccion = new RestriccionVolumenCritico()
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
            etiquetaInicio = estructura.nombre + ": " + "Vsano" + valorCorrespondiente.ToString() + unidadCorrespondiente;
        }

        public override void analizarPlanEstructura(PlanningItem plan, Structure estructura)
        {
            VolumePresentation volumePresentation;
            double valorCorrespondienteGy = valorCorrespondiente;
            if (unidadCorrespondiente == "%")
            {
                valorCorrespondienteGy = valorCorrespondiente * prescripcionEstructura / 100; //Convierto el % a Gy para extraer
            }
            DoseValue dosis = new DoseValue(valorCorrespondienteGy * 100, DoseValue.DoseUnit.cGy);

            if (unidadValor == "%")
            {
                volumePresentation = VolumePresentation.Relative;
            }
            else
            {
                volumePresentation = VolumePresentation.AbsoluteCm3;
            }
            double volumenADosis;
            if (plan is PlanSetup)
            {
                volumenADosis = ((PlanSetup)plan).GetVolumeAtDose(estructura, dosis, volumePresentation);
            }
            else
            {
                DVHPoint[] curveData = CacheDVH.Obtener(plan, estructura, volumePresentation).CurveData;
                volumenADosis = DVHDataExtensions_ESAPIX.GetVolumeAtDose(curveData, dosis);
            }
            valorMedido = Math.Round(volumenSano(volumenADosis, volumePresentation, estructura), 1);
        }
        public override void analizarPlanEstructura(PlanningItem plan, Structure estructura, double alfaBeta, int numeroFracciones)
        {
            VolumePresentation volumePresentation;
            double valorCorrespondienteGy = valorCorrespondiente;
            if (unidadCorrespondiente == "%")
            {
                double prescripcionEQD2 = EQD2.Dosis2Gy(prescripcionEstructura, alfaBeta, numeroFracciones);
                valorCorrespondienteGy = valorCorrespondiente * prescripcionEQD2 / 100; //Convierto el % (de la prescripción en EQD2) a Gy EQD2
            }
            DoseValue dosis = new DoseValue(EQD2.DosisFxAlt(valorCorrespondienteGy, alfaBeta, numeroFracciones) * 100, DoseValue.DoseUnit.cGy);

            if (unidadValor == "%")
            {
                volumePresentation = VolumePresentation.Relative;
            }
            else
            {
                volumePresentation = VolumePresentation.AbsoluteCm3;
            }
            double volumenADosis;
            if (plan is PlanSetup)
            {
                volumenADosis = ((PlanSetup)plan).GetVolumeAtDose(estructura, dosis, volumePresentation);
            }
            else
            {
                DVHPoint[] curveData = CacheDVH.Obtener(plan, estructura, volumePresentation).CurveData;
                volumenADosis = DVHDataExtensions_ESAPIX.GetVolumeAtDose(curveData, dosis);
            }
            valorMedido = Math.Round(volumenSano(volumenADosis, volumePresentation, estructura), 1);
        }

        // V_sano = V_total - V(D). En % ya es relativo al total (100% - V(D)%); en volumen absoluto se resta contra Structure.Volume (cm3).
        private static double volumenSano(double volumenADosis, VolumePresentation volumePresentation, Structure estructura)
        {
            if (volumePresentation == VolumePresentation.Relative)
            {
                return 100 - volumenADosis;
            }
            else
            {
                return estructura.Volume - volumenADosis;
            }
        }
    }
}
