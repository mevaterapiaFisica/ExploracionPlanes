# UI: estado, cambios hechos y contexto para evaluar WPF

Este archivo resume el trabajo de unificación visual hecho sobre la UI WinForms actual, las
dificultades encontradas para iterar sobre UI sin poder verla correr, y los datos técnicos que
hacen falta para que otra conversación evalúe el pasaje a WPF sin tener que re-descubrir todo esto.

---

## 1. Objetivo original

El pedido fue: (1) unificar criterios visuales entre ventanas (numeración de pasos, fuentes,
tamaños de botón, paleta), y (2) evaluar qué tan complejo sería pasar la app a WPF. Se decidió
hacer primero (1) y dejar (2) como evaluación aparte, por dos razones: el trabajo de unificación
no se pierde si WPF avanza (es la referencia de diseño para portar), y sirve para medir en un
cambio chico y reversible qué tan enredado está el code-behind con los controles — la misma
fricción que aparecería en una migración completa.

## 2. Cambios ya aplicados (commits `6ce9ee2`, `6331c39`)

### Numeración de pasos

- `Form1_ext.cs` (editor de plantillas) tenía **dos secciones marcadas "3."** (`GB_NuevaRestriccion`
  = "3. Nueva Restricción" y un label de nota = "3. Nota (opcional)"), y la lista de restricciones
  cargadas no tenía número. Se agregó "4. Restricciones cargadas" y se corrió la nota a "5.".
- `Form1_prioridades.cs` (mismo formulario, variante con prioridades/condicionamiento entre
  restricciones): mismo problema sin el grupo de Condiciones. Se agregó "3. Restricciones cargadas"
  y se corrió la nota de "3." a "4.".
- `Form2_DosPlanes.cs`: el label de selección de plan decía "3. Seleccionar plan" pero ahora hace
  falta elegir 2 (ver §3) → se cambió a "3. Seleccionar 2 planes".

### Fuentes de botones "de commit"

`Form2`/`Form2_DosPlanes` ya usaban `Microsoft Sans Serif 10F` en los botones finales (Analizar,
Imprimir, Guardar Reporte). El resto de los formularios usaba el font por defecto (8.25F):

- `BT_GuardarPlantilla` en `Form1_ext`/`Form1_prioridades` → ahora 10F.
- `Form3.BT_Analizar` → ahora 10F (texto corto, sin riesgo).
- `Form3.BT_GuardarPaciente` / `BT_Exportar`: **se probó subir a 10F y se revirtió** — con
  screenshot real se vio que a 10F el texto largo ("7. Guardar y cerrar paciente") se corta. Quedan
  con el font por defecto. Ver §4 sobre por qué esto importa para WPF.

### Coloreado pass/fail

Estaba duplicado palabra por palabra en `Form2.colorCelda`/`colorCeldasAnidadas` y en
`Form2_DosPlanes` (mismos métodos, mismo código). Se extrajo a una clase estática nueva,
`ColorearAnalisis.cs`, y ambos formularios delegan ahí. Paleta sin cambios (verde/amarillo/rojo).

### Funcionalidad nueva que también tocó UI (pedida aparte, no parte de "unificación", pero relevante para WPF por el patrón que usa)

- **Duplicar estructura** (`Form2.cs`): botón nuevo que clona un slot de estructura+restricciones
  para poder matchear una segunda estructura real del plan. Layout: se agregó un botón angosto
  ("Duplicar\nestructura") en el hueco de ~70px entre `DGV_Estructuras` y `DGV_Prescripciones`.
- **Ocultar restricciones no analizadas** (`Form2.cs`): checkbox nuevo, tildado por defecto, oculta
  filas de `DGV_Análisis` cuya estructura no se pudo asociar.
- **EQD2 en comparación de dos planes** (`Form2_DosPlanes.cs`): se trasladó el checkbox y la
  columna α/β que ya tenía `Form2`. Acá apareció el problema de espacio más serio de toda la
  sesión — ver §4.

## 3. Bug de UX corregido (no solo estético)

`Form2_DosPlanes` en modo standalone (sin Eclipse en contexto) tenía un flujo roto: el botón
"Comparar dos planes" en `Main` siempre abría un diálogo de selección de segundo plan
(`PlanesParaComparar`) que solo se llena con datos que vienen del plugin de Eclipse — en standalone
esa lista está **siempre vacía**. Se sacó ese diálogo del camino standalone y se cambió
`LB_Planes` a multiselección: el usuario elige los 2 planes directamente en la lista, y
"Analizar"/"Seleccionar plan" se habilitan solo con exactamente 2 seleccionados. Se sacó de paso un
hack viejo que adivinaba el segundo plan buscando la palabra "cam" en su nombre.

## 4. Dificultades encontradas (importan para cualquier trabajo futuro de UI, WPF incluido)

### No hay forma de auto-verificar visualmente desde esta sesión

Se probó lanzar el `.exe` standalone en esta misma PC y sacar una captura con PowerShell
(`GetWindowRect` + `Graphics.CopyFromScreen`). El proceso arranca bien, la ventana tiene un handle
válido — pero la captura devuelve contenido viejo/de otra sesión, no lo que la ventana realmente
tiene en pantalla en ese momento. La sesión de consola figura "Active" (`query session`), así que
no es el caso típico de RDP desconectado; la sospecha es que sin nadie mirando el monitor físico en
el momento exacto de la captura, el framebuffer que se lee no está actualizado. **No hay una forma
confiable de iterar UI con loop de captura automático en esta máquina.**

### Las PCs con acceso a Eclipse corren Windows 7

Relevante para automatizar cualquier cosa ahí: Claude Code (y Node.js 18+, que requiere) **no
corre en Windows 7** — Node dejó de soportar Win7 hace varias versiones. Cualquier verificación
del modo plugin (el que se usa en producción) tiene que ser manual, sacada por una persona que
mire la pantalla real.

### Estrategia que terminó funcionando

Round-trip manual con el usuario:
1. Yo edito el `.Designer.cs` calculando coordenadas a mano a partir del código existente y de
   screenshots previos (sin verlo correr).
2. El usuario corre la app real (standalone acá, o plugin desde una PC con Eclipse) y me deja
   screenshots en una carpeta de red (`screenshots/`, con subcarpetas `Standalone`/`Plugin`/`Nuevas`).
3. Yo las reviso con `Read` (lee imágenes directo) y corrijo lo que haga falta.

Esto **encontró bugs reales**, no solo estéticos, que ninguna revisión de código sola hubiera visto:
- `Form3.BT_GuardarPaciente` cortaba texto al subir la fuente (mencionado arriba).
- Un screenshot de Eclipse real (plugin) mostró "Duplicar estructura" funcionando, pero también
  destapó que la estructura duplicada no aparecía en la tabla de prescripciones → daba `Infinity%`
  en las restricciones por porcentaje (bug de lógica, no de layout, encontrado solo porque se vio
  la tabla real con datos reales).
- Al portar el checkbox EQD2 a `Form2_DosPlanes`, copiar el mismo ensanche de columna que usa
  `Form2` (+60px) hubiera superpuesto `DGV_Estructuras` con `DGV_Prescripciones`, que en este
  formulario está mucho más cerca (282px de margen en vez de los ~325px que tiene `Form2`). Esto
  se detectó por cálculo de coordenadas, no por captura, pero confirma que **cada formulario tiene
  su propio presupuesto de espacio** — no se puede asumir que un patrón que funciona en `Form2`
  aplica igual en otro formulario con controles vecinos distintos.

**Importante para la próxima conversación**: si se van a hacer más cambios de layout (en WinForms
o ya directamente en WPF), hay que seguir este mismo patrón de round-trip con el usuario. No asumir
que un cambio "se ve bien" solo porque compiló.

### Impacto en la evaluación de WPF

Esta dificultad de verificación visual **es un argumento en contra de hacer la migración a WPF en
un solo salto**: si ya cuesta verificar cambios chicos de `.Designer.cs` sin loop visual, migrar
formularios enteros a XAML sin poder verlos renderizar es mucho más riesgoso. Recomendación para
la migración, si se decide seguir: migrar un formulario chico primero (candidatos: `FormConfiguracion`,
45 líneas de code-behind, o `Form_ListaRestricciones`, 29 líneas) como prueba de concepto real,
con el mismo circuito de screenshots del usuario, antes de tocar `Form2`/`Form2_DosPlanes`
(los más grandes y los que más lógica tienen pegada a controles).

## 5. Datos técnicos para evaluar el pasaje a WPF

- **Proyecto**: `.csproj` clásico (no SDK-style), `OutputType=WinExe`, `TargetFrameworkVersion=v4.5.1`.
- **15 pares `.cs`/`.Designer.cs`** generados por el Designer clásico de WinForms (posicionamiento
  absoluto por `Point`/`Size`, sin `TableLayoutPanel`/`FlowLayoutPanel`).
- **Tamaño de code-behind** (líneas, de mayor a menor riesgo de migración):
  `Form2.cs` (~1080), `Form2_DosPlanes.cs` (~830), `Form1_ext.cs` (660), `Form1_prioridades.cs`
  (575+), `Form3.cs` (420), `Main.cs` (420+), el resto son formularios chicos de soporte
  (`FormTB`, `Form_ListaRestricciones`, `FormConfiguracion`, `SeleccionarPTV`,
  `PlanesParaComparar`, `PlanesSumaContext`, `PlantillaBlanco`, `ImportarNombresEstructuras`).
- **Acoplamiento UI/lógica**: alto en `Form2`/`Form2_DosPlanes`/`Form1_ext`/`Form1_prioridades` —
  manipulan `DataGridView` directo por índice de celda (`Rows[j].Cells[4].Value`,
  `Columns[1].Visible`) en vez de binding. Migrar implica no solo XAML nuevo sino reescribir la
  forma en que esa lógica lee/escribe la tabla (en WPF sería binding a una colección, no acceso
  directo a celdas).
- **Dependencias de terceros**: `PDFsharp`/`MigraDoc` (`Reporte.cs`, generación de PDF) usan
  `System.Drawing`/GDI+ — no dependen de WinForms en sí, pero si se migra a .NET moderno hay que
  revisar compatibilidad del paquete (`PDFsharp-MigraDoc-GDI`, target `net20`, ligado a
  `System.Drawing.Common`).
- **Referencias a ESAPI** (`VMS.TPS.Common.Model.API/.Interface/.Types`): apuntan por `HintPath` a
  la carpeta `bin` de otro proyecto en disco, no a un paquete NuGet — dependencia frágil pero
  independiente de WinForms/WPF, no cambia con la migración.
- **Existe un `ExploracionPlanes - copia.csproj`** en la raíz — csproj duplicado, posible backup
  accidental. No se tocó (modo lectura en esa exploración), preguntarle al usuario si conviene
  borrarlo antes de arrancar cualquier reestructuración de proyecto.

## 6. Preguntas abiertas para la conversación de WPF

1. ¿Migración completa de una sola vez, o formulario por formulario conviviendo con WinForms
   (ambos frameworks en el mismo `.exe`, WPF soporta hosting de WinForms y viceversa)? **Decidido:
   formulario por formulario (ver §7).**
2. ¿Se aprovecha la migración para pasar de .NET Framework 4.5.1 a .NET moderno (6/8), o se hace
   WPF sobre .NET Framework primero y el runtime se actualiza después? Afecta directamente a
   PDFsharp/MigraDoc y a las referencias ESAPI por `HintPath`. **Decidido: WPF sobre .NET Framework
   4.5.1 primero, runtime se actualiza después por separado (ver §7).**
3. ¿Vale la pena introducir un patrón de binding (MVVM aunque sea liviano) ya que se
   reescribe la lectura/escritura de las tablas, o se replica el mismo estilo imperativo actual
   sobre controles WPF (menos trabajo ahora, pero no se gana nada de lo que WPF ofrece)? **Pendiente
   — se decide en la Fase 2 de §7, antes de tocar `Form2`/`Form2_DosPlanes`.**
4. Dado que no hay loop de verificación visual automático disponible, ¿quién further valida cada
   pantalla migrada — el usuario en un round-trip como el de esta sesión, o hay alguna otra PC con
   entorno interactivo real donde sí se pueda iterar más rápido? **Pendiente — asumido round-trip
   con el usuario (mismo patrón de §4) hasta que se diga lo contrario.**

## 7. Plan de migración (evaluado, no iniciado)

Decisión: WPF incremental, formulario por formulario, conviviendo con WinForms en el mismo `.exe`.
No big-bang — la falta de loop de verificación visual automático (§4) hace que migrar todo de una
sola vez sea alto riesgo, sobre todo en `Form2`/`Form2_DosPlanes` (acoplamiento directo a
`DataGridView` por índice de celda).

**Fase 0 — POC: COMPLETADA.** `FormConfiguracion` migrado a `Window` WPF real (`FormConfiguracion.xaml`
+ `.xaml.cs`, reemplazan al `.cs`/`.Designer.cs`/`.resx` de WinForms). Validado por screenshot del
usuario (`screenshots/WPF/Configuracion.PNG`) — tokens de color/tipografía aplicados, valores de
`Settings` cargan bien, botones Guardar/Cancelar/Seleccionar funcionan.

Notas técnicas que sirven para las próximas fases:
- No hizo falta `ProjectTypeGuids` de WPF ni importar `Microsoft.WinFx.targets` en el `.csproj`
  clásico — alcanzó con agregar las referencias `PresentationCore`, `PresentationFramework`,
  `System.Xaml`, `WindowsBase` y usar ítems `<Page>`/`<Compile DependentUpon>` en vez de
  `<Compile SubType=Form>`/`<EmbeddedResource>`. Build con MSBuild de VS2022 sin warnings nuevos.
- El `Window` WPF se abre con `.ShowDialog()` igual que un `Form` — el call site en `Main.cs`
  (`BT_Configuracion_Click`) no necesitó cambios porque se mantuvo el mismo nombre de clase/namespace.
- Para el `FolderBrowserDialog` se reusó `System.Windows.Forms.FolderBrowserDialog` (ya referenciado)
  en vez de buscar un picker nativo WPF — WPF no trae uno propio y no vale la pena una dependencia
  nueva para esto.
- Captura desde esta sesión sigue sin ser confiable (§4) — la verificación fue con screenshot manual
  del usuario, como estaba previsto.

**Fase 1 — Formularios chicos sin lógica pesada: COMPLETADA (6 de 7).** Migrados a `Window` WPF:
`Form_ListaRestricciones`, `SeleccionarPTV`, `PlanesParaComparar`, `PlanesSumaContext`,
`ImportarNombresEstructuras`, `FormTB`. Build limpio con MSBuild, sin warnings nuevos. Falta
verificación por screenshot del usuario (pendiente, ver pedido más abajo).

Notas de esta ronda:
- `FormTB` es el más usado de la app (6 call sites en `Main.cs`, `Form1_ext.cs`,
  `Form1_prioridades.cs`, `Form2.cs`, `Form2_DosPlanes.cs`) y los callers **reflejaban directamente
  sobre `Controls.OfType<Label>()`/`Controls.OfType<CheckBox>()`** para setear el texto de
  instrucción y una casilla extra — `Window` de WPF no tiene `Controls`. Se resolvió expuniendo los
  campos `L_Texto` y `CHB_Extra` como `public` (`x:FieldModifier="public"` en el XAML) y actualizando
  los 6 call sites a acceso directo (`formTb.L_Texto.Content = "..."`) — más simple que lo que
  reemplaza, no hace falta reflexión para acceder a un campo del propio namespace.
- `FormTB` con `esPasword=true` usaba `TextBox.PasswordChar`, que no existe en WPF. Se resolvió con
  un `PasswordBox` superpuesto que se muestra en vez del `TextBox` cuando `esPasword` es true.
- El método `BT_Aceptar_Click` original encadenaba varios `if` sin `return`, por lo que en la
  práctica (con `salidaDouble=true`) llamaba a `Close()`/asignaba `DialogResult` dos veces seguidas
  — inofensivo en WinForms, pero un `Window` de WPF lanza excepción si se reasigna `DialogResult`
  después de cerrado. Se reescribió con `return` explícitos; mismo comportamiento observable para
  todos los call sites reales (nunca combinan `salidaDouble` y `esPasword` a la vez).
- Todos los diálogos de selección de lista (`Form_ListaRestricciones`, `SeleccionarPTV`,
  `PlanesParaComparar`, `PlanesSumaContext`) migraron `ListBox.DataSource` (WinForms) a
  `ListBox.ItemsSource` (WPF) sin fricción — mismo patrón, ningún caller dependía de detalles del
  control WinForms.
- `ImportarNombresEstructuras` usaba `CheckedListBox`, que WPF no tiene. Se armó agregando
  `CheckBox` como items de un `ListBox` común (en vez de introducir un patrón MVVM/binding solo
  para esto). De paso, la columna "Estructuras" tenía un label que el código dejaba siempre oculto
  (`Visible=false`, nunca se togglea) — se lo dejó visible como header de la lista, ya que ocultarlo
  no tenía ningún propósito activo.
- `PlantillaBlanco` **se sacó de Fase 1** y se reclasifica junto a Fase 2: usa `DataGridView` por
  índice de celda pasado directo a `Reporte.crearReporte(DataGridView)` — mismo acoplamiento que
  `Form2`/`Form2_DosPlanes`. Migrarlo antes de decidir el patrón de binding hubiera significado
  forzar un `WindowsFormsHost` (parche, no migración real) o reescribir `Reporte.cs` a destiempo.

### Bug real encontrado por el usuario (no solo estético): freeze de la ventana principal

Con los 6 diálogos WPF abiertos vía `.ShowDialog()` sin `Owner`, el usuario reportó que al hacer
Alt-Tab a otra ventana y volver, la ventana principal (WinForms) quedaba frizada — sin poder verse
ni cerrar el diálogo, forzando a matar el proceso desde el Administrador de tareas. Reproducido en
`FormTB` y en los diálogos de selección de plan; probablemente afecta a los 7 por igual (los otros
no llegaron a probarse con Alt-Tab).

Causa: sin `Owner` seteado, Windows no vincula el diálogo WPF a la ventana WinForms que lo abrió.
Como estos diálogos usan `ShowInTaskbar=False`/`WindowStyle=ToolWindow` (a propósito, igual que el
original), no aparecen en la barra de tareas ni en Alt-Tab — al volver a la app, Windows trae al
frente la ventana principal (que sigue bloqueada por el diálogo modal invisible), sin forma de
llegar al diálogo real.

Fix aplicado: `DialogoWpf.cs`, una clase base chica (`Window` + `OnSourceInitialized` que fija el
`Owner` vía `WindowInteropHelper` a `System.Windows.Forms.Form.ActiveForm`), de la que heredan los 7
diálogos (incluido `FormConfiguracion` de Fase 0) cambiando el tag raíz del XAML a
`<local:DialogoWpf>`. Un solo punto de arreglo en vez de tocar los ~15 call sites que hacen
`.ShowDialog()`.

**Pendiente de reverificar por el usuario** con el fix aplicado (Alt-Tab sobre cada diálogo).

### Anomalía de build a vigilar

Durante esta ronda, el `.csproj` apareció con `<OutputType>Library</OutputType>` en vez de
`WinExe` después de una tanda de builds con MSBuild — sin que ninguna edición explícita de esta
sesión lo tocara (no está en el historial de cambios hechos). Se corrigió a mano. No se identificó
la causa (sospecha: algún proceso de fondo del entorno tocando el `.csproj` al detectar contenido
WPF nuevo). Si vuelve a pasar, revisar `<OutputType>` antes de asumir que el build está roto por
otra razón.

**Fase 2 — Decisión de binding** antes de tocar los formularios con `DataGridView` por índice
(`PlantillaBlanco`, `Form2`, `Form2_DosPlanes`, `Form1_ext`, `Form1_prioridades`): MVVM liviano
(colección bindeada a `DataGrid`) vs replicar acceso directo a celdas. Sin esto la migración de
esos 5 es reescritura de lógica, no solo de XAML.

**Fase 3 — Los formularios con `DataGridView` pesado** (`PlantillaBlanco`, `Form2`,
`Form2_DosPlanes`, `Form1_ext`, `Form1_prioridades`), uno por uno, mismo circuito de screenshots.

Runtime: WPF se hace sobre .NET Framework 4.5.1 primero (no toca ESAPI por `HintPath` ni
PDFsharp/MigraDoc-GDI). Migrar a .NET moderno (6/8) queda como paso separado y posterior, evaluado
aparte cuando corresponda.

### Tokens de diseño para la UI nueva

- **Color**: `#1B2A4A` (azul clínico oscuro, headers/acentos), `#F7F8FA` (fondo neutro), `#2E7D5B`
  (verde pass), `#C24B3D` (rojo fail, terracota no saturado), `#E8A33D` (amarillo warning),
  `#FFFFFF` (superficies de tabla).
- **Tipografía**: Segoe UI Variable (headers, botones "de commit" — nativa en Win10/11, sin
  dependencia nueva); Segoe UI normal (body/labels); Consolas (valores numéricos de dosis, para
  alineación tabular en la tabla de análisis).
- **Layout**: `Grid`/`DockPanel` en vez de posicionamiento absoluto. La numeración de pasos se
  mantiene como eyebrow tipográfico — es información real del flujo clínico, no decoración.
- **Firma / foco de cuidado**: la tabla de análisis pass/fail (`DGV_Análisis`), es lo que un físico
  médico mira más veces por día. Punto pendiente de accesibilidad para la migración: hoy el
  pass/fail se comunica solo por color (`ColorearAnalisis.cs`) — agregar ✓/✗ además del color para
  no depender solo de percepción de color en una decisión clínica.
