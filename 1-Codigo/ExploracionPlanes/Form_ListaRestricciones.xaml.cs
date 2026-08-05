using System.ComponentModel;
using System.Windows;

namespace ExploracionPlanes
{
    public partial class Form_ListaRestricciones : DialogoWpf
    {
        public IRestriccion restriccionElegida;

        public Form_ListaRestricciones(BindingList<IRestriccion> _restricciones)
        {
            InitializeComponent();
            LB_ListaRestricciones.ItemsSource = _restricciones;
        }

        private void BT_Aceptar_Click(object sender, RoutedEventArgs e)
        {
            restriccionElegida = (IRestriccion)LB_ListaRestricciones.SelectedItem;
            DialogResult = true;
        }
    }
}
