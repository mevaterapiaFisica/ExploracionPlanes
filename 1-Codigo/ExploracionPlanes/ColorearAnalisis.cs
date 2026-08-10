using System;
using System.Windows.Forms;

namespace ExploracionPlanes
{
    // Paleta pass/fail de la tabla de análisis, compartida por Form2 y Form2_DosPlanes (antes duplicada en ambos).
    public static class ColorearAnalisis
    {
        public static void colorCelda(DataGridViewCell celda, IRestriccion restriccion)
        {
            if (double.IsNaN(restriccion.valorEsperado))
            {
                return;
            }
            else if (restriccion.cumple() == 0)
            {
                celda.Style.BackColor = System.Drawing.Color.LightGreen;
            }
            else if (restriccion.cumple() == 1)
            {
                celda.Style.BackColor = System.Drawing.Color.LightYellow;
            }
            else
            {
                celda.Style.BackColor = System.Drawing.Color.Red;
            }
        }

        public static void colorCeldasAnidadas(IRestriccion restriccionCondicionante, DataGridViewCell celdaCondicionante, IRestriccion restriccionCondicionada, DataGridViewCell celdaCondicionada)
        {
            if (restriccionCondicionante.cumple() == 0)
            {
                celdaCondicionante.Style.BackColor = System.Drawing.Color.LightGreen;
                celdaCondicionada.Style.BackColor = System.Drawing.Color.LightGreen;
            }
            else if (restriccionCondicionante.cumple() == 2 && restriccionCondicionada.cumple() == 0)
            {
                celdaCondicionante.Style.BackColor = System.Drawing.Color.LightYellow;
                celdaCondicionada.Style.BackColor = System.Drawing.Color.LightYellow;
            }
            else if (restriccionCondicionante.cumple() == 2 && restriccionCondicionada.cumple() == 2)
            {
                celdaCondicionante.Style.BackColor = System.Drawing.Color.Red;
                celdaCondicionada.Style.BackColor = System.Drawing.Color.Red;
            }
        }

        // Camino para diálogos ya migrados a WPF: no hay DataGridViewCell del que pintar el
        // Style.BackColor, así que se devuelve el Brush directo para bindear a FilaAnalisis.FondoEnPlan.
        private static readonly System.Windows.Media.Brush VerdeWpf = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(144, 238, 144));
        private static readonly System.Windows.Media.Brush AmarilloWpf = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 224));
        private static readonly System.Windows.Media.Brush RojoWpf = System.Windows.Media.Brushes.Red;
        private static readonly System.Windows.Media.Brush SinColorWpf = System.Windows.Media.Brushes.Transparent;

        public static System.Windows.Media.Brush fondoWpf(IRestriccion restriccion)
        {
            if (double.IsNaN(restriccion.valorEsperado))
            {
                return SinColorWpf;
            }
            else if (restriccion.cumple() == 0)
            {
                return VerdeWpf;
            }
            else if (restriccion.cumple() == 1)
            {
                return AmarilloWpf;
            }
            else
            {
                return RojoWpf;
            }
        }

        public static void fondoAnidadasWpf(IRestriccion restriccionCondicionante, IRestriccion restriccionCondicionada,
            out System.Windows.Media.Brush fondoCondicionante, out System.Windows.Media.Brush fondoCondicionada)
        {
            if (restriccionCondicionante.cumple() == 0)
            {
                fondoCondicionante = VerdeWpf;
                fondoCondicionada = VerdeWpf;
            }
            else if (restriccionCondicionante.cumple() == 2 && restriccionCondicionada.cumple() == 0)
            {
                fondoCondicionante = AmarilloWpf;
                fondoCondicionada = AmarilloWpf;
            }
            else if (restriccionCondicionante.cumple() == 2 && restriccionCondicionada.cumple() == 2)
            {
                fondoCondicionante = RojoWpf;
                fondoCondicionada = RojoWpf;
            }
            else
            {
                fondoCondicionante = SinColorWpf;
                fondoCondicionada = SinColorWpf;
            }
        }
    }
}
