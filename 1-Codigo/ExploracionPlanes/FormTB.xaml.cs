using System.Windows;

namespace ExploracionPlanes
{
    public partial class FormTB : DialogoWpf
    {
        public string salida { get; set; }
        public bool salidaDouble { get; set; }
        public bool esPasword { get; set; }
        public bool TBpuedeserEmpty { get; set; }
        public string password = "editaplantilla";

        private string Texto => esPasword ? PB_Llenar.Password : TB_Llenar.Text;

        public FormTB(string textoTB = "", bool _salidaDouble = false, bool _esPassword = false, bool _TBpuedeserEmpty = false)
        {
            InitializeComponent();
            salidaDouble = _salidaDouble;
            esPasword = _esPassword;
            TBpuedeserEmpty = _TBpuedeserEmpty;
            if (esPasword)
            {
                TB_Llenar.Visibility = Visibility.Collapsed;
                PB_Llenar.Visibility = Visibility.Visible;
                PB_Llenar.Password = textoTB;
            }
            else
            {
                TB_Llenar.Text = textoTB;
            }
            BT_Aceptar.IsEnabled = _TBpuedeserEmpty;
        }

        private void BT_Aceptar_Click(object sender, RoutedEventArgs e)
        {
            if (salidaDouble)
            {
                double aux = Metodos.validarYConvertirADouble(Texto);
                salida = Texto;
                if (double.IsNaN(aux))
                {
                    TB_Llenar.SelectAll();
                    return;
                }
                DialogResult = true;
                return;
            }

            salida = Texto;
            if (!esPasword || password == Texto)
            {
                DialogResult = true;
            }
            else
            {
                MessageBox.Show("La contraseña ingresada es incorrecta");
                PB_Llenar.Focus();
                PB_Llenar.SelectAll();
            }
        }

        private void ActualizarHabilitado()
        {
            if (!TBpuedeserEmpty)
            {
                BT_Aceptar.IsEnabled = !string.IsNullOrEmpty(Texto);
            }
        }

        private void TB_Llenar_TextChanged(object sender, RoutedEventArgs e) => ActualizarHabilitado();

        private void PB_Llenar_PasswordChanged(object sender, RoutedEventArgs e) => ActualizarHabilitado();
    }
}
