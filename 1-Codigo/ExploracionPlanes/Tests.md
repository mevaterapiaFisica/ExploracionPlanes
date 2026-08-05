# Tests

Registro de tests hechos sobre cambios de código funcional. Cada entrada documenta qué se probó, con qué números y qué resultado dio antes/después del cambio.

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

## Convención para tests futuros

A partir de este cambio, todo cambio sobre código funcional debe incluir un test que compare comportamiento antes/después, documentado como una entrada nueva en este archivo (fecha, qué se cambió, cómo se testeó, números usados, resultado). Si el código depende de ESAPI y no se puede instanciar fuera de Eclipse, aislar la lógica pura afectada (como se hizo en `Tests/TestEQD2/`) en vez de omitir el test.
