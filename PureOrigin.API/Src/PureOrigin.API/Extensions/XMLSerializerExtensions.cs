using System;
using System.IO;
using System.Xml.Serialization;

namespace PureOrigin.API.Extensions
{
    public static class XMLSerializerExtensions
    {
        public static T XmlDeserializeFromString<T>(string objectData)
        {
            return (T)XmlDeserializeFromString(objectData, typeof(T));
        }

        public static object XmlDeserializeFromString(string objectData, Type type)
        {
            var serializer = new XmlSerializer(type);
            object result;

            using (TextReader reader = new StringReader(objectData))
            {
                result = serializer.Deserialize(reader);
            }

            return result;
        }
    }
}
