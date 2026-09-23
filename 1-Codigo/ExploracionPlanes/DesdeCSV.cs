using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ExploracionPlanes
{
    // Importador de una sola corrida: genera las 2 plantillas unificadas (SBRT y RC) a partir de
    // tablas/<N> FX.csv (bloque craneal = RC, bloque de cuerpo = SBRT, separados por una linea "###")
    // y tablas/Constrains consortium vs volPTV.txt (cobertura de PTV por volumen, solo SBRT).
    // No se ejecuta en runtime de la app: se corre una vez y el resultado queda guardado como Plantilla.
    public static class DesdeCSV
    {
        public static void GenerarPlantillasUnificadas(string carpetaTablas)
        {
            BindingList<IRestriccion> restriccionesSBRT = new BindingList<IRestriccion>();
            BindingList<IRestriccion> restriccionesRC = new BindingList<IRestriccion>();

            foreach (string path in Directory.GetFiles(carpetaTablas, "*FX*.csv"))
            {
                ImportarArchivoFx(path, restriccionesSBRT, restriccionesRC);
            }

            string pathVolPTV = Path.Combine(carpetaTablas, "Constrains consortium vs volPTV.txt");
            if (File.Exists(pathVolPTV))
            {
                ImportarCoberturaPTV(pathVolPTV, restriccionesSBRT);
            }

            AgregarExtrasCuradosRC(restriccionesRC);
            AgregarExtrasCuradosSBRT(restriccionesSBRT);
            MapearEstructurasSBRT(restriccionesSBRT);

            FusionarNombresDuplicados(restriccionesRC);
            FusionarNombresDuplicados(restriccionesSBRT);
            UnificarNombresPedidosSBRT(restriccionesSBRT);
            OrdenarPTVPrimero(restriccionesRC);

            string notaRC = "[1] Timmerman\r\n[2] UK Consortium\r\n[H] Hombre\r\n[M] Mujer\r\n* Optimal";
            Plantilla.crear("RC", false, restriccionesRC, notaRC).guardar(false);

            string notaSBRT = "[1] Timmerman\r\n[2] UK Consortium\r\n[H] Hombre\r\n[M] Mujer\r\n* Optimal\r\nCoverage PTV: ver nota de cada restriccion (Pulmon / No pulmon)";
            foreach ((string nombrePlantilla, BindingList<IRestriccion> restricciones) in SepararSBRT(restriccionesSBRT))
            {
                Plantilla.crear(nombrePlantilla, false, restricciones, notaSBRT).guardar(false);
            }
        }

        // "SBRT" se separa en 2 plantillas por region anatomica (pedido del usuario, reemplaza a la
        // plantilla unica "SBRT"). Cada una filtra solo las estructuras de su lista y respeta el ORDEN
        // dado (no alfabetico ni el de aparicion en el CSV) -- por eso el orden queda hardcodeado aca en
        // vez de derivarse de algo. "Bronchus" no entra en ninguna de las 2 (pedido explicito: eliminar).
        private static readonly string[] OrdenToraxAbdomen = {
            "PTV", "Trachea+LargeBronchus ", "Braquial Plex_L", "Braquial Plex_R", "Esophagus",
            "Heart_PRV", "Pericardium_PRV", "Lung-ITV", "GreatVessels", "SpinalCord_PRV", "Ribs",
            "ChestWall", "Skin", "Liver-GTV", "Spleen", "Stomach", "Kidneys_PRV",
            "RenalHilum-VascularTrunk", "BileDuct", "Duodenum", "Jejunum/ileum", "Colon",
        };

        private static readonly string[] OrdenAbdomenPelvis = {
            "PTV", "GreatVessels", "SpinalCord_PRV", "Ribs", "ChestWall", "Skin", "Liver-GTV",
            "Stomach", "Kidneys_PRV", "RenalHilum-VascularTrunk", "Spleen", "BileDuct", "Duodenum",
            "Jejunum/ileum", "Ureter", "Colon", "CaudaEquina_PRV", "LumbSacPlex_L", "LumbSacPlex_R",
            "Bladder_PRV", "Rectum_PRV", "Urethra", "FemurHeadNeck", "PenileBulb",
        };

        private static IEnumerable<(string, BindingList<IRestriccion>)> SepararSBRT(BindingList<IRestriccion> restriccionesSBRT)
        {
            EliminarEstructura(restriccionesSBRT, "Bronchus");
            RenombrarEstructura(restriccionesSBRT, "Heart", "Heart_PRV");
            RenombrarEstructura(restriccionesSBRT, "CaudaEquina", "CaudaEquina_PRV");
            RenombrarEstructura(restriccionesSBRT, "Rectum", "Rectum_PRV");

            yield return ("SBRT Torax-Abdomen", FiltrarYOrdenar(restriccionesSBRT, OrdenToraxAbdomen));
            yield return ("SBRT Abdomen-Pelvis", FiltrarYOrdenar(restriccionesSBRT, OrdenAbdomenPelvis));
        }

        // Se queda solo con las restricciones cuya estructura esta en "orden", y las deja en ESE orden
        // (dentro de una misma estructura, se mantiene el orden relativo original -- sort estable).
        private static BindingList<IRestriccion> FiltrarYOrdenar(BindingList<IRestriccion> origen, string[] orden)
        {
            var indice = orden.Select((nombre, i) => (nombre, i)).ToDictionary(t => t.nombre, t => t.i);
            var filtrado = origen.Where(r => indice.ContainsKey(r.estructura.nombre)).OrderBy(r => indice[r.estructura.nombre]);
            return new BindingList<IRestriccion>(filtrado.ToList());
        }

        // Saca del todo una estructura (todas sus restricciones, en cualquier fx) -- a diferencia de
        // RenombrarEstructura, esta no se fusiona con nada, se descarta.
        private static void EliminarEstructura(BindingList<IRestriccion> lista, string nombre)
        {
            List<IRestriccion> aSacar = lista.Where(r => r.estructura.nombre == nombre).ToList();
            foreach (IRestriccion r in aSacar)
            {
                lista.Remove(r);
            }
        }

        // PTV primero en la lista (orden estable: el resto queda en el mismo orden relativo de antes).
        private static void OrdenarPTVPrimero(BindingList<IRestriccion> lista)
        {
            List<IRestriccion> ordenada = lista.OrderBy(r => r.estructura.nombre == "PTV" ? 0 : 1).ToList();
            lista.Clear();
            foreach (IRestriccion r in ordenada)
            {
                lista.Add(r);
            }
        }

        // Clave para detectar que 2 nombres de estructura distintos son en realidad la misma estructura
        // clinica (misma logica que la normalizacion del comparador: ignora prosa/descriptores extra y el
        // sufijo _PRV<#>, y colapsa mayusculas/espacios/guiones).
        private static string ClaveFusion(string nombre)
        {
            string s = nombre;
            int idxParen = s.IndexOf('(');
            if (idxParen >= 0)
            {
                s = s.Substring(0, idxParen);
            }
            int idxComa = s.IndexOf(',');
            if (idxComa >= 0)
            {
                s = s.Substring(0, idxComa);
            }
            s = Regex.Replace(s, @"_PRV\d*", "", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"[^a-zA-Z0-9]", "");
            return s.ToLowerInvariant();
        }

        // Cuando 2+ restricciones de la misma clave (misma estructura clinica) traen nombres literales
        // distintos (ej. "Brainstem_PRV02" en un fx y "Brainstem (not medulla)" en otro, o "BrainStem_PRV02"
        // con mayuscula distinta), se fusionan bajo el nombre MENOS verboso (el mas corto) -- ademas de
        // prolijidad, esto es necesario para que el matcheo de estructuras real (por nombresPosibles)
        // funcione igual sin importar de que archivo de fx salio cada restriccion.
        private static void FusionarNombresDuplicados(BindingList<IRestriccion> lista)
        {
            var grupos = lista.GroupBy(r => ClaveFusion(r.estructura.nombre)).ToList();
            foreach (var grupo in grupos)
            {
                List<string> nombresDistintos = grupo.Select(r => r.estructura.nombre).Distinct().ToList();
                if (nombresDistintos.Count <= 1)
                {
                    continue;
                }
                string canonico = nombresDistintos.OrderBy(n => n.Length).First();
                List<string> alias = grupo
                    .SelectMany(r => (r.estructura.nombresPosibles ?? new List<string>()).Concat(new[] { r.estructura.nombre }))
                    .Distinct()
                    .ToList();
                foreach (IRestriccion r in grupo)
                {
                    r.estructura.nombre = canonico;
                    r.estructura.nombresPosibles = new List<string>(alias);
                    r.crearEtiquetaInicio();
                    r.crearEtiqueta();
                }
            }
        }

        // Constraints que ya estaban curados a mano en las plantillas manuales (RC_Nfx.txt / SBRT_Nfx.txt)
        // y no salen de las tablas Timmerman/UK Consortium: se incorporan tal cual a la version unificada.
        private static void AgregarExtrasCuradosRC(BindingList<IRestriccion> restriccionesRC)
        {
            // 1) PTV: identico en RC_1fx/3fx/5fx.txt -> sin Condicion de fx, aplica siempre.
            restriccionesRC.Add(new RestriccionVolumen().crear(
                Estructura.crear("PTV", new List<string>()), "%", "%", false, 98, 95, 100, "", null));

            // 2) 1fx: "Healthy Brain" (manual) no viene del CSV -> se agrega igual, pero como "WholeBrain"
            // (mismo criterio de fusion de estructuras que el resto de la plantilla).
            restriccionesRC.Add(new RestriccionVolumen().crear(
                Estructura.crear("WholeBrain", new List<string>()), "cm3", "Gy", true, 10, double.NaN, 12, "",
                Condicion.crear(Tipo.NumFx, Operador.igual_a, 1)));

            // 3) 3fx y 5fx: el CSV ya trae los valores correctos para esta estructura, solo bajo el nombre
            // "BRAIN* (including targets)" -> se renombra a "WholeBrain" (no se tocan los valores).
            RenombrarEstructura(restriccionesRC, "BRAIN* (including targets)", "WholeBrain");
        }

        // Renombra todas las restricciones de una estructura (mismo objeto, no crea filas nuevas).
        // notaOriginal: si se pasa, deja constancia del nombre viejo en la nota (ej. "(Renal Cortex)").
        //
        // Ojo: varias restricciones de una misma linea del CSV (D, Dmax, UK) comparten la MISMA instancia
        // de Estructura. Si se chequeara "nombre == original" restriccion por restriccion durante el
        // recorrido, la primera que la renombra deja el objeto ya cambiado para las demas -> esas dejan
        // de matchear y se saltean su propio refresco de etiqueta (queda con el nombre viejo cacheado).
        // Por eso primero se saca la lista completa de afectadas, y recien despues se procesan todas.
        private static void RenombrarEstructura(BindingList<IRestriccion> lista, string nombreOriginal, string nombreNuevo, string notaOriginal = null)
        {
            List<IRestriccion> afectadas = lista.Where(r => r.estructura.nombre == nombreOriginal).ToList();
            foreach (IRestriccion r in afectadas)
            {
                r.estructura.nombre = nombreNuevo;
                r.estructura.nombresPosibles = new List<string> { nombreNuevo };
                if (!string.IsNullOrEmpty(notaOriginal))
                {
                    r.nota = string.IsNullOrEmpty(r.nota) ? $"({notaOriginal})" : r.nota + $" ({notaOriginal})";
                }
                r.crearEtiquetaInicio();
                r.crearEtiqueta();
            }
        }

        // Renombra la estructura original a nombreA y, ademas, agrega una copia identica de cada
        // restriccion bajo nombreB (mismos valores, misma Condicion) -- para cuando un unico renglon del
        // CSV en realidad representa 2 estructuras clinicas distintas (ej. "Heart/Pericardium"). Misma
        // logica de snapshot-primero que RenombrarEstructura, y por la misma razon.
        private static void DuplicarEstructura(BindingList<IRestriccion> lista, string nombreOriginal, string nombreA, string nombreB)
        {
            List<IRestriccion> afectadas = lista.Where(r => r.estructura.nombre == nombreOriginal).ToList();
            List<IRestriccion> nuevas = new List<IRestriccion>();
            foreach (IRestriccion r in afectadas)
            {
                Estructura estructuraB = Estructura.crear(nombreB, new List<string>());
                nuevas.Add(r.crear(estructuraB, r.unidadValor, r.unidadCorrespondiente, r.esMenorQue, r.valorEsperado, r.valorTolerado, r.valorCorrespondiente, r.nota, r.condicion, r.prioridad, r.planMod));
            }
            foreach (IRestriccion r in afectadas)
            {
                r.estructura.nombre = nombreA;
                r.estructura.nombresPosibles = new List<string> { nombreA };
                r.crearEtiquetaInicio();
                r.crearEtiqueta();
            }
            foreach (IRestriccion n in nuevas)
            {
                lista.Add(n);
            }
        }

        // Mapeo de nombres de estructura del CSV a los nombres/convencion ya usados en las plantillas
        // manuales (pedido explicito del usuario, distinto de la normalizacion "prosa"/"_PRV" del
        // comparador: estos SI cambian el dato generado, no solo la comparacion).
        private static void MapearEstructurasSBRT(BindingList<IRestriccion> restriccionesSBRT)
        {
            RenombrarEstructura(restriccionesSBRT, "Renal cortex (right and left)", "Kidneys_PRV", "Renal Cortex");
            DuplicarEstructura(restriccionesSBRT, "Heart/Pericardium", "Heart_PRV", "Pericardium_PRV");
            RenombrarEstructura(restriccionesSBRT, "Rib", "Ribs");
            RenombrarEstructura(restriccionesSBRT, "SpinalCord and medulla", "SpinalCord_PRV");
            RenombrarEstructura(restriccionesSBRT, "Bladder Wall (with urine)", "Bladder_PRV", "Bladder Wall (with urine)");
        }

        // Duplicados por nombre encontrados a mano por el usuario (no los agarra FusionarNombresDuplicados
        // porque son diferencias de vocabulario real -- "-GTV"/"-ITV"/"Wall"/"Vessels" no son prosa ni
        // sufijo _PRV, asi que el normalizador generico los trata como estructuras distintas a proposito).
        // Se pide mantener el PRIMER nombre de cada par -> RenombrarEstructura(lista, elQueSeVa, elQueQueda).
        // Braquial Plex/LumbSacPlex son el caso inverso: la version sin lateralidad se duplica en _L y _R
        // (mismo mecanismo ya usado para Heart/Pericardium).
        private static void UnificarNombresPedidosSBRT(BindingList<IRestriccion> restriccionesSBRT)
        {
            RenombrarEstructura(restriccionesSBRT, "GreatVes", "GreatVessels");
            RenombrarEstructura(restriccionesSBRT, "Liver", "Liver-GTV");
            RenombrarEstructura(restriccionesSBRT, "Lungs", "Lung-ITV");
            RenombrarEstructura(restriccionesSBRT, "BladderWall", "Bladder_PRV");
            DuplicarEstructura(restriccionesSBRT, "Braquial Plex", "Braquial Plex_L", "Braquial Plex_R");
            DuplicarEstructura(restriccionesSBRT, "LumbSacPlex", "LumbSacPlex_L", "LumbSacPlex_R");
            RenombrarEstructura(restriccionesSBRT, "SmallBowell", "Jejunum/ileum");
            RenombrarEstructura(restriccionesSBRT, "RenalCortex", "Kidneys_PRV");
        }

        private static void AgregarExtrasCuradosSBRT(BindingList<IRestriccion> restriccionesSBRT)
        {
            // Constraints de PTV identicos en las 4 plantillas manuales (SBRT_1/3/5/15fx.txt) -> sin
            // Condicion de fx, aplican a todos los fraccionamientos.
            Estructura ptv() => Estructura.crear("PTV", new List<string>());
            restriccionesSBRT.Add(new RestriccionDosis().crear(ptv(), "%", "%", false, 100, 99, 95, "", null));
            restriccionesSBRT.Add(new RestriccionDosis().crear(ptv(), "%", "%", false, 90, double.NaN, 100, "", null));
            restriccionesSBRT.Add(new RestriccionDosisMax().crear(ptv(), "%", null, true, 120, 130, 0, "", null));
        }

        // Lee un archivo partiendo SOLO por "\n" (normalizando "\r\n" antes, y descartando cualquier "\r"
        // suelto que haya quedado adentro de una celda). File.ReadAllLines corta tambien por un "\r" solo
        // -- varios de los CSV de tablas/ traen un "\r" pegado dentro de la celda "ChestWall" (corrupcion
        // de origen), que partia esa fila en 2 y hacia que la fila SIGUIENTE (ej. Skin) heredara como
        // nombre de estructura la mitad de la fila de ChestWall en vez de "ChestWall".
        private static string[] LeerLineas(string path)
        {
            return File.ReadAllText(path).Replace("\r\n", "\n").Replace("\r", "").Split('\n');
        }

        private static void ImportarArchivoFx(string path, BindingList<IRestriccion> destinoSBRT, BindingList<IRestriccion> destinoRC)
        {
            string[] archivo = LeerLineas(path);
            int numFx = Convert.ToInt32(archivo[0].Split(' ').First());
            bool tieneUK = archivo[1].Contains("UK");
            Condicion condicionFx = Condicion.crear(Tipo.NumFx, Operador.igual_a, numFx);

            int idxSeparador = Array.FindIndex(archivo, l => l.Trim() == "###");

            IEnumerable<string> lineasCraneal = archivo.Skip(3).Take(idxSeparador - 3).Where(l => !string.IsNullOrWhiteSpace(l));
            IEnumerable<string> lineasCuerpo = archivo.Skip(idxSeparador + 1).Where(l => !string.IsNullOrWhiteSpace(l));

            foreach (IRestriccion r in ParsearBloqueOAR(lineasCuerpo, tieneUK, condicionFx))
            {
                destinoSBRT.Add(r);
            }
            foreach (IRestriccion r in ParsearBloqueOAR(lineasCraneal, tieneUK, condicionFx))
            {
                destinoRC.Add(r);
            }
        }

        // Cantidad de columnas que puede llegar a leer una linea de datos (0..6); se rellena para no
        // depender de que la fila venga con la misma cantidad de comas en todos los archivos.
        private const int ColumnasEsperadas = 7;

        // La tabla trae celdas con texto sin valor numerico real (ej. "Mean dose", "≤16-17"): en vez de
        // tirar la importacion entera por una celda asi, se la salta (NaN) y se sigue con el resto de la fila.
        private static double Dbl(string valor)
        {
            return double.TryParse(valor.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double d) ? d : double.NaN;
        }

        // Split de CSV que respeta comillas: nombres de estructura con coma adentro (ej. "Eyelid, meibomian
        // glands (one side)") con un split por ',' a secas corrompen todas las columnas siguientes.
        private static string[] SplitCsv(string linea)
        {
            var campos = new List<string>();
            bool enComillas = false;
            var actual = new System.Text.StringBuilder();
            foreach (char c in linea)
            {
                if (c == '"')
                {
                    enComillas = !enComillas;
                }
                else if (c == ',' && !enComillas)
                {
                    campos.Add(actual.ToString());
                    actual.Clear();
                }
                else
                {
                    actual.Append(c);
                }
            }
            campos.Add(actual.ToString());
            return campos.ToArray();
        }

        // Celdas tipo "1500 (H)/950 (M)": son 2 valores (hombre/mujer) combinados en una sola columna,
        // no un unico numero. Si hay 2+ pares numero+sexo, se devuelven ambos (2 restricciones, una por
        // sexo); si no, cae al comportamiento de siempre (1 valor, "H"/"M" solo se usan para la nota).
        private static readonly Regex PatronNumeroSexo = new Regex(@"([\d.]+)\s*\(?([HM])\)?", RegexOptions.IgnoreCase);

        private static List<(double valor, string notaSexo)> ExtraerValoresConSexo(string celda)
        {
            var matches = PatronNumeroSexo.Matches(celda);
            var resultado = new List<(double, string)>();
            if (matches.Count >= 2)
            {
                foreach (Match m in matches)
                {
                    resultado.Add((Dbl(m.Groups[1].Value), " [" + m.Groups[2].Value.ToUpper() + "]"));
                }
                return resultado;
            }
            double valor = Dbl(celda.Replace("<", "").Replace("M", "").Replace("H", "").Replace("%", ""));
            string notaSexo = "";
            if (celda.Contains("H"))
            {
                notaSexo = " [H]";
            }
            else if (celda.Contains("M"))
            {
                notaSexo = " [M]";
            }
            resultado.Add((valor, notaSexo));
            return resultado;
        }

        private static BindingList<IRestriccion> ParsearBloqueOAR(IEnumerable<string> lineasDeDatos, bool tieneUK, Condicion condicion)
        {
            BindingList<IRestriccion> restricciones = new BindingList<IRestriccion>();
            string estructuraAnt = "";
            // Filas tipo "Liver,CRITICAL VOLUME (cm3),CRITICAL VOLUME DOSE MAX (Gy),,,,,": la fila con el
            // literal "CRITICAL" es un subencabezado (sin valores), pero las filas de datos que siguen
            // (columna 0 vacia) hasta la proxima estructura nombrada son RestriccionVolumenCritico, no
            // RestriccionDosis. La fila del subencabezado en si NO se saltea entera: su columna 4-6 (UK)
            // puede traer datos propios (ej. Renal cortex) y se procesa igual que cualquier otra fila.
            bool esVolumenCritico = false;

            foreach (string lineaCruda in lineasDeDatos)
            {
                string[] Linea = SplitCsv(lineaCruda);
                if (Linea.Length < ColumnasEsperadas)
                {
                    Linea = Linea.Concat(Enumerable.Repeat("", ColumnasEsperadas - Linea.Length)).ToArray();
                }

                if (Linea[0] != "")
                {
                    esVolumenCritico = false; // nueva estructura nombrada: no hereda el flag de la anterior
                }

                bool esSubencabezadoCritico = Linea[1].IndexOf("CRITICAL", StringComparison.OrdinalIgnoreCase) >= 0
                    || Linea[2].IndexOf("CRITICAL", StringComparison.OrdinalIgnoreCase) >= 0;
                if (esSubencabezadoCritico)
                {
                    esVolumenCritico = true;
                }

                Estructura estructura = Estructura.crear(Linea[0] != "" ? Linea[0] : estructuraAnt, new List<string>());

                if (!esSubencabezadoCritico)
                {
                    string unidad = Linea[1].Contains("%") ? "%" : "cm3";

                    if (Linea[1] == "")
                    {
                        // Sin valor de volumen/Dmean en esta fila (ej. "\"Eyelid, ...\",,,21.3"): puede
                        // igual traer un Dmax propio en col3, que se procesa mas abajo.
                    }
                    else if (Linea[1].ToLower().Contains("mean"))
                    {
                        // ej. "Eye (retina),Mean dose,<26,30": col1 es el rotulo "Mean dose", col2 es el
                        // valor de dosis media (no un umbral de volumen) -> Dmean, no Dosis.
                        double valorEsperadoMean = Dbl(Linea[2].Replace("<", ""));
                        if (!double.IsNaN(valorEsperadoMean))
                        {
                            restricciones.Add(new RestriccionDosisMedia().crear(estructura, "Gy", "", true, valorEsperadoMean, double.NaN, double.NaN, "[1]", condicion));
                        }
                    }
                    else if (esVolumenCritico)
                    {
                        // ej. "1500 (H)/950 (M)": 2 valores (hombre/mujer) combinados en 1 celda -> 2 restricciones.
                        double valorCorrespondienteCritico = Dbl(Linea[2].Replace("Gy", ""));
                        foreach (var (valorEsperadoCritico, notaSexo) in ExtraerValoresConSexo(Linea[1]))
                        {
                            if (!double.IsNaN(valorEsperadoCritico) && !double.IsNaN(valorCorrespondienteCritico))
                            {
                                restricciones.Add(new RestriccionVolumenCritico().crear(estructura, unidad, "Gy", false, valorEsperadoCritico, double.NaN, valorCorrespondienteCritico, "[1]" + notaSexo + " Critical volume", condicion));
                            }
                        }
                    }
                    else
                    {
                        double valorEsperado = Dbl(Linea[2]);
                        foreach (var (valorCorrespondiente, notaSexo) in ExtraerValoresConSexo(Linea[1]))
                        {
                            if (!double.IsNaN(valorCorrespondiente) && !double.IsNaN(valorEsperado))
                            {
                                restricciones.Add(new RestriccionDosis().crear(estructura, "Gy", unidad, true, valorEsperado, double.NaN, valorCorrespondiente, "[1]" + notaSexo, condicion));
                            }
                        }
                    }

                    if (Linea[3] != "" && !Linea[3].Contains("<"))
                    {
                        if (Linea[3].Contains("≤"))
                        {
                            // ej. "≤16-17": rango sin valor unico -> menor como esperado, mayor como tolerado.
                            string[] rango = Linea[3].Replace("≤", "").Trim().Split('-');
                            double menor = Dbl(rango[0]);
                            double mayor = rango.Length > 1 ? Dbl(rango[1]) : double.NaN;
                            if (!double.IsNaN(menor))
                            {
                                restricciones.Add(new RestriccionDosisMax().crear(estructura, "Gy", unidad, true, menor, mayor, double.NaN, "[1]", condicion));
                            }
                        }
                        else
                        {
                            double valorEsperadoMax = Dbl(Linea[3]);
                            if (!double.IsNaN(valorEsperadoMax))
                            {
                                restricciones.Add(new RestriccionDosisMax().crear(estructura, "Gy", unidad, true, valorEsperadoMax, double.NaN, double.NaN, "[1]", condicion));
                            }
                        }
                    }
                }

                if (tieneUK && Linea[4].Contains("V"))
                {
                    double valorCorrespondienteUK = Dbl(Linea[4].Replace("V", "").Replace("Gy", "").Replace("*", "").Trim());
                    if (!double.IsNaN(valorCorrespondienteUK))
                    {
                        string unidadValor = Linea[5].Contains("cc") ? "cm3" : "%";
                        restricciones.Add(RestriccionConsortium(new RestriccionVolumen(), estructura, Linea, unidadValor, "Gy", valorCorrespondienteUK, condicion));
                    }
                }
                else if (Linea[4].ToLower().Contains("mean") || Linea[4].ToLower().Contains("med"))
                {
                    // El valor puede venir de Linea[5] (optimal) O de Linea[6] (mandatory) segun cual este
                    // vacia -- mirar solo Linea[5] hacia "Gy" y asumir "%" en cualquier otro caso (incluida
                    // Linea[5] vacia) etiquetaba como "%" filas que en realidad son en Gy (el valor real
                    // estaba en Linea[6]). Default a "Gy" (la gran mayoria), "%" solo si aparece explicito.
                    string unidadValor = (Linea[5].Contains("%") || Linea[6].Contains("%")) ? "%" : "Gy";
                    restricciones.Add(RestriccionConsortium(new RestriccionDosisMedia(), estructura, Linea, unidadValor, "cm3", double.NaN, condicion));
                }
                else if (Linea[4].ToLower().Contains("cc"))
                {
                    double valorCorrespondienteUK = Dbl(Linea[4].Replace("cc", "").Replace("D", "").Replace("*", "").Trim());
                    if (!double.IsNaN(valorCorrespondienteUK))
                    {
                        // Mismo bug que en la rama "mean" de arriba: el valor real puede estar en Linea[6].
                        string unidadValor = (Linea[5].Contains("%") || Linea[6].Contains("%")) ? "%" : "Gy";
                        if (Math.Abs(valorCorrespondienteUK - 0.035) < 0.001)
                        {
                            // D0.035cc es la convencion clinica de "dosis de punto" = Dmax, no una Dosis
                            // a volumen aparte (D0.1cc si lo es, esa se deja como RestriccionDosis).
                            restricciones.Add(RestriccionConsortium(new RestriccionDosisMax(), estructura, Linea, unidadValor, "cm3", double.NaN, condicion));
                        }
                        else
                        {
                            restricciones.Add(RestriccionConsortium(new RestriccionDosis(), estructura, Linea, unidadValor, "cm3", valorCorrespondienteUK, condicion));
                        }
                    }
                }

                estructuraAnt = estructura.nombre;
            }
            return restricciones;
        }

        public static IRestriccion RestriccionConsortium(IRestriccion restriccion, Estructura estructura, string[] Linea, string unidadValor, string unidadCorrespondiente, double valorCorrespondiente, Condicion condicion)
        {
            double valorEsperado = double.NaN;
            double valorTolerado = double.NaN;
            string esperado = "";
            string tolerado = "";

            string nota = "[2]";
            if (Linea[5].Contains("Report") || Linea[6].Contains("Report"))
            {
                // "Report"/"Reportar": la literatura no fija un limite numerico, solo pide reportar el
                // valor medido. Se carga un valor excesivamente alto (100Gy) para que nunca "cumpla" en
                // silencio, y la nota deja explicito que hay que reportar el valor a mano.
                valorEsperado = 100;
                unidadValor = "Gy";
                nota = "Reportar";
            }
            else if (Linea[5] != "")
            {

                esperado = Linea[5].Replace("Gy", "").Replace("%", "").Replace("<", "").Replace("cc", "").Trim();
                tolerado = "";

                if (Linea[6] != "")
                {
                    tolerado = Linea[6].Replace("Gy", "").Replace("%", "").Replace("<", "").Replace("cc", "").Trim();
                    valorEsperado = Dbl(esperado);
                    valorTolerado = Dbl(tolerado);
                }
                else if (Linea[5].Contains("-"))
                {
                    string[] aux = esperado.Split('-');
                    valorEsperado = Dbl(aux[0]);
                    valorTolerado = Dbl(aux[1]);
                    nota += " *";
                }
                else
                {
                    valorEsperado = Dbl(esperado);
                }
            }
            else if (Linea[6] != "")
            {
                esperado = Linea[6].Replace("Gy", "").Replace("%", "").Replace("<", "").Replace("cc", "").Trim();
                valorEsperado = Dbl(esperado);
            }

            restriccion = restriccion.crear(estructura, unidadValor, unidadCorrespondiente, true, valorEsperado, valorTolerado, valorCorrespondiente, nota, condicion, "", "");
            return restriccion;
        }

        private static void ImportarCoberturaPTV(string path, BindingList<IRestriccion> destinoSBRT)
        {
            string[] lineas = LeerLineas(path);
            int idxPulmon = Array.FindIndex(lineas, l => l.Trim() == "###Para SBRT Pulmon");
            int idxNoPulmon = Array.FindIndex(lineas, l => l.Trim() == "###Para SBRT no Pulmon");

            ParsearCoberturaPulmon(lineas.Skip(idxPulmon + 1).Take(idxNoPulmon - idxPulmon - 1), destinoSBRT);
            ParsearCoberturaNoPulmon(lineas.Skip(idxNoPulmon + 1), destinoSBRT);
        }

        private static IEnumerable<string[]> FilasDeDatos(IEnumerable<string> lineas)
        {
            return lineas
                .Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("Volumen PTV"))
                .Select(SplitCsv);
        }

        private static void ParsearCoberturaPulmon(IEnumerable<string> lineas, BindingList<IRestriccion> destino)
        {
            foreach (string[] c in FilasDeDatos(lineas))
            {
                Condicion condVol = ParsearRangoVolumen(c[0]);
                string nota = "PTV (SBRT pulmon) - Volumen " + c[0] + "cm3";
                destino.Add(new RestriccionIndiceConformidad().crear(Estructura.crear("PTV", new List<string>()), "", "%", true, Dbl(c[1]), Dbl(c[2]), 100, nota, condVol));
                destino.Add(new RestriccionIndiceConformidad().crear(Estructura.crear("PTV", new List<string>()), "", "%", true, Dbl(c[3]), Dbl(c[4]), 50, nota, condVol));
                destino.Add(new RestriccionVolumen().crear(Estructura.crear("Lung-ITV", new List<string>()), "%", "Gy", true, Dbl(c[5]), double.NaN, 20, nota, condVol));
            }
        }

        private static void ParsearCoberturaNoPulmon(IEnumerable<string> lineas, BindingList<IRestriccion> destino)
        {
            foreach (string[] c in FilasDeDatos(lineas))
            {
                Condicion condVol = ParsearRangoVolumen(c[0]);
                string nota = "PTV (SBRT no pulmon) - Volumen " + c[0] + "cm3";
                destino.Add(new RestriccionIndiceConformidad().crear(Estructura.crear("PTV", new List<string>()), "", "%", true, Dbl(c[1]), Dbl(c[2]), 50, nota, condVol));
            }
        }

        private static Condicion ParsearRangoVolumen(string rango)
        {
            rango = rango.Trim();
            if (rango.StartsWith("<"))
            {
                return Condicion.crear(Tipo.VolPTV, Operador.menor_a, Dbl(rango.Replace("<", "")));
            }
            if (rango.StartsWith(">"))
            {
                return Condicion.crear(Tipo.VolPTV, Operador.mayor_a, Dbl(rango.Replace(">", "")));
            }
            string[] partes = rango.Split('-');
            return Condicion.crear(Tipo.VolPTV, Operador.entre, Dbl(partes[0]), Dbl(partes[1]));
        }
    }
}
