namespace ExploracionPlanes
{
    // Fila de DGV_Prescripciones: estructura + dosis de prescripción editable.
    public class FilaPrescripcion : ObservableBase
    {
        private string dosis;

        public string Estructura { get; set; }
        public string Dosis { get => dosis; set => Set(ref dosis, value); }
    }
}
