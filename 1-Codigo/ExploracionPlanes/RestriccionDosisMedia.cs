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
    public class RestriccionDosisMedia: RestriccionBase
    {
        protected override int IndiceTipoRestriccion => 1;
        protected override string valorCorrespondienteParaEdicion() => null;

        public override IRestriccion crear(Estructura _estructura, string _unidadValor, string _unidadCorrespondiente, bool _esMenorQue,
            double _valorEsperado, double _valorTolerado, double _valorCorrespondiente,string _nota, Condicion _condicion = null, string _prioridad = "", string _planMod="")
        {
            RestriccionDosisMedia restriccion = new RestriccionDosisMedia()
            {
                estructura = _estructura,
                unidadValor = _unidadValor,
                esMenorQue = _esMenorQue,
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
            etiquetaInicio = estructura.nombre + ": Dmedia";
        }

        public override void analizarPlanEstructura(PlanningItem plan, Structure estructura)
        {
            valorMedido = Math.Round(CacheDVH.Obtener(plan, estructura, VolumePresentation.Relative).MeanDose.Dose / 100, 1);
            if (unidadValor == "%")
            {
                valorMedido = Math.Round(valorMedido / prescripcionEstructura * 100,1); //extraigo en Gy y paso a porcentaje
            }
        }

        public override void analizarPlanEstructura(PlanningItem plan, Structure estructura, double alfaBeta, int numeroFracciones)
        {
            DVHPoint[] DVHData = CacheDVH.Obtener(plan, estructura, VolumePresentation.Relative).CurveData;
            double dmedia=0;
            double dmediaEQD2 = 0;
            for (int i=0;i<DVHData.Length-1;i++)
            {
                dmedia += (DVHData[i].Volume/100 - DVHData[i + 1].Volume/100) * DVHData[i].DoseValue.Dose/100;
                dmediaEQD2 += (DVHData[i].Volume/100 - DVHData[i + 1].Volume/100) * EQD2.Dosis2Gy(DVHData[i].DoseValue.Dose/100,alfaBeta,numeroFracciones);
            }
            valorMedido = Math.Round(dmediaEQD2, 1);
            if (unidadValor == "%")
            {
                double prescripcionEQD2 = EQD2.Dosis2Gy(prescripcionEstructura, alfaBeta, numeroFracciones);
                valorMedido = Math.Round(valorMedido / prescripcionEQD2 * 100, 1); //porcentaje relativo a la prescripción convertida a EQD2
            }
        }
    }
}
