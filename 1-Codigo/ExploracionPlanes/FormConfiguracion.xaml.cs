using System;
using System.Windows;

namespace ExploracionPlanes
{
    public partial class FormConfiguracion : DialogoWpf
    {
        public FormConfiguracion()
        {
            InitializeComponent();
            TB_Ruta.Text = Properties.Settings.Default.Path;
            TB_VolumenDM.Text = Properties.Settings.Default.VolDosisMax.ToString();
        }

        private void BT_Guardar_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.Path = TB_Ruta.Text;
            Properties.Settings.Default.VolDosisMax = Convert.ToDouble(TB_VolumenDM.Text);
            Properties.Settings.Default.Save();
            Close();
        }

        private void BT_Cancelar_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BT_SeleccionarRuta_Click(object sender, RoutedEventArgs e)
        {
            var fbd = new System.Windows.Forms.FolderBrowserDialog();
            fbd.SelectedPath = Properties.Settings.Default.Path;
            if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                TB_Ruta.Text = fbd.SelectedPath;
            }
        }
    }
}
