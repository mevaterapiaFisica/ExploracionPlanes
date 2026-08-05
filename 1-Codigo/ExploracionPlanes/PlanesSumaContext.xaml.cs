using System.Collections.Generic;
using System.Linq;
using System.Windows;
using VMS.TPS.Common.Model.API;

namespace ExploracionPlanes
{
    public partial class PlanesSumaContext : DialogoWpf
    {
        public PlanSum PlanSuma = null;
        public IEnumerable<PlanSum> planSumsContext;

        public PlanesSumaContext(IEnumerable<PlanSum> _planSumsContext)
        {
            InitializeComponent();
            planSumsContext = _planSumsContext;
            LB_PlanesSuma.ItemsSource = planSumsContext.ToList();
        }

        private void BT_Selecccionar_Click(object sender, RoutedEventArgs e)
        {
            PlanSuma = (PlanSum)LB_PlanesSuma.SelectedItem;
            Close();
        }
    }
}
