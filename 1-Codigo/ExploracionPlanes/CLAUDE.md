# ExploracionPlanes

App clínica (WPF + WinForms en migración) para evaluar planes de radioterapia contra plantillas de
restricciones, corriendo standalone o como plugin de Eclipse (ESAPI). Software en uso clínico real:
cualquier bug en el cálculo de dosis/volumen tiene impacto directo en pacientes.

## Regla de proceso (obligatoria)

- **Todo cambio sobre código funcional** debe incluir un test que compare comportamiento previo vs.
  nuevo, documentado como entrada nueva en `Tests.md` (fecha, qué cambió, cómo se testeó, números
  usados, resultado).
- Si el código depende de ESAPI (no corre fuera de Eclipse), aislar la lógica pura afectada y
  agregar el test en `Tests/TestMejoras/Program.cs` (proyecto standalone, `dotnet run`).
- Todas las respuestas en este proyecto deben ser en español.

## Compilar

No hay `dotnet build` para el proyecto principal (framework .NET viejo + refs ESAPI). El proyecto
principal existe como **3 `.csproj` separados**, uno por versión de Eclipse soportada, todos
apuntando a los mismos archivos fuente (misma lista de `<Compile>`/`<Page>` — al agregar un archivo
nuevo, agregarlo a los 3): `ExploracionPlanes.Eclipse13_6.csproj`, `ExploracionPlanes.Eclipse15_6.csproj`,
`ExploracionPlanes.Eclipse18_2.csproj`. Cada uno referencia sus propias DLL de ESAPI desde
`lib/ESAPI/<version>/` (no versionadas en git — son binarios propietarios de Varian, copiarlas ahí
manualmente desde la instalación real de esa versión de Eclipse antes de compilar). Cada proyecto
genera SIEMPRE dos outputs (no dos `.csproj` por tipo, sino overrides de `-p:` en la misma build):
`ExploracionPlanes.esapi.dll` (Library, para copiar como plugin en Eclipse) y `ExploracionPlanes.exe`
(standalone, entry point `ExploracionPlanes.Program`). Usar `build.ps1` (hace las 2 compilaciones por
versión, con `IntermediateOutputPath` separado por tipo — necesario porque si comparten `obj\`, el
incremental clean de MSBuild borra los archivos de la build anterior):

```powershell
.\build.ps1 13_6
.\build.ps1 15_6
.\build.ps1 18_2
.\build.ps1 13_6 -Configuration Release
```

Salida: `bin\Eclipse<version>\x64\<Configuration>\ExploracionPlanes.esapi.dll` y `...\ExploracionPlanes.exe`.

Si hace falta compilar un solo output a mano (sin el script), MSBuild acepta overridear `OutputType`/
`AssemblyName`/`StartupObject`/`IntermediateOutputPath` por línea de comando sin tocar el `.csproj`:

```
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe" ExploracionPlanes.Eclipse13_6.csproj -p:Configuration=Debug -p:Platform=x64
```

(sin overrides, compila con el `OutputType=Library`/`AssemblyName=ExploracionPlanes` que trae el
`.csproj` por default — no es ninguno de los 2 outputs finales, usar `build.ps1` para eso).

Cada `.csproj` define su propia constante de compilación (`ECLIPSE13_6`/`ECLIPSE15_6`/`ECLIPSE18_2`)
para el caso — excepcional — de que una API de ESAPI difiera entre versiones sin un mínimo común
denominador; usar `#if ECLIPSE1x_x` solo ahí, lo más cerca posible del punto de uso, no esparcido.

Warnings de arquitectura MSIL/AMD64 son preexistentes y esperables (refs de ESAPI son AMD64).

## Arquitectura / puntos de entrada

- `Program.cs`: entrada standalone (fuerza `NumberDecimalSeparator="."` en el hilo antes de todo).
- `Script.cs`: entrada como plugin de Eclipse (ESAPI `Execute(ScriptContext)`) — misma normalización
  de cultura que Program.cs, agregada porque sin ella el parseo de dosis/alfa-beta depende del
  locale de Windows en el server de Eclipse.
- `Main.xaml.cs`: ventana raíz (WPF). `Form1_prioridades`/`Form2`/`Form2_DosPlanes`/`PlantillaBlanco`
  y la mayoría de diálogos ya son WPF sobre `DialogoWpf` (chrome propio, ver clase). **Pendientes de
  migrar**: `Form1_ext.cs` y `Form3.cs` (siguen WinForms) — al abrirlos desde una ventana WPF hay que
  pasar el owner explícito (`DialogoWpf.OwnerWin32`), `ActiveForm`/chrome propio no aplican ahí.
- `IRestriccion` + 6 implementaciones (`RestriccionDosis`, `RestriccionDosisMax`,
  `RestriccionDosisMedia`, `RestriccionVolumen`, `RestriccionVolumenCritico`,
  `RestriccionIndiceConformidad`), todas heredan de `RestriccionBase` (evaluación de tolerancia,
  sampling coverage, edición en grupo). Cada subclase solo define `crearEtiquetaInicio()`,
  `analizarPlanEstructura()` y `crear()` — si un cambio aplica a la lógica compartida, va en
  `RestriccionBase`, no en cada subclase.
- `Chequeos.cs`: chequeos de QA (camilla-equipo, dose rate, nombre de curso, isocentros) sobre un
  plan/PlanSum antes de analizar.

## Cuidado con

- **Decimales**: nunca usar `double.Parse`/`TryParse` sin `NumberStyles` explícito ni cultura
  explícita — el default permite separador de miles y puede confundir "." con coma bajo un Windows
  en es-AR. Ver `Metodos.validarYConvertirADouble` y `Estructura.AlfaBeta` como referencia.
- **`Configuracion.*()`** (ej. `volDosisMaxima()`) lee `Properties.Settings.Default` en vivo — no
  cachear su valor en un campo `static` (ver historial en `Tests.md`, ya pasó con
  `RestriccionDosisMax`), porque el usuario puede cambiar la config sin reiniciar la app.
- **Form2.xaml.cs / Form2_DosPlanes.xaml.cs**: siguen siendo 2 ventanas/XAML separadas (flujo de
  análisis para 1 vs. 2 planes, genuinamente distinto), pero todo lo que era código C# idéntico
  (I/O de memoria por plan, `prescripcionPredefinida`, impresión, etc.) vive en `Form2Compartido`
  — llamar ahí antes de copiar un helper de una ventana a la otra. La fusión completa en una sola
  ventana (eliminar también la duplicación de XAML) sigue evaluada y pospuesta por el riesgo de un
  rewrite de layout no verificable sin Eclipse (ver `Tests.md`, 2026-09-10).
