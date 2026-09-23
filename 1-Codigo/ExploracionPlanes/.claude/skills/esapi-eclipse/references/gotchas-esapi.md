# Gotchas transversales al trabajar con ESAPI en este proyecto

## Decimales y cultura

Nunca usar `double.Parse`/`double.TryParse` sin `NumberStyles` y `CultureInfo` explícitos. El
default de .NET permite separador de miles y depende del locale de Windows del server donde corre
Eclipse — bajo es-AR puede confundir "." con ",". `Program.cs`/`Script.cs` fuerzan
`NumberDecimalSeparator="."` en el hilo antes de todo por esto mismo. Ver
`Metodos.validarYConvertirADouble` y `Estructura.AlfaBeta` como referencia de cómo parsear bien.

## Configuración leída en vivo

`Configuracion.*()` (ej. `volDosisMaxima()`) lee `Properties.Settings.Default` en el momento de la
llamada. No cachear su valor en un campo `static` ni en una variable de larga vida — el usuario
puede cambiar la ruta de red o el volumen de Dmax desde `FormConfiguracion` sin reiniciar la app, y
un valor cacheado queda obsoleto silenciosamente (ya pasó con `RestriccionDosisMax`, ver
`Tests.md`).

## `Application` / ciclo de vida de la conexión

- `Application.CreateApplication(null, null)` es login interactivo (sin credenciales embebidas) —
  usar en todos los puntos de entrada, no hardcodear usuario/password.
- Garantizar `ClosePatient()` seguido de `Dispose()` incluso si hay una excepción en el medio
  (`try/finally` o `using`). Sin `ClosePatient()` antes de `Dispose()`, el paciente puede quedar
  lockeado en ARIA para otros usuarios/sesiones.
- Un objeto ESAPI (`PlanSetup`, `Structure`, `StructureSet`) vive solo mientras la `Application` que
  lo originó sigue abierta — no guardarlos en un campo que sobreviva al cierre de esa `Application`
  y reusarlos después.

## DVH: arrays vacíos y tramos planos

- `Max()`/`Min()` de LINQ sobre una curva DVH vacía (`DVHPoint[]` de longitud 0) tira
  `InvalidOperationException` en vez de reportar el dato como faltante. El patrón del proyecto es
  chequear `dvh == null || dvh.Length == 0` primero y devolver `double.NaN`/`DoseValue.UndefinedDose()`
  — ver `DVHDataExtensions_ESAPIX.GetVolumeAtDose`/`GetDoseAtVolume`.
- La interpolación lineal (`interpolar1D`) divide por `(x2 - x1)` — en un tramo plano de la curva
  (`x1 == x2`) eso da NaN/Infinity sin guard. El fix ya aplicado devuelve `z1` directo cuando
  `x1 == x2` (la meseta ya tiene el mismo valor en ambos extremos).
- `PlanSum` no expone `GetDoseAtVolume()`/`GetVolumeAtDose()` — es limitación real de ESAPI, no un
  bug a "arreglar". Por eso existe `DVHDataExtensions_ESAPIX`, que interpola manualmente sobre el
  `DVHPoint[]` crudo. No asumir que un método de ESAPI que funciona en `PlanSetup` también existe en
  `PlanSum` sin chequear el intellisense/la DLL real primero.

## Cachear resultados de ESAPI: sí, pero con la clave correcta

`CacheDVH` cachea por `(PlanningItem, Structure, VolumePresentation)` — `DoseValuePresentation` y
`binWidth` no entran en la clave porque todo el código de producción los usa fijos
(`Absolute`/`0.01`). Si algún día un caller necesita variar esos dos parámetros, hay que agregarlos
a la clave o el cache va a devolver un DVH calculado con otros parámetros sin avisar. Llamar
`CacheDVH.Limpiar()` al arrancar cada análisis nuevo — si no, se puede arrastrar el DVH de un
plan/paciente anterior entre análisis sucesivos en la misma sesión de la app.

## Prescripción / fraccionamiento: usar siempre el shim

`PlanSetup.UniqueFractionation` (con `NumberOfFractions`/`PrescribedDosePerFraction`/
`DosePerFractionInPrimaryRefPoint`) solo existe en Eclipse 13.6; en 15.6/18.2 esas 3 propiedades
están aplanadas directo en `PlanSetup` y `UniqueFractionation` no existe. Llamar siempre
`plan.NumeroFracciones()` / `plan.DosisPrescriptaPorFraccion()` /
`plan.DosisPorFraccionEnPuntoRefPrimario()` (extensiones en `EsapiCompat.cs`) — nunca acceder a
`UniqueFractionation` ni a la propiedad aplanada directo desde código nuevo, aunque compile en la
versión que se esté probando en el momento (puede no compilar en las otras 2).

En Eclipse 18.2 las propiedades aplanadas ya están marcadas `[Obsolete]` (sugieren
`DosePerFraction`/`PlannedDosePerFraction`) pero siguen andando. Si en una versión futura Varian las
saca de verdad, el error de compilación (o el warning `CS0618` volviéndose error) va a aparecer solo
en la rama `#else` de `EsapiCompat.cs` — actualizar ahí, no en cada call site.

## `PlanSum` vs. dos etapas de tratamiento vs. dos lesiones simultáneas

El código hoy no distingue si un `PlanSum` representa "dos etapas del mismo tratamiento" (dosis
total = suma de ambas, el 100% de referencia de prescripción es la suma) o "dos planes simultáneos
en zonas distintas" (el 100% de referencia para un OAR es la dosis más alta de las dos, no la suma).
Esto es ambigüedad de negocio, no bug de código — si un cambio futuro necesita resolverlo (ver
memoria de proyecto sobre "prescripción solo a PTV/CTV"), preguntar al usuario cómo distinguir los
dos casos en vez de asumir uno.

## Restricciones (`IRestriccion`)

Las 6 implementaciones (`RestriccionDosis`, `RestriccionDosisMax`, `RestriccionDosisMedia`,
`RestriccionVolumen`, `RestriccionVolumenCritico`, `RestriccionIndiceConformidad`) heredan de
`RestriccionBase`, que centraliza evaluación de tolerancia, sampling coverage y edición en grupo.
Cada subclase solo define `crearEtiquetaInicio()`, `analizarPlanEstructura()` y `crear()`. Antes de
agregar lógica nueva, chequear si aplica a las 6 (va en `RestriccionBase`) o es específica de una
(va en la subclase) — no duplicar entre subclases lo que podría vivir en la base.

Excepción a tener en cuenta: `RestriccionIndiceConformidad.analizarPlanEstructura` pide el DVH de la
estructura BODY (no de la `estructura` de la fila) — un cache o lógica que asuma "la estructura de
la restricción" puede no aplicar ahí sin ajuste.
