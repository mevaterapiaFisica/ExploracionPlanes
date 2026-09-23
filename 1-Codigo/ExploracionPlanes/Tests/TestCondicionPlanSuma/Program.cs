// Reproduce el bug real (no una copia de la lógica): Condicion.ValorObtenido/CumpleCondicion con
// tipo NumFx (restricciones de OAR que dependen del número de fracciones, ej. plantillas SBRT)
// casteaba el plan directo a PlanSetup. Al analizar un PlanSum (curso con dos planes sumados)
// tira InvalidCastException apenas se llega a una restricción con esa condición, mientras que
// las de PTV/CTV (SinCondicion) no la disparan - por eso el usuario veía el error solo en OARs.
using System;
using ExploracionPlanes;
using VMS.TPS.Common.Model.API;

int fallas = 0;
void Assert(string nombre, bool ok)
{
    Console.WriteLine((ok ? "OK   " : "FAIL ") + nombre);
    if (!ok) fallas++;
}

PlanSetup planSetup = new PlanSetup();
planSetup.UniqueFractionation.NumberOfFractions = 5;

PlanSum planSuma = new PlanSum();
planSuma.PlanSetups.Add(planSetup);

Condicion condicionNumFx = Condicion.crear(Tipo.NumFx, Operador.igual_a, 5);

// Antes del fix: esto tiraba InvalidCastException ("Unable to cast object of type 'PlanSum' to 'PlanSetup'")
try
{
    bool cumplePlanSetup = condicionNumFx.CumpleCondicion(planSetup);
    bool cumplePlanSuma = condicionNumFx.CumpleCondicion(planSuma);
    Assert("CumpleCondicion con PlanSetup (5 fx == 5)", cumplePlanSetup);
    Assert("CumpleCondicion con PlanSum no tira InvalidCastException", true);
    Assert("CumpleCondicion con PlanSum toma las fracciones del PlanSetup interno (5 fx == 5)", cumplePlanSuma);
}
catch (InvalidCastException)
{
    Assert("CumpleCondicion con PlanSum no tira InvalidCastException", false);
}

Console.WriteLine();
Console.WriteLine(fallas == 0 ? "TODOS LOS CHEQUEOS OK" : $"{fallas} CHEQUEO(S) FALLARON");
Environment.Exit(fallas == 0 ? 0 : 1);
