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
    public class RestriccionIndiceConformidad : RestriccionBase
    {
        protected override int IndiceTipoRestriccion => 4;
        protected override bool IncluirUnidadValorEnEtiqueta => false;

        public override IRestriccion crear(Estructura _estructura, string _unidadValor, string _unidadCorrespondiente, bool _esMenorQue,
   double _valorEsperado, double _valorTolerado, double _valorCorrespondiente, string _nota, Condicion _condicion = null, string _prioridad = "", string _planMod="")

        {
            RestriccionIndiceConformidad restriccion = new RestriccionIndiceConformidad()
            {
                estructura = _estructura,
                esMenorQue = _esMenorQue,
                unidadCorrespondiente = _unidadCorrespondiente,
                valorCorrespondiente = _valorCorrespondiente, //qué isodosis busco
                valorEsperado = _valorEsperado,
                valorTolerado = _valorTolerado,
                nota =_nota,
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
            etiquetaInicio = estructura.nombre + ": IC" + " (" + valorCorrespondiente + "%)";
        }

        public override void analizarPlanEstructura(PlanningItem plan, Structure estructura)
        {
            Structure BODY;
            if (plan is PlanSetup)
            {
                BODY = ((PlanSetup)plan).StructureSet.Structures.Where(s => s.DicomType == "EXTERNAL").FirstOrDefault();
            }
            else
            {
                BODY = ((PlanSum)plan).StructureSet.Structures.Where(s => s.DicomType == "EXTERNAL").FirstOrDefault();
            }

            if (BODY == null)
            {
                MessageBox.Show("No se encuentra la estructura BODY. \nNo se puede analizar el I.C. de la estructura" + estructura.Id);
                valorMedido = Double.NaN;
            }
            else
            {
                double valorCorrespondienteGy = valorCorrespondiente * prescripcionEstructura / 100; //Convierto el % a Gy para extraer
                DoseValue dosis = new DoseValue(valorCorrespondienteGy * 100, DoseValue.DoseUnit.cGy); //y acá en cGy

                if (plan is PlanSetup)
                {
                    valorMedido = Math.Round(((PlanSetup)plan).GetVolumeAtDose(BODY, dosis, VolumePresentation.AbsoluteCm3) / estructura.Volume, 3);
                }
                else
                {
                    DVHPoint[] curveData = ((PlanSum)plan).GetDVHCumulativeData(BODY, DoseValuePresentation.Absolute, VolumePresentation.AbsoluteCm3, 0.01).CurveData;
                    valorMedido = Math.Round((DVHDataExtensions_ESAPIX.GetVolumeAtDose(curveData, dosis) / estructura.Volume), 3);
                }

            }
        }

        public override void analizarPlanEstructura(PlanningItem plan, Structure estructura, double alfaBeta, int numeroFracciones)
        {
            valorMedido = double.NaN;
        }

        public override bool dosisEstaEnPorcentaje() => true;
    }
}
