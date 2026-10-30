using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace EloCalculator.Core.Storage
{
    /// <summary>Loads and saves objects as XML files.</summary>
    public static class XmlStore
    {
        public static T Load<T>(string path) where T : class
        {
            var serializer = new XmlSerializer(typeof(T));
            using (var reader = XmlReader.Create(path))
                return (T)serializer.Deserialize(reader);
        }

        /// <summary>
        /// Writes to a temporary file first and then swaps it in, so a crash
        /// mid-write can't leave a half-written file behind.
        /// </summary>
        public static void Save<T>(string path, T value) where T : class
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var tempPath = path + ".tmp";
            var settings = new XmlWriterSettings { Indent = true };
            var namespaces = new XmlSerializerNamespaces(new[] { XmlQualifiedName.Empty });
            using (var writer = XmlWriter.Create(tempPath, settings))
                new XmlSerializer(typeof(T)).Serialize(writer, value, namespaces);

            if (File.Exists(path))
                File.Replace(tempPath, path, null);
            else
                File.Move(tempPath, path);
        }
    }
}
