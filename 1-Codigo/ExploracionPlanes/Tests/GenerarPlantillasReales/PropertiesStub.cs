namespace ExploracionPlanes.Properties
{
    // Reemplaza al Properties/Settings.Designer.cs real (atado a app.config/ApplicationSettingsBase):
    // Configuracion.cs solo necesita Default.Path (y Default.VolDosisMax, sin uso en este camino).
    public class SettingsStub
    {
        public string Path { get; set; } =
            @"C:\Users\SERVER~1\AppData\Local\Temp\claude\--fisica0-centro-de-datos2018-000-Centro-de-Datos-2021-12-Software-propio-2-En-uso-cl-nico-ExploracionPlanes-1-Codigo-ExploracionPlanes\f44e989b-912c-4143-a262-c190e45287e6\scratchpad\PlantillaOutput";
        public double VolDosisMax { get; set; } = 0;
    }

    public static class Settings
    {
        public static SettingsStub Default { get; } = new SettingsStub();
    }
}
