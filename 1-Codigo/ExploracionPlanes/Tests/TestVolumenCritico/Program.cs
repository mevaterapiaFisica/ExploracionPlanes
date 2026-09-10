// Test aislado, sin ESAPI, de la lógica pura de RestriccionVolumenCritico.cs:
//   V_sano = V_total - V(D)   (cm3, o directamente 100 - V(D)% cuando la unidad es %)
// y de cumple() (mismo criterio que RestriccionVolumen, pero evaluado sobre V_sano).

int ok = 0, fail = 0;

void Check(string nombre, bool condicion)
{
    if (condicion) { Console.WriteLine("OK   " + nombre); ok++; }
    else { Console.WriteLine("FAIL " + nombre); fail++; }
}

double VolumenSanoAbsoluto(double volumenTotal, double volumenADosis) => volumenTotal - volumenADosis;
double VolumenSanoPorcentaje(double volumenADosisPorcentaje) => 100 - volumenADosisPorcentaje;

int Cumple(bool esMenorQue, double valorMedido, double valorEsperado, double valorTolerado)
{
    if (esMenorQue)
    {
        if (valorMedido <= valorEsperado) return 0;
        if (!double.IsNaN(valorTolerado) && valorMedido <= valorTolerado) return 1;
        return 2;
    }
    else
    {
        if (valorMedido >= valorEsperado) return 0;
        if (!double.IsNaN(valorTolerado) && valorMedido >= valorTolerado) return 1;
        return 2;
    }
}

Console.WriteLine("=== V_sano en cm3, V_total=50 ===");
{
    double vTotal = 50;
    double vSano1 = VolumenSanoAbsoluto(vTotal, 15); // V(D)=15
    Check("V(D)=15cm3 -> Vsano=35cm3", vSano1 == 35);
    Check("Constraint Vsano>30, sin tolerancia -> cumple (0)", Cumple(false, vSano1, 30, double.NaN) == 0);

    double vSano2 = VolumenSanoAbsoluto(vTotal, 28); // V(D)=28
    Check("V(D)=28cm3 -> Vsano=22cm3", vSano2 == 22);
    Check("Constraint Vsano>30 (tolerado 20) -> tolerado (1)", Cumple(false, vSano2, 30, 20) == 1);

    double vSano3 = VolumenSanoAbsoluto(vTotal, 35); // V(D)=35
    Check("V(D)=35cm3 -> Vsano=15cm3", vSano3 == 15);
    Check("Constraint Vsano>30 (tolerado 20) -> no cumple (2)", Cumple(false, vSano3, 30, 20) == 2);
}

Console.WriteLine("=== V_sano en % ===");
{
    double vSano1 = VolumenSanoPorcentaje(40); // V(D)=40%
    Check("V(D)=40% -> Vsano=60%", vSano1 == 60);
    Check("Constraint Vsano>50%, sin tolerancia -> cumple (0)", Cumple(false, vSano1, 50, double.NaN) == 0);

    double vSano2 = VolumenSanoPorcentaje(70); // V(D)=70%
    Check("V(D)=70% -> Vsano=30%", vSano2 == 30);
    Check("Constraint Vsano>50% (tolerado 25%) -> tolerado (1)", Cumple(false, vSano2, 50, 25) == 1);

    double vSano3 = VolumenSanoPorcentaje(80); // V(D)=80%
    Check("V(D)=80% -> Vsano=20%", vSano3 == 20);
    Check("Constraint Vsano>50% (tolerado 25%) -> no cumple (2)", Cumple(false, vSano3, 50, 25) == 2);
}

Console.WriteLine("=== esMenorQue=true (constraint invertido) ===");
{
    double vSano = VolumenSanoAbsoluto(50, 15); // 35
    Check("Vsano=35, constraint Vsano<40 -> cumple (0)", Cumple(true, vSano, 40, double.NaN) == 0);
}

// Réplica del dispatch por índice de CB_TipoRestriccion en Form1_prioridades.xaml.cs / Form1_ext.cs
// tras agregar "Volumen crítico" al final (índice 5): confirma que no se corrió el índice de
// IndiceConformidad (4, sin cambios) y que 5 mapea al tipo nuevo.
string TipoPorIndice(int indiceSeleccionado)
{
    if (indiceSeleccionado == 0) return "Dosis";
    if (indiceSeleccionado == 1) return "Dmedia";
    if (indiceSeleccionado == 2) return "Dmax";
    if (indiceSeleccionado == 3) return "Volumen";
    if (indiceSeleccionado == 5) return "VolumenCritico";
    return "IndiceConformidad"; // else final, igual que en el código real
}

Console.WriteLine("=== Dispatch por índice de CB_TipoRestriccion (post-integración) ===");
{
    Check("Índice 3 sigue mapeando a Volumen (sin regresión)", TipoPorIndice(3) == "Volumen");
    Check("Índice 4 sigue mapeando a IndiceConformidad (sin regresión)", TipoPorIndice(4) == "IndiceConformidad");
    Check("Índice 5 (nuevo, al final del combo) mapea a VolumenCritico", TipoPorIndice(5) == "VolumenCritico");
}

Console.WriteLine();
Console.WriteLine(fail == 0 ? "TODOS LOS CHEQUEOS OK" : $"{fail} CHEQUEOS FALLARON");
