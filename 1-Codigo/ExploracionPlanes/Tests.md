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

## Convención para tests futuros

A partir de este cambio, todo cambio sobre código funcional debe incluir un test que compare comportamiento antes/después, documentado como una entrada nueva en este archivo (fecha, qué se cambió, cómo se testeó, números usados, resultado). Si el código depende de ESAPI y no se puede instanciar fuera de Eclipse, aislar la lógica pura afectada (como se hizo en `Tests/TestEQD2/`) en vez de omitir el test.
