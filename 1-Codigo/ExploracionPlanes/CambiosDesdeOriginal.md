# Cambios desde el proyecto original

Resumen de todo lo hecho sobre el ExploracionPlanes original en WinForms (2021-2022), hasta la migración completa a WPF y las mejoras posteriores (2026).

## Cambios para usuarios

- **Prescripciones recordadas**: la app ahora recuerda qué prescripción se eligió por estructura.
- **Matcheo automático de estructuras mejorado**: sugiere la estructura correcta aunque el nombre no sea exacto (ordenado por parecido), y no se rompe si a una estructura le cambiaron el nombre en Eclipse.
- **Memoria por plan**: matcheo, prescripción y plantilla elegidos se recuerdan por plan (antes se perdían si el structure set cambiaba); si no hay memoria, usa el plan más reciente del paciente.
- **Duplicar estructura**: nuevo botón para asignar una segunda estructura real del plan a los mismos constraints (útil para pares bilaterales, ej. riñón izq/der o para múltiples PTVs). Incluye botón "Eliminar duplicado".
- **Ocultar restricciones no analizadas**: checkbox para no ver en la tabla las restricciones que no aplican (tildado por defecto).
- **EQD2**: soporte agregado en comparación de dos planes (antes solo estaba en el análisis de un plan). Se corrigió que las restricciones en % (dosis, dosis máxima, dosis media, volumen) no estaban usando el valor convertido a EQD2 cuando estaba habilitado, ni en el modo de un plan ni en el de dos.
- **Interfaz visual renovada**: toda la aplicación pasó de las ventanas grises clásicas de Windows a un diseño propio, con barra de título de la marca (antes WinForms, ahora WPF) en las 14 ventanas.
- **Plantillas únicas separables por fraccionamiento**: pensadas para SBRT y radiocirugía (RC), donde los constraints dependen del número de fracciones.

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
