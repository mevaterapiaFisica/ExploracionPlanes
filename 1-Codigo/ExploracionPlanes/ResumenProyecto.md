Resumen Técnico: ExploracionPlanes
 
## 1. Propósito
 
Sistema clínico de análisis y validación de planes de radioterapia que aplica plantillas de restricciones de dosis a planes del sistema de planificación Varian Eclipse. Permite verificar el cumplimiento de criterios dosimétricos (DVH), generar reportes PDF y realizar análisis retrospectivos sobre múltiples pacientes. Es una herramienta de soporte de decisión para físicos médicos y radio-oncólogos.
 
---
 
## 2. Tipo de Ejecución
 
**Dual**: puede ejecutarse como:
- **Plugin de Eclipse (VMS.TPS Script)** — vía `Script.cs`, recibe el contexto del paciente directamente desde el TPS.
- **Aplicación standalone (WinExe, .NET 4.5.1)** — vía `Program.cs`, con conexión directa a la API de Eclipse usando credenciales hardcodeadas.
**Dependencias clave:**
 
| Dependencia | Rol |
|---|---|
| VMS.TPS.Common.Model.API (Varian ESAPI) | Acceso a planes, dosis, estructuras, haces |
| Newtonsoft.Json 11.0.2 | Serialización de plantillas |
| PDFsharp / MigraDoc 1.32 | Generación de reportes PDF |
| Red clínica `\\Ariamevadb-svr\va_data$` | Almacenamiento de plantillas, reportes y configuración |
 
---
 
## 3. Entradas
 
| Origen | Datos |
|---|---|
| **Eclipse (ESAPI)** | Paciente, curso, plan/suma de planes, estructuras, DVH, haces, equipo, dosis prescripta |
| **Plantillas JSON** | Restricciones de dosis configuradas por el usuario (`{Settings.Path}\Plantillas\`) |
| **Archivos de configuración** | `estructuras.txt` (diccionario de nombres), `alfaBeta.txt` (relaciones α/β por estructura) |
| **Usuario** | Selección de plantilla, PTV, modo edición (contraseña), parámetros de análisis |
| **CSV externo** | Restricciones SBRT (Timmerman) para importación de plantillas (`DesdeCSV.cs`) |
| **App.config / Settings** | Ruta de red, volumen para Dmax (VolDosisMax = 0.035 cm³) |
 
---
 
## 4. Salidas
 
| Tipo | Descripción | Ubicación |
|---|---|---|
| **PDF de reporte** | Tabla de análisis con estado pass/fail, encabezado clínico | `{Settings.Path}\Reportes\{PacienteID}_{Apellido}_{PlanID}_{Plantilla}.pdf` |
| **JSON de análisis** | Resultados serializados por plan | `{Settings.Path}\Reportes\Json\` |
| **CSV de minería** | Resumen retrospectivo de múltiples planes aprobados | `{Settings.Path}\Reportes\Json\Analisis\` |
| **CSV de exportación** | Análisis por lote de múltiples pacientes | `{Settings.Path}\Exportados\` |
| **Cache de estructuras** | Pares estructura-plantilla (se purgan automáticamente >1 mes) | `{Settings.Path}\paresEstructuras\` |
 
---
 
## 5. Estructura de Clases Principales
 
| Clase | Descripción |
|---|---|
| `Script.cs` | Punto de entrada del plugin Eclipse; pasa contexto clínico a la aplicación |
| `Program.cs` | Punto de entrada standalone; configura cultura decimal y lanza `Main` |
| `Main.cs` | Ventana principal; gestión CRUD de plantillas y navegación a formularios de análisis |
| `Plantilla.cs` | Modelo de plantilla: metadatos, lista de restricciones, lógica de auto-selección |
| `IRestriccion` | Interfaz común para todos los tipos de restricciones DVH |
| `RestriccionDosis.cs` | Restricción D(volumen): dosis en un percentil de volumen |
| `RestriccionDosisMax.cs` | Restricción Dmax: dosis máxima en volumen pequeño (usa `VolDosisMax`) |
| `RestriccionDosisMedia.cs` | Restricción de dosis media, con soporte EQD2 |
| `RestriccionVolumen.cs` | Restricción V(dosis): volumen que recibe cierta dosis |
| `RestriccionIndiceConformidad.cs` | Índice de conformidad: volumen de isodosis / volumen del blanco |
| `Estructura.cs` | Manejo de estructuras/ROIs: matching de nombres, lookup α/β, identificación de PTV |
| `Analisis.cs` | Par (Estructura, Restricción) para evaluación |
| `Condicion.cs` | Lógica condicional: activa restricciones según nº de fracciones, volumen PTV u otras condiciones |
| `EQD2.cs` | Conversión de dosis a equivalente en fraccionamiento de 2 Gy (modelo lineal-cuadrático) |
| `DVHDataExtensions_ESAPIX.cs` | Interpolación lineal de curvas DVH para sumas de planes (no disponible directo en ESAPI) |
| `Chequeos.cs` | Validación técnica del plan: geometría, equipos, tasas de dosis, estado de aprobación (>40 checks) |
| `Reporte.cs` | Generación de PDF con MigraDoc: encabezado clínico, tabla con colores de pass/fail |
| `Mineria.cs` | Análisis retrospectivo de JSONs de planes aprobados; exporta CSV resumen |
| `Form2.cs` | Formulario de análisis de un plan individual con tabla de resultados |
| `Form2_DosPlanes.cs` | Comparación visual de dos planes sobre la misma plantilla |
| `Form3.cs` | Análisis por lote sobre múltiples pacientes |
| `IO.cs` | Utilidad de serialización JSON con soporte de polimorfismo (`TypeNameHandling.Auto`) |
| `DesdeCSV.cs` | Importación de restricciones SBRT desde CSV (guías de Timmerman) |
| `FormConfiguracion.cs` | Editor de configuración (ruta de red, VolDosisMax) |
 
---
 
## 6. Flujo Principal
 
**Modo plugin (desde Eclipse):**
 
1. `Script.Execute()` recibe contexto Eclipse → instancia `Main` con paciente/plan activo
2. Usuario selecciona o acepta plantilla auto-seleccionada
3. `Main` abre `Form2` → se evalúan todas las restricciones de la plantilla contra el plan
4. Para cada `IRestriccion`: obtiene valor DVH del plan (ESAPI directo o interpolación), compara con límite
5. Tabla de resultados se colorea (verde/rojo/amarillo) según cumplimiento
6. Usuario genera PDF → `Reporte.cs` produce el archivo en la carpeta de red
7. Opcionalmente: `Chequeos.cs` valida parámetros técnicos del plan (geometría, equipos)
**Modo batch / minería:**
 
1. `Form3` conecta a Eclipse con credenciales hardcodeadas → itera lista de pacientes
2. Aplica plantilla seleccionada → guarda JSON y CSV por paciente
3. `Mineria.cs` lee JSONs históricos y genera resumen consolidado
---
 
## 7. Integraciones Externas
 
| Integración | Detalle |
|---|---|
| **Varian ESAPI** | DLLs en `..\..\..\1-En desarrollo\Proyecto Chequeos\PruebaTreeListView\bin\x64\Debug\` |
| **Servidor ARIA** | Red: `\\Ariamevadb-svr\va_data$` (almacenamiento compartido clínico) |
| **Eclipse Application** | Conexión directa: `Application.CreateApplication(null,null)` (login interactivo) en todos los puntos de entrada — credenciales hardcodeadas eliminadas |
| **Sistema de archivos local** | Paths hardcodeados: `C:\Users\Varian\Downloads\constrains SBRT_Edit...` y `C:\Users\Varian\Desktop\rep pros` |
 
> No hay integración con Google APIs, Sitramed ni servicios web externos.
 
---
 
## 8. TODOs, Limitaciones Conocidas y Riesgos
 
| Tipo | Descripción |
|---|---|
| **Paths locales hardcodeados** | `DesdeCSV.cs` apunta a `C:\Users\Varian\Downloads\...` — no portable |
| **PENDIENTE: Importar constraints RC/SBRT de tabla** | `DesdeCSV.LeerTabla()` comentado en el constructor de `Main.cs` — tarea de importación de restricciones SBRT/RC desde CSV incompleta. `Plantilla.filtrarPorFracciones` (auto-selección por `_Nfx`) queda atado a que esta importación se resuelva |
| **PENDIENTE: Bug en doseRate por equipo** | `Chequeos.doseRate`: cadena `if/else if` mal armada — para equipos con dosis especial válida (CRC_EQ1=320, Varian-600C=240, "6oo C/D"=300) la condición específica es falsa (está OK) pero cae al `else` final y se reporta error igual. Falso positivo en el chequeo. Diagnosticado, no corregido a pedido |
| **Sin TODOs explícitos** | No se encontraron comentarios `// TODO` ni `// FIXME` en el código |
| **Código comentado** | Soporte para `ExternalPlanSetup` desactivado; paths legacy `\\ARIAMEVADB-SVR` en comentarios |
| **Sin manejo de errores robusto** | Escasos bloques `try-catch`; la I/O de archivos asume éxito |
| **Sin async en batch** | `Form3` procesa múltiples pacientes de forma sincrónica (UI se bloquea) |
| **Suma de planes (PlanSum)** | ESAPI no expone `GetDoseAtVolume()` directamente → requiere interpolación manual en `DVHDataExtensions_ESAPIX` |
| **Target de publicación** | Path hardcodeado a `c:\PlanExplorer\` en el archivo de proyecto |

### Código muerto eliminado

7 archivos sin uso alcanzable, borrados junto con sus `.Designer.cs`/`.resx` y referencias en el `.csproj`: `Form1.cs` (reemplazado por `Form1_prioridades.cs`), `Form3copia.cs` (única instanciación estaba comentada), `Form4.cs` (cero referencias), `TBI.cs` (punto de entrada alternativo comentado en `Program.cs`), `Imprimir.cs` (impresión legacy, sin callers vivos), `PruebaImprimir.cs` (no estaba ni en el `.csproj`) y `MigraDocPrintDocument.cs` (copia vendoreada, superada por la clase homónima del paquete NuGet de MigraDoc). Se limpiaron también los comentarios y campos colgantes que quedaron en `Main.cs` y `Program.cs`. Build verificado con MSBuild (VS2022) tras el borrado.

### Optimizaciones y correcciones de lógica aplicadas

- `Estructura.diccionario()` / `Estructura.AlfaBeta()`: cacheadas en memoria en vez de releer los `.txt` en cada llamada (se llamaban 1-2 veces por fila de la tabla de análisis).
- `Estructura.nombreEnDiccionario`: usa `TryGetValue` en vez de `ContainsKey` + indexer (evita doble lookup).
- `Plantilla.ContarEstructurasCoincidentes` / `SeleccionarAutomaticamentePlantilla`: la lista de estructuras ESAPI del plan se calcula una sola vez y se pasa por parámetro, en vez de recalcularse por cada estructura de cada plantilla (era O(N plantillas × M estructuras)).
- `Condicion.CumpleCondicion`: eliminado el `if (this == null)` muerto (nunca se ejecutaba; los callers ya validan null antes).
- `DVHDataExtensions_ESAPIX.GetDoseAtVolume`: eliminado `throw` inalcanzable después de un `return`.
- `Mineria.listaPlantillas`: agregado guard contra lista vacía de JSONs (evitaba `IndexOutOfRangeException`).
- `Reporte.imprimir`: método muerto sin callers, eliminado.
- Paths derivados de `Settings.Default.Path` (`Plantilla.pathDestino`, `Reporte.pathDestino`, paths estáticos de `Form2`) pasaron de campo estático cacheado a propiedad calculada — se recalculan siempre desde `Settings.Default.Path` en vez de quedar obsoletos si el usuario cambia la ruta de red en `FormConfiguracion` sin reiniciar.
 
---
 
> **Contexto general**: Aplicación de producción clínica en uso activo en un centro de radioterapia. No tiene tests unitarios formales. La ausencia de manejo de errores sistemático y las credenciales embebidas representan deuda técnica relevante.