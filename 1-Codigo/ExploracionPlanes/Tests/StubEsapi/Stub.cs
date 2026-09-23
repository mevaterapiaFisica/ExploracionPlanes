using System;
using System.Collections.Generic;

// Stub minimo de ESAPI: solo la superficie de tipos/miembros que el codigo de produccion
// referencia en FIRMAS de metodo (IRestriccion, Condicion, Estructura, Plantilla, etc.) para
// poder compilar. DesdeCSV.GenerarPlantillasUnificadas nunca llama a ninguno de estos metodos
// en runtime (solo arma datos y los serializa) -> los cuerpos no importan, solo las firmas.

namespace VMS.TPS.Common.Model.Types
{
    public enum VolumePresentation { Relative, AbsoluteCm3 }
    public enum DoseValuePresentation { Absolute, Relative }

    public class DoseValue
    {
        public enum DoseUnit { cGy, Gy, Percent, Unknown }
        public double Dose { get; }
        public DoseUnit Unit { get; }
        public DoseValue(double dose, DoseUnit unit) { Dose = dose; Unit = unit; }
        public static DoseValue UndefinedDose() => new DoseValue(double.NaN, DoseUnit.Unknown);
    }

    public class DVHPoint
    {
        public double Volume { get; set; }
        public DoseValue DoseValue { get; set; } = new DoseValue(0, DoseValue.DoseUnit.Gy);
        public string VolumeUnit { get; set; } = "";
    }

    public class DVHData
    {
        public DVHPoint[] CurveData { get; set; } = Array.Empty<DVHPoint>();
        public double SamplingCoverage { get; set; }
        public DoseValue MeanDose { get; set; } = new DoseValue(0, DoseValue.DoseUnit.Gy);
    }

    public struct VVector
    {
        public double x, y, z;
    }
}

namespace VMS.TPS.Common.Model.API
{
    using VMS.TPS.Common.Model.Types;

    public class Patient
    {
        public string Id { get; set; } = "";
    }

    public class Course
    {
        public string Id { get; set; } = "";
    }

    public class Structure
    {
        public string Id { get; set; } = "";
        public double Volume { get; set; }
        public string DicomType { get; set; } = "";
        public bool IsEmpty { get; set; }
    }

    public class Image
    {
        public VVector UserOrigin { get; set; }
    }

    public class StructureSet
    {
        public IEnumerable<Structure> Structures { get; set; } = Array.Empty<Structure>();
        public Image Image { get; set; } = new Image();
    }

    public class ControlPointCollection : List<object> { }

    public class Beam
    {
        public ControlPointCollection ControlPoints { get; set; } = new ControlPointCollection();
        public VVector IsocenterPosition { get; set; }
    }

    public class Fractionation
    {
        public double NumberOfFractions { get; set; }
        public DoseValue PrescribedDosePerFraction { get; set; } = DoseValue.UndefinedDose();
        public DoseValue DosePerFractionInPrimaryRefPoint { get; set; } = DoseValue.UndefinedDose();
    }

    public abstract class PlanningItem
    {
        public string Id { get; set; } = "";
        public StructureSet StructureSet { get; set; } = new StructureSet();

        public virtual DVHData GetDVHCumulativeData(Structure structure, DoseValuePresentation dosePresentation, VolumePresentation volumePresentation, double binWidth)
            => throw new NotImplementedException("stub ESAPI: no se ejecuta durante GenerarPlantillasUnificadas");
    }

    public class PlanSetup : PlanningItem
    {
        public Fractionation UniqueFractionation { get; set; } = new Fractionation();

        // Aplanado equivalente a Eclipse 15.6/18.2 (ver EsapiCompat.cs), delega en UniqueFractionation
        // para que setear una u otra forma en un test actualice la misma data.
        public double NumberOfFractions
        {
            get => UniqueFractionation.NumberOfFractions;
            set => UniqueFractionation.NumberOfFractions = value;
        }
        public DoseValue PrescribedDosePerFraction
        {
            get => UniqueFractionation.PrescribedDosePerFraction;
            set => UniqueFractionation.PrescribedDosePerFraction = value;
        }
        public DoseValue DosePerFractionInPrimaryRefPoint
        {
            get => UniqueFractionation.DosePerFractionInPrimaryRefPoint;
            set => UniqueFractionation.DosePerFractionInPrimaryRefPoint = value;
        }

        public List<Beam> Beams { get; set; } = new List<Beam>();
        public Course Course { get; set; } = new Course();

        public double GetVolumeAtDose(Structure structure, DoseValue dose, VolumePresentation volumePresentation)
            => throw new NotImplementedException("stub ESAPI: no se ejecuta durante GenerarPlantillasUnificadas");

        public DoseValue GetDoseAtVolume(Structure structure, double volume, VolumePresentation volumePresentation, DoseValuePresentation dosePresentation)
            => throw new NotImplementedException("stub ESAPI: no se ejecuta durante GenerarPlantillasUnificadas");
    }

    public class PlanSum : PlanningItem
    {
        public List<PlanSetup> PlanSetups { get; set; } = new List<PlanSetup>();
        public Course Course { get; set; } = new Course();
    }
}
