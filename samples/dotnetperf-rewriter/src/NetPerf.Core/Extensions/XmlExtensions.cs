using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace System.Xml {
    
    public static class XmlExtensions {

        public static void WriteAttributeString(this XmlWriter writer, string attributeName, int value) {
            writer.WriteAttributeString(attributeName, value.ToStorageString());
        }

        public static void WriteAttributeString(this XmlWriter writer, string attributeName, double value) {
            writer.WriteAttributeString(attributeName, value.ToStorageString());
        }

        public static void WriteAttributeString(this XmlWriter writer, string attributeName, float value) {
            writer.WriteAttributeString(attributeName, value.ToStorageString());
        }

    }
}
