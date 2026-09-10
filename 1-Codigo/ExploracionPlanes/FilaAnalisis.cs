using System.Windows.Media;

namespace ExploracionPlanes
{
    // Fila bindeable a DataGrid WPF. Reemplaza el acceso directo por índice de celda
    // (Rows[i].Cells[j].Value) que usaba DataGridView en WinForms: asignar una propiedad
    // acá se refleja sola en la grilla, igual que antes se reflejaba asignar una celda.
    public class FilaAnalisis : ObservableBase
    {
        private string estructura;
        private string prioridad;
        private string metrica;
        private string volumen;
        private string enPlan;
        private string enPlan2;
        private string esperado;
        private string referencia;
        private Brush fondoEnPlan = Brushes.Transparent;
        private Brush fondoEnPlan2 = Brushes.Transparent;
        private Brush fondoMetrica = Brushes.Transparent;
        private bool oculta;

        public string Estructura { get => estructura; set => Set(ref estructura, value); }
        public string Prioridad { get => prioridad; set => Set(ref prioridad, value); }
        public string Metrica { get => metrica; set => Set(ref metrica, value); }
        public string Volumen { get => volumen; set => Set(ref volumen, value); }
        public string EnPlan { get => enPlan; set => Set(ref enPlan, value); }
        // Solo usado por Form2_DosPlanes (comparación de dos planes).
        public string EnPlan2 { get => enPlan2; set => Set(ref enPlan2, value); }
        public string Esperado { get => esperado; set => Set(ref esperado, value); }
        public string Referencia { get => referencia; set => Set(ref referencia, value); }

        // Solo usados por Form2/Form2_DosPlanes (análisis real de un plan); PlantillaBlanco no los toca.
        public Brush FondoEnPlan { get => fondoEnPlan; set => Set(ref fondoEnPlan, value); }
        public Brush FondoEnPlan2 { get => fondoEnPlan2; set => Set(ref fondoEnPlan2, value); }
        public Brush FondoMetrica { get => fondoMetrica; set => Set(ref fondoMetrica, value); }
        public bool Oculta { get => oculta; set => Set(ref oculta, value); }

        // Referencia a la restricción real de esta fila — evita indexar por posición visual, que se
        // desalinea si una restricción anterior no cumple condición y no llega a agregarse a la grilla.
        public IRestriccion Restriccion { get; set; }
    }
}
