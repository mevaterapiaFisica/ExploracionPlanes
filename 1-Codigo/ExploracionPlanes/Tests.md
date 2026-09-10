# Tests

Registro de tests hechos sobre cambios de código funcional. Cada entrada documenta qué se probó, con qué números y qué resultado dio antes/después del cambio.

---

## 2026-09-10 (4) — Revisión completa de código: bugs, redundancias, código muerto

### Pedido

Revisar el código completo del proyecto buscando bugs, redundancias, mejoras y código muerto (8 agentes en paralelo cubriendo correctness clínico, WPF, eficiencia, reuse/dead-code, altitude/root-cause, cross-file y convenciones), y aplicar los arreglos de mayor riesgo/impacto.

### Antes

- **Decimales**: `Script.cs` (entrada como plugin de Eclipse) no forzaba ninguna cultura de hilo, a diferencia de `Program.cs` (standalone). Bajo Windows en es-AR (decimal=",", miles="."), `Metodos.validarYConvertirADouble` usaba `Double.TryParse(entrada, out _)` sin `NumberStyles` explícito (permite miles por default) — un valor tipeado con punto como separador (ej. "45.678") se leía como 45678. Mismo problema sin guard en `Estructura.AlfaBeta` (`Convert.ToDouble` sin cultura ni try/catch de parseo).
- **Freeze de ventana**: `DialogoWpf.cs` fijaba el owner Win32 solo vía `System.Windows.Forms.Form.ActiveForm`. Desde que `Main` pasó a ser WPF (commit 449ad5b), `ActiveForm` es siempre null al abrir un diálogo desde Main → mismo freeze por falta de owner que ya se había arreglado una vez (documentado en `UI.md`).
- `RestriccionDosisMax.volumenDosisMaxima` era un campo `static` inicializado una sola vez desde `Properties.Settings.Default` — cambiar el volumen de Dmax en Configuración no tenía efecto hasta reiniciar la app.
- `Form1_ext.BT_CargarDesdePaciente_Click` no chequeaba `DialogResult`/null tras `ImportarNombresEstructuras.ShowDialog()` → NullReferenceException al cancelar (el mismo handler en `Form1_prioridades` sí lo hacía).
- `ImportarNombresEstructuras`: la sesión de Eclipse (`app`) solo se liberaba en el botón Cancelar — el camino exitoso (Importar) y cerrar con la X dejaban la sesión sin `Dispose()`. Además, re-seleccionar un plan duplicaba los checkboxes de estructuras (no se limpiaba la lista antes de repoblarla).
- `DVHDataExtensions_ESAPIX.GetVolumeAtDose`/`GetDoseAtVolume`: `Max`/`Min` sobre un array vacío tiraban `InvalidOperationException` en vez de reportar dato faltante; `interpolar1D` dividía por `(x2-x1)` sin guard, dando NaN/Infinity en un tramo plano de la DVH.
- `Main.xaml.cs`: `PlanesSumaContext` no tiene botón Cancelar — cerrarlo con la X dejaba `PlanSuma` null y `Chequeos.chequeos(null, true)` tiraba NRE. `Form3` (WinForms) se abría desde `Main` (WPF) sin owner, mismo riesgo de freeze que el punto de `DialogoWpf`.
- Código muerto: `Mineria.leerArchivos/leerCaso/buscarPlantilla/aplicarPlantilla/extraerDePaciente` (path hardcodeado de una PC específica, sin callers), `IO.appendObjectAsJson/readJsonList` (sin callers), `estructurasSinAsociar()` duplicado sin uso en `Form2.xaml.cs`/`Form2_DosPlanes.xaml.cs`, y `ExploracionPlanes - copia.csproj` versionado en git referenciando archivos pre-migración WPF que ya no existen.
- `Chequeos.estructuraNombreCursoCorrecta` creaba un `new Regex(...)` en cada llamada en vez de un `static readonly`.

### Cambio

- `Script.cs`: se agregó el mismo forzado de cultura (`NumberDecimalSeparator = "."`) que ya tenía `Program.cs`, antes de crear `Main`.
- `Metodos.validarYConvertirADouble`: se cambió `Double.TryParse(entrada, out _)` por `Double.TryParse(entrada, NumberStyles.Float, cultura, out _)` (sin `AllowThousands`), tanto en el parseo primario como en el alternativo con coma.
- `Estructura.AlfaBeta`: parseo con `NumberStyles.Float` + `CultureInfo.InvariantCulture` (igual criterio que `DesdeCSV.Dbl`), con fallback a 3 y aviso si el valor en `alfaBeta.txt` no es parseable (antes tiraba excepción sin catch).
- `DialogoWpf.cs`: si `ActiveForm` es null, se busca una ventana WPF activa (`Application.Current.Windows`) y se usa como owner.
- `RestriccionDosisMax.volumenDosisMaxima`: pasó de campo `static` cacheado a propiedad calculada que llama a `Configuracion.volDosisMaxima()` (helper que ya existía sin uso).
- `Form1_ext.BT_CargarDesdePaciente_Click`: se agregó el mismo guard de `DialogResult`/null + `cerrarPaciente()` que ya tenía `Form1_prioridades`.
- `ImportarNombresEstructuras.xaml.cs`: `Dispose()` de la sesión de Eclipse centralizado en el evento `Closed` (cubre Importar/Cancelar/X); `cerrarPaciente()` guardado contra `app` ya disposed; `BT_SeleccionarPlan_Click` limpia `CHLB_Estructuras.Items` antes de repoblar.
- `DVHDataExtensions_ESAPIX.cs`: guard de array vacío en ambos métodos (retornan `NaN`/`DoseValue.UndefinedDose()`); `interpolar1D` retorna `z1` cuando `x1==x2` en vez de dividir por cero; se materializa la curva con `.ToList()` una sola vez en `GetVolumeAtDose` (antes se re-evaluaba el `Select` en cada `Max`/`Min`/`First`/`Last`).
- `Main.xaml.cs`: guard de `planContext == null` tras `PlanesSumaContext.ShowDialog()` (cierra con mensaje, igual que las otras ramas de "debe seleccionar..."); `Form3` ahora se abre con `aplicarPorLote.ShowDialog(new DialogoWpf.OwnerWin32(this))`.
- Se agregó `DialogoWpf.OwnerWin32` (`IWin32Window` que envuelve el handle de una ventana WPF) para poder pasarle owner a diálogos WinForms abiertos desde ventanas WPF.
- Código muerto eliminado: los 5 métodos + struct `Caso` de `Mineria.cs`, `appendObjectAsJson`/`readJsonList` de `IO.cs`, `estructurasSinAsociar()` en `Form2.xaml.cs`/`Form2_DosPlanes.xaml.cs`, y `ExploracionPlanes - copia.csproj` (`git rm`).
- `Chequeos.cs`: regex de `estructuraNombreCursoCorrecta` movida a `static readonly FormatoNombreCurso`.
- Se creó `CLAUDE.md` con la regla de proceso (test + entrada en Tests.md, respuestas en español) y notas de arquitectura/cuidados para próximas sesiones.

### Cómo se testeó

- **Compilación completa** (`MSBuild ExploracionPlanes.csproj`) después de cada tanda de cambios: build OK, solo warnings preexistentes de arquitectura MSIL/AMD64.
- **`Tests/TestMejoras/Program.cs`** (lógica pura, sin ESAPI): se agregaron dos secciones nuevas, `dotnet run` sobre el proyecto:
  - Sección 6: réplica de `validarYConvertirADouble` viejo vs. nuevo. Bajo cultura es-AR nativa sin forzar (decimal=",", miles="."), el viejo código leía `"45.678"` como `45678` (bug reproducido, confirmado corriendo el test antes de asumir la causa). El nuevo código con `NumberStyles.Float` ya no confunde el punto con separador de miles (da `NaN`, formato ambiguo, en vez de un número incorrecto); bajo la cultura que fuerzan `Program.cs`/`Script.cs` (decimal "."), `"45.0"` da `45` directamente. Nota: se descartó por el camino una hipótesis inicial (que dejar `NumberGroupSeparator` en "." igual al decimal en `Program.cs` causaba la misma ambigüedad) porque el test mostró que .NET desambigua correctamente cuando ambos separadores coinciden — no se tocó `Program.cs` por esto.
  - Sección 7: réplica de `interpolar1D`. Con `x1==x2` (tramo plano de DVH) el código viejo daría NaN/Infinity (0/0); el nuevo devuelve `z1`. El caso normal (`x1 != x2`) interpola igual que antes.
  - Resultado: `TODOS LOS CHEQUEOS OK` (incluye las secciones 1-5 preexistentes, sin regresiones).
- Los demás fixes (DialogoWpf owner, Form3 owner, PlanesSumaContext null-check, RestriccionDosisMax dinámico, dispose de Eclipse en ImportarNombresEstructuras, checkboxes duplicados) son cambios de flujo WPF/WinForms y de ciclo de vida de objetos ESAPI que no se pueden reproducir fuera de Eclipse/una sesión interactiva — se validó que compilan y que la lógica del guard es la misma que ya usan sus pares (`Form1_prioridades`, otras ramas de `Main.xaml.cs`).

### Conclusión

- El bug de decimales (el más grave: podía multiplicar una dosis por ~10-1000x sin ningún error visible) queda cerrado en su origen: `Script.cs` ahora normaliza cultura igual que `Program.cs`, y `Metodos`/`Estructura.AlfaBeta` ya no dependen de la ambigüedad de "AllowThousands" por default.
- El freeze de ventana (regresión introducida al migrar `Main` a WPF) queda cerrado con un fallback de owner en `DialogoWpf`, y `Form3` (todavía WinForms) ahora recibe owner explícito desde `Main`.
- Quedan **sin tocar, a propósito**, por requerir una decisión de producto/física médica o ser un refactor grande de alto riesgo sin cobertura de tests end-to-end:
  - `Condicion.CumpleCondicion`: los operadores `menor_a`/`entre` son inclusivos en ambos extremos — un volumen de PTV exactamente en el límite de dos buckets contiguos (ej. 10cm³ entre "<10" y "10-30") cumple ambos a la vez. No se cambió la semántica porque `Operador` también lo arma a mano el usuario desde `Form1_ext` (plantillas con condiciones) y no hay forma de saber, sin preguntar, si algún límite exacto documentado depende del comportamiento actual.
  - Unificar `Form2.xaml.cs`/`Form2_DosPlanes.xaml.cs` (duplicación ~70%) y darle una base común a las 6 clases `Restriccion*` (duplicación ~400 líneas): ya evaluado y pospuesto deliberadamente en la entrada 2026-09-10 (3) de este archivo para el primero; el segundo es un refactor de la misma magnitud sobre código de cálculo clínico.
  - `Chequeos.doseRate`/`coincidenciaCamillas` (cadenas if/else largas) y las llamadas dobles a `GetDVHCumulativeData` por restricción: mejoras de mantenibilidad/performance reales pero de bajo riesgo clínico si no se tocan — se dejan para una ronda aparte, no mezcladas con esta de bugs.

### Pendiente

- Verificación en Eclipse/standalone real de: apertura de diálogos desde `Main` tras Alt-Tab (freeze), ingreso de una dosis con punto decimal en un equipo con Windows en es-AR, reimportación de estructuras desde paciente (checkboxes), y `Form3` (aplicar por lote) abierto desde `Main`.
- Decisión de física médica sobre la semántica de límites de `Condicion` (inclusivo vs. medio-abierto) antes de tocarla.

---

## 2026-09-10 (3) — Mejoras de diseño/usabilidad en Main, Form2 y Form2_DosPlanes (flujo vía script/contexto)

### Pedido

Revisar diseño de `Main`, `Form2` y `Form2_DosPlanes` (las más usadas, sobre todo vía script con contexto: Main → Aplicar a un plan / Comparar dos planes) en busca de mejoras de diseño y usabilidad, y aplicarlas.

### Antes

- `Main`: con contexto (`hayContext`), 9 de 11 botones quedaban deshabilitados pero igual visibles (pared de botones grises) antes de llegar a "Aplicar plantilla a un plan"/"Comparar dos planes". Sin filtro de texto sobre `LB_Plantillas`.
- `Form2`/`Form2_DosPlanes`: con contexto, la columna 1 ("1. Elegir paciente") se colapsa (comportamiento correcto y ya probado), pero los headers de las columnas siguientes seguían numerados "4. Asociar estructuras", "5. Ajustar prescripciones", "6. Analizar" — la numeración arrancaba en 4 sin haber mostrado 1-3.
- `Form2`/`Form2_DosPlanes`: `SizeToContent="Width"` sin techo — con muchas columnas `Auto` en los DataGrid, la ventana podía crecer más ancha que la pantalla sin scroll horizontal.
- Advertencia de restricciones evaluadas en plan modificado (`L_Advertencia`/`L_Advertencia2`) usaba solo un asterisco como marca visual.
- `Form2.xaml`/`Form2.xaml.cs` nombraban la grilla de análisis `DGV_Análisis` (con tilde), inconsistente con `DGV_Analisis` (sin tilde) de `Form2_DosPlanes` — mismo propósito, nombres distintos.
- Nota: `Height="850"` fijo en ambos formularios es una decisión deliberada previa (con `Auto` la ventana arrancaba chica y al Analizar se agrandaba y desbordaba) — **no se tocó**.

### Cambio

- `Main.xaml`/`Main.xaml.cs`: los botones de administración de plantillas (Nueva, Nueva con condiciones, Editar, Duplicar, Eliminar, Ocultar/Mostrar, Extraer por lote, Extraer de plantilla) se agruparon en `PanelAdminPlantillas` con un `Separator`, y ambos se colapsan (`Visibility.Collapsed`, no solo `IsEnabled=False`) cuando `hayContext`. Se agregó `TB_FiltroPlantillas` (TextBox) sobre la lista de plantillas que filtra `LB_Plantillas` por `etiqueta` (case-insensitive) en `leerPlantillas()`.
- `Form2.xaml`/`Form2_DosPlanes.xaml`: los headers de columna se nombraron (`Label_PasoEstructuras`, `Label_PasoPrescripciones`, `Label_PasoAnalizar`); en `prepararControlesContext()` de ambos code-behind se renumeran a "1. Asociar estructuras" / "2. Ajustar prescripciones" / "3. Analizar" cuando hay contexto.
- `Form2.xaml`/`Form2_DosPlanes.xaml`: se agregó `MaxWidth="{x:Static SystemParameters.PrimaryScreenWidth}"` a la ventana y se envolvió el `Grid` principal en un `ScrollViewer` (`HorizontalScrollBarVisibility="Auto"`, `VerticalScrollBarVisibility="Disabled"` para no interferir con la altura fija ya decidida).
- `Form2.xaml.cs`/`Form2_DosPlanes.xaml.cs`: el texto de advertencia pasó de `"* Restricciones evaluadas en ..."` a `"⚠ Restricciones evaluadas en ..."`.
- `Form2.xaml`/`Form2.xaml.cs`: se renombró `DGV_Análisis` → `DGV_Analisis` (alineado con `Form2_DosPlanes`).
- No se tocó: fusión de `Form2`/`Form2_DosPlanes` en un solo formulario (la duplicación entre ambos sigue existiendo) — cambio estructural más grande, se dejó pendiente por separado para no mezclar con esta ronda de ajustes de layout.

### Cómo se testeó

Cambios puramente de UI/layout (visibilidad, texto, filtro sobre lista ya cargada) — no hay lógica de cálculo nueva que testear numéricamente. Se compiló el proyecto completo (`MSBuild ExploracionPlanes.csproj`) para confirmar que no quedan referencias colgantes a los controles renombrados/reorganizados (`DGV_Análisis`, `Label_PasoEstructuras`, `PanelAdminPlantillas`, `TB_FiltroPlantillas`, etc.): build OK, sin errores (solo warnings preexistentes de arquitectura MSIL/AMD64 no relacionados).

### Conclusión

- Flujo vía script (contexto) en `Main`: solo se ven Aplicar/Comparar/Ver + Configuración/Habilitar Edición, sin el resto de botones de administración.
- `Form2`/`Form2_DosPlanes` con contexto: pasos numerados 1/2/3 en vez de 4/5/6.
- Ventana no puede exceder el ancho de pantalla; si el contenido es más ancho, aparece scroll horizontal.

### Pendiente

- Verificación visual en Eclipse (o standalone) de filtro de plantillas, colapso de panel admin, renumeración de pasos y scroll horizontal con una plantilla de muchas estructuras/restricciones.
- Evaluar en otra ronda si conviene unificar `Form2`/`Form2_DosPlanes` en un solo formulario para eliminar la duplicación de XAML/code-behind.

---

## 2026-09-10 (2) — Eliminar columna/botón "definir volumen de Dmax" en la grilla de Análisis (`Form2`/`Form2_DosPlanes`)

### Pedido

Quitar la última columna de la grilla de Análisis (sin header) que se habilitaba solo para restricciones de dosis máxima (`RestriccionDosisMax`) y mostraba un botón para redefinir a mano, por fila, el tamaño de volumen usado en el cálculo de Dmax (`RestriccionDosisMax.volumenDosisMaxima`, por defecto `Properties.Settings.Default.VolDosisMax`).

### Antes

- `Form2.xaml`/`Form2_DosPlanes.xaml`: última `DataGridTemplateColumn` con un `Button` bindeado a `FilaAnalisis.VolumenDmaxTexto`, visible solo si `FilaAnalisis.EsDmax == true`.
- Code-behind: al armar cada fila de tipo `RestriccionDosisMax` se seteaba `EsDmax = true` y `VolumenDmaxTexto`; el click (`BT_VolumenDmax_Click`) abría `FormTB` y, si se confirmaba, reanalizaba la fila con `RestriccionDosisMax.analizarPlanEstructura(plan, estructura, volumenOverride)` (overload de 3 args, exclusivo para este botón).
- `FilaAnalisis`: propiedades `EsDmax`/`VolumenDmaxTexto`.

### Cambio

- Se eliminó la columna en ambos XAML, el handler `BT_VolumenDmax_Click` en ambos code-behind, el seteo de `EsDmax`/`VolumenDmaxTexto` al armar la fila, las propiedades `EsDmax`/`VolumenDmaxTexto` en `FilaAnalisis`, y el overload de 3 args `analizarPlanEstructura(plan, estructura, volumenDosisMaximaOVR)` en `RestriccionDosisMax` (quedaba sin más callers).
- El cálculo de Dmax en sí (`analizarPlanEstructura(plan, estructura)`, que usa `volumenDosisMaxima` fijo de `Settings`) no cambia — solo se quita la posibilidad de overridearlo por fila desde la grilla.
- Se quitó también el comentario sobre el bug preexistente de coloreado (`FondoMetrica` en vez de `FondoEnPlan` al editar por botón) en `FilaAnalisis.cs`, porque el botón que lo disparaba ya no existe.

### Cómo se testeó

Cambio puramente de UI/eliminación de código muerto (no hay lógica nueva que testear). Se compiló el proyecto para confirmar que no quedaron referencias colgantes a `EsDmax`, `VolumenDmaxTexto`, `BT_VolumenDmax_Click` ni al overload de 3 args.

### Conclusión

- La grilla de Análisis en `Form2`/`Form2_DosPlanes` ya no muestra la columna/botón de volumen de Dmax.
- Sin efecto sobre el resto del análisis (Dmax se sigue calculando con el volumen fijo de `Settings`).

### Pendiente

- Verificación visual en Eclipse de que la grilla quedó con una columna menos y sin romper el ancho de las demás.

---

## 2026-09-10 — Feature: dejar de preguntar "¿cuál es el PTV?" e inferirlo del matcheo (`Form2`/`Form2_DosPlanes`)

### Pedido

Al analizar plantillas con restricciones condicionadas por volumen de PTV o nº de fracciones, la app preguntaba por diálogo (`SeleccionarPTV`) cuál era el PTV, incluso cuando:
- la plantilla no tenía ninguna restricción condicionada a volumen de PTV (solo a nº de fx) — la pregunta sobraba,
- había un solo PTV en el matcheo — se podía inferir sin preguntar,
- había más de un PTV (fila duplicada, ver `duplicarEstructura`) — la pregunta de "un solo PTV" no servía para ese caso.

Se pidió cambiar la lógica: no preguntar más. Identificar el/los PTV directamente del matcheo de estructuras ya hecho. Para cada restricción condicionada por volumen de PTV: si la restricción es sobre un PTV, usar el volumen de ESE PTV (una restricción por cada PTV duplicado); si es sobre otra estructura (ej. Lung), usar la suma de los volúmenes de todos los PTVs matcheados.

### Cambio

- `Condicion.ValorObtenido`/`CumpleCondicion`: el parámetro pasó de `Structure ptv` a `double volPTV` — ya no depende de una única `Structure` elegida por diálogo, sino de un volumen ya resuelto por el llamador (propio o suma).
- `Form2.xaml.cs` / `Form2_DosPlanes.xaml.cs`: se eliminó el diálogo `SeleccionarPTV` y el campo `ptvCondicion`. Se agregaron `ptvsMatcheadosEnGrilla()` (todas las estructuras reales del matcheo cuyo `DicomType == "PTV"`, dedupe por Id) y `volPTVParaCondicion(estructuraRestriccion, ptvsMatcheados)` (volumen propio si la restricción es sobre un PTV, suma de todos si no). El título de la ventana (fx/volumen de PTV) ahora se arma con esos mismos datos, sin diálogo.
- Se eliminaron `SeleccionarPTV.xaml(.cs)` (sin más callers) y `Estructura.ptvs()` (sin más callers).

### Cómo se testeó

La lógica (`ptvsMatcheadosEnGrilla`/`volPTVParaCondicion`) es pura, sin ESAPI. Se armó `Tests/TestVolumenPTVCondicion/` (standalone, `dotnet run`) con PTVs y una estructura no-PTV (Lung) inventados, cubriendo: un solo PTV matcheado, dos PTVs matcheados (duplicado), y ningún PTV matcheado:

```
OK   Un solo PTV matcheado
OK   Restricción de PTV usa su propio volumen (no el de otro PTV)
OK   Restricción de Lung usa el volumen del único PTV matcheado
OK   Dos PTVs matcheados tras duplicar
OK   Restricción del primer PTV usa SU volumen, no la suma
OK   Restricción del segundo PTV (duplicado) usa SU volumen, no la suma
OK   Restricción de Lung usa la SUMA de ambos PTVs
OK   Sin PTV matcheado, la lista queda vacía
OK   Sin PTV matcheado, Lung condicionado por volumen de PTV da 0 (no hay PTV para sumar)

TODOS LOS CHEQUEOS OK
```

### Conclusión

- Ya no aparece ningún diálogo pidiendo elegir el PTV, en ningún caso.
- Restricciones condicionadas por volumen de PTV sobre una fila duplicada (ej. dos PTVs) se evalúan cada una contra el volumen de su propio PTV, no contra uno elegido a mano ni contra la suma.
- Restricciones condicionadas por volumen de PTV pero sobre otra estructura (ej. cobertura de Lung por volumen de PTV) usan la suma de todos los PTVs matcheados en esa plantilla/plan.

### Pendiente

- Verificación end-to-end sobre un plan real en Eclipse (con PTV único, con PTV duplicado, y con una restricción tipo Lung condicionada por volumen de PTV) — no se pudo probar contra ESAPI real en esta sesión.

---

## 2026-09-10 — Feature: filtro por condicionantes en PlantillaBlanco

### Pedido

En `PlantillaBlanco` (vista de plantilla en blanco / reporte imprimible), al cargar una plantilla con restricciones sujetas a `Condicion` (por nº de fracciones o volumen de PTV), se mostraban TODAS las restricciones mezcladas, incluyendo variantes con condiciones incompatibles entre sí (ej. "médula si 5fx" y "médula si 10fx" ambas en la lista). Se pidió agregar listboxes para filtrar por los condicionantes existentes.

### Cambio

`PlantillaBlanco.xaml` / `PlantillaBlanco.xaml.cs`:
- Se agregaron dos `ComboBox` (`CB_FiltroNumFx`, `CB_FiltroVolPTV`), debajo de la grilla, cada uno con la opción `(Todas)` + los ids de `Condicion` distintos presentes en la plantilla para ese tipo (`Tipo.NumFx` / `Tipo.VolPTV`). Cada combo solo se muestra (`Visibility`) si la plantilla tiene restricciones de ese tipo.
- `llenarAnalisis()` filtra: una restricción con condición `NumFx`/`VolPTV` se salta si el combo correspondiente tiene un id seleccionado distinto al de esa restricción. Restricciones sin condición, o con `CondicionadaPor`/`CondicionaA`, no se filtran (siguen el comportamiento previo).
- El reporte PDF/impresión usa la misma colección `filas` ya filtrada, así que respeta la selección.

### Ajuste 2026-09-10

Pedido: los filtros debían ir abajo de la grilla (no arriba) y ser desplegables (`ComboBox`, no `ListBox`); además el combo de fracciones ordenaba alfabéticamente el `id` de texto (`NumFx=1, NumFx=10, NumFx=2, ...`) en vez de numéricamente. Fix: `llenarFiltro` ahora ordena por `Condicion.ValorEsperado` (mínimo entre `ValorEsperado`/`ValorEsperado2` si la condición es "entre") en vez de por el string `id`.

### Cómo se testeó

La lógica de filtrado y de orden del combo es pura (sin ESAPI ni WPF). Se armó `Tests/TestFiltroPlantillaBlanco/` (standalone, `dotnet run`) que reproduce el loop/orden viejo contra el nuevo, con 9 restricciones inventadas (2 sin condición, 5 con `NumFx` distinto incluyendo 1/2/3/5/10, 2 con `VolPTV` distinto):

```
OK   Viejo muestra todas las restricciones sin filtrar (comportamiento reportado)
OK   Nuevo sin filtro reproduce el comportamiento viejo
OK   Nuevo filtra por NumFx=5 (excluye NumFx=10, respeta el resto)
OK   Nuevo combina ambos filtros (NumFx=10 + VolPTV<50)
Combo NumFx (viejo, alfabético): NumFx=1, NumFx=10, NumFx=2, NumFx=3, NumFx=5
Combo NumFx (nuevo, numérico):   NumFx=1, NumFx=2, NumFx=3, NumFx=5, NumFx=10
OK   Viejo ordena el combo alfabéticamente (1,10,2,3,5 - bug reportado)
OK   Nuevo ordena el combo numéricamente (1,2,3,5,10)

TODOS LOS CHEQUEOS OK
```

### Conclusión

- Sin selección de filtro (`(Todas)` en ambos combo), el comportamiento es idéntico al anterior (no rompe plantillas sin condicionantes).
- Con un id seleccionado en un combo, solo pasan las restricciones sin condición de ese tipo o cuya condición coincide con el id elegido; el otro combo (si tiene selección) filtra en simultáneo sobre las restricciones del otro tipo.
- El combo de nº de fracciones ahora lista los valores en orden numérico creciente en vez de orden alfabético del texto.

---

## 2026-08-04 — Fix: restricciones en % mal calculadas al habilitar EQD2

### Bug original

En `Form2.cs`, al tildar "Evaluar con EQD2", las restricciones expresadas en `%` (`RestriccionDosis`, `RestriccionDosisMax`, `RestriccionDosisMedia`, `RestriccionVolumen`) seguían comparando contra `prescripcionEstructura` — la dosis prescripta física original — en vez de esa misma prescripción convertida a EQD2. En `RestriccionDosis`/`RestriccionDosisMax` el bug era peor: la conversión a EQD2 se aplicaba *después* de ya haber pasado el valor a porcentaje, es decir, la fórmula cuadrática del modelo lineal-cuadrático se aplicaba sobre un número en `%`, sin sentido físico.

### Fix

- `RestriccionDosis.cs` / `RestriccionDosisMax.cs`: se separó la extracción de dosis (`dosisEnGy`) de la conversión a `%`. En el overload EQD2, ahora se aplica `EQD2.Dosis2Gy` sobre la dosis en Gy primero, y el `%` se calcula al final contra `EQD2.Dosis2Gy(prescripcionEstructura, alfaBeta, numeroFracciones)`.
- `RestriccionDosisMedia.cs`: el cálculo de la dosis media en EQD2 ya era correcto; solo se corrigió el denominador del `%` para usar la prescripción convertida a EQD2.
- `RestriccionVolumen.cs`: el `%` de la prescripción usado para obtener el umbral de dosis a buscar en la DVH ahora se calcula sobre la prescripción convertida a EQD2 antes de invertir con `EQD2.DosisFxAlt`.
- `RestriccionIndiceConformidad.cs`: no se tocó — su overload EQD2 ya devuelve `NaN` (el índice de conformidad no es una magnitud convertible a EQD2), no tenía el bug.

### Cómo se testeó

ESAPI (Varian) no corre fuera de Eclipse, así que no se puede instanciar `PlanSetup`/`Structure` en un test aislado. La lógica de `EQD2.cs` y las conversiones a `%` que tenían el bug, en cambio, son matemática pura sin dependencia de ESAPI. Se armó un proyecto de consola standalone (`Tests/TestEQD2/`, sin dependencias) que:

1. Copia literal de `EQD2.Dosis2Gy` / `EQD2.DosisFxAlt` (sin cambios, es el mismo código de `EQD2.cs`).
2. Para cada tipo de restricción, reproduce la fórmula **vieja** (previa al fix) y la **nueva** (posterior al fix) con números inventados, y compara:
   - **Modo sin EQD2**: la fórmula no cambió → viejo y nuevo deben dar exactamente igual.
   - **Modo con EQD2**: viejo (bug) y nuevo (fix) deben dar valores distintos, y el nuevo debe coincidir con el cálculo esperado a mano (dosis y prescripción, ambas llevadas a EQD2, recién ahí el cociente).

Correr el test:

```
cd Tests/TestEQD2
dotnet run
```

### Resultados (números inventados)

**Caso 1 — `RestriccionDosisMedia`**, α/β=3, 5 fx, prescripción física = 25 Gy (5 Gy/fx), dosis media EQD2 = 30 Gy:

| Modo | Viejo | Nuevo | Esperado a mano |
|---|---|---|---|
| Sin EQD2 (dosis media física 22.5 Gy) | 90 % | 90 % | igual, no debe cambiar |
| Con EQD2 | 120 % (30 / 25) — **mal**, referencia física | 75 % (30 / 40) — prescripción EQD2 = `EQD2.Dosis2Gy(25,3,5)` = 40 Gy | 75 % |

**Caso 2 — `RestriccionDosis` / `RestriccionDosisMax`**, α/β=10, 3 fx, prescripción física = 24 Gy (8 Gy/fx), dosis extraída del DVH = 9 Gy físicos:

| Modo | Viejo | Nuevo | Esperado a mano |
|---|---|---|---|
| Sin EQD2 | 37.5 % (9/24) | 37.5 % | igual, no debe cambiar |
| Con EQD2 | 70.3 % — **mal**, fórmula EQD2 aplicada sobre un `%` (37.5) en vez de sobre Gy | 27.22 % — dosis EQD2 = 9.75 Gy, prescripción EQD2 = 36 Gy, 9.75/36 | ≈27 % |

**Caso 3 — `RestriccionVolumen`**, α/β=3, 5 fx, prescripción física = 25 Gy, restricción V95%:

| Modo | Viejo | Nuevo | Esperado a mano |
|---|---|---|---|
| Sin EQD2 | umbral = 23.75 Gy | 23.75 Gy | igual, no debe cambiar |
| Con EQD2 | busca en la DVH el volumen a 18.0 Gy físicos — **mal**, 95% de la prescripción física tratado como si fuera dosis EQD2 objetivo | busca a 24.22 Gy físicos — 95% de la prescripción EQD2 (40 Gy), invertido a dosis física con `DosisFxAlt` | 24.22 Gy |

Salida real del test (`dotnet run` en `Tests/TestEQD2`):

```
=== Caso 1: RestriccionDosisMedia, unidadValor="%" ===
OK   Sin EQD2 (Old==New, no debe cambiar): esperado=90 obtenido=90
  prescripcionEQD2 = 40 Gy (prescripción física 25 Gy convertida)
OK   Con EQD2 - valor BUG (se espera 120%, referencia incorrecta): esperado=120 obtenido=120
OK   Con EQD2 - valor FIX (se espera 75% aprox, referencia correcta): esperado=75 obtenido=75

=== Caso 2: RestriccionDosis / RestriccionDosisMax, unidadValor="%" ===
OK   Sin EQD2 (Old==New, no debe cambiar): esperado=37.5 obtenido=37.5
  % físico (antes de aplicar mal EQD2) = 37.5%
OK   Con EQD2 - valor BUG viejo (fórmula aplicada sobre %, resultado sin sentido físico): esperado=70.31 obtenido=70.3
  doseEQD2 = 9.8 Gy, prescripcionEQD2 = 36 Gy
OK   Con EQD2 - valor FIX (dosis y prescripción, ambas en EQD2, luego %): esperado=27.08 obtenido=27.22

=== Caso 3: RestriccionVolumen, unidadCorrespondiente="%" ===
OK   Sin EQD2 (no debe cambiar): esperado=23.75 obtenido=23.75
  dosis física buscada en la DVH -> Old=18 Gy vs New=24.22 Gy
OK   Con EQD2 - dosis física buscada, BUG: esperado=18 obtenido=18
OK   Con EQD2 - dosis física buscada, FIX: esperado=24.22 obtenido=24.22

TODOS LOS CHEQUEOS OK
```

### Conclusión

- **Sin EQD2 habilitado**: los tres casos dan idéntico resultado viejo vs. nuevo — el fix no afecta el flujo normal (sin EQD2).
- **Con EQD2 habilitado**: el nuevo código corrige el error y da los valores esperados a mano; el viejo código reproduce el bug reportado (y en el caso de `RestriccionDosis`/`RestriccionDosisMax`, se confirma que el bug era aún más grave que solo "referencia incorrecta" — aplicaba la fórmula EQD2 sobre un porcentaje).

---

## 2026-08-04 — Fixes de robustez en comparación de dos planes (`Form2_DosPlanes.cs`)

Bug reportado: "a veces falla el análisis" al comparar planes. Revisión de lógica encontró 5 problemas (ninguno confirmado aún como LA causa reportada, se corrigieron todos y se agregó logging para diagnosticar la próxima vez que aparezca):

1. **`plan2` se auto-elegía con `.Where(...).First()`** (busca un plan cuyo Id contenga "cam") — tiraba `InvalidOperationException` sin mensaje al usuario si el curso no tenía ningún plan así. Solo pasa en modo standalone (`!hayContext`), en el plugin (`hayContext`) el segundo plan viene dado por Eclipse. Fix: `FirstOrDefault()` + aviso al usuario si no se encontró.
2. **La asociación de estructuras (`DGV_Estructuras`) solo se resolvía contra `plan`**, y esa misma asociación (por ID) se reusaba para buscar la estructura en `plan2`. Si `plan2` tiene otro structure set con otros IDs, fallaba por estructura. Fix: nuevo método `estructuraCorrespondiente2` que re-asocia por nombre/alias directo contra el structure set de `plan2`, igual que se hace para `plan` en `asociarEstructuras()`.
3. **No se chequeaba si `plan2` estaba calculado** (solo `plan`). Fix: mismo chequeo "no está calculado" para ambos planes.
4. **Faltaba el filtro por `Condicion`** que sí tiene `Form2.cs` — se analizaban todas las restricciones de la plantilla sin importar si su condición (por nº de fracciones o volumen de PTV) se cumplía. Las restricciones condicionadas no están en uso activo todavía (pendiente de la importación de constraints SBRT/RC), pero se corrigió para no dejar la divergencia. Fix: mismo filtro `if (restriccion.condicion != null && !CumpleCondicion(...)) continue;` y diálogo de selección de PTV cuando la plantilla tiene condiciones de tipo VolPTV.
5. **Índice de fila incorrecto**: `DGV_Analisis.Rows[i]` en vez de `Rows[j]` para pintar el botón de `RestriccionDosisMax`. Antes del fix #4, `i` y `j` siempre coincidían (nunca se salteaba una fila) así que no se notaba; con el filtro de #4 ya activo, una restricción salteada hace que `i` y `j` diverjan. Fix: usar `j` (índice real de fila) en todos los casos.
6. **Logging**: se cambió `File.WriteAllText("log.txt", ...)` (sobreescribía el log en cada error, se perdía el historial) por un `logError()` compartido que hace `AppendAllText` con timestamp y contexto (paciente, IDs de ambos planes, restricción en curso). Se agregó `try/catch` por restricción dentro del loop de análisis (para que una restricción rota no tire abajo la comparación completa) y un `try/catch` general en `BT_Analizar_Click`.

### Cómo se testeó

Los puntos 1, 2, 3 y 6 dependen de ESAPI (no se pueden instanciar `PlanSetup`/`Structure`/`Course` fuera de Eclipse) y no tienen lógica de cálculo nueva — son guard clauses / manejo de excepciones directos, sin ramas que ameriten un test aislado.

El punto 4+5 (filtro de `Condicion` + reindexado de filas) sí es lógica nueva pura, sin ESAPI: qué restricciones generan fila y en qué posición. Se armó `Tests/TestFiltroCondicion/` (standalone, `dotnet run`) que simula el loop viejo (sin filtro, fila = i) contra el nuevo (con filtro, fila = j), con 5 restricciones inventadas (2 marcadas como "no aplica"):

```
Filas (viejo, sin filtro): R0_PTV_D95, R1_MEDULA_5fx, R2_PULMON, R3_RIÑON_5fx, R4_HIGADO
Filas (nuevo, con filtro): R0_PTV_D95, R2_PULMON, R4_HIGADO
OK   Viejo analiza restricciones que no aplican (bug #4 reproducido)
OK   Viejo agrega una fila por cada restricción de la plantilla, sin filtrar
OK   Nuevo filtra las que no aplican (fix #4)
OK   fix #5: R2 cae en la fila j=1 (no en i=2) tras saltear R1
OK   fix #5: R4 cae en la fila j=2 (no en i=4) tras saltear R1 y R3

TODOS LOS CHEQUEOS OK
```

Confirma que el nuevo loop filtra correctamente y que el índice de fila (`j`) usado para pintar el botón de `RestriccionDosisMax` sigue apuntando a la fila correcta aun cuando se saltean restricciones intermedias — el bug #5 (usar `i`) se hubiera notado recién con el fix #4 puesto, por eso valía la pena testear ambos juntos.

### Pendiente / no corregido

Al portar el filtro de `Condicion` desde `Form2.cs`, se notó que `colorCeldasAnidadas` (para restricciones `CondicionadaPor`) busca la fila de la restricción condicionante con `plantilla.listaRestricciones.IndexOf(restriccionCondicionante)` — eso da un índice en el espacio de la plantilla (`i`), pero se usa como índice de fila (`DGV_Analisis.Rows[...]`, espacio `j`). Si alguna restricción anterior a la condicionante se saltea, ese índice queda mal. Este bug ya existe igual en `Form2.cs` (no lo introduje yo, lo importé al portar el mismo patrón). No lo corregí porque las restricciones condicionadas no están en uso activo todavía — queda anotado para cuando se retome la importación de constraints SBRT/RC.

---

## 2026-08-05 — Matcheo aproximado, memoria por plan, reordenamiento de plantillas, duplicar estructura, ocultar no analizadas, unificar colores

Seis cambios pedidos juntos sobre el flujo de análisis (`Form2.cs`, `Estructura.cs`, `Plantillla.cs`, `Main.cs`), más un archivo nuevo compartido `MemoriaPlan.cs`.

### 1) Matcheo aproximado de estructuras (Damerau-Levenshtein)

Antes: `Estructura.asociarConLista` solo hacía matcheo exacto (case-insensitive) contra `nombresPosibles`; si fallaba, quedaba en blanco (o memoria vieja por StructureSet).

Ahora: `Estructura.DistanciaDamerauLevenshtein` + `Estructura.candidatosPorDistancia` (`Estructura.cs`) calculan la distancia de cada estructura del plan contra los nombres posibles. En `Form2.asociarEstructuras()`:
- El combo de cada fila se llena ordenado por distancia ascendente (antes era el orden arbitrario de `Estructura.listaEstructurasID`).
- Si hay match exacto, se usa ese (sin cambios de comportamiento).
- Si no, y la memoria de plan no tiene una asociación válida, se autoselecciona el candidato más cercano si su distancia es `<= Estructura.DistanciaMaximaSugerida` (3); si no, queda en blanco para elección manual (igual que antes).

### 2) Memoria de matcheo/prescripción: por plan, con fallback y manejo de errores

Antes: la memoria (`paresEstructuras\` y `prescripciones\`) se guardaba por `PacienteID + StructureSetId` (dos planes con el mismo set de estructuras compartían memoria sin querer), sin fallback entre planes, y `leerArchivoParEstructura`/`leerArchivoPrescripcion` no tenían try/catch (una línea corrupta o el separador decimal `,` de la cultura es-AR colisionando con el separador de campo `,` producía crash o dato truncado silenciosamente).

Ahora (`MemoriaPlan.cs` + `Form2.cs`):
- Clave = `PacienteID_CursoId_PlanId` (por plan, vía `MemoriaPlan.clave`).
- Si el plan actual no tiene memoria propia pero el paciente sí tiene en otro plan, se usa la del plan más reciente (`MemoriaPlan.rutaParaLeer`/`rutaArchivoFallbackPaciente`) como punto de partida; en cuanto el usuario analiza, se escribe en el archivo del plan actual (de ahí en adelante usa su propia memoria).
- Lectura/escritura envueltas en try/catch (archivo corrupto o ruta de red caída avisa y no rompe la apertura del formulario).
- Se fuerza `CultureInfo.InvariantCulture` al leer/escribir dosis, eliminando la colisión con el separador de campo.
- Bug real corregido en `prescripcionPredefinida`: si el archivo de memoria existía pero no tenía la estructura puntual, el código viejo (`if/else if`) nunca llegaba a las heurísticas por nombre de plantilla (Cabeza/Prostata/Mama) y devolvía la prescripción física sin más. Ahora se busca la estructura específica en la memoria y, si no está, se aplican las heurísticas igual que si no hubiera memoria.

### 3) Selección automática de plantilla: reordenamiento de criterios + memoria por plan

Antes: `filtrarPorFracciones` (heurística de nombre `_Nfx`) se aplicaba **antes** de puntuar por coincidencia de estructuras, pudiendo descartar la plantilla que en realidad matchea mejor si no seguía esa convención de nombre.

Ahora: se puntúan **todas** las plantillas por coincidencia de estructuras primero (criterio objetivo); el filtro por fracciones pasa a ser el primer desempate dentro de `reconocerPlantillaFino` (antes de imrt/hipo/der/pros), solo cuando hay empate de score. Se agregó memoria de plantilla seleccionada por plan (`Plantilla.GuardarSeleccion`/`plantillaRecordada`, misma clave y mismo fallback al plan más reciente del paciente que en el punto 2); si existe, se usa directamente sin correr la heurística. Se persiste al confirmar en `Main.BT_AplicarAUnPlan_Click`.

### 4) Duplicar estructura a analizar

Nuevo botón "Duplicar estructura" en `Form2` (junto a `DGV_Estructuras`): clona todas las restricciones del slot seleccionado bajo un nuevo slot `"Nombre (2)"` (usando el método `crear` que cada `IRestriccion` ya exponía), permitiendo matchear una segunda estructura real del plan y aplicar los mismos constraints por separado. Se guarda en memoria por plan (`duplicadosEstructura\`, mismo esquema de `MemoriaPlan`) y se reaplica automáticamente al reabrir el mismo plan.

### 5) Ocultar restricciones no analizadas

Nuevo checkbox `CHB_OcultarNoAnalizadas` en `Form2`, tildado por defecto. En `llenarDGVAnalisis`, las filas cuya estructura no pudo asociarse (`estructura == null`) quedan con `Visible = false` cuando el checkbox está tildado.

### 6) Unificar coloreado pass/fail

`Form2.colorCelda`/`colorCeldasAnidadas` y `Form2_DosPlanes.colorCelda`/`colorCeldasAnidadas` tenían la misma paleta duplicada palabra por palabra. Se extrajo a `ColorearAnalisis.cs` (clase estática compartida); ambos formularios delegan ahí. No se tocó la paleta en sí (mismos colores). **Unificación visual más amplia (fuentes/tamaños de botones entre Main/Form2/Form3) y evaluación de WPF quedaron fuera de este cambio**, a pedido explícito: requieren verificación visual en el Designer que no se puede hacer a ciegas editando texto.

### Cómo se testeó

Ninguna de las cuatro piezas de lógica nueva (Levenshtein, fallback de memoria por plan, reordenamiento de criterios, fix de `prescripcionPredefinida`) depende de ESAPI en su núcleo, así que se aisló cada una en `Tests/TestMejoras/` (standalone, `dotnet run`), reproduciendo viejo vs. nuevo comportamiento con datos inventados:

```
cd Tests/TestMejoras
dotnet run
```

Resultado:

```
=== 1) Damerau-Levenshtein ===
OK   Idénticas -> distancia 0
OK   Case-insensitive -> distancia 0
OK   Una sustitución -> distancia 1
OK   Transposición adyacente cuenta como 1 (Damerau, no Levenshtein simple)
OK   PTV_5400 vs PTV_5040 (transposición) distancia baja
OK   Nombres muy distintos -> distancia alta
Orden real: PTV(0), PTV_2(2), MEDULA(6)
OK   Exacto primero
OK   Aproximado (PTV_2) segundo, antes que MEDULA

=== 2) Fallback de memoria por plan ===
OK   Plan sin memoria propia cae al plan más reciente del paciente (Plan2, el último escrito)
OK   Paciente sin ningún plan con memoria -> null (no rompe, deja en blanco)

=== 3) Orden de criterios en SeleccionarAutomaticamentePlantilla ===
OK   Viejo: el filtro por fracciones descarta a B aunque matchea mejor estructuras (bug reproducido)
OK   Nuevo: puntúa primero, elige B (mejor match de estructuras) sin importar el nombre
OK   Nuevo: con empate de score, fracciones desempata correctamente (elige C, 15fx)

=== 4) prescripcionPredefinida: memoria parcial no debe tapar las heurísticas ===
OK   Viejo: memoria existe pero no tiene 'WB' -> devuelve la prescripción física sin heurística (bug)
OK   Nuevo: memoria no tiene 'WB' -> aplica la heurística de Mama (40.05)
OK   Nuevo: memoria SÍ tiene 'Sb' -> usa la memoria (60), no la heurística

TODOS LOS CHEQUEOS OK
```

El resto (duplicar estructura, checkbox ocultar, coloreado, wiring de `DataGridView`) depende de `PlanningItem`/`Structure`/`DataGridView` reales y del Designer — se verificó por lectura de código y compilación completa con MSBuild (VS2022), sin errores:

```
MSBuild ... ExploracionPlanes.csproj /t:Build
  ExploracionPlanes -> ...\bin\Debug\ExploracionPlanes.exe
```

### Pendiente / diferido

- **Unificación visual completa (fuentes, tamaños de botones) entre `Main`, `Form2` y `Form3`, y evaluación de migración a WPF**: quedan fuera de este cambio a pedido explícito del usuario (ver punto 6). Requieren abrir cada formulario en el Designer de Visual Studio y verificar visualmente — no es seguro hacerlo a ciegas editando los `.Designer.cs` a mano.
- **Duplicar estructura**: no maneja el caso de una restricción `CondicionadaPor` (referencia a otra restricción por `etiqueta`) dentro del set duplicado — quedaría con la etiqueta de la restricción condicionante original, no la duplicada. No está en uso activo hoy (ver limitación ya documentada de restricciones condicionadas), se deja para cuando se retome esa funcionalidad.

---

## 2026-08-05 — Unificación visual: numeración de pasos y fuente de botones primarios

Cambio puramente de UI (`.Designer.cs`), sin tocar lógica — no aplica la convención de test antes/después (no hay comportamiento que verificar, solo layout). Basado en los screenshots que pasó el usuario (`screenshots/Plugin` y `screenshots/Standalone`):

- **Numeración de pasos duplicada/faltante en los editores de plantilla**: `Form1_ext.cs` tenía dos secciones marcadas "3." (`GB_NuevaRestriccion` = "3. Nueva Restricción" y `label5` = "3. Nota (opcional)"), y la lista de restricciones cargadas no tenía número de paso. Se agregó `label7` = "4. Restricciones cargadas" y se corrió `label5` a "5. Nota (opcional)". En `Form1_prioridades.cs` (mismo problema, sin el grupo de Condiciones) se agregó `label8` = "3. Restricciones cargadas" y se corrió `label5` de "3." a "4. Nota (opcional)".
- **Fuente de botones "de commit" inconsistente**: en `Form2`/`Form2_DosPlanes` los botones finales (Analizar, Imprimir, Guardar Reporte) usan `Microsoft Sans Serif 10F`; en `Form1_ext`/`Form1_prioridades` (`BT_GuardarPlantilla`) y `Form3` (`BT_Analizar`, `BT_GuardarPaciente`, `BT_Exportar`) usaban el font por defecto del formulario (8.25F). Se les agregó el mismo `Font` de Form2, sin tocar tamaño/posición (los textos largos de `BT_GuardarPaciente`/`BT_Exportar` ya usan una altura de 37px que da margen).

No se tocó nada más (paneles, DataGridView, orden de tabulación) para no arriesgar romper layout que no puedo verificar visualmente en esta máquina (ver limitación de captura de pantalla abajo). Compila limpio con MSBuild.

### Corrección tras screenshots reales del usuario

El usuario confirmó con capturas reales (`screenshots/Nuevas/`, standalone) que la numeración de Form1_ext/Form1_prioridades quedó bien. Pero encontró 2 problemas:

1. **`BT_GuardarPaciente` en Form3 cortaba el texto** ("7. Guardar y" en vez de "7. Guardar y cerrar paciente") al subir la fuente a 10pt — el botón es angosto (109px) para ese texto largo a esa fuente. Se revirtió el `Font` en `BT_GuardarPaciente` y, por el mismo riesgo, en `BT_Exportar` (texto "Exportar información", no confirmado en el screenshot). Se dejó el cambio de fuente solo en `BT_Analizar` (texto corto "Analizar", sin riesgo, igual que en Form2).
2. **Bug real en "Duplicar estructura" (item 4), encontrado en un screenshot de Eclipse real**: al duplicar `PTV_Low-04` (restricciones D95%/D99%, en `%`), el estructura nuevo `PTV_Low-04 (2)` no aparecía en la tabla "Ajustar prescripciones" → `prescripcionEstructura` quedaba en 0 → el análisis daba `Infinity%` en vez de un porcentaje real. Causa: `BT_DuplicarEstructura_Click` (Form2.cs) solo refrescaba `llenarDGVEstructuras()`, no `llenarDGVPrescripciones()`. Fix: se agregó el segundo refresco en el mismo click. (El flujo de reapertura del plan —constructor y `BT_SeleccionarPlan_Click`— ya llamaba a ambos, así que solo el click en vivo tenía el bug).

Confirmado además en ese mismo screenshot que el resto de "Duplicar estructura" funciona como se pidió: `PTV_Low-04 (2)` matcheado a una segunda estructura real del plan (`zOptiPTV_Low-04`), con sus propias filas D95%/D99% en el análisis, y el checkbox "Ocultar no analizadas" (item 5) funcionando (tildado, oculta lo no matcheado).

### Limitación descubierta: no puedo autoverificar visualmente en esta PC

Se probó lanzar el `.exe` standalone acá y capturarlo con PowerShell (`GetWindowRect` + `Graphics.CopyFromScreen`): el proceso corre y la ventana tiene un handle válido, pero la captura devuelve contenido desactualizado/de otra sesión, no lo que la ventana realmente renderiza en ese momento (probable framebuffer no refrescado sin uso interactivo real en el momento de la captura). Por eso estos cambios de layout se hicieron calculando coordenadas a mano a partir del código y de los screenshots ya provistos, sin loop de verificación visual propio — pendiente que el usuario confirme con una captura real desde esa PC.

---

## 2026-08-05 — Dos bugs en comparación de dos planes (`Form2_DosPlanes.cs`)

### Bug 1: sin opción de evaluar EQD2

`Form2.cs` tiene checkbox `CHB_EvaluarConEQD2` + columna α/β en `DGV_Estructuras` + rama EQD2 en el análisis; `Form2_DosPlanes.cs` no tenía nada de eso, no había forma de comparar dos planes en EQD2.

Se trasladó la lógica: checkbox `CHB_EvaluarConEQD2` nuevo, `CHB_EvaluarConEQD2_CheckedChanged`/`cargarAlfaBetaDGVEstructuras` calcados de Form2 (valida que ni `plan` ni `plan2` sean `PlanSum` ni tengan dosis/fracción de 200cGy), y en `analizarRestriccion` cada plan usa `restriccion.analizarPlanEstructura(..., alfaBeta, numeroFracciones)` con **su propio** número de fracciones (`plan` y `plan2` pueden estar fraccionados distinto) pero el **mismo** α/β (es una propiedad de la anatomía, no del plan — se busca una sola vez contra el structure ID de `plan`, antes de que la variable `estructura` se reasigne al structure de `plan2`). Se agregó también el acumulado de nota "Se analizaron evaluando EQD2: ..." igual que Form2.

**Diferencia de layout con Form2**: Form2 ensancha `DGV_Estructuras` (+60px) al mostrar la columna α/β porque tiene margen antes del siguiente control. En `Form2_DosPlanes`, `DGV_Prescripciones` arranca a solo 282px del borde de `DGV_Estructuras` — ensanchar igual que Form2 la superpondría. Se dejó `DGV_Estructuras` con su ancho fijo y que `AutoSizeColumnsMode` acomode las 3 columnas ahí adentro (más apretado, sin superposición).

### Bug 2: en standalone, "Comparar dos planes" mostraba la lista de planes vacía antes de entrar a Eclipse

Causa: `Main.BT_CompararPlanes_Click` siempre abría el diálogo `PlanesParaComparar` usando el campo `planesParaComparar`, que **solo se llena con el contexto que pasa `Script.cs` desde Eclipse** (`_plansContext`/`_planSumsContext`). En standalone ese campo nunca tiene datos → diálogo con lista vacía siempre. Además, el bloque de "plan Mod" que sigue usa `planContext.Id`, que es `null` en standalone → hubiera tirado `NullReferenceException` si alguna plantilla tenía restricciones en plan Mod.

Fix, sin replicar la lógica de contexto de Eclipse en Main (correctamente identificado como "rebuscado" por lo compleja que sería):

- `Main.BT_CompararPlanes_Click`: el diálogo `PlanesParaComparar` y el chequeo de plan Mod ahora corren **solo si `hayContext`** (plugin). En standalone, abre `Form2_DosPlanes` directo con paciente/plan en `null`, igual que ya se hace con `Form2` para un solo plan — el formulario elige paciente/curso/planes él mismo.
- `Form2_DosPlanes`: `LB_Planes` pasa a multiselección (`SelectionMode = MultiExtended`). `planSeleccionado()` ya no adivina el segundo plan buscando "cam" en el nombre (ese hack quedaba documentado como corrección parcial el 2026-08-04); ahora toma directamente los dos ítems seleccionados en `LB_Planes`. `BT_SeleccionarPlan` y `BT_Analizar` se habilitan solo cuando hay **exactamente 2** planes seleccionados (antes era `== 1`). Label actualizado a "3. Seleccionar 2 planes".

### Cómo se testeó

Ambos bugs dependen de ESAPI real (`PlanSetup`/`Application.CreateApplication`) para reproducirse de punta a punta, así que no se armó un test aislado nuevo — se verificó por lectura de código (los mismos puntos de entrada/guard clauses que ya se testearon en la sesión del 2026-08-04 para este archivo) y compilación completa con MSBuild, sin errores. Pendiente de confirmación del usuario en Eclipse real (plugin) y standalone.

---

## 2026-08-05 — Migración de Form1_prioridades a WPF (Fase 3): IRestriccion.editar() ya no depende de WinForms

### Motivo del cambio de alcance

Al migrar `Form1_prioridades` (editor de plantillas, sin `DataGridView` — usa `LB_listaRestricciones`,
un `ListBox` simple) se encontró que `IRestriccion.editar(ComboBox, TextBox, ...)` — parte de la
interfaz, implementada en `RestriccionDosis`/`RestriccionDosisMedia`/`RestriccionDosisMax`/
`RestriccionVolumen`/`RestriccionIndiceConformidad` — tomaba controles `System.Windows.Forms`
directo por parámetro y les escribía `.Text`/`.SelectedIndex`. No compila contra controles WPF.
`Plantilla.editar(TextBox, CheckBox, ...)` (el overload de 4 parámetros) tenía el mismo problema.

Se verificó que ambos métodos tienen un solo caller cada uno:
- `IRestriccion.editar(...)` (el de 11 parámetros): solo `Form1_prioridades.cs`. `editarGrupo(...)`
  (el que sí usa `DataGridView`) es un método distinto, usado solo por `Form1_ext.cs` — no se tocó.
- `Plantilla.editar(...)` de 4 parámetros: solo `Form1_prioridades.cs`. El overload de 6 parámetros
  (con las listas de condiciones) es el que usa `Form1_ext.cs` — no se tocó.

Esto permitió una refactorización acotada: `Plantilla.editar(...)` de 4 parámetros se eliminó (su
lógica se inlineó en el único call site, ya que `Plantilla.nombre`/`esParaExtraccion`/`nota` ya son
propiedades públicas). `IRestriccion.editar(...)` pasó a `IRestriccion.datosEdicion()`, que devuelve
un DTO nuevo (`DatosEdicionRestriccion.cs`) sin ninguna referencia a controles de UI — el formulario
que lo llame decide a qué control asignar cada campo. Cero impacto en `Form1_ext`/`Form2`/
`Form2_DosPlanes` (no tocan estos métodos).

### Qué se testeó

La lógica de `datosEdicion()` por tipo de restricción es pura (sin ESAPI) pero no se puede probar
importando las clases reales (el proyecto principal no compila como librería .NET moderna por las
referencias a ESAPI). Se replicó la lógica literal en un test nuevo aislado,
`Tests/TestEdicionRestricciones/`, siguiendo la convención de `Tests/TestMejoras/`:

```
=== datosEdicion(): índice de tipo por restricción ===
OK   RestriccionDosis -> índice 0
OK   RestriccionDosisMedia -> índice 1
OK   RestriccionDosisMax -> índice 2
OK   RestriccionVolumen -> índice 3
OK   RestriccionIndiceConformidad -> índice 4
=== datosEdicion(): join de nombres alternativos (mismo comportamiento que editar() original) ===
OK   Dosis/IC: sin línea vacía inicial
OK   DosisMedia/DosisMax/Volumen: con línea vacía inicial (bug preexistente preservado)
OK   Dosis/IC con un solo alt: sin salto de línea sobrante
OK   DosisMedia/DosisMax/Volumen con un solo alt: sigue con línea vacía inicial
=== datosEdicion(): ValorCorrespondiente es null solo para Dmedia/Dmax (no se edita para esos tipos) ===
OK   Dosis expone ValorCorrespondiente ('12')
OK   Volumen expone ValorCorrespondiente ('95')
OK   IndiceConformidad expone ValorCorrespondiente ('60')
OK   DosisMedia NO expone ValorCorrespondiente (queda null, el caller no toca el TextBox)
OK   DosisMax NO expone ValorCorrespondiente (queda null, el caller no toca el TextBox)

TODOS LOS CHEQUEOS OK
```

Importante: 3 de los 5 tipos (`DosisMedia`/`DosisMax`/`Volumen`) tenían — y siguen teniendo, a
propósito, sin corregir — un bug preexistente donde el join de nombres alternativos antepone una
línea vacía inicial (`TB_nombresAlt.Text += "\r\n" + nombre` desde `i=0` en vez de comprobar `i>1`
como hacen `Dosis`/`IndiceConformidad`). No se corrigió porque no era el pedido y cambiar el
comportamiento observable de un campo de texto real sería un cambio funcional aparte, no parte de
la migración a WPF.

### El resto (formulario completo)

Depende del `Estructura`/`Plantilla`/DTOs con datos reales y del layout — se verificó por lectura de
código y compilación completa con MSBuild (VS2022), sin errores, y por el circuito de screenshots
del usuario (`screenshots/WPF/`) ya establecido en fases anteriores.

---

## 2026-08-11 — Nuevo tipo de restricción: `RestriccionVolumenCritico`

### Qué se agregó

`RestriccionVolumenCritico.cs`, nueva implementación de `IRestriccion`, análoga a `RestriccionVolumen`
pero para volumen sano mínimo: dado un OAR con volumen total `V_total`, y un constraint tipo
`V_critico > D` (o en `%`), se busca primero `V(D)` (volumen que recibe la dosis `D`, igual que
`RestriccionVolumen`) y se evalúa el constraint contra `V_sano = V_total - V(D)` en vez de contra
`V(D)` directamente. En `%`, como `GetVolumeAtDose` con `VolumePresentation.Relative` ya devuelve el
volumen relativo al total, `V_sano% = 100 - V(D)%` (no hace falta `Structure.Volume`). En volumen
absoluto, `V_sano = Structure.Volume - V(D)`.

Todo el resto (`cumple()`, etiquetas, `crear()`, `datosEdicion()` con `IndiceTipoRestriccion = 5`,
`chequearSamplingCoverage`, etc.) es copia literal de `RestriccionVolumen.cs` — la única lógica nueva
es el cálculo de `V_sano` dentro de los dos overloads de `analizarPlanEstructura`.

### Integración en los editores de plantillas (mismo día)

Se agregó "Volumen crítico" al final de `CB_TipoRestriccion` (índice 5, después de "IC") en:
- `Form1_prioridades.xaml` / `.xaml.cs` (editor WPF activo): nuevo `esRestriccionVolumenCritico()`,
  rama en `actualizarPorRestriccion()` (comparte UI con `esRestriccionVolumen()`: dosis correspondiente
  + volumen esperado/tolerado) y en `restriccionActual()`.
- `Form1_ext.Designer.cs` / `Form1_ext.cs` (editor WinForms con condiciones, diferido de la migración a
  WPF pero sigue compilando y en uso): mismo patrón, en `actualizarPorRestriccion()` y
  `restriccionesActuales()`.

Se agregó al final del combo (no en medio de "Volumen"/"IC") para que el índice quede en 5, igual al
`IndiceTipoRestriccion = 5` ya fijado en `RestriccionVolumenCritico.datosEdicion()`.

No se tocó `DesdeCSV.cs`: ese archivo no es un dispatch general por tipo de restricción, es el parser
de un formato CSV puntual (columnas "V"/"mean"/"cc" del consorcio UK/Timmerman) sin ninguna noción de
volumen sano — no hay lugar natural donde encajaría un nuevo tipo ahí.

### Cómo se testeó la integración

El dispatch por índice (`esRestriccionVolumen()`, `esRestriccionIndiceConformidad()`, etc.) es lógica
pura de branching, replicada en `Tests/TestVolumenCritico/Program.cs` para confirmar que agregar el
índice 5 no corrió el índice 4 (`IndiceConformidad`, que antes era el último y el catch-all `else`):

```
=== Dispatch por índice de CB_TipoRestriccion (post-integración) ===
OK   Índice 3 sigue mapeando a Volumen (sin regresión)
OK   Índice 4 sigue mapeando a IndiceConformidad (sin regresión)
OK   Índice 5 (nuevo, al final del combo) mapea a VolumenCritico
```

El resto (WPF/WinForms real: visibilidad de controles, `DataGridView`, Designer) se verificó por
lectura de código — mismo patrón ya usado por `esRestriccionVolumen()` en ambos formularios, sin
tocar el layout de los controles (solo se agrega un `ComboBoxItem`/string al final de la lista
existente). Pendiente de confirmación visual del usuario en Eclipse/standalone, igual que otros
cambios de estos formularios.

### Cómo se testeó

La resta `V_total - V(D)` (o `100 - V(D)%`) y el criterio `cumple()` (idéntico al de `RestriccionVolumen`,
solo que evaluado sobre `V_sano`) son matemática pura sin ESAPI. Se armó `Tests/TestVolumenCritico/`
(standalone, sin dependencias):

```
cd Tests/TestVolumenCritico
dotnet run
```

Casos (números inventados), volumen absoluto (`V_total=50 cm3`) y en `%`, más el caso `esMenorQue=true`:

```
=== V_sano en cm3, V_total=50 ===
OK   V(D)=15cm3 -> Vsano=35cm3
OK   Constraint Vsano>30, sin tolerancia -> cumple (0)
OK   V(D)=28cm3 -> Vsano=22cm3
OK   Constraint Vsano>30 (tolerado 20) -> tolerado (1)
OK   V(D)=35cm3 -> Vsano=15cm3
OK   Constraint Vsano>30 (tolerado 20) -> no cumple (2)
=== V_sano en % ===
OK   V(D)=40% -> Vsano=60%
OK   Constraint Vsano>50%, sin tolerancia -> cumple (0)
OK   V(D)=70% -> Vsano=30%
OK   Constraint Vsano>50% (tolerado 25%) -> tolerado (1)
OK   V(D)=80% -> Vsano=20%
OK   Constraint Vsano>50% (tolerado 25%) -> no cumple (2)
=== esMenorQue=true (constraint invertido) ===
OK   Vsano=35, constraint Vsano<40 -> cumple (0)

TODOS LOS CHEQUEOS OK
```

El resto (extracción real de `V(D)` vía `GetVolumeAtDose`/DVH, `Structure.Volume`) depende de ESAPI
real y es copia literal del mismo código ya usado y testeado en `RestriccionVolumen` — se verificó por
lectura de código y compilación completa con MSBuild, sin errores.

---

## 2026-08-14 — `DesdeCSV.cs`: importador de plantillas unificadas SBRT/RC por fraccionamiento + cobertura PTV por volumen

### Qué se cambió

Antes: SBRT (1/3/5/15fx) y RC (1/3/5fx) eran 7 archivos `Plantilla` separados a mano, distinguidos solo
por convención de nombre (`_Nfx`). `DesdeCSV.LeerTabla()` era un script de un solo uso, hardcodeado a un
único CSV local (`C:\Users\Varian\Downloads\...`), que generaba una `Plantilla` nueva por corrida.

Ahora: `DesdeCSV.GenerarPlantillasUnificadas(carpetaTablas)` recorre todos los `tablas/<N> FX.csv`
(1,2,3,4,5,8,10,15fx) más `tablas/Constrains consortium vs volPTV.txt`, y arma **2** plantillas unificadas
en una sola corrida:

- Cada `<N> FX.csv` trae 2 bloques separados por una línea `###`: el bloque craneal (antes del `###`,
  organos tipo Brainstem/Eye/Cochlea) y el de cuerpo (después, organos tipo Liver/Lungs/Rectum). El bloque
  de cuerpo se agrega siempre a la plantilla `SBRT`; el craneal solo se agrega a la plantilla `RC` si
  `numFx` ∈ {1,3,5} (los craneales de 2/4/8/10/15fx existen en la tabla de literatura pero quedan fuera del
  alcance clínico actual de RC). Cada restricción queda etiquetada con `Condicion(NumFx, igual_a, numFx)`
  — el filtrado por fraccionamiento en tiempo de análisis ya lo hace `Form2.xaml.cs`/`Form2_DosPlanes.xaml.cs`
  sin cambios (motor existente, ver `Condicion.CumpleCondicion`).
- `Constrains consortium vs volPTV.txt` (cobertura de PTV por volumen, independiente de fx) se parsea en
  2 secciones (`###Para SBRT Pulmon` / `###Para SBRT no Pulmon`) y se agrega solo a `SBRT`, con
  `Condicion(VolPTV, entre/menor_a/mayor_a, rango)` reusando `RestriccionIndiceConformidad` (columnas IC)
  y `RestriccionVolumen` (columna Lung-ITV V20%). Pulmón vs no-Pulmón se distingue solo con una `nota` en
  la restricción (decisión explícita del usuario, no se agregó lógica de matching de estructura para esto).
- Se corrigió un bug latente del parser original: filas tipo `Liver,CRITICAL VOLUME (cm3),CRITICAL VOLUME
  DOSE MAX (Gy),,,,,` (subencabezado sin valores, presente en los `tablas/*.csv` reales pero no en la
  muestra usada para escribir el parser original) hacían `Convert.ToDouble("CRITICAL VOLUME (cm3)")` y
  crasheaban. Ahora se detectan (`Linea.Any(c => c.Contains("CRITICAL"))`) y se saltean, dejando que la
  fila siguiente (columna 0 vacía) resuelva el nombre de estructura vía `estructuraAnt`.
- Se cambiaron todos los `Convert.ToDouble` por un helper `Dbl()` que devuelve `NaN` en vez de tirar
  excepción: la tabla real trae celdas de texto sin valor numérico (`"Mean dose"`, `"≤16-17"`) que el
  parser original no contemplaba. Si el valor no parsea, esa restricción puntual se saltea (no crashea
  toda la importación) — igual de silenciosa que el original con "Report" (ya devolvía `NaN` a propósito
  en ese caso).

### Cómo se testeó

`DesdeCSV.cs` no se puede instanciar fuera de Eclipse/ESAPI (arrastra `IRestriccion`/`Estructura`/`Plantilla`,
cuyas firmas usan `VMS.TPS.Common.Model.API`). Se aisló la lógica de parseo pura (split por `###`, padding
de columnas, skip de filas `CRITICAL`, `Dbl()`) en `Tests/TestDesdeCSV/Program.cs`, corrida contra los
archivos reales de `tablas/`:

```
cd Tests/TestDesdeCSV
dotnet run
```

```
OK   Encuentra los 8 archivos de fx
OK   1FX: Brainstem_PRV02 <0.5cc / 10Gy (Timmerman)
OK   1FX: Liver resuelve via CRITICAL header (700cc/11.6Gy)
OK   10FX: fila 'Eye (retina),Mean dose,<26,30' no crashea (texto no numerico se saltea)
OK   10FX: filas de Hippocampus con '≤16-17' no crashean
OK   10FX: Liver-GTV resuelve via CRITICAL header (700cc/27Gy)
OK   5FX: Renal cortex tiene varias filas encadenadas tras el header CRITICAL
OK   Fx de RC (1,3,5) presentes entre los archivos
OK   Fx exclusivos de SBRT (2,4,8,10,15) tambien presentes
OK   Se generaron filas de cuerpo (SBRT) en todos los archivos
OK   Se generaron filas craneales (candidatas a RC) en todos los archivos
OK   Encuentra las 2 secciones de la tabla de cobertura PTV
OK   Seccion Pulmon tiene 5 rangos de volumen
OK   Seccion No Pulmon tiene 3 rangos de volumen
OK   Pulmon <20cc: IC100 optimo=1.25, mandatorio=1.4
OK   Pulmon <20cc: Lung-ITV V20=5%
OK   No Pulmon <20cc: IC50 optimo=7.5, mandatorio=9.5

TODOS LOS CHEQUEOS OK
```

### Pendiente (fuera de este cambio, ver plan)

- Correr `GenerarPlantillasUnificadas` de verdad (dentro de Eclipse/ESAPI) para generar y guardar las
  plantillas `SBRT`/`RC` reales, y compararlas contra las plantillas manuales existentes (solo la parte
  de OAR, que es la que estas tienen hoy).
- Verificación end-to-end del filtrado por `NumFx`/`VolPTV` sobre un plan real (Form2.xaml.cs), y
  migración/archivado de las 7 plantillas manuales viejas.
- No se tocó `IRestriccion`, `Condicion`, `Plantilla`, ni la UI de edición (`Form1_ext.cs`,
  `Form1_prioridades.xaml.cs`) — pospuesto explícitamente por el usuario.

---

## 2026-08-14 (2) — `DesdeCSV.cs`: 4 ajustes de mapeo + fix de 3 bugs de parseo encontrados al pedir "no saltear ninguna línea"

### Los 4 ajustes pedidos

1. **"Report"/"Reportar"** (columna UK Consortium): antes `valorEsperado = NaN` (silencioso). Ahora, en
   `RestriccionConsortium`, si `Linea[5]` o `Linea[6]` contiene "Report": `valorEsperado = 100` (Gy),
   `nota = "Reportar"` — nunca "cumple" en silencio y queda explícito que hay que reportar el valor a mano.
2. **`Dmean` → `RestriccionDosisMedia`**: ya estaba bien para la columna UK (`Linea[4]` conteniendo
   "mean"/"med"). El caso real que faltaba: filas donde la columna Timmerman (`Linea[1]`) trae el
   *rótulo* "Mean dose" en vez de un umbral de volumen (ej. `"Eye (retina),Mean dose,<26,30"` en 10FX) —
   antes esto no encajaba en el parseo de volumen y se perdía en silencio. Ahora se detecta
   (`Linea[1].Contains("mean")`) y genera `RestriccionDosisMedia` con `valorEsperado = Dbl(Linea[2])`.
3. **RC en todos los fraccionamientos**: se sacó el filtro `FxParaRC = {1,3,5}` — el bloque craneal de
   cada `<N> FX.csv` (1,2,3,4,5,8,10,15) se agrega siempre a la plantilla RC, no solo en 1/3/5.
4. **"Critical Volumen" → `RestriccionVolumenCritico`**: antes la fila con el literal "CRITICAL VOLUME..."
   se saltaba entera (`continue`), perdiendo cualquier dato propio de esa fila (ver bug #4 más abajo) y
   tratando la fila de datos siguiente como una `RestriccionDosis` común. Ahora un flag `esVolumenCritico`
   se activa al ver el subencabezado "CRITICAL" y se mantiene para las filas de continuación (columna 0
   vacía) hasta la próxima estructura nombrada; mientras está activo, la columna 1/2 genera
   `RestriccionVolumenCritico` (`valorEsperado` = volumen sano mínimo, `valorCorrespondiente` = dosis Gy,
   `esMenorQue = false`) en vez de `RestriccionDosis`.

### 3 bugs reales encontrados al pedir "no debería saltear ninguna línea"

Se re-armó el test aislado (`Tests/TestDesdeCSV/`) agregando detección explícita de "salteos" (fila con
contenido en alguna columna relevante que no generó ninguna restricción) y se corrió contra los 8
archivos reales. Primera corrida: **12 salteos**. Investigando cada uno aparecieron 3 bugs de parseo
independientes de los 4 ajustes pedidos:

- **Bug #1 — split de CSV no respeta comillas.** Nombres de estructura con coma adentro, ej.
  `"Eyelid, meibomian glands (one side)",,,21.3`, con `Split(',')` a secas se partían en la coma de
  adentro, corriendo todas las columnas siguientes una posición y perdiendo el dato real (`21.3`).
  Fix: `SplitCsv()`, un split manual que no corta dentro de comillas.
- **Bug #2 — celdas "N (H)/M (M)" con 2 valores combinados.** Ej. `"1500 (H)/950 (M)"` (volumen crítico
  de pulmón, hombre/mujer en una sola celda): el `.Replace("H","").Replace("M","")` original solo servía
  para el formato viejo de una letra suelta (`"1500 H "`), no para el formato combinado nuevo — el
  `Convert`/`Dbl` fallaba y la fila entera se perdía. Fix: `ExtraerValoresConSexo()`, que si detecta 2+
  pares número+sexo genera 2 restricciones (una por sexo, notas `[H]`/`[M]`); si no, cae al comportamiento
  de siempre.
- **Bug #3 — Dmax anidado dentro del gate de la columna 1.** El parseo de `Linea[3]` (Dmax) estaba
  adentro del `if (Linea[1] != "")`, así que una fila con Dmax pero sin valor en columna 1 (ej. la fila de
  Eyelid de arriba, después del fix del bug #1) perdía igual su Dmax. Fix: el bloque ahora entra siempre
  que la fila no sea un subencabezado "CRITICAL" (`if (!esSubencabezadoCritico)`), y adentro se distingue
  el caso `Linea[1] == ""` (sin volumen/Dmean, pasa directo al chequeo de Dmax) del resto.

### Cómo se testeó (segunda corrida, con los 4 ajustes + los 3 fixes)

```
cd Tests/TestDesdeCSV
dotnet run
```

```
OK   Encuentra los 8 archivos de fx
OK   1FX: Brainstem_PRV02 <0.5cc / 10Gy (Timmerman)
OK   1FX: Liver (formato compacto, sin 'CRITICAL') -> RestriccionDosis (700cc/11.6Gy)
OK   10FX: 'Eye (retina),Mean dose,<26,30' -> Dmean=26 (no se saltea)
OK   10FX: misma fila tambien genera DosisMax=30
OK   10FX: Liver-GTV (con 'CRITICAL') -> RestriccionVolumenCritico (700cc/27Gy)
OK   10FX: Lungs '1500 (H)/950 (M)' -> 2 restricciones VolumenCritico (H y M)
OK   10FX: 'Eyelid, meibomian glands (one side)',,,21.3 -> DosisMax=21.3 (coma en nombre no rompe el parseo)
OK   5FX: Renal cortex -> VolumenCritico (200cc/17.5Gy)
OK   5FX: Renal cortex header-critico tambien genero su UK Dmean (col4-6 no se pierde)
OK   RC (craneal) ahora se junta en TODOS los fx, no solo 1/3/5
OK   Se generaron restricciones de cuerpo (SBRT) en todos los archivos
OK   Se generaron restricciones craneales (RC) en todos los archivos
OK   Encuentra las 2 secciones de la tabla de cobertura PTV

=== Salteos detectados (fila con dato pero sin restriccion generada): 2 ===
SALTEO: 10 FX .csv [craneal/RC] Hippocampus (RTOG 0933): "Hippocampus (RTOG 0933),,,≤16-17"
SALTEO: 10 FX .csv [craneal/RC] Hippocampus (RTOG 0933): ",D100%,,≤9-10"

TODOS LOS CHEQUEOS OK
```

### Última pieza: Hippocampus con Dmax en rango ("≤16-17" / "≤9-10")

Decisión del usuario: para un rango sin valor único, el **menor va como `valorEsperado`** y el **mayor
como `valorTolerado`** (mismo criterio que ya usa `RestriccionDosisMax` en general: cumple si
`valorMedido <= valorEsperado`, tolerado si `<= valorTolerado`). Implementado en el bloque de Dmax de
`ParsearBloqueOAR`: si `Linea[3]` contiene "≤", se le saca el símbolo, se separa por "-" y se arma
`RestriccionDosisMax` con `(menor, mayor)` en vez de `(valorUnico, NaN)`.

Con este último fix, la corrida sobre `tablas/` da **0 salteos**:

```
=== Salteos detectados (fila con dato pero sin restriccion generada): 0 ===
TODOS LOS CHEQUEOS OK
```

(agregado también el check `OK   10FX: Hippocampus '≤16-17'/'≤9-10' -> DosisMax(rango) esperado=menor, tolerado=mayor`)

---

## 2026-08-14 (3) — Comparación de las plantillas unificadas contra las 7 manuales existentes

### Qué se hizo

No se pudo compilar/correr `DesdeCSV.GenerarPlantillasUnificadas` real (requiere las DLL de ESAPI, no
disponibles en este entorno). En su lugar se armó `Tests/CompararPlantillas/` (standalone, sin ESAPI):
reimplementa el mismo parseo ya validado en `Tests/TestDesdeCSV` (0 salteos) y lo compara en memoria
contra las 7 plantillas manuales reales leídas directamente de
`\\ARIAMEVADB-SVR\va_data$\Plantillas\{RC,SBRT}_Nfx.txt` (JSON de Newtonsoft, se parsea con
`System.Text.Json`). **No escribe nada en ese share** — solo lee. Los exports aproximados
(`RC_generado.json`, `SBRT_generado.json`) y el diff completo (`comparacion_full.txt`) quedan en el
scratchpad de la sesión, no en el repo.

Comparación por (estructura normalizada, tipo de restricción), matcheando valores con tolerancia 0.05.

### 2 ajustes de mapeo pedidos tras la primera corrida

1. **Match de nombre de estructura** — reglas dadas por el usuario: (a) ignorar prosa/descriptores extra
   (`"Brainstem (not medulla)"` ~ `"Brainstem"`, corta en el primer `(` o `,`); (b) `<Nombre>` y
   `<Nombre>_PRV<#>` son la misma estructura (se le saca el sufijo `_PRV\d*`). Implementado como
   `Normalizar()` en el comparador — **solo ahí**, no cambia `DesdeCSV.cs` (el comparador es el que
   necesita reconciliar nombres contra las plantillas viejas; el importador real sigue usando el nombre
   crudo de la tabla, la curación de alias queda para el editor, como ya estaba pospuesto).
2. **`D0.035cc` = Dmax** (dosis de punto), `D0.1cc` sigue siendo `RestriccionDosis` aparte. Este sí se
   corrigió en `DesdeCSV.cs` (producción): en la rama UK que contiene "cc", si el valor es ≈0.035 ahora
   genera `RestriccionDosisMax` en vez de `RestriccionDosis` (reusando `RestriccionConsortium` con
   `new RestriccionDosisMax()`).

### Resultado (antes → después de los 2 ajustes)

| Archivo | manual | generado (fx) | coinciden antes → después |
|---|---|---|---|
| RC_1fx | 11 | 11 | 5 → **9** |
| RC_3fx | 12 | 10 | 0 → **8** |
| RC_5fx | 11 | 12 | 0 → **8** |
| SBRT_1fx | 70 | 102 | 13 → **46** |
| SBRT_3fx | 76 | 97 | 6 → **49** |
| SBRT_5fx | 74 | 94 | 5 → **47** |
| SBRT_15fx | 46 | 64 | 6 → **30** |

Con los 2 ajustes, "solo-manual" queda reducido casi enteramente a 3 categorías esperadas (no bugs):

- **Constraints de prescripción de PTV** (D95%, D100%, Dmax<130% de Rx) — no vienen de Timmerman/UK, son
  curación manual previa, fuera del alcance de estas tablas.
- **"Healthy Brain"** (RC) — entrada duplicada manual sin equivalente en la tabla de literatura.
- **Sinónimos/plurales/lateralidad no cubiertos por las 2 reglas dadas** (ej. `GreatVes_PRV` vs
  `GreatVessels`, `Jejunum/ileum_PRV` vs `SmallBowell`, `Kidney(s)_PRV` vs `RenalCortex`/
  `RenalHilum-VascularTrunk`, `Ribs` vs `Rib`, `Braquial_Plex_PRV`/`LumbSacPlex_PRV` fusionados vs
  `..._L`/`..._R` separados en el CSV) — reales, pendientes de una tabla de alias (decisión explícita del
  usuario: se resuelven después de cerrar la comparación, junto con definir en qué fx agregar los
  constraints de PTV).

"Solo-generado" son en su mayoría restricciones **nuevas** que las plantillas manuales todavía no tienen
(la tabla de cobertura de PTV por volumen — `RestriccionIndiceConformidad`/`Lung-ITV V20%`, siempre
"solo-generado" porque manual no la tiene en absoluto), más algunas estructuras que la tabla de
literatura trae y la plantilla manual actual no tenía cargadas (ej. `Lens`, `Orbit` en RC_1fx).

### Pendiente

- Tabla de alias para los sinónimos reales listados arriba (Kidney/RenalCortex, GreatVes/GreatVessels,
  etc.) — a definir con el usuario.
- Definir en qué fraccionamiento(s) entran los constraints de cobertura de PTV nuevos (ya cargados en el
  generador, falta decidir si aplican a todos los fx de SBRT o solo a algunos).

---

## 2026-08-14 (4) — Por qué "hacía falta ESAPI" (no debería, y de hecho no hace falta): se corrió el generador REAL

### El porqué

`DesdeCSV.GenerarPlantillasUnificadas` en sí **no llama a ESAPI en ningún momento** (solo arma datos y
serializa JSON) — confirmado con `grep "VMS.TPS" DesdeCSV.cs`: cero resultados. La dependencia es
**de compilación, no de runtime**: `IRestriccion`, `Condicion`, `Estructura`, `Plantillla.cs` y las 6
implementaciones de `IRestriccion` tienen `using VMS.TPS.Common.Model.API/Types` porque *otros* métodos
de esas clases (`analizarPlanEstructura`, `SeleccionarAutomaticamentePlantilla`, etc. — nunca invocados
por el generador) sí usan tipos de ESAPI en su firma. El compilador exige que esos tipos existan aunque
no se ejecuten.

### La solución: stub de ESAPI + compilar el código de producción tal cual

Se armó `Tests/StubEsapi/` (proyecto aparte, `net9.0-windows`): clases mínimas
(`PlanningItem`, `Structure`, `StructureSet`, `PlanSetup`, `PlanSum`, `Patient`, `Course`, `Fractionation`,
`Beam`, `Image`, `VVector`, `DoseValue`, `DVHPoint`, `DVHData`, `VolumePresentation`,
`DoseValuePresentation`) con solo la firma que el compilador necesita — los cuerpos de los métodos que sí
tocan ESAPI de verdad (`GetVolumeAtDose`, `GetDVHCumulativeData`, etc.) tiran `NotImplementedException`,
porque el generador nunca los llama.

`Tests/GenerarPlantillasReales/` compila **los archivos de producción reales, sin copiarlos ni
reimplementarlos** (`DesdeCSV.cs`, `Plantillla.cs`, `IRestriccion.cs`, `Condicion.cs`, `Estructura.cs`,
`MemoriaPlan.cs`, `IO.cs`, `Configuracion.cs`, `DatosEdicionRestriccion.cs`, las 6 `Restriccion*.cs`,
`Metodos.cs`, `DVHDataExtensions_ESAPIX.cs`, `EQD2.cs`, incluidos vía `<Compile Include>` apuntando al
repo) contra el stub, más un `PropertiesStub.cs` propio en vez del `Properties/Settings.Designer.cs` real
(atado a `app.config`).

Único obstáculo real: `Plantilla.guardar()` hace `MessageBox.Show()` después de escribir el archivo — sin
desktop interactivo esto cuelga el proceso esperando un click que nunca llega (confirmado: colgó, pero el
archivo ya estaba escrito antes del `MessageBox.Show`). Se evitó **sin tocar producción**: se invocan por
reflection los métodos privados `DesdeCSV.ImportarArchivoFx`/`ImportarCoberturaPTV` (el parseo real) y se
serializa con `IO.writeObjectAsJson` directo, salteando el wrapper `guardar()` que muestra el diálogo.

### Resultado: se generaron los archivos reales

```
SBRT: 496 restricciones. RC: 75 restricciones.
```

**Coincide exacto** con los totales que ya había calculado la reimplementación de `Tests/CompararPlantillas`
(496 SBRT / 75 RC) — confirma que esa reimplementación es fiel al comportamiento real, y que la
comparación reportada en la entrada anterior es válida sobre datos reales, no una aproximación.

Los archivos quedaron en el scratchpad de la sesión (`SBRT.txt`, `RC.txt`, formato JSON idéntico al de
`\\ARIAMEVADB-SVR\va_data$\Plantillas\*.txt`). Único ajuste manual: el `$type` quedó tageado con el
ensamblado de compilación local (`GenerarPlantillasReales`) en vez de `ExploracionPlanes` — se corrigió
con un `sed` de texto plano (`", GenerarPlantillasReales"` → `", ExploracionPlanes"`) para que sean
cargables tal cual por la app real. **No se escribió nada en el share clínico** — quedan en el scratchpad
hasta que el usuario decida moverlos.

### Pendiente

- Mover (o no) `SBRT.txt`/`RC.txt` al share clínico — decisión del usuario, no se hizo automáticamente.
- Resto de pendientes sin cambios (tabla de alias, fx de cobertura PTV) — ver entrada anterior.

---

## 2026-08-14 (5) — Merge de constraints curados a mano en las plantillas manuales existentes

### Qué se agregó

3 pedidos del usuario, ninguno sale de Timmerman/UK — se agregaron como `AgregarExtrasCuradosRC`/
`AgregarExtrasCuradosSBRT` en `DesdeCSV.cs`, llamados al final de `GenerarPlantillasUnificadas` (antes de
guardar), sobre los mismos `BindingList<IRestriccion>` ya armados por el parseo de CSV:

1. **RC, PTV**: `RestriccionVolumen` (esp=98, tol=95, corr=100, %) — verificado idéntico en
   `RC_1fx.txt`/`3fx.txt`/`5fx.txt` manual. Se agrega **sin** `Condicion` (aplica a cualquier fx de RC).
2. **RC, Brain**:
   - **1fx**: el manual tiene una entrada separada `"Healthy Brain"` (esp=10, tol=NaN, corr=12) que no
     sale de ningún CSV. Se agrega igual, pero como `WholeBrain` (fusionada con la estructura ya generada
     por el CSV en vez de un duplicado con nombre distinto) — queda con `Condicion(NumFx=1)`.
   - **3fx y 5fx**: el CSV ya trae los valores correctos bajo el nombre `"BRAIN* (including targets)"`
     (verificado: `V18Gy<30cc`/`V20Gy<20cc` en 3fx y `V24Gy<20cc` en 5fx coinciden exacto con el
     `WholeBrain` del manual) — solo hacía falta el nombre. Se hace un rename post-parseo (`estructura.nombre`,
     `nombresPosibles`, y se recalculan `etiquetaInicio`/`etiqueta` porque quedan cacheadas desde el
     `crear()` original).
3. **SBRT, PTV**: 3 constraints de prescripción, verificados **idénticos** en las 4 plantillas manuales
   (`SBRT_1/3/5/15fx.txt`): `D95%>100(99)%`, `D100%>90%`, `Dmax<120(130)%`. Se agregan **sin** `Condicion`
   (comunes a todos los fx de SBRT, según lo pedido).

### Cómo se testeó

No se reimplementó nada de esto por separado: se corrió de nuevo `Tests/GenerarPlantillasReales`
(el runner que compila `DesdeCSV.cs` real contra el stub de ESAPI, ver entrada anterior), que ahora invoca
también `AgregarExtrasCuradosRC`/`SBRT` por reflection.

```
SBRT: 499 restricciones (496 + 3 PTV). RC: 77 restricciones (75 + PTV + WholeBrain 1fx).
```

Verificado por lectura del JSON real generado:
- Las 5 filas `WholeBrain` en RC (2 en 1fx, 2 en 3fx, 1 en 5fx) tienen `nombresPosibles:["WholeBrain"]`
  limpio (sin residuo de `"BRAIN* (including targets)"`) y la etiqueta ya dice `"WholeBrain: ..."` — el
  primer intento dejó la etiqueta vieja cacheada (bug propio, encontrado y corregido en el momento: hacía
  falta llamar `crearEtiquetaInicio()`/`crearEtiqueta()` de nuevo después del rename).
- Los 3 PTV nuevos en SBRT y el PTV nuevo en RC matchean carácter por carácter los valores de las
  plantillas manuales correspondientes.

`SBRT_real.txt`/`RC_real.txt` actualizados en `Tests/CompararPlantillas/salida/` (repo, no en el share
clínico).

---

## 2026-08-14 (6) — Publicadas las plantillas unificadas en el share clínico

### Qué se hizo

Por pedido explícito del usuario, se copiaron los archivos generados en la entrada anterior
(`Tests/CompararPlantillas/salida/{SBRT,RC}_real.txt`, código de producción real + los 3 merges curados
a mano) a `\\ARIAMEVADB-SVR\va_data$\Plantillas\`, con el nombre que usa la app (`plantilla.nombre` es
"SBRT"/"RC", que es la base del nombre de archivo que usaría `Plantilla.guardar()`):

- `\\ARIAMEVADB-SVR\va_data$\Plantillas\SBRT.txt` (499 restricciones)
- `\\ARIAMEVADB-SVR\va_data$\Plantillas\RC.txt` (77 restricciones)

Se verificó antes de copiar que no existía ya un `SBRT.txt`/`RC.txt` en esa carpeta (no se pisó nada).

### Qué NO se tocó

Las 7 plantillas viejas por fraccionamiento siguen intactas en el mismo share:
`RC_1fx.txt`, `RC_3fx.txt`, `RC_5fx.txt`, `SBRT_1fx.txt`, `SBRT_3fx.txt`, `SBRT_5fx.txt`, `SBRT_15fx.txt`.
Su migración/archivado (para que `SeleccionarAutomaticamentePlantilla` no las siga usando en paralelo con
las nuevas) es una decisión aparte, todavía pendiente — hoy conviven las 9 plantillas en la carpeta.

### Pendiente

- Decidir cuándo archivar/borrar las 7 plantillas viejas.
- Tabla de alias para sinónimos reales (Kidney/RenalCortex, GreatVes/GreatVessels, etc.) — sigue pendiente.
- Definir en qué fraccionamiento(s) entran los constraints de cobertura de PTV nuevos.
- Verificación end-to-end sobre un plan real dentro de la app (filtrado por `NumFx`/`VolPTV`,
  reconocimiento de estructuras) — todo lo hecho hasta acá fue fuera de Eclipse/ESAPI.

---

## 2026-08-14 (7) — Mapeo de 5 estructuras (pedido del usuario) + bug real de instancia compartida + comparador reescrito para usar los archivos reales

### Los 5 mapeos pedidos

Implementados en `DesdeCSV.cs` (`MapearEstructurasSBRT`, llamado al final de `GenerarPlantillasUnificadas`,
después de `AgregarExtrasCuradosSBRT`), sobre `restriccionesSBRT` ya armado:

1. `Renal cortex (right and left)` → `Kidneys_PRV`, nota agrega `(Renal Cortex)`.
2. `Heart/Pericardium` → **duplicado**: la fila original queda como `Heart_PRV` y se agrega una copia
   idéntica (mismos valores, misma `Condicion`) como `Pericardium_PRV`.
3. `Rib` → `Ribs`.
4. `SpinalCord and medulla` → `SpinalCord_PRV`.
5. `Bladder Wall (with urine)` → `Bladder_PRV`, nota agrega `(Bladder Wall (with urine))`.

Se generalizaron `RenombrarEstructura`/`DuplicarEstructura` (ya usados para el rename de `WholeBrain` de
la entrada anterior) en vez de repetir el patrón 5 veces.

### Bug real encontrado: instancia de `Estructura` compartida entre restricciones de una misma línea

Primera corrida: 3 de los 5 mapeos quedaron **a medias** — la restricción de `RestriccionDosis` de una
línea quedaba bien renombrada, pero la `RestriccionDosisMax`/UK de la **misma línea del CSV** (que
comparte la MISMA instancia de `Estructura`, no una copia — `ParsearBloqueOAR` crea un solo objeto por
línea y lo reusa para D/Dmax/UK) se quedaba con la etiqueta vieja cacheada, y en el caso de
`Heart/Pericardium` ni siquiera se duplicaba.

Causa: `RenombrarEstructura`/`DuplicarEstructura` recorrían la lista completa chequeando
`estructura.nombre == nombreOriginal` **restricción por restricción**, mutando el objeto compartido en la
primera visita — las siguientes restricciones que apuntaban al mismo objeto ya no matcheaban la condición
(el nombre ya había cambiado) y quedaban sin re-etiquetar ni duplicar.

Fix: ambos métodos ahora sacan primero una **lista fija** (`.Where(...).ToList()`) de las restricciones
afectadas, y recién después las procesan todas — así ninguna mutación a mitad de camino cambia qué cuenta
como "afectada".

Verificado por lectura del JSON real generado: 0 residuos de los 5 nombres viejos (fuera de las notas
agregadas a propósito), `Heart_PRV`=15 filas y `Pericardium_PRV`=15 filas (mismos valores, confirmado
línea por línea), y todas las etiquetas (`etiquetaInicio`/`etiqueta`) consistentes con el nombre nuevo.

### El comparador se reescribió para leer los 2 archivos reales, no reimplementar el parseo

Hasta ahora `Tests/CompararPlantillas` reimplementaba el parseo de CSV por su cuenta (para no depender de
ESAPI). Motivo del cambio: con `Tests/GenerarPlantillasReales` ya compilando el código real, mantener una
segunda reimplementación en paralelo es una fuente de divergencia (de hecho así se encontró el bug de
arriba — la reimplementación del comparador no lo tenía). Ahora `Tests/CompararPlantillas` lee
`salida/{SBRT,RC}_real.txt` (generados por el runner real) con el mismo parser JSON que usa para leer las
plantillas manuales — un solo camino de lectura para los 2 lados de la comparación.

### Resultado (antes → después del mapeo, ya sobre archivos reales)

| Archivo | manual | generado (fx) | coinciden |
|---|---|---|---|
| RC_1fx | 11 | 13 | 10 |
| RC_3fx | 12 | 11 | 11 |
| RC_5fx | 11 | 13 | 10 |
| SBRT_1fx | 70 | 105 | 51 |
| SBRT_3fx | 76 | 103 | 65 |
| SBRT_5fx | 74 | 100 | 63 |
| SBRT_15fx | 46 | 69 | 40 |

### Residuos que quedan (explicados, no arreglados esta ronda)

- **`RestriccionVolumenCritico` vs `RestriccionDosis` para el mismo dato**: las plantillas manuales
  (`Liver`, `Kidneys_PRV`/`RenalCortex`) fueron armadas ANTES de que existiera `RestriccionVolumenCritico`
  (ver entrada del 2026-08-14, cambio #4 de esa ronda) — modelan el mismo número (ej. Liver 700cc/17.7Gy)
  como `RestriccionDosis` simple. El generador ahora lo tipa como `RestriccionVolumenCritico` (correcto
  según lo pedido en su momento), así que quedan en buckets distintos: aparecen como "solo-generado"
  (la VolumenCritico nueva) y fuerzan un "valor-distinto" falso contra otra fila de la misma estructura
  que sí es `RestriccionDosis` (ej. la UK D700cc). No es un bug del mapeo de esta ronda.
- **Numérico real distinto**: `Ureter` Dmax, CSV 3FX trae `40Gy`, plantilla manual tiene `45Gy` —
  discrepancia de dato entre la tabla y lo cargado a mano (posible literatura/versión distinta), no un
  bug de parseo. Queda para que el usuario decida cuál vale.
- **`Healthy Brain`/`WholeBrain`** (RC): la fila que se agregó renombrada a `WholeBrain` en la entrada
  anterior ahora "roba" el match de la fila `WholeBrain` real del CSV, y la fila manual `Healthy Brain`
  (que el manual nunca renombró) queda sin pareja — mismos valores, solo un artefacto de agrupación.
- **Sinónimos aún no mapeados**: `Braquial Plex_L`/`_R` y `LumbSacPlex_L`/`_R` (CSV, separados) vs
  `Braquial_Plex_PRV`/`LumbSacPlex_PRV` (manual, fusionados) — mismo patrón que `Heart/Pericardium`, no
  pedido todavía.

---

## 2026-09-01 — 2 correcciones pedidas por el usuario tras revisar `comparacion_full.txt`

### 1. `RestriccionVolumen`/`RestriccionDosis` son equivalentes con los campos cruzados

El usuario señaló que `RestriccionVolumen(esperado=V0, correspondiente=D0)` y
`RestriccionDosis(esperado=D0, correspondiente=V0)` describen la MISMA restricción de DVH ("el volumen
que recibe D0 es < V0" ⇔ "la dosis en V0 es < D0"), solo parametrizada al revés — varios de los
"VALOR-DISTINTO" que reportaba el comparador (Liver, Lungs) eran falsas alarmas por esto: comparaba
`(esperado,tolerado,correspondiente)` sin considerar que un lado podía tener los 2 primeros campos
invertidos frente al otro.

Fix en `Tests/CompararPlantillas/Program.cs` (solo el comparador — no es un cambio de qué tipo genera
`DesdeCSV.cs`, es una regla de equivalencia para juzgar si 2 filas dicen lo mismo):
- `TipoBucket()`: agrupa `RestriccionDosis` y `RestriccionVolumen` bajo una misma clave de comparación
  (el resto de los tipos sigue cada uno en su propia clave).
- `Equivalentes()`: dentro de ese bucket, además del match directo, prueba el match cruzado
  (`a.esperado≈b.correspondiente && a.correspondiente≈b.esperado`), exigiendo que `tolerado` sí coincida
  tal cual (no tiene un cruce limpio — es una segunda cota sobre el mismo eje que `esperado`, no un valor
  intercambiable con `correspondiente`).

### 2. Bug de datos real: "Braquial Plex" mal ubicado en `5 FX.csv`

El usuario notó que a veces "Braquial Plex" aparecía del lado RC (craneal) — no debería, es plexo
braquial (tórax), siempre debe caer en SBRT (cuerpo). Investigado: en los 8 archivos de `tablas/`,
`Braquial Plex` está bien ubicado (después del separador `###`, bloque de cuerpo) en 7 de ellos —
**excepto en `5 FX.csv`**, donde la fila estaba pegada 2 líneas antes del `###` (línea 10 vs separador en
línea 12), es decir del lado craneal por error de tipeo en el archivo fuente, no un bug de `DesdeCSV.cs`.

Se le preguntó también por `LumbSacPlex` (¿mismo problema?) — se confirmó que **no**: en los 8 archivos
`LumbSacPlex` está correctamente después del `###` en todos los casos. Solo `Braquial Plex` en `5 FX.csv`
tenía el problema.

Fix: se movió esa única fila en `tablas/5 FX.csv` de antes del `###` a después (mismo contenido, sin
tocar valores). Se regeneraron `SBRT_real.txt`/`RC_real.txt` (`Tests/GenerarPlantillasReales`) y se
volvieron a publicar en el share clínico.

### Resultado (antes → después de los 2 fixes)

| Archivo | manual | generado (fx) | coinciden antes → después |
|---|---|---|---|
| RC_1fx | 11 | 13 | 10 → 10 (sin cambio, no lo afectaba) |
| RC_3fx | 12 | 11 | 11 → 11 (sin cambio) |
| RC_5fx | 11 | 10 | 10 → **10** (bajó de 13 a 10 generadas: -3 por sacar Braquial Plex de RC, correcto) |
| SBRT_1fx | 70 | 105 | 51 → **53** |
| SBRT_3fx | 76 | 103 | 65 → **66** |
| SBRT_5fx | 74 | 103 | 63 → **66** (+3 restricciones nuevas por Braquial Plex sumado a SBRT) |
| SBRT_15fx | 46 | 69 | 40 → **41** |

Los "VALOR-DISTINTO" que quedan (Liver, Lungs, Kidneys, Ureter) son todos el mismo patrón ya documentado
en la entrada anterior (`RestriccionVolumenCritico` vs `RestriccionDosis` para el mismo dato en filas que
el manual armó antes de que existiera ese tipo, o discrepancias numéricas reales CSV-vs-manual como
Ureter) — no bugs nuevos, confirmado revisando `comparacion_full.txt` línea por línea.

---

## 2026-09-02 — Título de `Form2`/`Form2_DosPlanes`: incluir fx y volumen de PTV para plantillas condicionadas

### Qué se cambió

Pedido del usuario: cuando la plantilla está condicionada por fraccionamiento/volumen de PTV
(`plantilla.tieneCondicionesTipo1()`), el título de la ventana de análisis debe mostrar
`<Plantilla> + <fracciones> fx + PTV <volumen> cm3`, no solo el nombre de la plantilla.

Antes (`Form2.xaml.cs`): `Title += " volPTV: " + vol + "cm3 " + numFx + " fx";` — con 2 problemas:
1. Orden pedido por el usuario es fx primero, después PTV — el código tenía PTV primero.
2. `Title +=` **acumula**: si el usuario reanaliza el mismo plan más de una vez (click en "Analizar" de
   nuevo), el sufijo se pegaba una y otra vez al título ya modificado. `Form2_DosPlanes.xaml.cs` ni
   siquiera tenía este sufijo — solo mostraba el nombre de la plantilla, nunca fx/PTV.

Ahora, en ambos formularios: se guarda `tituloBase` (nombre de plantilla + paciente si hay contexto) una
sola vez al final del constructor, y cada vez que se corre el análisis el título se **reconstruye entero**
desde `tituloBase` (`Title = tituloBase + " + " + numFx + " fx + PTV " + volPTV + " cm3"`, o
`Title = tituloBase` si la plantilla no tiene condiciones) — nunca `+=`. Esto de paso arregla el bug de
acumulación en re-análisis, que no había sido pedido pero era una consecuencia directa de tocar esta
lógica.

### Cómo se testeó

Lógica de armado de string, sin ESAPI ni WPF — aislada en `Tests/TestTituloForm2/`:

```
cd Tests/TestTituloForm2
dotnet run
```

```
OK   Formato pedido: '<Plantilla> + <fx> fx + PTV <vol> cm3'
OK   Con paciente en contexto, el sufijo fx/PTV se agrega despues del nombre del paciente
OK   Plantilla sin condiciones de fx/VolPTV -> titulo sin sufijo
OK   Reanalizar 2 veces no acumula el sufijo (se reconstruye desde tituloBase, no += )

TODOS LOS CHEQUEOS OK
```

El resto (WPF real: que `Title` se refleje en la barra de título de la ventana, que `planSeleccionado()`
resuelva bien en `Form2_DosPlanes`) se verificó por lectura de código — mismo patrón que ya usa
`Form2.xaml.cs`, no se pudo compilar/correr la ventana real en este entorno (sin ESAPI/WPF tooling
disponible). Pendiente confirmación visual del usuario en Eclipse.

### Ajuste (mismo día): se sacó el `MessageBox.Show` de PTV/fx en `Form2.xaml.cs`

Pedido del usuario tras ver el cambio de título: el `MessageBox.Show("PTV volumen: ... Numero de
fracciones ...")` que aparecía al analizar quedó redundante ahora que esa info ya se ve en el título de
la ventana — se eliminó (línea que estaba justo antes de armar `Title`). `Form2_DosPlanes.xaml.cs` nunca
tuvo este `MessageBox` (no hacía falta tocarlo ahí).

---

## 2026-09-02 (2) — Fix: botón "Analizar" no se rehabilitaba al eliminar una estructura duplicada (modo contexto/script Eclipse)

### Bug original

Reportado por el usuario: en la plantilla de RC, al duplicar la estructura PTV se deshabilitaba el botón
"Analizar" y no volvía a habilitarse aunque se eliminara el duplicado.

Causa: `actualizarBotonAnalizar()` (`Form2.xaml.cs`) condicionaba el botón a
`LB_Planes.SelectedItems.Count == 1`. Pero cuando `Form2` se abre en modo contexto (`hayContext = true`,
que es como lo abre el script en Eclipse), `LB_Planes` nunca se puebla — el plan llega directo por
`planContext`, no por selección en esa lista. `duplicarEstructura`/`eliminarDuplicado` llaman a
`llenarDGVEstructuras()`, que al final llama a `actualizarBotonAnalizar()`, y esa función siempre daba
`false` en modo contexto (0 seleccionados en `LB_Planes`) sin importar el estado de los duplicados. El
único momento en que el botón quedaba habilitado era el `BT_Analizar.IsEnabled = true;` fijo del
constructor, que no se vuelve a ejecutar después.

### Fix

`actualizarBotonAnalizar()` ahora considera también `hayContext`:

```csharp
BT_Analizar.IsEnabled = (hayContext || LB_Planes.SelectedItems.Count == 1) && filasEstructuras.Count > 0;
```

### Cómo se testeó

Lógica pura, sin WPF ni ESAPI — aislada en `Tests/TestBotonAnalizar/`:

```
cd Tests/TestBotonAnalizar
dotnet run
```

```
OK   Contexto, con estructuras: viejo queda deshabilitado (el bug): esperado=False obtenido=False
OK   Contexto, con estructuras: nuevo queda habilitado (fix): esperado=True obtenido=True
OK   Contexto, sin estructuras (plantilla vacía): nuevo sigue deshabilitado: esperado=False obtenido=False
OK   Standalone, un plan seleccionado, con estructuras: viejo habilitado: esperado=True obtenido=True
OK   Standalone, un plan seleccionado, con estructuras: nuevo igual: esperado=True obtenido=True
OK   Standalone, ningún plan seleccionado: nuevo sigue deshabilitado: esperado=False obtenido=False

TODOS LOS CHEQUEOS OK
```

Confirma que el fix habilita el botón en modo contexto (con estructuras) sin romper el modo standalone
(selección manual de plan en `LB_Planes`), donde el comportamiento no cambió. Pendiente confirmación
visual del usuario en Eclipse: duplicar PTV, eliminar el duplicado, verificar que "Analizar" se
rehabilita.

---

## 2026-09-07 — 3 bugs reportados por el usuario en `Form2`/`Form2_DosPlanes`: matcheo perdido al duplicar, botón "Analizar" deshabilitado, y foco titilando

### Bug 1: `asociarEstructuras()` pisaba el matcheo manual al duplicar/eliminar otra fila

Reportado con capturas: en la plantilla RC, "PTV" estaba matcheado a mano con "PTV_1mm" (no era el
candidato automático por distancia). Al duplicar la estructura, el combo de "PTV" cambiaba solo a
"GTV01" (el candidato automático) y el botón "Analizar" quedaba deshabilitado.

Causa: `asociarEstructuras()` (`Form2.xaml.cs`) se ejecuta completa cada vez que se llama
`llenarDGVEstructuras()` — incluyendo al duplicar o eliminar una fila distinta — y siempre
recalculaba el `StructureId` de **todas** las filas desde cero (exacto → memoria en disco →
candidato por distancia), sin mirar el valor que ya tenía la fila. El matcheo manual recién se
graba en disco al apretar "Analizar" (`escribirArchivoParEstructuras`), así que cualquier match
manual hecho antes de analizar se perdía apenas se tocaba otra fila.

Fix: si la fila ya tiene un `StructureId` no vacío que sigue siendo una opción válida (la
estructura sigue existiendo en el plan), se conserva tal cual y no se recalcula. Solo se recalcula
si la fila nunca tuvo match o el que tenía ya no es válido (estructura borrada del plan).

### Bug 2: botón "Analizar" seguía deshabilitado al duplicar en modo contexto

Consecuencia directa del bug 1.

### Bug 3: botón "Analizar" queda "titilando" tras analizar

El usuario aclaró: es el foco. Tras el click el botón se queda con el foco de teclado, y el tema de
Windows (Aero2/Fluent) anima su highlight (pasa de azul oscuro a claro) mientras está enfocado — se
frena al sacarle el foco. Fix: `Keyboard.ClearFocus()` al final de `BT_Analizar_Click` en
`Form2.xaml.cs` y `Form2_DosPlanes.xaml.cs`.

Confirmado por el usuario: bugs 1 y 3 (foco/deshabilitado) quedaron resueltos.

### Ajuste (mismo día): el fix del bug 1 estaba incompleto

El usuario reprodujo el bug 2 (matcheo perdido al duplicar) de nuevo, ya con el build recompilado.
El primer fix (preservar `StructureId` dentro de `asociarEstructuras()` si sigue siendo una opción
válida) no alcanzaba: **antes** de que `asociarEstructuras()` se ejecute, `llenarDGVEstructuras()`
hace `filasEstructuras.Clear()` y crea objetos `FilaEstructura` nuevos desde cero (con
`StructureId` vacío) para reconstruir la lista completa — así que para cuando `asociarEstructuras()`
mira `fila.StructureId`, ya está vacío sin importar lo que el usuario había puesto a mano.

Fix completo: `llenarDGVEstructuras()` ahora guarda un diccionario `NombreSlot -> StructureId` de
las filas actuales (las que tienen un match no vacío) antes del `Clear()`, y al crear cada
`FilaEstructura` nueva le asigna el `StructureId` previo si existía uno para ese `NombreSlot`. La
fila nueva del slot recién duplicado (p.ej. "PTV (2)") no tiene entrada previa, así que arranca
vacía y se auto-matchea normalmente en `asociarEstructuras()`.

### Cómo se testeó

`asociarEstructuras()`/`llenarDGVEstructuras()` dependen de ESAPI (`Structure`/`PlanningItem`,
`DataGrid`) y no se pueden instanciar fuera de Eclipse. Se aisló la lógica pura de los dos fixes en
`Tests/TestAsociarEstructuras/`, sin dependencias:

```
cd Tests/TestAsociarEstructuras
dotnet run
```

```
OK   Viejo pisa el match manual con el candidato automático (el bug): esperado='GTV01' obtenido='GTV01'
OK   Nuevo preserva el match manual: esperado='PTV_1mm' obtenido='PTV_1mm'
OK   Nuevo, fila sin match previo, usa el candidato automático: esperado='GTV01' obtenido='GTV01'
OK   Nuevo, fila sin match previo (null), usa el candidato automático: esperado='GTV01' obtenido='GTV01'
OK   Nuevo, match previo ya no es una opción válida, recalcula: esperado='GTV01' obtenido='GTV01'
OK   Viejo: al reconstruir filas para agregar 'PTV (2)', el match de PTV se pierde (el bug real): esperado='' obtenido=''
OK   Nuevo: al reconstruir filas para agregar 'PTV (2)', el match de PTV se preserva: esperado='PTV_1mm' obtenido='PTV_1mm'
OK   Nuevo: la fila nueva 'PTV (2)' arranca sin match (se auto-matchea después): esperado='' obtenido=''

TODOS LOS CHEQUEOS OK
```

Pendiente confirmación visual del usuario en Eclipse con el build recompilado: duplicar una
estructura ya matcheada a mano y confirmar que el match se mantiene.

### Ajuste final (mismo día): la causa real era otra — el ComboBox nunca escribía el match manual

El usuario reprodujo el bug una tercera vez con el fix anterior ya desplegado (confirmó el flujo de
build/deploy correcto: recompila en VS, cierra Eclipse, renombra a `.esapi.dll`, sobreescribe
`.dll`/`.pdb`, corre el script — no era un problema de build viejo cacheado). Los dos fixes previos
(preservar `StructureId` en `asociarEstructuras()`, y preservarlo a través del `Clear()` de
`llenarDGVEstructuras()`) eran correctos pero apuntaban a una causa equivocada: no era la
reconstrucción de filas la que perdía el dato.

Se agregó logging temporal (`debug_asociar.txt`) en `llenarDGVEstructuras()` y en el evento
`SelectionChanged` del `ComboBox` de la grilla para ver el valor real de `StructureId` en cada paso.
El log del usuario mostró la causa exacta:

```
13:51:58 COMBO slot='PTV' SelectedItem='PTV_1mm' fila.StructureId='GTV'
...
13:52:01 CLICK DUPLICAR slot='PTV' filaActual.StructureId='GTV'
```

El `ComboBox` (dentro de un `DataGridTemplateColumn.CellTemplate` sin `CellEditingTemplate`,
`Form2.xaml`) mostraba "PTV_1mm" seleccionado visualmente, pero el binding `SelectedItem="{Binding
StructureId, Mode=TwoWay}"` nunca empujó ese valor de vuelta al modelo — 3 segundos después (al
clickear "Duplicar"), `StructureId` seguía en el valor automático viejo ("GTV"). No era un problema
de duplicar/reconstruir filas: **cualquier** matcheo manual, con o sin duplicar de por medio, se
perdía apenas se leía el modelo desde código (por ejemplo al Analizar), porque nunca había llegado a
escribirse. Los fixes anteriores actuaban correctamente sobre un `StructureId` que ya estaba mal
desde el vamos.

Fix real: se agregó un handler explícito de `SelectionChanged` al `ComboBox` que asigna
`fila.StructureId` a mano con el valor elegido, sin depender del binding automático:

```csharp
private void ComboBoxStructureId_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    if (sender is ComboBox cb && cb.DataContext is FilaEstructura fila)
    {
        fila.StructureId = cb.SelectedItem as string ?? "";
    }
}
```

Aplicado en `Form2.xaml`/`Form2.xaml.cs` y, por el mismo patrón de `ComboBox`, también en
`Form2_DosPlanes.xaml`/`Form2_DosPlanes.xaml.cs` (ese formulario no tiene "Duplicar estructura", pero
tenía el mismo riesgo de que un rematcheo manual del combo no se guardara). Se sacó todo el logging
temporal de diagnóstico (`debug_asociar.txt`) una vez encontrada la causa.

Confirmado por el usuario en Eclipse: con este fix, duplicar una estructura ya matcheada a mano
mantiene el match y "Analizar" queda habilitado.

### Por qué no se pudo testear en aislado

La causa real es específicamente un comportamiento del binding de WPF (`ComboBox` dentro de un
`DataGridTemplateColumn`), no lógica de negocio — no hay forma de reproducir "el binding no
actualiza la fuente" con un test de consola sin WPF real. La verificación fue con logging temporal
en la app real (arriba) más confirmación visual del usuario en Eclipse, que es la que corrobora el
fix. Los tests en `Tests/TestAsociarEstructuras/` y `Tests/TestBotonAnalizar/` siguen valiendo: cubren
la lógica de preservación de match y habilitación de botón que sigue vigente una vez que
`StructureId` sí llega bien escrito al modelo.

---

## 2026-09-07 (2) — Orden de sugerencias de matcheo: penalizar sustituciones más que inserciones

### Pedido del usuario

Al revisar el orden de candidatos que sugiere el combo de "Asociar estructuras" para el slot "PTV",
el orden era: `GTV` (entendible, difiere en 1 letra), después `Skin` (no se entendía por qué), y
recién después `PTV_1mm` (la que en realidad quería). Pidió que se penalicen más los reemplazos
(sustituciones) que las adiciones (inserciones), ya que un nombre que es "el slot buscado + sufijo"
debería ordenar mejor que un nombre totalmente distinto de longitud parecida.

### Causa

`Estructura.DistanciaDamerauLevenshtein` (`Estructura.cs`) pesaba todas las operaciones de edición
igual (costo 1): insertar, borrar o sustituir un carácter. Con ese esquema, "PTV" -> "PTV_1mm" (4
inserciones, para agregar "_1mm") y "PTV" -> "Skin" (todo sustituciones + 1 inserción) daban la
MISMA distancia (4) — el desempate quedaba librado al orden en que `Estructura.listaEstructuras()`
devuelve las estructuras del plan, sin ningún criterio real.

### Fix

Se pesó la sustitución al doble que la inserción/borrado (`CostoSustitucion = 2`,
`CostoInsercionOBorrado = 1`), dejando la transposición (típicamente un typo, ej. "PTV_5400" vs
"PTV_5040") en el costo barato de 1, sin encarecerla como una sustitución real. Con esto: "PTV" vs
"GTV" (1 sustitución) = 2; "PTV" vs "PTV_1mm" (4 inserciones) = 4; "PTV" vs "Skin" (sustituciones +
inserción) = 7. Orden resultante: GTV, PTV_1mm, Skin — igual que antes en el primer lugar (el
usuario entendía por qué GTV iba primero), pero ahora PTV_1mm le gana a Skin.

`DistanciaMaximaSugerida` (umbral para auto-seleccionar un candidato aproximado sin intervención del
usuario) se subió de 3 a 4 para mantener aproximadamente la misma generosidad de auto-match que
antes: con el peso nuevo, 2 sustituciones reales (antes distancia 2, dentro del viejo umbral 3) ahora
dan distancia 4, y sin este ajuste hubieran dejado de auto-matchear. Esto es un cambio de
comportamiento clínicamente relevante (afecta qué estructura se auto-selecciona sin que el usuario
la confirme) — pendiente que el usuario lo revise en casos reales y ajuste el umbral si hace falta.

### Cómo se testeó

Lógica pura, sin ESAPI — extendido `Tests/TestMejoras/Program.cs` (ya tenía una copia literal del
algoritmo viejo, se actualizó al nuevo esquema pesado y se agregó el caso real reportado):

```
cd Tests/TestMejoras
dotnet run
```

```
=== 1) Damerau-Levenshtein (pesado: sustitución cuesta más que inserción/borrado) ===
OK   Idénticas -> distancia 0
OK   Case-insensitive -> distancia 0
OK   Una sustitución -> distancia 2 (antes 1, ahora pesa el doble que un carácter agregado)
OK   Transposición adyacente sigue costando 1 (no se encarece como una sustitución)
OK   PTV_5400 vs PTV_5040 (transposición) distancia baja
OK   Nombres muy distintos -> distancia alta

=== 1b) Caso real: PTV debe sugerir GTV, después PTV_1mm, Skin al final ===
  PTV vs GTV = 2, PTV vs PTV_1mm = 4, PTV vs Skin = 7
OK   GTV primero (1 sustitución, sigue siendo el más parecido)
OK   PTV_1mm le gana a Skin (agregar caracteres es más barato que reemplazar todos)
Orden real: PTV(0), PTV_2(2), MEDULA(9)
OK   Exacto primero
OK   Aproximado (PTV_2) segundo, antes que MEDULA
...
TODOS LOS CHEQUEOS OK
```

Los tests 2, 3 y 4 del mismo archivo (fallback de memoria, orden de criterios de plantilla,
prescripción predefinida) no fueron tocados y siguen pasando — no dependen de la distancia de edición.

Pendiente confirmación visual del usuario en Eclipse: que el combo de "PTV" ahora muestre "PTV_1mm"
antes que "Skin" en el desplegable, y revisar que el umbral nuevo (4) no auto-seleccione candidatos
que antes requerían confirmación manual.

### Ajuste (mismo día): el usuario pidió ir más lejos — que PTV_1mm/PTV05 le ganen también a GTV

El fix anterior solo evitaba que "Skin" (sin relación real con "PTV") apareciera antes que "PTV_1mm".
El usuario pidió más: que variantes del mismo nombre con sufijos clínicos (número de fase/tamaño de
margen, "PRV", "mm") se reconozcan como el mismo órgano/volumen SIEMPRE, ganándole incluso a un
nombre real pero distinto que por casualidad esté a poca distancia de edición (como "GTV" vs "PTV").
Ejemplo pedido: "PTV" debe reconocer "PTV05" y "PTV_1mm" como si fueran "PTV"; "Bladder" debe
reconocer "Bladder_PRV2" como "Bladder".

Fix: `Estructura.nucleoNombreClinico()` saca los sufijos clínicos (`PRV`, `mm`, cualquier dígito, y el
`_` que suele separarlos) antes de comparar. `candidatosPorDistancia()` ahora ordena primero por la
distancia entre estos "núcleos" (sin sufijos) y usa la distancia completa (con sufijos, la de la
sección 1/1b) solo para desempatar candidatos de igual núcleo — así "PTV_1mm" y "PTV05" (núcleo "PTV",
distancia de núcleo 0) van antes que "GTV" (núcleo "GTV", distancia de núcleo 2), y a igualdad de
núcleo se prefiere el nombre con menos caracteres extra.

`candidatosPorDistancia()` devuelve ahora la distancia de NÚCLEO (antes devolvía la distancia
completa) — el umbral de auto-selección `DistanciaMaximaSugerida` sigue comparándose contra este
valor sin cambios de código, pero su significado cambió: ahora es "qué tan lejos está el núcleo del
nombre", más estricto en el sentido correcto (no auto-selecciona por casualidad de letras, solo por
identidad real del órgano/volumen más sufijos clínicos).

### Cómo se testeó

Se extendió `Tests/TestMejoras/Program.cs` con una copia literal de `nucleoNombreClinico()` y del
nuevo `candidatosPorDistancia()` (núcleo + desempate por distancia completa):

```
cd Tests/TestMejoras
dotnet run
```

```
=== 1b) Núcleo del nombre: sufijos clínicos (PRV, mm, números) no cuentan ===
OK   PTV05 -> núcleo 'PTV'
OK   PTV_1mm -> núcleo 'PTV'
OK   Bladder_PRV2 -> núcleo 'Bladder'
OK   GTV -> núcleo 'GTV' (no es lo mismo que PTV)

=== 1c) Caso real: PTV debe preferir PTV_1mm/PTV05 (mismo núcleo) sobre GTV y Skin ===
  núcleo PTV vs GTV = 2, vs PTV_1mm = 0, vs Skin = 7
OK   PTV_1mm primero (mismo núcleo 'PTV', distancia 0)
OK   GTV segundo, le sigue ganando a Skin

=== 1d) candidatosPorDistancia con núcleo: PTV_1mm y PTV05 antes que GTV ===
  Orden real: PTV05(0), PTV_1mm(0), GTV(2), Skin(7)
OK   PTV_1mm y PTV05 (núcleo 0) van antes que GTV
OK   GTV va antes que Skin
Orden real: PTV(0), PTV_2(0), MEDULA(9)
OK   Exacto primero
OK   Aproximado (PTV_2) segundo, antes que MEDULA

TODOS LOS CHEQUEOS OK
```

Pendiente confirmación visual del usuario en Eclipse: que "PTV" sugiera "PTV_1mm"/"PTV05" antes que
"GTV" en el combo, y ojo con templates que tengan VARIOS slots del mismo núcleo pero distinto
significado clínico real (p.ej. "PTV_5400" y "PTV_6000" como dos niveles de dosis distintos en la
misma plantilla) — al normalizar ambos a "PTV", el auto-match ya no los distingue por número y elige
por la distancia completa como desempate; si eso genera confusión conviene revisarlo con casos reales.

---

## 2026-09-07 — PTV primero, fusión genérica de nombres duplicados, y bug real de `\r` suelto en 7 de 8 CSV

### Pedido del usuario

1. Los constraints de PTV deben ir arriba de todo en la lista de restricciones.
2. Había estructuras duplicadas por nombre (ej. `Brainstem_PRV02` y `Brainstem (not medulla)` para el
   mismo órgano, generadas desde distintos archivos de fx) — dejar la versión menos verbosa.
3. Publicar en `\\ARIAMEVADB-SVR\va_data$\Plantillas_` (con guion bajo, carpeta de staging existente —
   mirror de `Plantillas`, ya tenía copias de `SBRT.txt`/`RC.txt` de antes), **no** en la carpeta real.

### 1) `OrdenarPTVPrimero`

Ordenamiento estable (`OrderBy`) al final de `GenerarPlantillasUnificadas`: las restricciones de `PTV`
pasan al principio de la lista, el resto mantiene su orden relativo de siempre.

### 2) `FusionarNombresDuplicados` — mismo criterio de normalización que ya usaba el comparador, ahora aplicado a los datos generados

Se generalizó la regla de "ignorar prosa y sufijo `_PRV<#>`" (que hasta ahora solo vivía en
`Tests/CompararPlantillas` para no ensuciar la comparación) para que **la propia `Plantilla` generada**
fusione estructuras que son la misma pero llegaron con nombre distinto según el archivo de fx de origen:
agrupa por una clave normalizada (corta en `(`/`,`, saca `_PRV\d*`, colapsa mayúsculas/espacios), y si un
grupo tiene 2+ nombres literales distintos, se queda con el **más corto** como nombre canónico — el resto
pasa a `nombresPosibles` como alias (no se pierde ningún nombre para el matcheo real de estructuras).

Resultado en RC: `BrainStem_PRV02`/`Brainstem_PRV02`/`Brainstem (not medulla)` → un solo `Brainstem_PRV02`;
`Cochlea`/`Cochlea_PRV02` → `Cochlea`; `OpticPathway`/`OpticPathway_PRV02` → `OpticPathway`. En SBRT:
`Heart`/`Heart_PRV` (este último venía de la duplicación `Heart/Pericardium` de una entrada anterior) se
fusionaron en `Heart` — sin afectar `Pericardium_PRV`, que sigue aparte (es una estructura clínica
distinta, no un sinónimo). Lo que NO se tocó a propósito: `GreatVes`/`GreatVessels`,
`RenalCortex`/`Kidneys_PRV` — son diferencias de vocabulario real (abreviatura/sinónimo), no de
prosa/sufijo, y fusionarlas a ciegas sería adivinar; quedan en la lista de pendientes de antes.

### Bug real encontrado en el camino: `\r` suelto dentro de la celda "ChestWall" en 7 de 8 `tablas/*.csv`

Al revisar los nombres de estructura generados en SBRT apareció uno directamente basura:
`",,,,D0.1cc,36.9 Gy,"`. Investigado con un hexdump de la línea real: la celda "ChestWall" en
`3 FX .csv` (y en 2/4/5/8/10/15 FX también, todas menos `1 FX.csv`) tiene un `\r` pegado ANTES de la
comilla de cierre — `"ChestWall\r",,,,D0.1cc,36.9 Gy,` — corrupción de origen (probablemente un
copy-paste con salto de línea mezclado).

`File.ReadAllLines` corta también por un `\r` suelto (no solo por `\r\n` o `\n`), así que esa única línea
física se partía en 2 "líneas" para el parser: `"ChestWall` (comilla sin cerrar, capturaba toda la fila
como nombre="ChestWall") y `",,,,D0.1cc,36.9 Gy,` (comilla de apertura sin cerrar, capturaba TODO el
resto como nombre) — la fila siguiente (ej. "Skin") heredaba ese nombre basura como `estructuraAnt`.

Fix de raíz en `DesdeCSV.cs`: nuevo helper `LeerLineas(path)` que arma las líneas con
`File.ReadAllText(path).Replace("\r\n","\n").Replace("\r","").Split('\n')` en vez de
`File.ReadAllLines` — cualquier `\r` (parte de `\r\n` o suelto) se descarta antes de partir por línea,
así que un CR corrupto dentro de una celda ya no puede fantasear un salto de línea. Reemplazado en los
2 lugares que leían archivo (`ImportarArchivoFx`, `ImportarCoberturaPTV`) y en el test aislado
`Tests/TestDesdeCSV` (mismo fix, para que siga siendo fiel a producción).

### Cómo se testeó

`Tests/TestDesdeCSV` sigue en 0 salteos tras el fix. Se corrió de nuevo `Tests/GenerarPlantillasReales`
(código real) y se verificó por lectura del JSON:

```
SBRT: 519 restricciones (antes 517 — 2 más por el fix de ChestWall). RC: 74 restricciones (sin cambio).
```

- 0 nombres basura (`grep -c '"nombre":",,,,'` → 0).
- Primeras entradas de ambas plantillas son `PTV`.
- Nombres distintos por plantilla: RC bajó de 16 a 11 (fusión); SBRT sin duplicados de prosa/PRV
  residuales (verificado a mano, lista completa revisada).

Comparación contra las 7 manuales (mismo comparador, sin cambios de reglas ahí): mejora leve en
SBRT_3fx/5fx por el fix de ChestWall (66→68 y 66→67 coincidencias, solo-manual bajó de 4 a 2 y de 4 a 3).

### Publicación

`SBRT.txt`/`RC.txt` actualizados en `\\ARIAMEVADB-SVR\va_data$\Plantillas_` (carpeta de staging, **no**
la real `Plantillas`) y en `Tests/CompararPlantillas/salida/{SBRT,RC}_real.txt` (repo).

---

## 2026-09-08 — Form2_DosPlanes: matching de estructuras (combo sin ordenar + plan2 sin fallback por distancia)

### Pedido del usuario (de una ronda de screenshots reales)

`Form2_DosPlanes`: el combo de "Asociar estructuras" listaba TODAS las estructuras del plan sin
ordenar por parecido (a diferencia de `Form2`, que ordena por distancia Damerau-Levenshtein), y el
plan2 se re-asociaba automáticamente contra su propio structure set solo por nombre exacto/alias
(`Estructura.asociarConLista`), sin combo manual ni fallback — si el plan2 nombraba una estructura
apenas distinto (típico entre dos planes/etapas), no matcheaba nada, `estructuraCorrespondiente2`
devolvía `null` y el análisis fallaba con "No se encontró la estructura ... en el planX". Alcance
acordado con el usuario: mejorar el auto-match (ordenar combo + agregar fallback por distancia
también en plan2), sin agregar un combo manual para plan2 (eso queda pendiente si hiciera falta).

### Cambios en `Form2_DosPlanes.xaml.cs`

- `llenarDGVEstructuras()`: las opciones del combo de cada fila ahora se arman con
  `Estructura.candidatosPorDistancia(...)` (mismo criterio que `Form2.asociarFila`) en vez de la
  lista completa sin ordenar.
- `asociarEstructuras()` (plan1) y `estructuraCorrespondiente2()` (plan2, nuevo): ambos calculan
  ahora exacto/alias + memoria (solo plan1, plan2 no tiene archivo de memoria propio) + mejor
  candidato por distancia, y delegan la prioridad final a una función pura nueva.

### `MatchingEstructuras.ElegirStructureId` — lógica pura aislada (sin ESAPI)

Prioridad: exacto/alias → memoria guardada (si sigue siendo una opción válida) → mejor candidato por
distancia (si entra dentro de `Estructura.DistanciaMaximaSugerida`) → sin match (`""`). Mismo orden
que ya usaba `Form2.asociarFila`, factorizado para que ambos plan1 y plan2 de `Form2_DosPlanes` lo
compartan y para poder testearlo sin ESAPI (recibe IDs/tuplas ya calculados, no `Structure`).

### Cómo se testeó

`Tests/TestMatchingEstructuras/Program.cs` (aislado, `dotnet run`, sin ESAPI — el único acoplamiento
con `Estructura.cs` es un umbral repetido como literal, comentado, para no arrastrar la dependencia
a ESAPI al proyecto de test):

```
OK   Exacto gana sobre memoria y distancia
OK   Sin exacto, memoria válida gana sobre distancia
OK   Memoria inválida (estructura borrada) cae a distancia
OK   Sin exacto/memoria, candidato dentro del umbral se autoasocia
OK   Candidato fuera del umbral no se autoasocia
OK   Sin ningún candidato, sin match

TODOS LOS CHEQUEOS OK
```

El caso 4 (candidato dentro del umbral, sin exacto ni memoria) es el bug real reportado: antes
`estructuraCorrespondiente2` no tenía esa rama y devolvía `null` directo. Build del proyecto principal
verificado limpio (`ExploracionPlanes -> bin\Debug\ExploracionPlanes.dll`, mismos warnings
preexistentes de arquitectura de referencias ESAPI). Pendiente de siempre: confirmación con datos
reales de Eclipse (esto no se puede probar sin dos planes reales con estructuras nombradas distinto).

---

## 2026-09-10 — Nueva feature: ocultar plantillas poco usadas (`Plantilla.Visible`)

### Cambio

Muchas instalaciones acumulan decenas de plantillas y la mayoría no se usa nunca. Se agregó `bool Visible` a `Plantillla.cs` (default `true`). En `Main`:

- Nuevo botón "Ocultar/Mostrar plantilla/s" (se habilita con 1+ seleccionadas): invierte `Visible` de cada plantilla seleccionada y reescribe su JSON (`Plantilla.ActualizarVisible`).
- Nuevo checkbox "Mostrar ocultas" debajo de los botones de abajo: destildado (default), `leerPlantillas()` solo lista `Visible == true`; tildado, lista todas.

### Cómo se testeó

`Main.xaml.cs` depende de WPF y no se puede instanciar fuera de Eclipse/un runtime WPF, así que se aisló la lógica pura (la misma que `leerPlantillas()`) en `Tests/TestMejoras/Program.cs`, sección 5:

1. Se simulan 3 plantillas: una "vieja" sin el campo `Visible` en el JSON (representa cómo queda tras deserializar con Newtonsoft: el default del property initializer no se pisa si el campo no está presente), una oculta y una visible.
2. Se reproduce el filtro (`Where(p => p.Visible)` cuando el checkbox no está tildado, sin filtro cuando sí).

### Resultados

```
OK   Plantilla vieja (sin Visible en el JSON) se sigue mostrando por default
OK   Con 'Mostrar ocultas' destildado, la oculta no aparece
OK   Con 'Mostrar ocultas' tildado, aparecen las 3
```

Correr: `cd Tests/TestMejoras && dotnet run`. Pendiente: build completo del proyecto WPF no se corrió en esta sesión (sin acceso a MSBuild/Eclipse SDK); revisar que compile antes de publicar.

---

## 2026-09-10 — Plantilla SBRT: unificar 5 pares de nombres duplicados pedidos + 1 encontrado

### Pedido del usuario

Unificar (manteniendo el primer nombre de cada par) 5 pares de estructuras duplicadas en `SBRT.txt`
(`\\ARIAMEVADB-SVR\va_data$\Plantillas_`, staging): `Braquial Plex_L/R` con `Braquial Plex` (caso especial:
duplicar el constraint sin lateralidad en `_L` y `_R`), `GreatVessels` con `GreatVes`, `Liver-GTV` con
`Liver`, `Lung-ITV` con `Lungs`, `Bladder_PRV` con `BladderWall`. Más: "buscar si hay otros".

### Por qué no los agarró la fusión genérica de la entrada anterior

`FusionarNombresDuplicados` (2026-09-07) normaliza sacando prosa/paréntesis y el sufijo `_PRV<#>` — estos
5 pares difieren por vocabulario real (`-GTV`, `-ITV`, `Wall`, `Ves`/`Vessels`, con/sin lateralidad), no
por eso, así que la clave normalizada les da distinto a propósito y no se tocan solos.

### Implementado en `DesdeCSV.cs`

Nuevo `UnificarNombresPedidosSBRT`, corrido después de `FusionarNombresDuplicados` y antes de
`OrdenarPTVPrimero`:
- 4 pares simples con `RenombrarEstructura(lista, elQueSeVa, elQueQueda)` (ya existía, reusado tal cual).
- Braquial Plex con `DuplicarEstructura(lista, "Braquial Plex", "Braquial Plex_L", "Braquial Plex_R")`
  (mismo mecanismo ya usado para `Heart/Pericardium`): la fila original se renombra a `_L` y se agrega una
  copia idéntica como `_R`.

### "Buscar si hay otros" — encontrado 1, más 1 dudoso sin tocar

- **`LumbSacPlex` / `LumbSacPlex_L` / `LumbSacPlex_R`**: mismo patrón exacto que Braquial Plex (una
  versión sin lateralidad conviviendo con las versiones `_L`/`_R`). Se aplicó el mismo tratamiento
  (`DuplicarEstructura`) sin esperar confirmación, por ser idéntico al caso ya explícitamente pedido.
- **`RenalCortex` vs `Kidneys_PRV`**: **no se tocó**. A diferencia de los otros pares, esto no es una
  variante de escritura del mismo nombre — corteza renal es una sub-región anatómica dentro del riñón,
  podría ser una distinción clínica real y no un simple duplicado de origen. Queda para que el usuario
  decida.

### Resultado

```
SBRT: 552 restricciones (antes 519; +33 por duplicar Braquial Plex y LumbSacPlex en _L/_R). RC: 74 (sin cambio).
```

Verificado por lectura del JSON: 33 nombres de estructura distintos en SBRT (antes 39), ninguno de los 6
nombres unificados sigue apareciendo suelto, `Braquial Plex_L`=20 filas y `_R`=20 filas (simétrico, como
se esperaba de la duplicación).

Nota sobre la comparación contra las plantillas manuales (mismo `Tests/CompararPlantillas`, sin cambios de
reglas ahí): el conteo de "coinciden" bajó para SBRT (ej. 68→57 en 3fx) — **no es una regresión de
datos**, es que el comparador solo sabe reconocer "mismo nombre" ignorando prosa/paréntesis/sufijo `_PRV`,
y ahora los nombres canónicos elegidos (`Liver-GTV`, `Lung-ITV`, etc.) se alejan más de como los llaman
las plantillas manuales viejas que antes — mismo problema que ya existía con `GreatVes`/`GreatVessels`
antes de unificarlos acá. No se tocó el comparador por esto; ya está documentado como límite conocido.

### Publicación

`SBRT.txt`/`RC.txt` actualizados en `\\ARIAMEVADB-SVR\va_data$\Plantillas_` (staging, **no** la carpeta
real `Plantillas`) y en `Tests/CompararPlantillas/salida/{SBRT,RC}_real.txt` (repo).

---

## 2026-09-10 (2) — 2 unificaciones más: `SmallBowell`→`Jejunum/ileum`, `RenalCortex`→`Kidneys_PRV`

Pedido del usuario, sobre `UnificarNombresPedidosSBRT` (misma función de la entrada anterior):

- `RenombrarEstructura(restriccionesSBRT, "SmallBowell", "Jejunum/ileum")` — acá se mantiene el
  **segundo** nombre (`Jejunum/ileum` ya era mayoría: 18 filas contra 4 de `SmallBowell`), al revés de la
  convención "primer nombre" de los 5 pares anteriores — el usuario lo pidió así explícitamente
  ("Convertir SmallBowel en Jejunum/Illeum").
- `RenombrarEstructura(restriccionesSBRT, "RenalCortex", "Kidneys_PRV")` — el par que había quedado
  marcado como dudoso/sin tocar en la entrada anterior; el usuario confirmó unificarlo iguel.

Regenerado con `Tests/GenerarPlantillasReales`: `SBRT` se mantiene en 552 restricciones (son renames, no
agregan filas), 32 nombres de estructura distintos (antes 33). Verificado 0 residuos de `SmallBowell`/
`RenalCortex`. Publicado en `\\ARIAMEVADB-SVR\va_data$\Plantillas_` (staging) y en
`Tests/CompararPlantillas/salida/{SBRT,RC}_real.txt`.

---

## 2026-09-10 (3) — Se separa "SBRT" en 2 plantillas por región: "SBRT Torax-Abdomen" y "SBRT Abdomen-Pelvis"

### Pedido del usuario

Reemplazar la plantilla única `SBRT` por 2 plantillas filtradas por región anatómica, cada una con un
**orden explícito** de estructuras (no alfabético, no el de aparición en el CSV), más 3 renames nuevos
(`Heart`→`Heart_PRV`, `CaudaEquina`→`CaudaEquina_PRV`, `Rectum`→`Rectum_PRV`) y eliminar `Bronchus` del
todo (no entra en ninguna de las 2).

### Implementado en `DesdeCSV.cs`

- `OrdenToraxAbdomen`/`OrdenAbdomenPelvis`: los 2 arrays de nombres, hardcodeados en el orden exacto
  pedido (no se podía derivar de nada existente).
- `EliminarEstructura(lista, nombre)`: saca una estructura entera de la lista (a diferencia de
  `RenombrarEstructura`, no fusiona nada — se descarta). Usado para `Bronchus`.
- `SepararSBRT(restriccionesSBRT)`: aplica los 3 renames nuevos + elimina `Bronchus`, y devuelve las 2
  listas ya filtradas y ordenadas vía `FiltrarYOrdenar(origen, orden)` (se queda solo con las
  estructuras que están en el array de orden, ordenadas por la posición en ese array — `OrderBy` estable,
  así que dentro de una misma estructura se mantiene el orden relativo de siempre).
- `GenerarPlantillasUnificadas` ya no guarda una `Plantilla` `"SBRT"` — guarda las 2 que devuelve
  `SepararSBRT` (`"SBRT Torax-Abdomen"`, `"SBRT Abdomen-Pelvis"`), cada una con su propio archivo.

`Tests/GenerarPlantillasReales/Program.cs` (el runner que compila el código real, sin ESAPI) se actualizó
para reflejar esto: invoca `SepararSBRT` por reflection y, como devuelve una tupla `(string,
BindingList<IRestriccion>)` que no se puede castear directo desde `object` sin referenciar el tipo
genérico exacto, se leen los campos `Item1`/`Item2` de la tupla también por reflection.

### Cómo se testeó

Se corrió el generador real y se verificó por lectura del JSON:

```
RC: 74 restricciones.
SBRT Torax-Abdomen: 384 restricciones.
SBRT Abdomen-Pelvis: 388 restricciones.
```

- El orden de primera aparición de cada nombre de estructura en ambos archivos coincide **exacto**,
  elemento por elemento, con las 2 listas que pasó el usuario.
- `Bronchus`: 0 apariciones en cualquiera de las 2.
- `Heart`/`CaudaEquina`/`Rectum` (sin sufijo): 0 residuos — todos renombrados a `_PRV`.

### Publicación

`RC.txt`, `SBRT Torax-Abdomen.txt` y `SBRT Abdomen-Pelvis.txt` en `\\ARIAMEVADB-SVR\va_data$\Plantillas_`
(staging, **no** la carpeta real) — se borró el `SBRT.txt` viejo (combinado) de esa carpeta de staging,
ya que queda reemplazado por las 2 nuevas. En el repo: `Tests/CompararPlantillas/salida/RC_real.txt`,
`SBRT_Torax-Abdomen_real.txt`, `SBRT_Abdomen-Pelvis_real.txt` (se borró el `SBRT_real.txt` viejo ahí
también).

### Pendiente

`Tests/CompararPlantillas` compara contra las plantillas manuales viejas agrupadas por fraccionamiento
(`SBRT_1/3/5/15fx.txt`) — ese comparador queda desactualizado para SBRT (ahora la separación es por
región, no por fx, y busca un archivo `SBRT_real.txt` que ya no se genera). No se tocó porque no fue
pedido esta vez; si se quiere seguir comparando SBRT contra las manuales hay que rediseñar esa lógica
(comparar cada plantilla nueva contra la unión de las 4 manuales, filtrando por estructura en vez de fx).

---

## Convención para tests futuros

A partir de este cambio, todo cambio sobre código funcional debe incluir un test que compare comportamiento antes/después, documentado como una entrada nueva en este archivo (fecha, qué se cambió, cómo se testeó, números usados, resultado). Si el código depende de ESAPI y no se puede instanciar fuera de Eclipse, aislar la lógica pura afectada (como se hizo en `Tests/TestEQD2/`) en vez de omitir el test.
