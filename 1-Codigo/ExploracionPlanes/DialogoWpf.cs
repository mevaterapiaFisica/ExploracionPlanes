using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace ExploracionPlanes
{
    // ponytail: fija el Owner Win32 del diálogo WPF a la ventana WinForms activa.
    // Sin esto, Alt-Tab deja al diálogo sin relación de Z-order con la ventana principal
    // y esta queda frizada hasta matar el proceso desde el Administrador de tareas.
    public class DialogoWpf : Window
    {
        public DialogoWpf()
        {
            var activo = System.Windows.Forms.Form.ActiveForm;
            if (activo != null)
            {
                new WindowInteropHelper(this).Owner = activo.Handle;
            }
            Loaded += (s, e) => BuscarPrimerCampoDeTexto(this)?.Focus();
        }

        private static Control BuscarPrimerCampoDeTexto(DependencyObject padre)
        {
            int hijos = VisualTreeHelper.GetChildrenCount(padre);
            for (int i = 0; i < hijos; i++)
            {
                var hijo = VisualTreeHelper.GetChild(padre, i);
                if ((hijo is TextBox || hijo is PasswordBox) && hijo is UIElement ui && ui.IsVisible && ui.IsEnabled)
                {
                    return (Control)hijo;
                }
                var encontrado = BuscarPrimerCampoDeTexto(hijo);
                if (encontrado != null)
                {
                    return encontrado;
                }
            }
            return null;
        }
    }
}
