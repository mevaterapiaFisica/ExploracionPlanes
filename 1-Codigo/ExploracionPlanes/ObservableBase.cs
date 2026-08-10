using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ExploracionPlanes
{
    // Base chica para las filas bindeables a DataGrid WPF (FilaAnalisis, FilaEstructura,
    // FilaPrescripcion) — asignar una propiedad se refleja sola en la grilla, sin volver a
    // asignar el ItemsSource ni llamar Refresh().
    public class ObservableBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void Set<T>(ref T campo, T valor, [CallerMemberName] string nombrePropiedad = null)
        {
            campo = valor;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nombrePropiedad));
        }
    }
}
