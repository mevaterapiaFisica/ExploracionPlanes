using System.Collections.ObjectModel;

namespace ExploracionPlanes
{
    // Fila de DGV_Estructuras: un slot de la plantilla + el combo de estructuras reales del plan
    // que puede matchearle. Cada fila tiene su propia lista de opciones (ordenadas por parecido a
    // ese slot en particular), a diferencia de una columna de combo compartida por toda la grilla.
    public class FilaEstructura : ObservableBase
    {
        private string structureId;
        private string alfaBeta;

        public string NombreSlot { get; set; }
        public ObservableCollection<string> Opciones { get; set; } = new ObservableCollection<string>();
        public string StructureId { get => structureId; set => Set(ref structureId, value); }
        public string AlfaBeta { get => alfaBeta; set => Set(ref alfaBeta, value); }
    }
}
