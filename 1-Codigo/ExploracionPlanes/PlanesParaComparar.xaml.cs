using System.Collections.Generic;
using System.Linq;
using System.Windows;
using VMS.TPS.Common.Model.API;

namespace ExploracionPlanes
{
    public partial class PlanesParaComparar : DialogoWpf
    {
        public PlanningItem planParaComparar = null;
        public List<PlanningItem> planesContext;

        public PlanesParaComparar(List<PlanningItem> _planesContext)
        {
            InitializeComponent();
            planesContext = _planesContext;
            LB_PlanesComparar.ItemsSource = planesContext.ToList();
        }

        private void BT_Selecccionar_Click(object sender, RoutedEventArgs e)
        {
            planParaComparar = (PlanningItem)LB_PlanesComparar.SelectedItem;
            Close();
        }
    }
}
