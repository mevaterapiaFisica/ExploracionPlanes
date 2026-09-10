using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
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
            main.ShowDialog();
        }
    }
}
