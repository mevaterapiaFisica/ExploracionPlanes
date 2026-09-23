# Fundamentos de ESAPI (Eclipse Scripting API Reference Guide, 13.6)

Resumen de la doc oficial de Varian (`P1008614-003-C`), lo que aplica a este proyecto. No repite lo
que ya está en `SKILL.md`/otros references — solo agrega contexto de la API en sí.

## Object model: jerarquía real

```
Patient
├── Study → Series → Image
├── StructureSet → Structure
├── Registration
└── Course
    ├── PlanSetup (ExternalPlanSetup | BrachyPlanSetup) : PlanningItem
    │   ├── Beam[]  (MLC, ControlPoint[], Applicator, Compensator, Block[], Wedge[],
    │   │            FieldReferencePoint[], ExternalBeamTreatmentUnit) — BeamDose opcional
    │   ├── StructureSet, Fractionation, EstimatedDVH  (todos nullable)
    │   ├── OptimizationSetup → OptimizationParameter[] / OptimizationObjective[]
    │   └── PlanningItemDose (Dose) opcional
    └── PlanSum : PlanningItem
        ├── Course, PlanSumComponents[]  (13.6+)
        └── GetPlanSumOperation() / GetPlanWeight() (13.6+)
```

`PlanSetup` y `PlanSum` heredan de `PlanningItem` — por eso `CacheDVH.Obtener` (`Estructura.cs`,
`CacheDVH.cs`) puede tomar cualquiera de los dos como parámetro sin distinguir tipo.

## `PlanSum`: cómo resolver la ambigüedad etapas-vs-zonas (dato nuevo, no estaba en el código)

`gotchas-esapi.md` documenta que el código hoy no distingue si un `PlanSum` es "dos etapas del
mismo tratamiento" o "dos planes simultáneos en zonas distintas" — quedaba como pregunta abierta al
usuario. La API 13.6 sí expone información que podría resolverlo sin preguntar:

- `PlanSum.PlanSumComponents` (colección, agregada en 13.6): da acceso a los planes componentes del
  suma.
- `PlanSum.GetPlanWeight(planComponente)`: devuelve el peso con que cada plan entra al suma.
- `PlanSum.GetPlanSumOperation()`: devuelve la operación de combinación usada (suma ponderada, etc).

Estas 3 solo existen en Eclipse 13.6+ (agregadas en esa versión según el changelog de la doc) — en
13.6 sí están, confirmar disponibilidad real si algún día se soporta una versión más vieja. Si se
retoma la mejora de "prescripción solo PTV/CTV" (ver memoria de proyecto), investigar primero si
`GetPlanWeight`/`PlanSumComponents` alcanza para inferir el caso automáticamente antes de pedirle al
usuario que lo indique a mano.

## Coordenadas y unidades

- Todo método/propiedad de posición/distancia usa **milímetros** y el **sistema DICOM** — distinto
  de la UI de Eclipse, que muestra centímetros en el sistema de coordenadas de planificación (tiene
  en cuenta origen de usuario de la imagen, orientación del tratamiento, y definición de ejes). Si
  se compara un valor leído por ESAPI contra lo que un físico ve en pantalla, convertir con cuidado
  — no asumir mismo sistema ni misma unidad.
- Ángulos de unidad de tratamiento/accesorios: siempre en escala **IEC61217**, independiente del
  fabricante.

## `DoseValue`: unidad puede ser porcentaje

`DoseValue` (`VMS.TPS.Common.Model.Types`) siempre trae valor + unidad (`Gy`, `cGy`, o **`Percent`**
si la dosis es relativa). Esto es justamente la raíz de la memoria de proyecto sobre "prescripción
solo a PTV/CTV" (`RestriccionDosisMax` con `unidadValor="%"` en OARs) — cualquier código nuevo que
lea un `DoseValue` debe chequear `.Unit` antes de asumir que el número es absoluto.

## Conectar: firma real y threading

```csharp
using (Application app = Application.CreateApplication(userId, password))
{
    // userId/password null => login interactivo (patrón ya usado en todo el proyecto)
}
```

- `CreateApplication` también inicializa la API — no hay paso previo de "init" separado.
- **Debe ejecutarse en un thread STA** (`[STAThread]` en `Main`) — la API solo se puede acceder desde
  un único thread en el default application domain. Si algún día se agrega paralelismo (ej. batch de
  `Form3`/`Mineria` en varios threads), esto es una restricción dura de la API, no una decisión de
  diseño del proyecto — no paralelizar llamadas a ESAPI entre threads.
- `Application` implementa `IDisposable` — de ahí el patrón `using`/`try-finally` con `Dispose()`
  descrito en `SKILL.md` §2.

## Scripts: plug-in vs standalone (contexto para `Script.cs`/`Program.cs`)

- **Plug-in**: recibe `ScriptContext` con paciente/plan/imagen activos en la Eclipse abierta —
  limitado a un paciente a la vez, hereda los permisos con que el usuario ya inició sesión en
  Eclipse. Extensión requerida para plug-in binario: `.esapi.dll` (por eso el output de este
  proyecto se llama `ExploracionPlanes.esapi.dll` — Eclipse no lo reconoce con otro sufijo).
- **Standalone**: puede escanear la base y abrir cualquier paciente, pero solo un paciente a la vez
  — hay que cerrar explícitamente el actual (`ClosePatient()`) antes de abrir otro, si no salta
  excepción de acceso a datos ya cerrados.
- Un **plug-in de un solo archivo** (`.cs` suelto) lo compila Eclipse al vuelo cada vez que se corre
  — si el código usa un miembro `[Obsolete]` de ESAPI, ese warning hace **fallar la compilación** de
  este tipo de plug-in (no solo avisa). En binary plug-in o standalone, un `[Obsolete]` es solo
  warning salvo que el proyecto tenga "Treat warnings as errors" activado. Relevante para el shim de
  `EsapiCompat.cs`: las propiedades de fraccionamiento aplanadas ya están `[Obsolete]` en 18.2 (ver
  `gotchas-esapi.md`) — si este proyecto alguna vez se empaquetara como single-file plugin en vez de
  binario, ese warning rompería el build; hoy no aplica porque `ExploracionPlanes` es siempre binary
  plug-in + standalone.

## Compatibilidad entre versiones (histórico oficial, más allá del fraccionamiento ya resuelto)

La doc documenta breaking changes previos de Varian entre 11.0→13.0 con el mismo patrón que
`EsapiCompat.cs` ya resuelve para 13.6→15.6/18.2 (tipo vuelto inmutable, miembro marcado obsoleto y
reemplazado). Sirve como precedente: Varian no garantiza compatibilidad hacia adelante entre
versiones de ESAPI — cualquier versión nueva que se agregue (ver `migrar-version-eclipse.md`) puede
traer un cambio de este estilo, no son casos aislados.
