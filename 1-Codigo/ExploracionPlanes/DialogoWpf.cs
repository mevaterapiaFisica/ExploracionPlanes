using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shell;

namespace ExploracionPlanes
{
    // ponytail: fija el Owner Win32 del diálogo WPF a la ventana WinForms activa.
    // Sin esto, Alt-Tab deja al diálogo sin relación de Z-order con la ventana principal
    // y esta queda frizada hasta matar el proceso desde el Administrador de tareas.
    //
    // También reemplaza el chrome nativo de Windows (barra de título gris, fuente del sistema)
    // por uno propio (fondo #1B2A4A, mismo azul que headers de tabla y botones primarios) — sin
    // esto, cada ventana WPF abría con el chrome de Windows por default, inconsistente con el
    // resto de la paleta ya migrada. Se hace con XamlReader.Parse (no un .xaml/ResourceDictionary
    // separado): en el modo plugin de Eclipse (Script.cs) nunca se crea un System.Windows.Application,
    // y la resolución de "pack://application:,,," para cargar un recurso por URI no es confiable sin
    // uno. Parsear el XAML como string en memoria no depende de eso.
    public class DialogoWpf : Window
    {
        private static readonly ResourceDictionary TemaVentana = (ResourceDictionary)XamlReader.Parse(XamlTema);

        private Grid barraCaption;
        private Button botonMin;
        private Button botonMaxRestore;
        private Button botonCerrar;

        // ponytail: lista propia de ventanas abiertas en vez de System.Windows.Application.Current.Windows.
        // En modo plugin de Eclipse (Script.cs) nunca se crea un System.Windows.Application, así que
        // Application.Current da null y el fallback de más abajo nunca encontraba dueño - reaparecía
        // el freeze de Alt-Tab (ver comentario de clase) pero solo corriendo dentro de Eclipse, no en
        // el modo standalone (Program.cs sí crea un Application).
        private static readonly System.Collections.Generic.List<Window> VentanasAbiertas = new System.Collections.Generic.List<Window>();

        public DialogoWpf()
        {
            var activo = System.Windows.Forms.Form.ActiveForm;
            if (activo != null)
            {
                new WindowInteropHelper(this).Owner = activo.Handle;
            }
            else
            {
                // La última ventana abierta antes de esta es su dueña: los diálogos de esta app se
                // abren siempre de forma modal y secuencial, nunca en paralelo.
                // Main abre FormChequeos/PlanesSumaContext desde su PROPIO constructor, antes de
                // llamar a Main.ShowDialog() (eso pasa recién después, en Script.cs) - en ese momento
                // Main todavía no tiene handle nativo. `WindowInteropHelper(ventanaDueña).Handle` sin
                // EnsureHandle() da IntPtr.Zero en ese caso (Owner quedaba sin setear); la propiedad
                // Owner (WPF managed) tampoco sirve, tira InvalidOperationException si la dueña nunca
                // se mostró ("Cannot set Owner property to a Window that has not been shown
                // previously"). EnsureHandle() fuerza la creación del HWND nativo de la dueña SIN
                // mostrarla (no la hace visible), que es justo lo que hace falta acá.
                Window ventanaDueña = VentanasAbiertas.LastOrDefault();
                if (ventanaDueña != null)
                {
                    new WindowInteropHelper(this).Owner = new WindowInteropHelper(ventanaDueña).EnsureHandle();
                }
            }
            VentanasAbiertas.Add(this);
            Closed += (s, e) => VentanasAbiertas.Remove(this);
            Loaded += (s, e) => BuscarPrimerCampoDeTexto(this)?.Focus();

            // WindowStyle por default (SingleBorderWindow) + WindowChrome con CaptionHeight=0 depende
            // de que Windows tenga la composición de escritorio (DWM) activa para suprimir el chrome
            // nativo - en un Citrix/RDP sin esa composición, la ventana volvía a mostrar la barra de
            // título nativa (blanca, sin ícono ni texto) tapando la barra propia. WindowStyle=None saca
            // el chrome nativo de forma incondicional, sin depender de composición; WindowChrome se
            // sigue usando solo para el borde de resize.
            WindowStyle = WindowStyle.None;
            Resources.MergedDictionaries.Add(TemaVentana);
            Template = (ControlTemplate)Resources["PlantillaVentanaDialogoWpf"];
            WindowChrome.SetWindowChrome(this, new WindowChrome
            {
                CaptionHeight = 0,
                ResizeBorderThickness = new Thickness(6),
                GlassFrameThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0),
                UseAeroCaptionButtons = false
            });
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            barraCaption = GetTemplateChild("PART_Caption") as Grid;
            botonMin = GetTemplateChild("PART_Min") as Button;
            botonMaxRestore = GetTemplateChild("PART_MaxRestore") as Button;
            botonCerrar = GetTemplateChild("PART_Close") as Button;

            if (barraCaption != null)
            {
                barraCaption.MouseLeftButtonDown += BarraCaption_MouseLeftButtonDown;
            }
            if (botonMin != null)
            {
                botonMin.Click += (s, e) => WindowState = WindowState.Minimized;
            }
            if (botonMaxRestore != null)
            {
                botonMaxRestore.Click += (s, e) => AlternarMaximizado();
            }
            if (botonCerrar != null)
            {
                botonCerrar.Click += (s, e) => Close();
            }

            bool esVentanaChica = WindowStyle == WindowStyle.ToolWindow || ResizeMode == ResizeMode.NoResize;
            if (esVentanaChica)
            {
                if (botonMin != null)
                {
                    botonMin.Visibility = Visibility.Collapsed;
                }
                if (botonMaxRestore != null)
                {
                    botonMaxRestore.Visibility = Visibility.Collapsed;
                }
            }

            StateChanged += (s, e) => ActualizarIconoMaximizar();
            ActualizarIconoMaximizar();
        }

        private void BarraCaption_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // El click puede originarse en uno de los 3 botones de la barra (son hijos de este
            // Grid) - si no se descarta esto, DragMove() se dispara antes de que el Button procese
            // su propio Click, y cerrar/minimizar/maximizar queda intermitente.
            if (e.OriginalSource is DependencyObject origen && EncontrarAncestro<Button>(origen) != null)
            {
                return;
            }
            if (e.ClickCount == 2)
            {
                AlternarMaximizado();
                return;
            }
            try
            {
                if (WindowState != WindowState.Maximized)
                {
                    DragMove();
                }
            }
            catch (InvalidOperationException)
            {
                // ponytail: DragMove puede tirar si el botón ya se soltó antes de procesar el evento; ignorar.
            }
        }

        private static T EncontrarAncestro<T>(DependencyObject actual) where T : DependencyObject
        {
            while (actual != null)
            {
                if (actual is T coincide)
                {
                    return coincide;
                }
                actual = VisualTreeHelper.GetParent(actual);
            }
            return null;
        }

        private void AlternarMaximizado()
        {
            if (ResizeMode == ResizeMode.NoResize)
            {
                return;
            }
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void ActualizarIconoMaximizar()
        {
            if (botonMaxRestore != null)
            {
                botonMaxRestore.Content = WindowState == WindowState.Maximized ? "\u25A3" : "\u25A1";
            }
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

        // ponytail: helper para pasar una ventana WPF como owner de un Form/diálogo WinForms
        // (ShowDialog(IWin32Window) no tiene overload que acepte un Window de WPF directamente).
        public class OwnerWin32 : System.Windows.Forms.IWin32Window
        {
            public IntPtr Handle { get; }
            public OwnerWin32(Window ventana) => Handle = new WindowInteropHelper(ventana).Handle;
        }

        private const string XamlTema = @"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                     xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
    <Style x:Key='BotonCaption' TargetType='Button'>
        <Setter Property='Background' Value='Transparent' />
        <Setter Property='Foreground' Value='White' />
        <Setter Property='BorderThickness' Value='0' />
        <Setter Property='FontFamily' Value='Segoe UI' />
        <Setter Property='FontSize' Value='13' />
        <Setter Property='Width' Value='40' />
        <Setter Property='Height' Value='36' />
        <Setter Property='Cursor' Value='Arrow' />
        <Setter Property='Template'>
            <Setter.Value>
                <ControlTemplate TargetType='Button'>
                    <Border x:Name='Fondo' Background='{TemplateBinding Background}'>
                        <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center' />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property='IsMouseOver' Value='True'>
                            <Setter TargetName='Fondo' Property='Background' Value='#2E4373' />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
    <Style x:Key='BotonCaptionCerrar' TargetType='Button' BasedOn='{StaticResource BotonCaption}'>
        <Setter Property='Template'>
            <Setter.Value>
                <ControlTemplate TargetType='Button'>
                    <Border x:Name='Fondo' Background='{TemplateBinding Background}'>
                        <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center' />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property='IsMouseOver' Value='True'>
                            <Setter TargetName='Fondo' Property='Background' Value='#C24B3D' />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
    <ControlTemplate x:Key='PlantillaVentanaDialogoWpf' TargetType='Window'>
        <Border Background='{TemplateBinding Background}' BorderBrush='#1B2A4A' BorderThickness='1'>
            <Grid>
                <Grid.RowDefinitions>
                    <RowDefinition Height='36' />
                    <RowDefinition Height='*' />
                </Grid.RowDefinitions>

                <Grid x:Name='PART_Caption' Grid.Row='0' Background='#1B2A4A'>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width='*' />
                        <ColumnDefinition Width='Auto' />
                    </Grid.ColumnDefinitions>
                    <TextBlock Text='{TemplateBinding Title}' Foreground='White' FontFamily='Segoe UI Semibold'
                               FontSize='13' VerticalAlignment='Center' Margin='12,0,0,0' TextTrimming='CharacterEllipsis' />
                    <StackPanel Grid.Column='1' Orientation='Horizontal'>
                        <Button x:Name='PART_Min' Content='&#x2212;' Style='{StaticResource BotonCaption}' />
                        <Button x:Name='PART_MaxRestore' Content='&#x25A1;' Style='{StaticResource BotonCaption}' />
                        <Button x:Name='PART_Close' Content='&#xD7;' Style='{StaticResource BotonCaptionCerrar}' />
                    </StackPanel>
                </Grid>

                <ContentPresenter Grid.Row='1' />
            </Grid>
        </Border>
    </ControlTemplate>
</ResourceDictionary>
";
    }
}
