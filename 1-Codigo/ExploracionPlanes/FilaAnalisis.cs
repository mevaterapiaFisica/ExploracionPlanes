using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ExploracionPlanes
{
    // Fila bindeable a DataGrid WPF. Reemplaza el acceso directo por índice de celda
    // (Rows[i].Cells[j].Value) que usaba DataGridView en WinForms: asignar una propiedad
    // acá se refleja sola en la grilla, igual que antes se reflejaba asignar una celda.
    public class FilaAnalisis : INotifyPropertyChanged
    {
        private string estructura;
        private string prioridad;
        private string metrica;
        private string volumen;
        private string enPlan;
        private string esperado;
        private string referencia;

        public string Estructura { get => estructura; set => Set(ref estructura, value); }
        public string Prioridad { get => prioridad; set => Set(ref prioridad, value); }
        public string Metrica { get => metrica; set => Set(ref metrica, value); }
        public string Volumen { get => volumen; set => Set(ref volumen, value); }
        public string EnPlan { get => enPlan; set => Set(ref enPlan, value); }
        public string Esperado { get => esperado; set => Set(ref esperado, value); }
        public string Referencia { get => referencia; set => Set(ref referencia, value); }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Set<T>(ref T campo, T valor, [CallerMemberName] string nombrePropiedad = null)
        {
            campo = valor;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nombrePropiedad));
        }
    }
}
