using System;
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

        // Distancia máxima de edición para sugerir/autoseleccionar un matcheo aproximado.
        public const int DistanciaMaximaSugerida = 3;

        public static int DistanciaDamerauLevenshtein(string a, string b)
        {
            a = (a ?? "").ToLowerInvariant();
            b = (b ?? "").ToLowerInvariant();
            int[,] d = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) d[0, j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                for (int j = 1; j <= b.Length; j++)
                {
                    int costo = a[i - 1] == b[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + costo);
                    if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                    {
                        d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + costo);
                    }
                }
            }
            return d[a.Length, b.Length];
        }

        // Estructuras del plan ordenadas de más a menos parecida a alguno de los nombresPosibles (menor distancia primero).
        public static List<Tuple<Structure, int>> candidatosPorDistancia(List<string> listaNombres, List<Structure> listaEstructura)
        {
            return listaEstructura
                .Select(s => new Tuple<Structure, int>(s, listaNombres.Min(n => DistanciaDamerauLevenshtein(n, s.Id))))
                .OrderBy(t => t.Item2)
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

        public static List<Structure> ptvs(PlanningItem plan)
        {
            List<Structure> PTVs = new List<Structure>();
            foreach (Structure estructura in listaEstructuras(plan))
            {
                if (estructura.DicomType == "PTV")
                {
                    PTVs.Add(estructura);
                }
            }
            return PTVs;
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
            return coincidencia == null ? 3 : Convert.ToDouble(coincidencia.Split('\t')[1]);
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