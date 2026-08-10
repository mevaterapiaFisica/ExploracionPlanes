using System;
using System.Globalization;
using System.Threading;
using System.Windows;

namespace ExploracionPlanes
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            CultureInfo current = (CultureInfo)CultureInfo.CurrentCulture.Clone();
            current.NumberFormat.NumberDecimalSeparator = ".";
            Thread.CurrentThread.CurrentCulture = current;
            Thread.CurrentThread.CurrentUICulture = current;
            // OnExplicitShutdown: por default (OnLastWindowClose) WPF cierra toda la app cuando
            // se cierra CUALQUIER ventana rastreada, no solo la principal - cerrar un dialogo hijo
            // (ej. "Aplicar a un plan") tiraba abajo Main con él.
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var main = new Main();
            main.Closed += (s, e) => app.Shutdown();
            app.Run(main);
        }
    }
}
