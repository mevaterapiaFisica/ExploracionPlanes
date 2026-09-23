# Instructivo: migrar un proyecto ESAPI de "solo Eclipse 13.6" a multi-versión (13.6/15.6/18.2)

Playbook para otro agente que va a hacer esta misma migración en un proyecto C#/.NET distinto que hoy
compila solo contra Eclipse 13.6 (ESAPI). Basado en una migración ya hecha en otro proyecto hermano
(ExploracionPlanes). Estrategia: **build separado por versión** (un `.csproj` por versión de Eclipse,
no un binario único) — evita limitar el código a la API más restrictiva y permite detectar breaking
changes reales vía errores de compilación en vez de adivinar.

## 0. Preguntas a resolver con el usuario antes de tocar código

- ¿Tiene o puede conseguir las DLL de ESAPI (`VMS.TPS.Common.Model.API/Interface/Types.dll`) de cada
  versión objetivo? Sin ellas no se puede compilar ni detectar breaking changes reales.
- ¿Build separado por versión está bien, o necesita un binario único? (single binario es mucho más
  restrictivo: obliga a usar solo la API común a todas las versiones).
- Si el código depende de ESAPI real y no corre fuera de Eclipse: ¿hay ya un stub/mock para tests
  standalone (`dotnet run` sin Eclipse)? Si no existe, avisar que los tests de regresión van a ser más
  limitados.

## 1. Vendorizar las DLL de ESAPI por versión

- Crear `lib/ESAPI/<version>/` (ej. `lib/ESAPI/13.6/`, `lib/ESAPI/15.6/`, `lib/ESAPI/18.2/`) y pedir al
  usuario que copie ahí, por versión, **al menos**:
  - `VMS.TPS.Common.Model.API.dll`
  - `VMS.TPS.Common.Model.Types.dll`
  - `VMS.TPS.Common.Model.Interface.dll`
- **`Interface.dll` hace falta aunque el código nunca la nombre directamente.** `API.dll` la referencia
  internamente (tipos devueltos por propiedades, p.ej. `Fractionation`), y sin ella el compilador tira
  errores raros (`CS1061` en miembros que "deberían" existir) o `MissingAssembly` al reflexionar. No
  asumir que no hace falta solo porque `grep` no encuentra el namespace en el código propio — verificar
  con `[Reflection.Assembly]::LoadFrom($dll).GetReferencedAssemblies()` en PowerShell.
- Ojo con nombres de archivo parecidos pero que NO son el reemplazo: en versiones nuevas de Eclipse
  puede haber otras DLL con nombre similar (ej. `VMS.TPS.PlanningModelLibrary.Interface.dll`) que no es
  lo mismo que `VMS.TPS.Common.Model.Interface.dll`. Confirmar el nombre exacto antes de dar por buena
  una entrega del usuario.
- Si el usuario no tiene claro de dónde sacarlas: suele estar en la carpeta de referencias de ESAPI que
  se instala junto con Eclipse (a veces llamada `esapi`/`ESAPI dll de...`), normalmente con dos
  subcarpetas — una completa (todo el runtime de Eclipse) y una `API/` más chica pensada para
  desarrollo de scripts. Ambas pueden faltar `Interface.dll` si solo se copió la subcarpeta `API/`.
- **No versionar las DLL en git** (son binarios propietarios de Varian): agregar `lib/ESAPI/` al
  `.gitignore`.

## 2. Triplicar (o N-plicar) el `.csproj`

- Renombrar el `.csproj` actual a `<Proyecto>.Eclipse13_6.csproj` (`git mv`, preserva historial) y
  copiarlo una vez por cada versión objetivo nueva.
- En cada copia, cambiar SOLO:
  - `ProjectGuid` (generar uno nuevo por copia — evita colisiones si alguna vez entran a la misma .sln)
  - Los 3 `HintPath` de ESAPI → `lib\ESAPI\<version>\...`
  - `OutputPath` (agregar subcarpeta por versión, ej. `bin\Eclipse15_6\x64\Debug\`) — si no se separa,
    los builds se pisan entre sí.
  - `DefineConstants`: agregar una constante propia (`ECLIPSE13_6`/`ECLIPSE15_6`/`ECLIPSE18_2`) para el
    caso excepcional de necesitar código condicional (paso 4).
- Todo lo demás (lista de `<Compile>`/`<Page>`, otras referencias) debe quedar **idéntico** entre las
  copias — un solo árbol de fuente, no duplicar código. Al agregar un archivo nuevo al proyecto más
  adelante, agregarlo a los N `.csproj`.
- Verificar con `diff` entre los `.csproj` que la única diferencia sea justamente esos campos.
- Si el proyecto tiene un `.sln`: agregar los `.csproj` nuevos ahí también (o dejar constancia de que
  quedan pendientes de agregar, si el usuario prefiere hacerlo a mano en Visual Studio).

## 3. Compilar cada versión y usar los errores como mapa de incompatibilidades

Comando base (ajustar ruta de MSBuild si cambia la instalación de Visual Studio):

```
MSBuild.exe <Proyecto>.Eclipse<version>.csproj -p:Configuration=Debug -p:Platform=x64 -nologo -v:minimal -t:Rebuild
```

Gotchas de esta parte:

- **Especificar `-p:Platform=x64` explícitamente.** Si el `.csproj` tiene grupos de configuración
  separados para `AnyCPU` y `x64` (común en proyectos viejos con refs de ESAPI, que son x64), MSBuild
  sin este flag compila silenciosamente contra `AnyCPU` — no falla, pero usa otro grupo de propiedades
  (puede que ni siquiera tenga las refs de ESAPI actualizadas) y el resultado no dice nada sobre
  compatibilidad real.
- **Si se corre desde Git Bash en Windows**: los flags que empiezan con `/` (`/nologo`, `/p:...`) se
  confunden con paths por la conversión automática de rutas de Git Bash. Usar `export
  MSYS_NO_PATHCONV=1` antes del comando, y preferir la sintaxis `-p:X=Y` (con guión) en vez de `/p:X=Y`.
- Compilar **todas** las versiones, no solo la nueva — sirve para confirmar que la vieja (13.6) sigue
  compilando igual que antes de tocar nada.
- Anotar cada error real (no warnings de arquitectura MSIL/AMD64, esos son preexistentes y esperables
  con refs ESAPI x64) — son la lista real de incompatibilidades a resolver en el paso 4.
- **No filtrar la salida de MSBuild buscando solo la palabra "error".** Una referencia que no se pudo
  resolver (ej. falta una DLL, un `HintPath` roto) es un **warning** (`MSB3245: Could not resolve this
  reference...`), no un error — y si nada del código compilado termina necesitando un tipo de esa DLL,
  la build **compila OK igual**, con esa referencia colgando sin servir para nada. Pasó en la práctica:
  se filtró la salida con `grep -i error` y quedó sin verse un `MSB3245` sobre `Interface.dll` faltante
  para una versión — el build "pasó" pero la referencia nunca estuvo resuelta. Para no repetirlo: correr
  al menos una vez por versión SIN filtrar nada (o filtrando por `error|warning`), leer la salida
  completa, y si aparece un `MSB3245` sobre una referencia que en teoría hacía falta, decidir a
  consciencia si sacarla (si nada la usa) o conseguir el archivo real (si hace falta).

## 4. Resolver cada incompatibilidad (ladder, del más al menos preferido)

1. Si compila igual sin cambios en las N versiones: no tocar nada.
2. Si hay una firma/miembro que cambió de nombre o de forma pero existe un mínimo común denominador
   (ej. un overload disponible en todas): usar ESE, una sola línea de código válida en todas las
   versiones.
3. Solo si es inevitable (breaking change real, sin equivalente común): aislar la diferencia en un
   **método de extensión pequeño**, con `#if ECLIPSE13_6 / #elif ECLIPSE15_6 / #else`, en un archivo
   dedicado (ej. `EsapiCompat.cs`) — NO esparcir `#if` por todos los archivos que llaman a esa API.
   Reemplazar todos los call-sites del miembro viejo por una llamada al método de extensión.

### Cómo detectar QUÉ cambió cuando el error de compilación no alcanza

Si el error es genérico (`CS1061: no contiene una definición para X`) y no está claro si el miembro se
renombró, se movió, o directamente desapareció, comparar los símbolos de la DLL real de cada versión
sin necesidad de resolver todas sus dependencias (reflexión completa suele fallar si falta alguna DLL
referenciada indirectamente):

```bash
grep -a -o "[A-Za-z_][A-Za-z0-9_]\{5,\}" "ruta/a/la.dll" | grep -i "<palabra clave>" | sort -u
```

Corriendo esto contra la DLL de cada versión (mismo `<palabra clave>` relacionada al miembro que
falla) se ve rápido si el símbolo desapareció, se renombró, o se movió a otra clase — mucho más rápido
que adivinar o que cargar la DLL con reflexión completa. Ejemplo real: `UniqueFractionation`/
`Fractionation` existían en 13.6 y desaparecieron totalmente en 15.6/18.2, reemplazados por las mismas
3 propiedades pero aplanadas directo en la clase contenedora.

### Cuidado con tipos numéricos al escribir el shim

Una propiedad puede ser `int` en una versión de la API real y `int?`/`double` en otra (o distinto del
tipo asumido por un stub de test) — un cast explícito (`(double)valor`) suele resolverlo sin cambiar el
comportamiento (si el valor es `null` en un `Nullable<T>`, el cast explícito preserva el mismo riesgo
de excepción que tenía el código original, no lo esconde).

## 5. Actualizar stubs/mocks de test si existen

Si el proyecto tiene un stub de ESAPI para tests standalone (compilar código de producción tal cual
contra tipos falsos, sin Eclipse):

- Los tests NO definen ninguna constante `ECLIPSE1x_x` — van a tomar la rama `#else`/default del shim
  del paso 4. El stub tiene que exponer esa forma (además de, o en reemplazo de, la vieja) para que siga
  compilando.
- Si algún archivo de producción que ahora llama al shim está linkeado (`<Compile Include>`) en un
  proyecto de test aparte, agregar también el archivo del shim (`EsapiCompat.cs`) a esa lista de
  `<Compile>` del proyecto de test — si no, el test no compila (el método de extensión no está en su
  alcance).

## 6. Si el proyecto compila a la vez plugin (DLL) y standalone (EXE)

Muchos de estos proyectos generan dos outputs del mismo código: una DLL para copiar como plugin dentro
de Eclipse (`OutputType=Library`, entry point `Execute(ScriptContext)`) y un EXE standalone
(`OutputType=Exe`, con su propio `Program.Main`). Si hoy eso se hace a mano (compilar, renombrar el
`.dll`, volver a compilar como exe), automatizarlo con un script en vez de tocar el `.csproj`:

- MSBuild permite overridear `OutputType`, `AssemblyName` y `StartupObject` por línea de comando
  (`-p:OutputType=Exe -p:AssemblyName=... -p:StartupObject=Namespace.Program`) sin modificar el
  `.csproj` — mismo proyecto, dos invocaciones con distintos overrides.
- **Gotcha importante**: si ambas invocaciones comparten el mismo `IntermediateOutputPath` (el `obj\`
  por default es el mismo para todo el proyecto/Configuration/Platform, sin importar `AssemblyName`),
  el "incremental clean" de MSBuild borra los archivos de la build anterior (los reconoce como "ya no
  son output de esta build" porque el `AssemblyName` cambió) — el resultado es que después de la
  segunda build desaparece el output de la primera. Hay que overridear también
  `-p:IntermediateOutputPath=obj\<algo-distinto-por-tipo>\` en cada invocación para que no se pisen.
- Envolver las 2 (o N, si también hay N versiones de Eclipse) invocaciones en un script (`build.ps1` o
  similar) parametrizado por versión, para no repetir la secuencia a mano cada vez.

## 7. Verificación final

- Compilar los N `.csproj` con MSBuild, sin errores (solo warnings preexistentes esperables).
- Correr TODOS los proyectos de test standalone existentes (no solo los tocados) — un stub compartido
  cambiado puede afectar a otros tests que no se tocaron directamente.
- Documentar el cambio (si el repo tiene convención de registro de tests — ej. `Tests.md`, con entradas
  de "qué se probó antes/después"): motivo del cambio, breaking changes encontrados (con la evidencia:
  qué símbolo desapareció en qué versión), archivos tocados, resultado de compilar cada versión y de
  correr los tests, y qué queda pendiente de verificar en vivo (correr dentro de un Eclipse real de esa
  versión suele no ser posible desde un entorno de desarrollo sin esa instalación — dejarlo explícito
  como pendiente, no simular que se probó).
- Actualizar el doc de build del repo (README/CLAUDE.md/equivalente) con los N comandos de MSBuild, uno
  por versión, y dónde conseguir/poner las DLL de `lib/ESAPI/<version>/`.

## Resumen de gotchas (para no repetirlos)

| Problema | Causa | Solución |
|---|---|---|
| MSBuild compila la config equivocada sin avisar | Falta `-p:Platform=x64` | Especificarlo siempre explícito |
| Flags `/algo` se comen como paths en Git Bash | Path conversion automática de Git Bash | `MSYS_NO_PATHCONV=1` + sintaxis `-p:` |
| Error de compilación en un miembro que "no debería faltar" | Falta `Interface.dll` (dependencia interna de `API.dll`, no usada directo por el código) | Vendorizar también `Interface.dll` por versión, aunque nada la nombre en el código |
| DLL con nombre parecido pero equivocada | Varian renombra/reorganiza assemblies entre versiones | Confirmar nombre EXACTO esperado, no aceptar "algo parecido" |
| No queda claro si un miembro se renombró o desapareció | Reflexión completa falla por dependencias faltantes | `grep -a -o` de símbolos ASCII directo sobre el binario, comparar entre versiones |
| Cast error en el shim (`int?` vs `double`) | Tipos de propiedad distintos entre stub/versiones reales | Cast explícito, sin esconder el riesgo de null que ya existía |
| Al compilar DLL y luego EXE del mismo proyecto, desaparece el output anterior | `obj\` (IntermediateOutputPath) compartido entre ambas builds, incremental clean lo borra | `-p:IntermediateOutputPath` distinto por tipo de output en cada invocación |
| Build "pasa" pero una referencia (ej. `Interface.dll`) nunca se resolvió | Filtrar la salida de MSBuild solo por `error` esconde el `warning MSB3245` de referencia no resuelta | Correr sin filtrar (o filtrar `error\|warning`) al menos una vez por versión; una ref no resuelta que nada usa compila igual pero queda colgando |
