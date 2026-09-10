using System;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VMS.TPS.Common.Model.API;

namespace ExploracionPlanes
{
    public class Estructura
    {
        public string nombre { get; set; }
        public List<string> nombresPosibles { get; set; }

        public static Estructura crear(string _nombre, List<string> _nombresAlt)
        {
            List<string> _nombresPosibles = _nombresAlt;
            _nombresPosibles.Insert(0, _nombre);
            return new Estructura()
            {
                nombre = _nombre,
                nombresPosibles = _nombresPosibles,
            };
        }
        public static string asociarExactoID(string nombreEstructura, List<string> listaEstructurasID)
        {
            return listaEstructurasID.Where(c => c.ToLower().Equals(nombreEstructura.ToLower())).FirstOrDefault();
        }

        public static Structure asociarConLista(List<string> listaNombres, List<Structure> listaEstructura)
        {
            foreach (string nombre in listaNombres)
            {
                string estructuraID = asociarExactoID(nombre, listaEstructurasID(listaEstructura));
                if (!string.IsNullOrEmpty(estructuraID))
                {
                    return listaEstructura.Where(c => c.Id.Equals(estructuraID)).FirstOrDefault();
                }
            }
            return null;
        }

        // Distancia máxima de edición para sugerir/autoseleccionar un matcheo aproximado. Subida de
        // 3 a 4 junto con el peso de sustitución de abajo, para mantener aprox. la misma generosidad
        // de auto-match que antes (2 sustituciones ya no entraban en 3 con el nuevo peso).
        public const int DistanciaMaximaSugerida = 4;

        // Una sustitución (letra por otra) pesa más que una inserción/borrado: un nombre que es el
        // slot buscado MÁS caracteres agregados (p.ej. "PTV" -> "PTV_1mm") debe ordenar mejor que uno
        // de la misma longitud pero con letras distintas (p.ej. "PTV" -> "Skin"), aunque la distancia
        // sin pesar diera igual. Pedido por el usuario: priorizar adiciones sobre reemplazos.
        public const int CostoSustitucion = 2;
        public const int CostoInsercionOBorrado = 1;

        public static int DistanciaDamerauLevenshtein(string a, string b)
        {
            a = (a ?? "").ToLowerInvariant();
            b = (b ?? "").ToLowerInvariant();
            int[,] d = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) d[i, 0] = i * CostoInsercionOBorrado;
            for (int j = 0; j <= b.Length; j++) d[0, j] = j * CostoInsercionOBorrado;
            for (int i = 1; i <= a.Length; i++)
            {
                for (int j = 1; j <= b.Length; j++)
                {
                    int costoSustitucion = a[i - 1] == b[j - 1] ? 0 : CostoSustitucion;
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + CostoInsercionOBorrado, d[i, j - 1] + CostoInsercionOBorrado), d[i - 1, j - 1] + costoSustitucion);
                    if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                    {
                        // La transposición (típicamente un typo de tipeo, "5400"/"5040") se mantiene
                        // barata como el borrado/inserción, no tan cara como una sustitución real.
                        d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + CostoInsercionOBorrado);
                    }
                }
            }
            return d[a.Length, b.Length];
        }

        // Sufijos clínicos típicos (margen "_PRV", tamaño "_1mm", numeración "PTV05"/"PTV_2") que no
        // cambian de qué órgano/volumen se trata. Pedido del usuario: "PTV" debe reconocer "PTV05" o
        // "PTV_1mm" como si fueran el mismo nombre, y "Bladder" reconocer "Bladder_PRV2" igual.
        private static readonly System.Text.RegularExpressions.Regex sufijosClinicos =
            new System.Text.RegularExpressions.Regex(@"prv|mm|\d+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        public static string nucleoNombreClinico(string nombre)
        {
            return sufijosClinicos.Replace(nombre ?? "", "").Replace("_", "").Trim();
        }

        // Estructuras del plan ordenadas de más a menos parecida a alguno de los nombresPosibles (menor distancia primero).
        // Se ordena primero por el "núcleo" del nombre (sin sufijos clínicos): un candidato con el
        // mismo núcleo (p.ej. "PTV" y "PTV_1mm") va antes que uno de núcleo distinto aunque letra por
        // letra esté más cerca (p.ej. "GTV"). La distancia completa solo desempata entre candidatos
        // de igual núcleo.
        public static List<Tuple<Structure, int>> candidatosPorDistancia(List<string> listaNombres, List<Structure> listaEstructura)
        {
            return listaEstructura
                .Select(s => new
                {
                    Estructura = s,
                    DistanciaNucleo = listaNombres.Min(n => DistanciaDamerauLevenshtein(nucleoNombreClinico(n), nucleoNombreClinico(s.Id))),
                    DistanciaCompleta = listaNombres.Min(n => DistanciaDamerauLevenshtein(n, s.Id))
                })
                .OrderBy(x => x.DistanciaNucleo)
                .ThenBy(x => x.DistanciaCompleta)
                .Select(x => new Tuple<Structure, int>(x.Estructura, x.DistanciaNucleo))
                .ToList();
        }

        public static List<Structure> listaEstructuras(PlanningItem plan) //CHEQUEAR FILTRAR POR TIPO
        {
            List<Structure> sinFiltrar = new List<Structure>();
            List<Structure> filtradas = new List<Structure>();

            if (plan is PlanSetup)
            {
                sinFiltrar = ((PlanSetup)plan).StructureSet.Structures.ToList();
            }
            else //(plan.GetType() == typeof(PlanSum))
            {
                sinFiltrar = ((PlanSum)plan).StructureSet.Structures.ToList();
            }
            foreach (Structure estructura in sinFiltrar)
            {
                if (estructura.DicomType != "SUPPORT" && !estructura.IsEmpty)
                {
                    filtradas.Add(estructura);
                }
            }
            return filtradas;
        }

        public static List<string> listaEstructurasID(List<Structure> lista)
        {
            List<string> listaS = lista.Select(e => e.Id).ToList<string>();
            listaS.Add("");
            return listaS;
        }

        private static Dictionary<string, string> _diccionario;

        public static Dictionary<string, string> diccionario()
        {
            if (_diccionario == null)
            {
                _diccionario = new Dictionary<string, string>();
                try
                {
                    string[] estructuras = File.ReadAllLines(Properties.Settings.Default.Path + @"\PlanExplorer\" + "estructuras.txt");
                    foreach (string linea in estructuras)
                    {
                        _diccionario.Add(linea.Split('\t')[0], linea.Split('\t')[1]);
                    }
                }
                catch (Exception exp)
                {
                    MessageBox.Show("No se pudo leer estructuras.txt, se van a mostrar los nombres originales de las estructuras:\n" + exp.Message);
                }
            }

            return _diccionario;
        }
        public static string nombreEnDiccionario(Estructura estructura)
        {
            if (diccionario().TryGetValue(estructura.nombre, out string nombreDiccionario))
            {
                return nombreDiccionario;
            }
            else
            {
                return estructura.nombre;
            }
        }

        private static string[] _alfaBetaLineas;

        public static double AlfaBeta(string nombre)
        {
            if (_alfaBetaLineas == null)
            {
                try
                {
                    string path = Properties.Settings.Default.Path + @"\PlanExplorer\alfaBeta.txt";
                    _alfaBetaLineas = File.ReadAllLines(path);
                }
                catch (Exception exp)
                {
                    MessageBox.Show("No se pudo leer alfaBeta.txt, se va a usar el valor por defecto (3) para todas las estructuras:\n" + exp.Message);
                    _alfaBetaLineas = new string[0];
                }
            }
            string coincidencia = _alfaBetaLineas.FirstOrDefault(s => nombre.Contains(s.Split('\t')[0]));
            if (coincidencia == null)
            {
                return 3;
            }
            // ponytail: InvariantCulture (igual que DesdeCSV.Dbl) — alfaBeta.txt es un archivo de
            // configuración de texto plano, no debe depender de la cultura del hilo actual.
            if (!double.TryParse(coincidencia.Split('\t')[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double alfaBeta))
            {
                MessageBox.Show("Valor de alfa/beta inválido en alfaBeta.txt para \"" + nombre + "\", se va a usar el valor por defecto (3).");
                return 3;
            }
            return alfaBeta;
        }
    }



    public struct parEstructura
    {
        public string estructuraNombre { get; set; }
        public string structureID { get; set; }
    }

    public struct prescripcion
    {
        public string estructura { get; set; }
        public double dosis { get; set; }
    }

    





}