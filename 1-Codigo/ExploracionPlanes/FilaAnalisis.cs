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
        private string esperado;
        private string referencia;
        private Brush fondoEnPlan = Brushes.Transparent;
        private Brush fondoMetrica = Brushes.Transparent;
        private bool oculta;
        private bool esDmax;
        private string volumenDmaxTexto;

        public string Estructura { get => estructura; set => Set(ref estructura, value); }
        public string Prioridad { get => prioridad; set => Set(ref prioridad, value); }
        public string Metrica { get => metrica; set => Set(ref metrica, value); }
        public string Volumen { get => volumen; set => Set(ref volumen, value); }
        public string EnPlan { get => enPlan; set => Set(ref enPlan, value); }
        public string Esperado { get => esperado; set => Set(ref esperado, value); }
        public string Referencia { get => referencia; set => Set(ref referencia, value); }

        // Solo usados por Form2/Form2_DosPlanes (análisis real de un plan); PlantillaBlanco no los toca.
        public Brush FondoEnPlan { get => fondoEnPlan; set => Set(ref fondoEnPlan, value); }
        // ponytail: preserva un bug preexistente — al editar el volumen de Dmax por botón, el
        // código original pinta la celda "Métrica" (Cells[2]) en vez de "En Plan" (Cells[4]).
        // No se corrige acá; ver Tests.md/UI.md.
        public Brush FondoMetrica { get => fondoMetrica; set => Set(ref fondoMetrica, value); }
        public bool Oculta { get => oculta; set => Set(ref oculta, value); }
        public bool EsDmax { get => esDmax; set => Set(ref esDmax, value); }
        public string VolumenDmaxTexto { get => volumenDmaxTexto; set => Set(ref volumenDmaxTexto, value); }

        // Referencia a la restricción real de esta fila — permite que el click de la columna de
        // botón (Dmax) opere sobre el objeto correcto sin depender del índice visual de la fila
        // (a diferencia del DataGridView original, que indexaba por posición y podía desalinearse
        // si una restricción anterior no cumplía condición y no llegaba a agregarse a la grilla).
        public IRestriccion Restriccion { get; set; }
    }
}
