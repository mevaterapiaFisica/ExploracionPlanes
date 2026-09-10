using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace ExploracionPlanes
{
    public enum Tipo
    {
        VolPTV,
        NumFx,
        CondicionadaPor,
        CondicionaA,
        SinCondicion,
    };
    public enum Operador
    {
        menor_a,
        mayor_a,
        entre,
        igual_a,
    };
    public class Condicion
    {
        public Tipo tipo { get; set; }
        public Operador operador { get; set; }
        public double ValorEsperado { get; set; }
        public double ValorEsperado2 { get; set; }
        public string EtiquetaRestriccionAnidada { get; set; }
        public string id { get; set; }

        public double ValorObtenido(PlanningItem planActual, double volPTV = 0)
        {
            if (tipo == Tipo.VolPTV)
            {
                return volPTV;
            }
            else if (tipo == Tipo.NumFx)
            {
                return Convert.ToDouble(((PlanSetup)planActual).UniqueFractionation.NumberOfFractions);
            }
            else //dejo abierto para agregar otros tipos
            {
                return Double.NaN;
            }
        }

        public bool CumpleCondicion(PlanningItem planActual, double volPTV = 0)
        {
            if (tipo == Tipo.SinCondicion ||  tipo == Tipo.CondicionaA || tipo == Tipo.CondicionadaPor)
            {
                return true;
            }
            if (operador==Operador.mayor_a)
            {
                return (ValorObtenido(planActual,volPTV) >= ValorEsperado);
            }
            else if (operador==Operador.menor_a)
            {
                return (ValorObtenido(planActual, volPTV) <= ValorEsperado);
            }
            else if (operador==Operador.entre)
            {
                return (ValorObtenido(planActual, volPTV) <= Math.Max(ValorEsperado,ValorEsperado2) && ValorObtenido(planActual, volPTV) >= Math.Min(ValorEsperado, ValorEsperado2));
            }
            else //(operador == Operador.igual)
            {
                return (ValorObtenido(planActual, volPTV) == ValorEsperado);
            }
        }

        public void escribirId()
        {
            if (tipo == Tipo.SinCondicion)
            {
                id = "";
            }
            else if (tipo == Tipo.CondicionaA || tipo == Tipo.CondicionadaPor)
            {
                id = tipo.ToString() + "=" + EtiquetaRestriccionAnidada;
            }
            else if (operador == Operador.entre)
            {
                id = (Math.Min(ValorEsperado, ValorEsperado2)).ToString() + "<" + tipo.ToString() + "<" + (Math.Max(ValorEsperado, ValorEsperado2)).ToString();
            }
            else if (operador == Operador.menor_a)
            {
                id = tipo.ToString() + "<" + ValorEsperado.ToString();
            }
            else if (operador == Operador.mayor_a)
            {
                id = tipo.ToString() + ">" + ValorEsperado.ToString();
            }
            else
            {
                id = tipo.ToString() + "=" + ValorEsperado.ToString();
            }
        }

        public static Condicion crear(Tipo _tipo, Operador _operador, double _valorEsperado, double _valorEsperado2= double.NaN, string _idRestriccionAnidada="")
        {
            Condicion condicion = new Condicion
            {
                tipo = _tipo,
                operador = _operador,
                ValorEsperado = _valorEsperado,
                ValorEsperado2 = _valorEsperado2,
                EtiquetaRestriccionAnidada = _idRestriccionAnidada,
            };
            condicion.escribirId();
            return condicion;
        }
    }
}
