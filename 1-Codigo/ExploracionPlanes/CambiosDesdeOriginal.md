# Cambios desde el proyecto original (WinForms, 2021)

Resumen de todo lo hecho sobre el ExploracionPlanes original en WinForms (2021-2022), hasta la migración completa a WPF y las mejoras posteriores (2026).

## Cambios para usuarios

- **Prescripciones recordadas**: la app ahora recuerda qué prescripción se eligió por estructura, y las predefinidas cargan automáticamente Sb en mama.
- **Import desde CSV**: soporte para leer datos desde archivo CSV.
- **Matcheo automático de estructuras mejorado**: sugiere la estructura correcta aunque el nombre no sea exacto (ordenado por parecido), y no se rompe si a una estructura le cambiaron el nombre en Eclipse.
- **Memoria por plan**: matcheo, prescripción y plantilla elegidos se recuerdan por plan (antes se perdían si el structure set cambiaba); si no hay memoria, usa el plan más reciente del paciente.
- **Duplicar estructura**: nuevo botón para asignar una segunda estructura real del plan a los mismos constraints (útil para pares bilaterales, ej. riñón izq/der). Incluye botón "Eliminar duplicado".
- **Ocultar restricciones no analizadas**: checkbox para no ver en la tabla las restricciones que no aplican (tildado por defecto).
- **EQD2**: soporte agregado en comparación de dos planes (antes solo estaba en el análisis de un plan). Se corrigió que las restricciones en % (dosis, dosis máxima, dosis media, volumen) no estaban usando el valor convertido a EQD2 cuando estaba habilitado, ni en el modo de un plan ni en el de dos.
- **Comparación de dos planes**: selección de los 2 planes ahora se hace tildando en la lista (antes trataba de adivinar cuál era el segundo plan buscando "cam" en el nombre).
- **Interfaz visual renovada**: toda la aplicación pasó de las ventanas grises clásicas de Windows a un diseño propio, con barra de título de la marca (antes WinForms, ahora WPF) en las 14 ventanas.
- **Corrección de bugs encontrados con el uso real** (visibles en Eclipse):
  - Duplicar una estructura a veces no tomaba la fila elegida.
  - El combo para elegir la estructura correcta a veces abría salteado al final de la lista.
  - Al duplicar una estructura, a veces no se le asignaba prescripción y mostraba "Infinity%".
  - Cerrar y reabrir un paciente a veces generaba un error.
  - Cerrar una ventana secundaria (hijo) a veces cerraba toda la aplicación.
  - Botón de comparar planes quedaba habilitado sin haber elegido plantilla.
  - Con Windows configurado en español, escribir una dosis o alfa/beta con punto decimal podía leerse hasta 1000 veces mal sin avisar.
- **Ajustes de tamaño/tipografía** en varias ventanas pedidos tras revisar capturas reales (texto que quedaba apretado o cortado en botones).
- Queda pendiente, documentado y no resuelto (a pedido): un caso raro donde cerrar la ventana de análisis con login manual a Eclipse cierra toda la app (falla de Eclipse, no de esta app).
- Queda pendiente: pedir la prescripción solo a PTV/CTV y no a órganos de riesgo (ambigüedad sin resolver aún).

## Cambios técnicos

- **Migración completa WinForms → WPF** (2026-08 a 2026-08-10, por fases): las 14 ventanas de la app (diálogos chicos, Form1_prioridades, PlantillaBlanco, Form2, Form2_DosPlanes, Main) migradas a Window/XAML, salvo Form1_ext (diferido a pedido, atado a una importación de constraints SBRT/RC pendiente) y Form3 (WinForms aún).
  - Clase base común `DialogoWpf` para los diálogos: fija el owner correcto (evita que Alt-Tab congele la ventana principal) y foco automático.
  - `FilaAnalisis`/`FilaEstructura`/`FilaPrescripcion` con `INotifyPropertyChanged` reemplazan el acceso por índice a `DataGridView` (Form2 y Form2_DosPlanes).
  - `IRestriccion.editar()` dejó de recibir controles WinForms como parámetro; se reemplazó por `datosEdicion()` devolviendo un DTO sin dependencia de UI.
  - Chrome de ventana unificado (barra de título propia) vía `ControlTemplate` compartido en `DialogoWpf`, con fallback a símbolos Unicode básicos porque Segoe MDL2 Assets no existe en Windows 7 (PCs con acceso a Eclipse).
  - Migración de `Main` (ventana raíz) de `System.Windows.Forms.Application.Run` a `System.Windows.Application.Run`; ajuste de `ShutdownMode` para que cerrar un diálogo hijo no cierre toda la app.
- **Refactor de restricciones**: `RestriccionBase.cs` concentra la lógica común de las 6 clases `Restriccion*` (antes duplicada), bajando de ~1755 a ~807 líneas combinadas sin cambiar comportamiento.
- **`Form2Compartido.cs`**: dedup del código idéntico entre Form2 y Form2_DosPlanes (I/O de memoria por plan, prescripción predefinida, impresión, cierre de sesión de Eclipse).
- **Robustez de I/O**: try/catch aislado por archivo al leer plantillas (una plantilla corrupta ya no rompe las demás); manejo de errores si `estructuras.txt`/`alfaBeta.txt` no están accesibles (caída de red), con fallback a valores por defecto.
- **Limpieza de código muerto**: eliminación de 7 archivos sin uso (Form1, Form3copia, Form4, TBI, Imprimir, PruebaImprimir, MigraDocPrintDocument) y métodos sin callers varios; eliminación de bloques ClickOnce y paths legacy comentados.
- **Seguridad**: reemplazo de credenciales hardcodeadas de Eclipse por login interactivo.
- **Suite de tests standalone** (sin dependencia de ESAPI/Eclipse) agregada en `Tests/`, con casos para EQD2, edición de restricciones, filtro de condición y mejoras varias; documentados en `Tests.md`.
- **Cacheo**: lecturas de `estructuras.txt`/`alfaBeta.txt` cacheadas; se evita recalcular la lista de estructuras del plan por cada plantilla.

---
*Elaborado a partir del historial de git del repositorio (commits desde 2021-07-30 hasta 2026-09-10).*
