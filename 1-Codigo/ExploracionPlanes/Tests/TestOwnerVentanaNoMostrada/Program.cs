// Reproduce el bug real reportado por el usuario: Main abre FormChequeos desde su PROPIO
// constructor (Main.xaml.cs líneas 60/88), antes de llamar a Main.ShowDialog() (eso pasa recién
// después, en Script.cs). En ese momento Main no tiene handle nativo todavía.
// El fallback viejo de DialogoWpf hacía `new WindowInteropHelper(this).Owner =
// new WindowInteropHelper(ventanaDueña).Handle` - con ventanaDueña (Main) sin mostrar, esa
// Handle da IntPtr.Zero, y el diálogo hijo queda sin dueño real -> freeze de Alt-Tab.
// (Se probó primero con la propiedad Owner managed - tira InvalidOperationException si la
// dueña nunca se mostró, así que tampoco sirve). El fix real usa
// WindowInteropHelper.EnsureHandle(), que fuerza la creación del HWND nativo de la dueña
// SIN mostrarla.
using System;
using System.Windows.Interop;
using ExploracionPlanes;

class Program
{
    [STAThread]
    static void Main()
    {
        int fallas = 0;
        void Assert(string nombre, bool ok)
        {
            Console.WriteLine((ok ? "OK   " : "FAIL ") + nombre);
            if (!ok) fallas++;
        }

        // Simula Main: se construye pero NO se muestra todavía (igual que en Script.cs, donde
        // Main.ShowDialog() se llama recién después de que el constructor de Main ya terminó).
        DialogoWpf ventanaPrincipal = new DialogoWpf { Title = "Main (sin mostrar)" };
        Assert("Ventana principal (primera creada) no tiene handle nativo todavía", new WindowInteropHelper(ventanaPrincipal).Handle == IntPtr.Zero);

        // Simula FormChequeos, creada y mostrada DENTRO del constructor de Main, antes de que
        // Main tenga handle nativo.
        DialogoWpf dialogoHijo = new DialogoWpf { Title = "FormChequeos" };
        IntPtr ownerDelHijo = new WindowInteropHelper(dialogoHijo).Owner;
        IntPtr handleVentanaPrincipal = new WindowInteropHelper(ventanaPrincipal).Handle;
        Assert("Crear el diálogo hijo forzó la creación del handle nativo de la ventana principal", handleVentanaPrincipal != IntPtr.Zero);
        Assert("El diálogo hijo toma como Owner el handle nativo de la última ventana abierta (Main), aunque no se haya mostrado", ownerDelHijo == handleVentanaPrincipal);

        // Confirma que mostrar y cerrar ambas ventanas en ese orden no tira excepción (el escenario
        // real: FormChequeos se muestra y cierra antes de que Main se muestre).
        try
        {
            dialogoHijo.Show();
            dialogoHijo.Close();
            ventanaPrincipal.Show();
            ventanaPrincipal.Close();
            Assert("Mostrar y cerrar el hijo antes que la ventana dueña no tira excepción", true);
        }
        catch (Exception e)
        {
            Assert("Mostrar y cerrar el hijo antes que la ventana dueña no tira excepción (" + e.GetType().Name + ": " + e.Message + ")", false);
        }

        Console.WriteLine();
        Console.WriteLine(fallas == 0 ? "TODOS LOS CHEQUEOS OK" : $"{fallas} CHEQUEO(S) FALLARON");
        Environment.Exit(fallas == 0 ? 0 : 1);
    }
}
