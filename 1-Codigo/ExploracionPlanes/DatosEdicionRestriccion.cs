namespace ExploracionPlanes
{
    // DTO de solo datos para que IRestriccion.datosEdicion() no dependa de controles de UI.
    // Antes, IRestriccion.editar(...) tomaba ComboBox/TextBox de WinForms directo por parámetro
    // y les escribía .Text/.SelectedIndex — no compila contra controles WPF. El caller (el
    // formulario, sea WinForms o WPF) es quien decide a qué control asignar cada campo.
    public class DatosEdicionRestriccion
    {
        public string NombreEstructura;
        public string NombresAlt;
        public int IndiceTipoRestriccion;
        public string Prioridad;
        public string ValorCorrespondiente;
        public bool EsMenorQue;
        public string ValorEsperado;
        public string ValorTolerado;
        public string UnidadValor;
        public string UnidadCorrespondiente;
        public string Nota;
    }
}
