# Compilar ExploracionPlanes (3 versiones de Eclipse × 2 outputs)

No hay `dotnet build` simple: framework .NET viejo + referencias ESAPI x64. Cada `.csproj` genera
SIEMPRE dos outputs del mismo código (no dos `.csproj` por tipo, sino overrides de `-p:` en la misma
build): `ExploracionPlanes.esapi.dll` (Library, plugin de Eclipse) y `ExploracionPlanes.exe`
(standalone, entry point `ExploracionPlanes.Program`).

## Uso normal: `build.ps1`

```powershell
.\build.ps1 13_6
.\build.ps1 15_6
.\build.ps1 18_2
.\build.ps1 13_6 -Configuration Release
```

Salida: `bin\Eclipse<version>\x64\<Configuration>\ExploracionPlanes.esapi.dll` y
`...\ExploracionPlanes.exe`.

El script existe porque a mano hace falta dos invocaciones de MSBuild con `IntermediateOutputPath`
distinto (ver gotcha abajo) — no reimplementar esa secuencia inline, usar el script.

## A mano (un solo output, sin el script)

```
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe" ExploracionPlanes.Eclipse13_6.csproj -p:Configuration=Debug -p:Platform=x64
```

Sin overrides compila con `OutputType=Library`/`AssemblyName=ExploracionPlanes` (el default del
`.csproj`) — no es ninguno de los 2 outputs finales, solo sirve para chequear que compila.

Para un output específico, overridear por línea de comando sin tocar el `.csproj`:

```
-p:OutputType=Exe -p:AssemblyName=ExploracionPlanes -p:StartupObject=ExploracionPlanes.Program -p:IntermediateOutputPath=obj\exe\
```

**Gotcha**: si el plugin (Library) y el standalone (Exe) comparten `IntermediateOutputPath` (el
`obj\` por default es el mismo para todo el proyecto/Configuration/Platform, sin importar
`AssemblyName`), el "incremental clean" de MSBuild borra los archivos de la build anterior al
detectar que el `AssemblyName` cambió — el resultado es que la segunda build hace desaparecer el
output de la primera. Por eso `build.ps1` overridea `IntermediateOutputPath` distinto por tipo de
output en cada invocación.

## Gotchas generales de MSBuild + ESAPI

- **Especificar `-p:Platform=x64` explícitamente.** Sin este flag, si el `.csproj` tiene grupos de
  configuración separados para `AnyCPU`/`x64` (común con refs de ESAPI, que son x64), MSBuild
  compila silenciosamente contra `AnyCPU` — no falla, pero usa otro grupo de propiedades (puede que
  ni tenga las refs de ESAPI actualizadas) y el resultado no dice nada sobre compatibilidad real.
- **Desde Git Bash en Windows**: los flags que empiezan con `/` (`/nologo`, `/p:...`) se confunden
  con paths por la conversión automática de rutas de Git Bash. Usar `export MSYS_NO_PATHCONV=1`
  antes del comando, y preferir `-p:X=Y` (con guión) en vez de `/p:X=Y`.
- **No filtrar la salida de MSBuild buscando solo la palabra "error".** Una referencia sin resolver
  (ej. falta una DLL, un `HintPath` roto) es un **warning** (`MSB3245: Could not resolve this
  reference...`), no un error — si nada del código compilado termina necesitando un tipo de esa DLL,
  la build "compila OK" igual, con esa referencia colgando sin servir para nada. Correr al menos una
  vez sin filtrar (o filtrando `error|warning`) y, si aparece un `MSB3245` sobre una referencia que
  en teoría hacía falta (ej. `Interface.dll`), decidir a consciencia si sacarla (si nada la usa) o
  conseguir el archivo real.
- Warnings de arquitectura MSIL/AMD64 son preexistentes y esperables (refs de ESAPI son AMD64) — no
  son señal de problema.

## Al agregar un archivo nuevo al proyecto

Agregarlo a los 3 `.csproj` (`<Compile Include>` o `<Page>` según corresponda) — no solo al que se
esté usando para probar. Verificar con `diff` entre los `.csproj` que la única diferencia entre
ellos siga siendo `ProjectGuid`, los 3 `HintPath` de ESAPI, `OutputPath` y `DefineConstants`.
