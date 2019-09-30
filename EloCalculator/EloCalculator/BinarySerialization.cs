using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace EloCalculator
{

    public static class BinarySerialization
    {
        /// <Serialization Methods courtesy of Daniel Schroeder http://blog.danskingdom.com/saving-and-loading-a-c-objects-data-to-an-xml-json-or-binary-file/ >
        public static void WriteToBinaryFile<BasePlayerClass>(string filePath, BasePlayerClass objectToWrite, bool append = true)
        {
            using (Stream stream = File.Open(filePath, append ? FileMode.Append : FileMode.Create))
            {
                var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                binaryFormatter.Serialize(stream, objectToWrite);
            }
        }


        public static BasePlayerClass ReadFromBinaryFile<BasePlayerClass>(string filePath)
        {
            using (Stream stream = File.Open(filePath, FileMode.Open))
            {
                var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                return (BasePlayerClass)binaryFormatter.Deserialize(stream);
            }
        }
    }
}