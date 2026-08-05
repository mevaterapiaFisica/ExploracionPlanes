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
    }
}
