using System;
using System.IO;
using System.Linq;
using VMS.TPS.Common.Model.API;

namespace ExploracionPlanes
{
    // Memoria persistida en .txt por plan (paciente+curso+plan). Usada para matcheo de estructuras,
    // prescripciones y plantilla seleccionada: misma clave, mismo fallback al plan más reciente del paciente.
    public static class MemoriaPlan
    {
        private static string sanear(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                s = s.Replace(c, '_');
            }
            return s;
        }

        public static string clave(Patient paciente, PlanningItem plan)
        {
            string cursoId = plan is PlanSetup ? ((PlanSetup)plan).Course.Id : ((PlanSum)plan).Course.Id;
            return sanear(paciente.Id) + "_" + sanear(cursoId) + "_" + sanear(plan.Id);
        }

        public static string rutaArchivo(string carpeta, Patient paciente, PlanningItem plan)
        {
            if (!Directory.Exists(carpeta))
            {
                Directory.CreateDirectory(carpeta);
            }
            return Path.Combine(carpeta, clave(paciente, plan) + ".txt");
        }

        // Archivo de memoria de OTRO plan del mismo paciente, el más reciente. Null si no hay ninguno.
        public static string rutaArchivoFallbackPaciente(string carpeta, Patient paciente, PlanningItem planActual)
        {
            try
            {
                string rutaActual = rutaArchivo(carpeta, paciente, planActual);
                return Directory.GetFiles(carpeta, sanear(paciente.Id) + "_*.txt")
                    .Where(f => !f.Equals(rutaActual, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                    .FirstOrDefault();
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Ruta a usar para LEER: la propia del plan si existe, sino la del plan más reciente del paciente.
        public static string rutaParaLeer(string carpeta, Patient paciente, PlanningItem plan)
        {
            string ruta = rutaArchivo(carpeta, paciente, plan);
            if (File.Exists(ruta))
            {
                return ruta;
            }
            return rutaArchivoFallbackPaciente(carpeta, paciente, plan);
        }
    }
}
