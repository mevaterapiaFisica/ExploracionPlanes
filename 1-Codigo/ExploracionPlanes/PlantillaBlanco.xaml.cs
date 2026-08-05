using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;

namespace ExploracionPlanes
{
    public partial class PlantillaBlanco : DialogoWpf
    {
        Plantilla plantilla;
        ObservableCollection<FilaAnalisis> filas = new ObservableCollection<FilaAnalisis>();

        public PlantillaBlanco(Plantilla _plantilla)
        {
            InitializeComponent();
            plantilla = _plantilla;
            DGV_Analisis.ItemsSource = filas;
            llenarAnalisis();
            Title = plantilla.nombre;
        }

        private void llenarAnalisis()
        {
            filas.Clear();
            Col_Prioridad.Visibility = plantilla.tienePrioridades() ? Visibility.Visible : Visibility.Collapsed;

            foreach (IRestriccion restriccion in plantilla.listaRestricciones)
            {
                var fila = new FilaAnalisis();
                fila.Estructura = restriccion.estructura.nombre;
                fila.Metrica = restriccion.metrica();
                fila.Referencia = restriccion.nota;
                if (!string.IsNullOrEmpty(restriccion.planMod))
                {
                    fila.Referencia += " *";
                }
                if (restriccion.condicion != null && restriccion.condicion.tipo == Tipo.CondicionadaPor)
                {
                    fila.Estructura = "(" + Estructura.nombreEnDiccionario(restriccion.estructura) + ")";
                    fila.Metrica = "(" + restriccion.metrica() + ")";
                }
                string menorOmayor = restriccion.esMenorQue ? "<" : ">";
                string valorEsperadoString;
                if (double.IsNaN(restriccion.valorEsperado))
                {
                    valorEsperadoString = "Reportar";
                }
                else
                {
                    valorEsperadoString = menorOmayor + restriccion.valorEsperado + restriccion.unidadValor;
                }
                if (!double.IsNaN(restriccion.valorTolerado))
                {
                    valorEsperadoString += " (" + restriccion.valorTolerado + restriccion.unidadValor + ")";
                }
                if (!string.IsNullOrEmpty(restriccion.prioridad))
                {
                    fila.Prioridad = restriccion.prioridad;
                }
                fila.Esperado = valorEsperadoString;
                filas.Add(fila);
            }
            if (plantilla.TieneRestriccionEnPlanMod())
            {
                plantilla.nota += "\r\n* Restricciones se evaluarán en plan " + plantilla.ExtensionPlanMod();
            }
        }

        #region Imprimir

        private List<ColumnaReporte> columnasReporte()
        {
            return new List<ColumnaReporte>
            {
                new ColumnaReporte { Encabezado = "Estructura", Ancho = 60 },
                new ColumnaReporte { Encabezado = "Prioridad", Ancho = 50 },
                new ColumnaReporte { Encabezado = "Métrica", Ancho = 60 },
                new ColumnaReporte { Encabezado = "Vol [cm3]", Ancho = 60 },
                new ColumnaReporte { Encabezado = "En Plan", Ancho = 70 },
                new ColumnaReporte { Encabezado = "Esperado", Ancho = 70 },
                new ColumnaReporte { Encabezado = "Ref.", Ancho = 30 },
            };
        }

        private TablaReporte tablaReporte()
        {
            var tabla = new TablaReporte { Columnas = columnasReporte() };
            foreach (var fila in filas)
            {
                var filaReporte = new FilaReporte();
                filaReporte.Valores.AddRange(new[] { fila.Estructura, fila.Prioridad, fila.Metrica, fila.Volumen, fila.EnPlan, fila.Esperado, fila.Referencia });
                for (int i = 0; i < filaReporte.Valores.Count; i++)
                {
                    filaReporte.Fondos.Add(System.Drawing.Color.White);
                }
                tabla.Filas.Add(filaReporte);
            }
            return tabla;
        }

        private Document reporte()
        {
            return Reporte.crearReporte("", "", "", "", plantilla.nombre, plantilla.nota, "", "", "", tablaReporte());
        }

        private void BT_GuardarReporte_Click(object sender, RoutedEventArgs e)
        {
            Reporte.exportarAPdf("", "", "", "", plantilla.nombre, reporte());
        }

        private void BT_Imprimir_Click(object sender, RoutedEventArgs e)
        {
            var pd = new MigraDoc.Rendering.Printing.MigraDocPrintDocument();
            var rendered = new DocumentRenderer(reporte());
            rendered.PrepareDocument();
            pd.Renderer = rendered;
            var printDialog = new System.Windows.Forms.PrintDialog { UseEXDialog = true };
            if (printDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                pd.PrinterSettings = printDialog.PrinterSettings;
                pd.Print();
            }
        }

        #endregion
    }
}
