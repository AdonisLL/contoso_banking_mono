using System;
using System.IO;
using Newtonsoft.Json;

namespace Contoso.LegacyBank.Shared
{
    /// <summary>
    /// Writes job/metadata files atomically (write to a temp file, then move) so a reader
    /// polling the folder never observes a half-written file - the same technique the real
    /// file-queue integration relies on instead of a transactional message broker.
    /// </summary>
    public static class JsonFile
    {
        public static void WriteAtomic<T>(string destinationPath, T value)
        {
            var tempPath = destinationPath + ".tmp";
            File.WriteAllText(tempPath, JsonConvert.SerializeObject(value, Formatting.Indented));
            if (File.Exists(destinationPath))
                File.Delete(destinationPath);
            File.Move(tempPath, destinationPath);
        }

        public static T Read<T>(string path)
        {
            return JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
        }
    }
}
