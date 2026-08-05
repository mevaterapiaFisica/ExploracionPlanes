using System.Collections.Generic;
using System.Windows;
using VMS.TPS.Common.Model.API;

namespace ExploracionPlanes
{
    public partial class SeleccionarPTV : DialogoWpf
    {
        public Structure ptv = null;
        public List<Structure> ptvs;

        public SeleccionarPTV(List<Structure> _ptvs)
        {
            InitializeComponent();
            ptvs = _ptvs;
            LB_PlanesComparar.ItemsSource = ptvs;
        }

        private void BT_Selecccionar_Click(object sender, RoutedEventArgs e)
        {
            ptv = (Structure)LB_PlanesComparar.SelectedItem;
            Close();
        }
    }
}
