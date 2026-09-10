using System.Windows;

namespace ExploracionPlanes
{
    public partial class FormChequeos : DialogoWpf
    {
        public FormChequeos(string texto)
        {
            InitializeComponent();
            L_Texto.Text = string.IsNullOrEmpty(texto) ? "Todo bien" : texto;
        }

        private void BT_Aceptar_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
