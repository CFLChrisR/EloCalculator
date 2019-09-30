﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml.Serialization;
using System.Xml;

namespace EloCalculator
{
    /// <Serialization Methods courtesy of Daniel Schroeder http://blog.danskingdom.com/saving-and-loading-a-c-objects-data-to-an-xml-json-or-binary-file/ >

    public static class XmlSerialization
    {
        /// <summary>
        /// Writes the given object instance to an XML file.
        /// <param name="append">If false the file will be overwritten if it already exists. If true the contents will be appended to the file.</param>
        public static void WriteToXmlFile<T>(string filePath, T objectToWrite, bool append = true) where T : new()
        {
            TextWriter writer = null;
            /*todo: add Player Lists folder in user documents, check if it exists and create it just like 
            Below and use as default loader in XmlReader

            */
            try
            {
                var serializer = new XmlSerializer(typeof(peopleList));
                if (!File.Exists(filePath.ToString()))
                {
                    var dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    StringBuilder ns = new StringBuilder();
                    ns.Append(dir + "\\Player Lists\\");// folder location

                    if (!Directory.Exists(ns.ToString()))
                        {  
                        Directory.CreateDirectory(ns.ToString());
                        Console.WriteLine("No file found @ " + filePath);
                        writer = new StreamWriter(filePath, append);
                        serializer.Serialize(writer, objectToWrite);
                        }
                }

                else
                {
                    System.IO.File.WriteAllText(filePath, string.Empty);

                    var emptyNamepsaces = new XmlSerializerNamespaces(new[] { XmlQualifiedName.Empty });
                    var settings = new XmlWriterSettings();
                    {
                        settings.Indent = true;
                        settings.OmitXmlDeclaration = true; settings.ConformanceLevel = ConformanceLevel.Auto
                                   ; settings.Indent = true;
                    }
                    using (var stream = new StringWriter())
                    using (var writer2 = XmlWriter.Create(stream, settings))
                    {
                        writer = new StreamWriter(filePath, append);

                        serializer.Serialize(writer, objectToWrite, emptyNamepsaces);
                    }

                }
            }
            finally
            {
                if (writer != null)
                    writer.Close();
            }
        }

        /// <summary>
        /// Reads an object instance from an XML file.
        /// <para>Object type must have a parameterless constructor.</para>
        /// </summary>
        /// <typeparam name="T">The type of object to read from the file.</typeparam>
        /// <param name="filePath">The file path to read the object instance from.</param>
        /// <returns>Returns a new instance of the object read from the XML file.</returns>
        public static peopleList ReadFromXmlFile<peopleList>(string filePath) where peopleList : new()
        {

            TextReader reader = null;
            try
            {
                var serializer = new XmlSerializer(typeof(peopleList));
                reader = new StreamReader(filePath);
                return (peopleList)serializer.Deserialize(reader);
            }
            finally
            {
                if (reader != null)
                    reader.Close();
            }
        }
    }
}