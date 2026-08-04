using System;
using System.Windows.Forms;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExploracionPlanes
{
    public class Configuracion
    {
        public static string pathPlantilla()
        {
            return Properties.Settings.Default.Path + @"\Plantillas\";
        }

        public static string pathExportados()
        {
            return Properties.Settings.Default.Path + @"\Exportados\";
        }

        public static string pathReportes()
        {
            return Properties.Settings.Default.Path + @"\Reportes\";
        }

        public static double volDosisMaxima()
        {
            return Properties.Settings.Default.VolDosisMax;
        }
    }
}
