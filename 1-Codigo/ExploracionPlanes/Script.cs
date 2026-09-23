using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Interop;
using VMS.TPS.Common.Model.API;


namespace VMS.TPS
{
    class Script
    {
        public Script()
        {
        }
        public void Execute(ScriptContext context)
        {
            // ponytail: mismo forzado de cultura que Program.cs (modo standalone) — sin esto, bajo
            // un Eclipse con locale de coma decimal, el parseo de dosis/alfa-beta ingresado a mano
            // puede leer "45.0" como 450 (ver Metodos.validarYConvertirADouble).
            CultureInfo current = (CultureInfo)CultureInfo.CurrentCulture.Clone();
            current.NumberFormat.NumberDecimalSeparator = ".";
            current.NumberFormat.NumberGroupSeparator = ",";
            Thread.CurrentThread.CurrentCulture = current;
            Thread.CurrentThread.CurrentUICulture = current;

            ExploracionPlanes.Main main = new ExploracionPlanes.Main(true, context.Patient, context.PlanSetup, context.CurrentUser,context.PlanSumsInScope,context.PlansInScope);
            // ponytail: el plugin corre in-process dentro de Eclipse (no es un proceso separado), así
            // que MainWindowHandle es la ventana de Eclipse. Sin esto, Main (y por herencia toda la
            // cadena de diálogos de DialogoWpf, que la usa como primera "ventana dueña") queda sin
            // relación de Z-order con Eclipse - Alt-Tab la deja huérfana y hay que matar el proceso
            // para volver a verla (mismo síntoma que el freeze de UI.md, pero un nivel más arriba:
            // ahí el problema era diálogo-hijo sin dueño Main, acá es Main sin dueño Eclipse).
            IntPtr ventanaEclipse = Process.GetCurrentProcess().MainWindowHandle;
            if (ventanaEclipse != IntPtr.Zero)
            {
                new WindowInteropHelper(main).Owner = ventanaEclipse;
            }
            main.ShowDialog();
        }
    }
}
