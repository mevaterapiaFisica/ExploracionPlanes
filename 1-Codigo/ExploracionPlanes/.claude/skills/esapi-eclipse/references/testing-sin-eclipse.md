# Testear código que usa ESAPI sin tener Eclipse instalado

ESAPI (Varian) no corre fuera de Eclipse: no se puede instanciar `PlanSetup`/`Structure`/`Course`/
`Application` reales en un entorno de desarrollo normal. Eso NO significa que el código que toca
ESAPI sea siempre intestable — la clave es separar qué parte del cambio es lógica pura y qué parte
depende de datos reales devueltos por el servidor.

## Paso 1: clasificar el cambio

- **Lógica pura con tipos de ESAPI solo en la firma** (compara números, filtra/ordena una lista,
  arma un string, decide qué fila mostrar) → testeable. El método puede recibir un `PlanSetup`/
  `Structure` como parámetro sin necesitar que ESAPI esté instalado, siempre que el *cuerpo* del
  método no llame a algo que solo ESAPI real puede responder.
- **Depende de un valor que solo el servidor de Eclipse puede calcular** (un `DVHData` real vía
  `GetDVHCumulativeData`, aprobación de plan, geometría real de haces, `Application.CreateApplication`
  en sí) → no testeable fuera de Eclipse. Documentarlo en `Tests.md` como pendiente de verificación
  en vivo — nunca simular que se probó ni inventar un resultado esperado.

Muchos cambios son una mezcla: la lógica de decisión es pura, pero está en un método cuya firma
tiene un parámetro `PlanSetup`. En ese caso, sí se puede testear con el patrón de stub de abajo —
solo hace falta que el *cuerpo* de las funciones invocadas en el test no dependa de una llamada
real a ESAPI.

## Paso 2: el patrón de stub (`Tests/StubEsapi`)

`Tests/StubEsapi/Stub.cs` reimplementa el subconjunto mínimo de `VMS.TPS.Common.Model.API`/`.Types`
que el código de producción referencia en FIRMAS de método (`Structure`, `PlanSetup`, `PlanSum`,
`DVHData`, `DoseValue`, etc.) — sin lógica real, los cuerpos de métodos que "tocan ESAPI de verdad"
(`GetDVHCumulativeData`, `GetDoseAtVolume`, ...) tiran `NotImplementedException` a propósito, porque
no deberían ejecutarse durante el test.

La idea central: **compilar el archivo de producción REAL (`.cs` tal cual, vía `<Compile Include>`
apuntando al repo) contra el stub**, en vez de reimplementar o copiar la lógica en el proyecto de
test. Así el test verifica el código que de verdad se va a shippear, no una reescritura paralela que
puede divergir con el tiempo.

Estructura típica de un proyecto de test nuevo (ver `Tests/TestCondicionPlanSuma/` o
`Tests/GenerarPlantillasReales/` como referencia concreta):

1. Proyecto de consola nuevo bajo `Tests/<NombreTest>/`, `net9.0-windows` (o el TFM que ya usan los
   demás), sin referencia a las DLL reales de ESAPI.
2. `<Compile Include>` apuntando a los `.cs` de producción reales que hace falta compilar (ej.
   `Condicion.cs`, `Estructura.cs`, lo que toque la lógica bajo test) — nunca copiar/pegar el
   contenido.
3. Agregar también `Tests/StubEsapi/Stub.cs` (y `EsapiCompat.cs` si el archivo bajo test lo usa) a
   la lista de `<Compile>`.
4. `Program.cs` del test: arma instancias del stub (`new PlanSetup { ... }`, `new PlanSum { ... }`),
   llama al método real de producción, y compara el resultado viejo vs. nuevo (o el esperado)
   imprimiendo un resumen pass/fail — sin frameworks de test, patrón consola simple + `dotnet run`
   (ver `Tests/TestMejoras/Program.cs` como ejemplo consolidado del estilo del proyecto).

## Al actualizar el stub

Si se agrega una propiedad/método nuevo de ESAPI a un archivo de producción y el stub no la tiene,
el build del test falla con `CS1061` — agregar la propiedad mínima al stub (mismo nombre y forma que
la real, cuerpo simple o `NotImplementedException` si nunca debería ejecutarse en test). Si el
cambio es un breaking change entre versiones de Eclipse resuelto vía `EsapiCompat.cs`
(`#if ECLIPSE1x_x`), el stub debe exponer la forma que toma la rama `#else`/default (los tests no
definen ninguna constante `ECLIPSE1x_x`) — ver `migrar-version-eclipse.md` paso 5.

## Ejemplos reales en este repo (para copiar el patrón, no reinventar)

- `Tests/GenerarPlantillasReales/`: compila `DesdeCSV.GenerarPlantillasUnificadas` real contra el
  stub — la función en sí nunca llama a ESAPI en runtime (solo arma datos y serializa), pero su
  firma toca tipos de ESAPI, así que sin el stub ni siquiera compila fuera de Eclipse.
- `Tests/TestCondicionPlanSuma/`: crea un `PlanSetup` con 5 fx y un `PlanSum` que lo contiene, prueba
  `Condicion.crear(Tipo.NumFx, Operador.igual_a, 5)` — reproduce el bug/fix sin instanciar nada real.
- `Tests/TestOwnerVentanaNoMostrada/`: caso sin ESAPI en absoluto (WPF puro), mismo espíritu —
  compila `DialogoWpf.cs` de producción tal cual contra una ventana de prueba, reproduce el orden
  exacto de un bug real (`Cannot set Owner property to a Window that has not been shown previously`).
- `Tests/TestMejoras/Program.cs`: convención general del proyecto para lógica pura sin ESAPI en
  absoluto — varias secciones acumuladas, cada una prueba un fix puntual con datos inventados.

## Qué documentar en `Tests.md`

Para cada test: fecha, qué cambió, cómo se testeó (qué proyecto de `Tests/`, qué datos), resultado
antes/después con números concretos, y — si parte del cambio no se pudo testear por depender de
ESAPI real — decirlo explícito como pendiente de verificación en Eclipse real, no omitirlo.
