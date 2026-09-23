---
name: esapi-eclipse
description: Trabajar con la API de Varian Eclipse (ESAPI, VMS.TPS.Common.Model.API/Types/Interface) en ExploracionPlanes — compilar el proyecto multi-versión (13.6/15.6/18.2), conectar/desconectar la Application, leer DVH/estructuras/prescripción de un PlanSetup o PlanSum, testear lógica que depende de ESAPI sin tener Eclipse instalado, y resolver breaking changes entre versiones de Eclipse. Usar siempre que se toque un archivo que importa VMS.TPS.Common.Model.*, que se agregue o modifique una Restriccion/Chequeo/lectura de plan, que se necesite compilar o testear el proyecto, o que aparezca un error de compilación tipo CS1061/CS0117 en tipos de ESAPI (probablemente breaking change entre versiones de Eclipse, no bug propio).
---

# ESAPI / Eclipse en ExploracionPlanes

ExploracionPlanes es una app clínica de producción (evalúa planes de radioterapia contra
plantillas de restricciones de dosis) que corre contra la API de Varian Eclipse (ESAPI:
`VMS.TPS.Common.Model.API` / `.Types` / `.Interface`). Un bug en cómo se lee dosis/volumen desde
ESAPI tiene impacto directo en pacientes — leer con cuidado antes de tocar código que llama a la API.

Este skill cubre: compilar, conectar/desconectar de Eclipse, patrones de lectura de datos clínicos
(DVH, estructuras, prescripción), testear sin Eclipse instalado, y qué hacer ante un breaking
change entre versiones de Eclipse.

**Regla de proceso del proyecto (ver `CLAUDE.md`)**: todo cambio a código funcional necesita un
test antes/después documentado en `Tests.md`, y las respuestas del agente deben ser en español.
Este skill no reemplaza esa regla — la instrumenta para el caso específico de código que toca ESAPI.

## 1. El proyecto compila 3 veces, una por versión de Eclipse

No hay un único binario. Tres `.csproj` (`ExploracionPlanes.Eclipse13_6.csproj`,
`...15_6.csproj`, `...18_2.csproj`) comparten el mismo árbol de fuentes pero referencian DLL de
ESAPI distintas desde `lib/ESAPI/<version>/` (no versionadas en git, son binarios propietarios de
Varian — copiarlas ahí desde una instalación real de esa versión de Eclipse). Al agregar un
archivo `.cs`/`.xaml` nuevo al proyecto, agregarlo a los 3 `.csproj`, no solo a uno.

Compilar con `build.ps1` (genera plugin `.esapi.dll` + standalone `.exe` por versión, con
`IntermediateOutputPath` separado para que no se pisen):

```powershell
.\build.ps1 13_6
.\build.ps1 15_6
.\build.ps1 18_2
```

Un cambio que toca una API de ESAPI se considera terminado solo si compila en las 3 versiones, no
solo en la que se probó primero. Ver detalle completo (comando MSBuild a mano, overrides de
`OutputType`/`AssemblyName`, gotchas de `IntermediateOutputPath` compartido) en
`references/compilar-multiversion.md`.

## 2. Conectar y desconectar de Eclipse

Patrón usado en todos los puntos de entrada (`Form2`, `Form2_DosPlanes`, `Form3`, `Mineria`,
`ImportarNombresEstructuras`) — login interactivo, sin credenciales embebidas:

```csharp
var app = VMS.TPS.Common.Model.API.Application.CreateApplication(null, null);
try
{
    // ... trabajo con app.OpenPatientById(...), plan, estructuras, DVH ...
}
finally
{
    app.ClosePatient();
    app.Dispose();
}
```

`ClosePatient()` y `Dispose()` son ambos necesarios (`Dispose` sin `ClosePatient` previo puede
dejar el paciente lockeado en ARIA). Si el flujo es lineal y corto, `using (var app = ...)` alcanza
(ver `Mineria.cs`); si hay múltiples salidas/reintentos, usar `try/finally` explícito como en
`Form3.cs`.

Como plugin de Eclipse (`Script.cs`, `Execute(ScriptContext)`), el contexto (paciente/plan activo)
ya viene inyectado — no se abre `Application` propia ahí.

## 3. Leer datos clínicos: dónde mirar antes de escribir código nuevo

No reinventar estos patrones — ya existen y están cacheados/optimizados:

- **DVH de una estructura**: nunca llamar `plan.GetDVHCumulativeData(...)` directo si puede haber
  más de una restricción sobre la misma estructura en el mismo análisis. Usar
  `CacheDVH.Obtener(plan, estructura, volumePresentation)` (`CacheDVH.cs`) — cachea por
  `(PlanningItem, Structure, VolumePresentation)`, evita recalcular el histograma completo en el
  servidor. Llamar `CacheDVH.Limpiar()` al arrancar un análisis nuevo (evita arrastrar DVH de un
  plan/paciente anterior).
- **`GetDoseAtVolume`/`GetVolumeAtDose` sobre un `PlanSum`**: ESAPI no los expone para `PlanSum`
  (solo para `PlanSetup`). Usar `DVHDataExtensions_ESAPIX.GetDoseAtVolume`/`GetVolumeAtDose`
  (interpolación manual sobre `DVHPoint[]`) en vez de asumir que existen — no es un bug, es
  limitación real de la API.
- **Prescripción / fraccionamiento** (`NumberOfFractions`, `PrescribedDosePerFraction`,
  `DosePerFractionInPrimaryRefPoint`): estas propiedades cambiaron de forma entre versiones de
  Eclipse (ver §4). Llamar siempre a través del shim `EsapiCompat.cs`
  (`plan.NumeroFracciones()`, `plan.DosisPrescriptaPorFraccion()`,
  `plan.DosisPorFraccionEnPuntoRefPrimario()`), nunca a `UniqueFractionation.*` ni a la propiedad
  aplanada directo.
- **Estructuras / nombres / α-β**: `Estructura.cs` ya tiene el diccionario de nombres
  (`estructuras.txt`) y tabla α/β (`alfaBeta.txt`) cacheados en memoria — no releer esos `.txt` a
  mano.
- **Restricciones DVH** (`IRestriccion` + 6 implementaciones): todas heredan de `RestriccionBase`
  (evaluación de tolerancia, sampling coverage, edición en grupo). Un cambio a lógica compartida va
  en `RestriccionBase`, no duplicado en cada subclase — mirar ahí primero.

## 4. Breaking changes conocidos entre versiones de Eclipse

Ya diagnosticados y resueltos con shims — no reabrir la investigación, usar lo que ya existe:

| Qué cambió | 13.6 | 15.6 / 18.2 | Dónde está resuelto |
|---|---|---|---|
| Fraccionamiento | `PlanSetup.UniqueFractionation.{NumberOfFractions,PrescribedDosePerFraction,DosePerFractionInPrimaryRefPoint}` | Mismas 3 propiedades aplanadas directo en `PlanSetup` (`UniqueFractionation` no existe) | `EsapiCompat.cs`, `#if ECLIPSE13_6` |

Si aparece un error de compilación de ESAPI nuevo (`CS1061`, `CS0117`, etc.) al agregar una versión
o al tocar código que usa un tipo de `VMS.TPS.Common.Model.*`, es señal de un breaking change nuevo,
no de un typo — seguir el procedimiento de `references/migrar-version-eclipse.md` (comparar símbolos
de las DLL reales, aislar la diferencia en `EsapiCompat.cs` con `#if ECLIPSE1x_x`, nunca esparcir
`#if` por los call sites).

## 5. Testear código que usa ESAPI sin tener Eclipse instalado

ESAPI no corre fuera de Eclipse — no se puede instanciar `PlanSetup`/`Structure`/`Course` reales en
un entorno de desarrollo normal. Antes de decir "no se puede testear", separar:

1. **¿La lógica del cambio es matemática/de datos pura** (compara números, filtra una lista, arma
   un string) **y solo la FIRMA del método toca tipos de ESAPI?** → sí se puede testear, aislando
   con el patrón de stub. Ver `references/testing-sin-eclipse.md` (proyectos `Tests/StubEsapi`,
   `Tests/GenerarPlantillasReales`, `Tests/TestCondicionPlanSuma`, patrón `TestMejoras`).
2. **¿La lógica depende de datos reales devueltos por ESAPI en runtime** (un `GetDVHCumulativeData`
   real, aprobación de plan, geometría real de haces)? → no es testeable fuera de Eclipse. Decirlo
   explícitamente en `Tests.md` como pendiente de verificación en vivo — nunca simular que se probó
   ni inventar un resultado.

La convención de este proyecto (ver `CLAUDE.md`) es: aislar la lógica pura afectada y agregar el
test en un proyecto standalone bajo `Tests/` (`dotnet run`), documentando en `Tests.md` fecha, qué
cambió, cómo se testeó, números usados y resultado — antes/después, no solo "funciona".

## 6. Object model, coordenadas, threading (fundamentos oficiales de ESAPI)

Antes de escribir código nuevo contra un tipo de ESAPI que no se usó todavía en el proyecto
(`Course`, `OptimizationSetup`, `Beam`/`ControlPoint`, brachy, etc.), revisar
`references/object-model-y-fundamentos.md` — resume la jerarquía real `Patient → Course →
PlanSetup/PlanSum → Beam`, unidades (mm/DICOM, no cm/planificación como en la UI), que `DoseValue`
puede venir en `%` (relevante para la ambigüedad de prescripción en OARs, ver §7), el requisito de
thread STA para `Application`, y la diferencia de compilación entre plug-in de un solo archivo vs.
binario/standalone ante un miembro `[Obsolete]`.

## 7. Gotchas transversales (afectan cualquier código que toque ESAPI)

Ver `references/gotchas-esapi.md` para el detalle y el porqué de cada uno; resumen:

- **Decimales**: nunca `double.Parse`/`TryParse` sin `NumberStyles`/cultura explícitos — depende del
  locale de Windows del server. Ver `Metodos.validarYConvertirADouble`.
- **`Configuracion.*()`** (ej. `volDosisMaxima()`) lee `Properties.Settings.Default` en vivo — no
  cachear en un campo `static`, el usuario puede cambiar la config sin reiniciar la app.
- **`DoseValue`/DVH con arrays vacíos**: `Max()`/`Min()` sobre curva vacía tira excepción en vez de
  reportar dato faltante si no hay guard — ver el patrón ya aplicado en
  `DVHDataExtensions_ESAPIX.cs`.
- **Dispose de objetos ESAPI**: cualquier flujo que abre `Application` debe garantizar
  `ClosePatient()`/`Dispose()` incluso si hay una excepción en el medio (try/finally o `using`).

## Cuándo pedir ayuda al usuario en vez de asumir

- Si falta una DLL de `lib/ESAPI/<version>/` para compilar o testear una versión → pedirla, no
  simular el resultado de compilar.
- Si un cambio de comportamiento depende de decidir "qué versión de Eclipse es la fuente de verdad"
  ante un breaking change sin equivalente común → preguntar, no elegir en silencio cuál versión
  queda "peor soportada".
