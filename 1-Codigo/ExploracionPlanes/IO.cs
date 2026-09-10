using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Windows.Forms;


namespace ExploracionPlanes
{
    public static class IO
    {
        /// <summary>
        /// Escribe un objeto como Json en un archivo
        /// </summary>
        /// <param name="file">la ruta del archivo</param>
        /// <param name="theObj">el objeto para escribir</param>
        public static void writeObjectAsJson(string file, object theObj)
        {
            var settings = new JsonSerializerSettings();
            settings.TypeNameHandling = TypeNameHandling.Auto;
            File.WriteAllText(file, JsonConvert.SerializeObject(theObj,settings));
        }
        public static T readJson<T>(string file)
        {
            var settings = new JsonSerializerSettings();
            settings.TypeNameHandling = TypeNameHandling.Auto;
            return JsonConvert.DeserializeObject<T>(File.ReadAllText(file), settings);
        }
        /// <summary>
        /// Devuelve un string con un nombre único para un archivo
        /// </summary>
        /// <param name="path">La ruta a la carpeta</param>
        /// <param name="baseName">El nombre deseado</param>
        /// <param name="maxAttempts">el número máximo que se le concatenará al baseName</param>
        /// <returns></returns>
        /// 
        public static string GetUniqueFilename(string path, string baseName, string extention = "txt", int maxAttempts = 128)
        {
            if (!File.Exists(string.Format("{0}{1}.{2}", path, baseName, extention)))
            {
                return string.Format("{0}{1}.{2}", path, baseName, extention);
            }
            else
            {
                for (int i = 1; i < maxAttempts; i++)
                {
                    if (!File.Exists(string.Format("{0}{1} ({2}).{3}", path, baseName, i, extention)))
                    {
                        return string.Format("{0}{1} ({2}).{3}", path, baseName, i, extention);
                    }
                }
            }
            return string.Format("{0}{1} - {2:yyyy-MM-dd_hh-mm-ss}.{3}", path, baseName, DateTime.Now, extention);
        }
    }
}