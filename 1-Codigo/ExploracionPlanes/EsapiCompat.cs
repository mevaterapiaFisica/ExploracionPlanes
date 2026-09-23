using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

// PlanSetup.UniqueFractionation (objeto Fractionation con NumberOfFractions/PrescribedDosePerFraction/
// DosePerFractionInPrimaryRefPoint) existe solo en Eclipse 13.6. En 15.6 y 18.2 Varian aplano esas 3
// propiedades directo en PlanSetup y elimino Fractionation. Este shim aisla la diferencia para que el
// resto del codigo (y el stub de tests) llame siempre igual.
// ponytail: en 18.2, PrescribedDosePerFraction/DosePerFractionInPrimaryRefPoint ya estan marcadas
// [Obsolete] (sugieren DosePerFraction/PlannedDosePerFraction) pero siguen andando -> si Varian las
// saca en una version futura, actualizar la rama #else de este archivo (evidencia: warning CS0618
// al compilar ExploracionPlanes.Eclipse18_2.csproj).
static class EsapiCompat
{
    public static double NumeroFracciones(this PlanSetup plan)
    {
#if ECLIPSE13_6
        return (double)plan.UniqueFractionation.NumberOfFractions;
#else
        return (double)plan.NumberOfFractions;
#endif
    }

    public static DoseValue DosisPrescriptaPorFraccion(this PlanSetup plan)
    {
#if ECLIPSE13_6
        return plan.UniqueFractionation.PrescribedDosePerFraction;
#else
        return plan.PrescribedDosePerFraction;
#endif
    }

    public static DoseValue DosisPorFraccionEnPuntoRefPrimario(this PlanSetup plan)
    {
#if ECLIPSE13_6
        return plan.UniqueFractionation.DosePerFractionInPrimaryRefPoint;
#else
        return plan.DosePerFractionInPrimaryRefPoint;
#endif
    }
}
