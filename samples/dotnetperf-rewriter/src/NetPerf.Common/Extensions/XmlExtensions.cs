using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NetPerf.Common.Configuration;

namespace System.Xml {
	public static class XmlExtensions {

		public static bool IsEndElement(this XmlReader reader, string elementName) {
			return !reader.EOF && (reader.IsEmptyElement || (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == elementName));
		}

		public static bool IsWithinElement(this XmlReader reader, string elementName) {
			return !reader.EOF && !(reader.NodeType == XmlNodeType.EndElement && reader.LocalName == elementName);
		}

		public static int ReadAttributeAsInt(this XmlReader reader) {
			return int.Parse(reader.Value, CultureInfo.InvariantCulture);
		}

		public static T ReadElementAsEnum<T>(this XmlReader reader) where T : struct {
			if (!typeof(T).IsEnum)
				throw new ArgumentException("The generic type must be an enumeration type.", "T");

			var val = reader.ReadElementContentAsString();

			return (T)Enum.Parse(typeof(T), val);
		}

		public static TElement ReadElement<TElement>(this XmlReader reader, string expectedElementName,
			Func<PropertyBag, TElement> constructObject,
			Action<XmlReader, string, TElement> readChildElement = null,
			Action<PropertyBag, XmlReader, string> readAttribute = null) //
		{
			var elementName = reader.LocalName;
			if (!elementName.IgnoreCaseEq(expectedElementName))
				throw new XmlException(string.Format(
					"Unexpected element name: '{0}'. Expected: '{1}'.",
					elementName,
					expectedElementName));

			readAttribute = readAttribute
				?? new Action<PropertyBag, XmlReader, string>((pbag, rdr, attributeName) => {
					pbag[attributeName] = rdr.Value;
				});


			var isEmpty = reader.IsEmptyElement;
			var bag = new PropertyBag();
			while (reader.MoveToNextAttribute()) {
				readAttribute(bag, reader, reader.LocalName);
			}

			var elem = constructObject(bag);
			if (isEmpty) {
				reader.ReadStartElement();
				return elem;
			}

			reader.ReadStartElement();

			if (readChildElement != null) {
				while (reader.IsWithinElement(elementName)) {
					readChildElement(reader, reader.LocalName, elem);
				}
			}
			if (reader.IsEndElement(elementName))
				reader.ReadEndElement();

			return elem;
		}

		public static IList<T> ReadElementList<T>(this XmlReader reader, Func<XmlReader, T> listItemReader) {
			var elementName = reader.LocalName;

			var items = new List<T>();
			if (reader.IsEmptyElement) {
				reader.ReadStartElement();
				return items;
			}

			reader.ReadStartElement();

			while (reader.IsWithinElement(elementName)) {
				var nextItem = listItemReader(reader);
				items.Add(nextItem);
			}

			if (!reader.EOF)
				reader.ReadEndElement();

			return items;
		}

		public static void WriteElementList<T>(this XmlWriter writer, IEnumerable<T> items, string elementName, Action<XmlWriter, T> listItemWriter) {

			if (items.IsNotEmpty()) {

				writer.WriteStartElement(elementName);

				foreach (var item in items) {
					listItemWriter(writer, item);
				}

				writer.WriteEndElement();
			}
		}
	}
}
