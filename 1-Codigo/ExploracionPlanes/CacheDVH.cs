using System.Collections.Generic;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace ExploracionPlanes
{
    // Comparar dos planes (o cualquier análisis contra un PlanSum) pide el DVH completo de una
    // estructura por ESAPI (GetDVHCumulativeData) una vez por restricción, aunque varias restricciones
    // de la misma plantilla apunten a la misma estructura - cada llamada recalcula el histograma
    // completo (overlap estructura/grilla de dosis) en el servidor, sin cache propio de ESAPI para
    // PlanSum. Esta clase comparte ese resultado entre restricciones de un mismo análisis.
    public static class CacheDVH
    {
        private static readonly Dictionary<(PlanningItem, Structure, VolumePresentation), DVHData> cache = new Dictionary<(PlanningItem, Structure, VolumePresentation), DVHData>();

        // Llamar al arrancar cada análisis (llenarDGVAnalisis) para no arrastrar DVHData de un plan/paciente anterior.
        public static void Limpiar()
        {
            cache.Clear();
        }

        public static DVHData Obtener(PlanningItem plan, Structure estructura, VolumePresentation volumePresentation, DoseValuePresentation doseValuePresentation = DoseValuePresentation.Absolute, double binWidth = 0.01)
        {
            var clave = (plan, estructura, volumePresentation);
            if (!cache.TryGetValue(clave, out DVHData dvhData))
            {
                dvhData = plan.GetDVHCumulativeData(estructura, doseValuePresentation, volumePresentation, binWidth);
                cache[clave] = dvhData;
            }
            return dvhData;
        }
    }
}
