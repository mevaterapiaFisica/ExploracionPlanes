# Agregar o actualizar una versión de Eclipse soportada

Playbook para cuando hay que sumar una versión nueva de Eclipse (ej. hoy 13.6/15.6/18.2, mañana una
más) o actualizar las DLL de una ya soportada. Estrategia del proyecto: **build separado por
versión** (un `.csproj` por versión, no un binario único) — evita limitar el código a la API más
restrictiva y permite detectar breaking changes reales vía errores de compilación en vez de
adivinar.

## 0. Antes de tocar código

- Conseguir las DLL de ESAPI de la versión nueva: `VMS.TPS.Common.Model.API.dll`,
  `VMS.TPS.Common.Model.Types.dll`, `VMS.TPS.Common.Model.Interface.dll`. Sin ellas no se puede
  compilar ni detectar breaking changes reales — pedirlas al usuario, no simular el resultado.
- **`Interface.dll` hace falta aunque el código nunca la nombre directamente.** `API.dll` la
  referencia internamente (tipos devueltos por propiedades, p.ej. `Fractionation`), y sin ella el
  compilador tira errores raros (`CS1061` en miembros que "deberían" existir) o `MissingAssembly` al
  reflexionar. No asumir que no hace falta solo porque un `grep` no encuentra el namespace en el
  código propio — verificar con
  `[Reflection.Assembly]::LoadFrom($dll).GetReferencedAssemblies()` en PowerShell.
- Ojo con nombres de archivo parecidos pero que NO son el reemplazo (ej.
  `VMS.TPS.PlanningModelLibrary.Interface.dll` no es `VMS.TPS.Common.Model.Interface.dll`) —
  confirmar el nombre exacto antes de dar por buena una entrega.
- Las DLL suelen estar en la carpeta de referencias de ESAPI que se instala junto con Eclipse
  (a veces "esapi"/"ESAPI dll de..."), normalmente con dos subcarpetas: una completa (runtime
  completo) y una `API/` más chica para desarrollo de scripts. Ambas pueden faltar `Interface.dll`
  si solo se copió la subcarpeta `API/`.
- No versionar las DLL en git (binarios propietarios de Varian) — ya están en `.gitignore` vía
  `lib/ESAPI/`.

## 1. Vendorizar

Copiar las 3 DLL a `lib/ESAPI/<version>/` (ej. `lib/ESAPI/20.1/`).

## 2. Triplicar (N-plicar) el `.csproj`

Copiar un `.csproj` existente. Cambiar SOLO:

- `ProjectGuid` (uno nuevo por copia).
- Los 3 `HintPath` de ESAPI → `lib\ESAPI\<version>\...`.
- `OutputPath` (subcarpeta propia, ej. `bin\Eclipse20_1\x64\Debug\`) — si no se separa, los builds
  se pisan entre sí.
- `DefineConstants`: agregar una constante propia (`ECLIPSE20_1`) para el caso excepcional de
  necesitar código condicional (paso 4).

Todo lo demás (lista de `<Compile>`/`<Page>`, otras referencias) debe quedar **idéntico** — un solo
árbol de fuente. Verificar con `diff` entre los `.csproj` que la única diferencia sea justamente
esos campos. Si hay `.sln`, agregar el `.csproj` nuevo ahí también.

## 3. Compilar y usar los errores como mapa de incompatibilidades

Ver `compilar-multiversion.md` para el comando MSBuild y sus gotchas (`-p:Platform=x64`,
`MSYS_NO_PATHCONV`, no filtrar por "error" solo). Compilar **las 3 versiones**, no solo la nueva —
confirma que las viejas siguen compilando igual. Anotar cada error real (no warnings MSIL/AMD64
preexistentes) — son la lista de incompatibilidades a resolver en el paso 4.

## 4. Resolver cada incompatibilidad (ladder, del más al menos preferido)

1. Si compila igual sin cambios en las N versiones: no tocar nada.
2. Si hay una firma/miembro que cambió de nombre o forma pero existe un mínimo común denominador
   (ej. un overload disponible en todas): usar ESE, una sola línea válida en todas las versiones.
3. Solo si es inevitable (breaking change real, sin equivalente común): aislar la diferencia en un
   **método de extensión pequeño**, con `#if ECLIPSE1x_x / #elif ... / #else`, en `EsapiCompat.cs` —
   NO esparcir `#if` por todos los archivos que llaman a esa API. Reemplazar todos los call-sites
   del miembro viejo por una llamada al método de extensión (ver el shim de fraccionamiento ya
   existente ahí como ejemplo).

### Cómo detectar QUÉ cambió cuando el error de compilación no alcanza

Si el error es genérico (`CS1061: no contiene una definición para X`) y no está claro si el miembro
se renombró, se movió, o desapareció, comparar los símbolos de la DLL real de cada versión sin
resolver todas sus dependencias (reflexión completa suele fallar si falta alguna DLL indirecta):

```bash
grep -a -o "[A-Za-z_][A-Za-z0-9_]\{5,\}" "ruta/a/la.dll" | grep -i "<palabra clave>" | sort -u
```

Corriendo esto contra la DLL de cada versión (misma palabra clave relacionada al miembro que falla)
se ve rápido si el símbolo desapareció, se renombró, o se movió a otra clase. Ejemplo real:
`UniqueFractionation`/`Fractionation` existían en 13.6 y desaparecieron en 15.6/18.2, reemplazados
por las mismas 3 propiedades aplanadas directo en `PlanSetup` (ver `EsapiCompat.cs`).

### Cuidado con tipos numéricos al escribir el shim

Una propiedad puede ser `int` en una versión de la API real y `int?`/`double` en otra (o distinto
del tipo asumido por un stub de test) — un cast explícito (`(double)valor`) suele resolverlo sin
cambiar el comportamiento (si el valor es `null` en un `Nullable<T>`, el cast explícito preserva el
mismo riesgo de excepción que tenía el código original, no lo esconde).

## 5. Actualizar `Tests/StubEsapi/Stub.cs`

Los tests standalone (`Tests/TestMejoras`, `Tests/GenerarPlantillasReales`,
`Tests/TestCondicionPlanSuma`, etc.) compilan código de producción real contra este stub, no contra
ESAPI real. Los tests no definen ninguna constante `ECLIPSE1x_x` — toman la rama `#else`/default del
shim del paso 4. El stub tiene que exponer esa forma (además de, o en reemplazo de, la vieja) para
seguir compilando. Si algún archivo que ahora llama al shim está linkeado (`<Compile Include>`) en
un proyecto de test aparte, agregar también `EsapiCompat.cs` a esa lista de `<Compile>`.

## 6. Verificación final

- Compilar los N `.csproj` con MSBuild, sin errores (solo warnings preexistentes esperables).
- Correr TODOS los proyectos de test standalone existentes (no solo los tocados) — un stub
  compartido cambiado puede afectar a otros tests que no se tocaron directamente.
- Documentar en `Tests.md`: motivo del cambio, breaking changes encontrados (con evidencia — qué
  símbolo desapareció en qué versión), archivos tocados, resultado de compilar cada versión y de
  correr los tests, y qué queda pendiente de verificar en vivo (correr dentro de un Eclipse real de
  esa versión suele no ser posible desde un entorno de desarrollo sin esa instalación — dejarlo
  explícito como pendiente, no simular que se probó).
- Actualizar `CLAUDE.md` con el comando de build de la versión nueva y dónde conseguir/poner sus DLL.

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
| Build "pasa" pero una referencia (ej. `Interface.dll`) nunca se resolvió | Filtrar la salida de MSBuild solo por `error` esconde el `warning MSB3245` de referencia no resuelta | Correr sin filtrar (o filtrar `error\|warning`) al menos una vez por versión |
