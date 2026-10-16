using System;
using System.Linq;
using System.Xml;
using System.Diagnostics.Contracts;
using System.Collections.Generic;
using System.IO;

namespace NetPerf.Common.Instrumentation {

	[Pure]
	public static class InstrumentationDataSerializer {

		const string CustomAttributes = "customAttributes";
		const string CustomAttribute = "customAttribute";
		const string RootElementName = "InstrumentationData";
		const string GenericArguments = "genericArguments";
		const string GenericArgument = "genericArgument";
		public const int CurrentVersion = 1;

		#region read methods

		/// <summary>
		/// Unlike the 'IXmlSerializable.ReadXml' method, this one reads the root element.
		/// </summary>
		/// <param name="reader"></param>
		/// <returns></returns>
		public static ErrorTuple<IInstrumentationData> Read(FilePath fromFile) {
			Contract.Requires(fromFile != null, "fromFile");

			Contract.Ensures(Contract.Result<ErrorTuple<IInstrumentationData>>() != null);

			return ExceptionHelper.WrapFileOperation<IInstrumentationData>(() => {
				var sett = new XmlReaderSettings() {
					IgnoreComments = true,
					IgnoreWhitespace = true
				};

				using (var reader = XmlReader.Create(fromFile, sett)) {
					reader.MoveToContent();
					return Read(reader);
				}
			});
		}

		/// <summary>
		/// Unlike the 'IXmlSerializable.ReadXml' method, this one reads the root element.
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		public static ErrorTuple<IInstrumentationData> Read(string data) {
			Contract.Requires(data != null, "data");

			Contract.Ensures(Contract.Result<ErrorTuple<IInstrumentationData>>() != null);

			return ExceptionHelper.WrapFileOperation<IInstrumentationData>(() => {
				var sett = new XmlReaderSettings() {
					IgnoreComments = true,
					IgnoreWhitespace = true
				};
				using (StringReader sr = new StringReader(data)) {
					using (var reader = XmlReader.Create(sr, sett)) {
						reader.MoveToContent();
						return Read(reader);
					}
				}
			});
		}

		private static IInstrumentationData Read(XmlReader reader) {
			Contract.Requires(reader != null, "reader");
			Contract.Ensures(Contract.Result<IInstrumentationData>() != null);

			var elementName = reader.LocalName;
			if (!elementName.IgnoreCaseEq(RootElementName))
				throw new XmlException("Cannot read files of type '" + elementName ?? "NULL" + "'");

			var version = -1;
			var instanceHash = string.Empty;
			while (reader.MoveToNextAttribute()) {
				if (reader.LocalName == "version") {
					version = reader.ReadAttributeAsInt();
				}
				if (reader.LocalName == "instanceHash") {
					instanceHash = reader.Value;
				}
			}

			if (version < 1)
				throw new XmlException("Missing or invalid file version. Unable to parse files without a file version.");

			if (version > CurrentVersion)
				throw new XmlException("Unable to read files later than version " + CurrentVersion);

			reader.ReadStartElement();

			var data = new InstrumentationData() {
				InstanceHash = instanceHash
			};

			while (reader.IsWithinElement(elementName)) {
				switch (reader.LocalName) {
					case "assemblies":
						var list = reader.ReadElementList(ReadAssemblyRecord);
						data.LinkAssemblies(list);
						break;
					default:
						throw ExceptionHelper.NewUnrecognizedXmlElement(elementName, reader.LocalName);
				}
			}

			if (reader.IsEndElement(RootElementName))
				reader.ReadEndElement();

			return data;
		}

		private static IAssemblyRecord ReadAssemblyRecord(XmlReader reader) {
			Contract.Requires(reader != null, "reader");
			Contract.Ensures(Contract.Result<IAssemblyRecord>() != null);

			return reader.ReadElement("assembly",
				(bag) => {
					return new AssemblyRecord(
						id: UniqueIdGenerator.NextId(typeof(AssemblyRecord)),
						identity: (string)bag["identity"],
						preInstrumentedHash: (string)bag["preInstrumentedHash"],
						preInstrumentedHashAlgorithm: HashAlgorithm.FromString((string)bag["preInstrumentedHashAlgorithm"]));
				},
				(rdr, elementName, assemblyRec) => {
					switch (elementName) {
						case "modules":
							var modules = reader.ReadElementList(r => ReadModuleRecord(r, assemblyRec.Id));
							assemblyRec.LinkModules(modules);
							break;
						default:
							throw ExceptionHelper.NewUnrecognizedXmlElement(elementName, reader.LocalName);
					}
				});
		}

		private static IModuleRecord ReadModuleRecord(XmlReader reader, int assemblyId) {
			Contract.Requires(reader != null, "reader");
			Contract.Ensures(Contract.Result<IModuleRecord>() != null);

			Contract.Assert(assemblyId > 0);

			return reader.ReadElement("module",
				(bag) => {
					return new ModuleRecord(
						id: int.Parse((string)bag["id"]),
						assemblyId: assemblyId,
						name: (string)bag["name"]);
				},
				(rdr, elementName, moduleRec) => {
					switch (elementName) {
						case "sourceFiles":
							var files = rdr.ReadElementList(r => ReadSourceFileRecord(r, moduleRec.Id));
							moduleRec.LinkSourceFiles(files);
							break;
						case "types":
							var types = rdr.ReadElementList(r => ReadTypeRecord(r, moduleRec.Id));
							moduleRec.LinkTypes(types);
							break;
						default:
							throw ExceptionHelper.NewUnrecognizedXmlElement(elementName, reader.LocalName);
					}
				});
		}

		private static ISourceFileRecord ReadSourceFileRecord(XmlReader reader, int moduleId) {
			Contract.Requires(reader != null, "reader");
			Contract.Ensures(Contract.Result<ISourceFileRecord>() != null);
			Contract.Assert(moduleId > 0);

			return reader.ReadElement("sourceFile",
				(bag) => {
					return new SourceFileRecord(
						((string)bag["id"]).ParseStorageString(),
						moduleId,
						(string)bag["uri"],
						(string)bag["hash"],
						(string)bag["hashAlgorithm"],
						(string)bag["language"]);
				});
		}

		private static ITypeRecord ReadTypeRecord(XmlReader reader, int moduleId) {
			Contract.Requires(reader != null, "reader");
			Contract.Requires(moduleId > 0);
			Contract.Ensures(Contract.Result<ITypeRecord>() != null);

			return reader.ReadElement("type",
				(bag) => {
					return new TypeRecord(
						UniqueIdGenerator.NextId(typeof(TypeRecord)),
						moduleId,
						(string)bag["signature"],
						null);
				},
				(rdr, elementName, typeRec) => {
					switch (elementName) {
						case "methods":
							var methods = rdr.ReadElementList(r => ReadMethodRecord(r, typeRec.Id));
							typeRec.LinkMethods(methods);
							break;
						case CustomAttributes:
							typeRec.AddCustomAttributes(ReadCustomAttributeList(rdr));
							break;
						case GenericArguments:
							typeRec.AddGenericArguments(ReadGenericArgumentList(rdr));
							break;
						default:
							throw ExceptionHelper.NewUnrecognizedXmlElement(elementName, reader.LocalName);
					}
				});
		}

		private static IEventRecord ReadEventRecord(XmlReader reader, int typeId) {
			Contract.Requires(reader != null, "reader");
			Contract.Requires(typeId > 0);
			Contract.Ensures(Contract.Result<IEventRecord>() != null);

			return reader.ReadElement("event",
				(bag) => {
					var others = ((string)bag["others"]).SplitAndRemoveEmpty(';')
						.Select(str => str.ParseStorageString())
						.ToArray();

					return new EventRecord(
						UniqueIdGenerator.NextId(typeof(EventRecord)),
						typeId,
						(string)bag["signature"],
						bag.ContainsKey("add") ? (int?)((string)bag["add"]).ParseStorageString() : null,
						bag.ContainsKey("remove") ? (int?)((string)bag["remove"]).ParseStorageString() : null,
						bag.ContainsKey("fire") ? (int?)((string)bag["fire"]).ParseStorageString() : null,
						others);
				},
				(rdr, elementName, evntRec) => {
					if (elementName == CustomAttributes) {
						evntRec.AddCustomAttributes(ReadCustomAttributeList(rdr));
					}
				});
		}

		private static IMethodRecord ReadMethodRecord(XmlReader reader, int typeId) {
			Contract.Requires(reader != null, "reader");
			Contract.Requires(typeId > 0);
			Contract.Ensures(Contract.Result<IMethodRecord>() != null);

			return reader.ReadElement("method",
				(bag) => {

					var sourceFileIds = bag.ContainsKey("sourceFileIds") ? (string)bag["sourceFileIds"] : string.Empty;
					var sourceids = sourceFileIds.SplitAndRemoveEmpty(';')
						.Select(str => str.ParseStorageString())
						.ToArray();

					var hash = bag.ContainsKey("hash") ? (string)bag["hash"] : string.Empty;
					var hashAlgorithm = bag.ContainsKey("hashAlgorithm") ? (string)bag["hashAlgorithm"] : "Unknown";

					return new MethodRecord(
						((string)bag["id"]).ParseStorageString(),
						typeId,
						(string)bag["signature"],
						hash,
						HashAlgorithm.FromString(hashAlgorithm),
						null,
						sourceids);
				},
				(rdr, elementName, methRec) => {
					switch (elementName) {
						case CustomAttributes:
							methRec.AddCustomAttributes(ReadCustomAttributeList(rdr));
							break;
						case GenericArguments:
							methRec.AddGenericArguments(ReadGenericArgumentList(rdr));
							break;
						case "points":
							var points = reader.ReadElementList(r => ReadPointRecord(r, methRec.Id));
							methRec.LinkPoints(points);
							break;
					}
				});
		}

		private static IPointRecord ReadPointRecord(XmlReader reader, int methodId) {
			Contract.Requires(reader != null);
			Contract.Requires(methodId > 0);
			Contract.Ensures(Contract.Result<IPointRecord>() != null);

			return reader.ReadElement("point",
				(bag) => {
					return new PointRecord(
						((string)bag["id"]).ParseStorageString(),
						methodId,
						bag.ContainsKey("sourceFileId") ? ((string)bag["sourceFileId"]).ParseStorageString() : (int?)null,
						bag.ContainsKey("startLine") ? ((string)bag["startLine"]).ParseStorageString() : (int?)null,
						bag.ContainsKey("startColumn") ? ((string)bag["startColumn"]).ParseStorageString() : (int?)null,
						bag.ContainsKey("endLine") ? ((string)bag["endLine"]).ParseStorageString() : (int?)null,
						bag.ContainsKey("endColumn") ? ((string)bag["endColumn"]).ParseStorageString() : (int?)null
						);
				},
				(rdr, elementName, pointRec) => {
					return; // no child elements
				});

		}

		private static IPropertyRecord ReadPropertyRecord(XmlReader reader, int typeId) {
			Contract.Requires(reader != null, "reader");
			Contract.Requires(typeId > 0);
			Contract.Ensures(Contract.Result<IPropertyRecord>() != null);

			return reader.ReadElement("property",
				(bag) => {
					var others = ((string)bag["others"]).SplitAndRemoveEmpty(';')
						.Select(str => str.ParseStorageString())
						.ToArray();

					return new PropertyRecord(
						UniqueIdGenerator.NextId(typeof(PropertyRecord)),
						typeId,
						(string)bag["signature"],
						bag.ContainsKey("get") ? (int?)((string)bag["get"]).ParseStorageString() : null,
						bag.ContainsKey("set") ? (int?)((string)bag["set"]).ParseStorageString() : null,
						others);
				},
				(rdr, elementName, propRec) => {
					if (elementName == CustomAttributes) {
						propRec.AddCustomAttributes(ReadCustomAttributeList(rdr));
					}
				});
		}

		private static IList<string> ReadCustomAttributeList(XmlReader reader) {
			Contract.Requires(reader != null, "reader");
			return reader.ReadElementList(r => r.ReadElementString(CustomAttribute));
		}

		private static IList<string> ReadGenericArgumentList(XmlReader reader) {
			Contract.Requires(reader != null, "reader");
			return reader.ReadElementList(r => r.ReadElementString(GenericArgument));
		}

		#endregion read methods

		#region write methods

		/// <summary>
		/// Unlike the IXmlSerializable.WriteXml method, this method writes the root element.
		/// </summary>
		public static ErrorTuple Write(FilePath toFile, IInstrumentationData data) {
			Contract.Requires(toFile != null, "toFile");
			Contract.Requires(data != null, "data");

			return ExceptionHelper.WrapFileOperation(() => {
				var sett = new XmlWriterSettings() {
					CloseOutput = true,
					Indent = true,
					IndentChars = " ",
					OmitXmlDeclaration = false,
					CheckCharacters = true
				};

				if (!DirectoryStub.Exists(toFile.ParentDirectory)) {
					DirectoryStub.CreateDirectory(toFile.ParentDirectory);
				}

				using (var writer = XmlWriter.Create(toFile, sett)) {
					Write(writer, data);
				}
			});
		}

		/// <summary>
		/// Unlike the IXmlSerializable.WriteXml method, this method writes the root element.
		/// </summary>
		/// <param name="writer"></param>
		public static ErrorTuple WriteAsString(out string value, IInstrumentationData data) {
			Contract.Requires(data != null, "data");

			var tempVal = string.Empty;

			var retVal = ExceptionHelper.WrapFileOperation(() => {
				var sett = new XmlWriterSettings() {
					CloseOutput = false,
					Indent = false,
					OmitXmlDeclaration = true,
					CheckCharacters = true
				};

				using (var ms = new MemoryStream()) {
					using (var writer = XmlWriter.Create(ms, sett)) {
						Write(writer, data);
					}
					ms.Position = 0;
					using (StreamReader sr = new StreamReader(ms)) {
						tempVal = sr.ReadToEnd();
					}
				}
			});
			value = tempVal; //lamba issue
			return retVal;
		}

		private static void Write(XmlWriter writer, IInstrumentationData data) {
			Contract.Requires(writer != null, "writer");
			Contract.Requires(data != null, "data");

			writer.WriteStartElement(RootElementName);
			writer.WriteAttributeString("version", CurrentVersion);
			writer.WriteAttributeString("instanceHash", data.InstanceHash);

			// bug #24 - we only want to serialize assemblies that have modules
			var assembliesToWrite = data.Assemblies.Where(a => a.Modules.IsNotEmpty());
			writer.WriteElementList(assembliesToWrite, "assemblies", WriteAssemblyRecord);

			writer.WriteEndElement();
		}

		private static void WriteAssemblyRecord(XmlWriter writer, IAssemblyRecord record) {
			Contract.Requires(writer != null, "writer");
			Contract.Requires(record != null, "record");

			writer.WriteStartElement("assembly");
			writer.WriteAttributeString("preInstrumentedHash", record.PreInstrumentedHash);
			writer.WriteAttributeString("preInstrumentedHashAlgorithm", record.PreInstrumentedHashAlgorithm.ToString());
			writer.WriteAttributeString("identity", record.Signature);

			// bug #24 - we only want to serialize modules that have types
			var modulesToWrite = record.Modules.Where(m => m.Types.IsNotEmpty() || m.SourceFiles.IsNotEmpty());
			writer.WriteElementList(modulesToWrite, "modules", WriteModuleRecord);

			writer.WriteEndElement();
		}

		private static void WriteModuleRecord(XmlWriter writer, IModuleRecord record) {
			Contract.Requires(writer != null, "writer");
			Contract.Requires(record != null, "record");

			writer.WriteStartElement("module");
			writer.WriteAttributeString("id", record.Id);
			writer.WriteAttributeString("name", record.Signature);

			writer.WriteElementList(record.SourceFiles, "sourceFiles", WriteSourceFileRecord);
			
			// bug #24 - we only want to serialize types that have methods or properties or events
			var typesToWrite = record.Types.Where(t => t.Methods.IsNotEmpty() || t.Properties.IsNotEmpty() || t.Events.IsNotEmpty());
			writer.WriteElementList(typesToWrite, "types", WriteTypeRecord);

			writer.WriteEndElement();
		}

		private static void WriteSourceFileRecord(XmlWriter writer, ISourceFileRecord record) {
			Contract.Requires(writer != null, "writer");
			Contract.Requires(record != null, "record");

			writer.WriteStartElement("sourceFile");
			writer.WriteAttributeString("id", record.Id.ToStorageString());
			writer.WriteAttributeString("uri", record.Signature);
			writer.WriteAttributeString("hash", record.Hash);
			writer.WriteAttributeString("hashAlgorithm", record.HashAlgorithm);
			writer.WriteAttributeString("language", record.SourceLanguage);

			writer.WriteEndElement();
		}

		private static void WriteTypeRecord(XmlWriter writer, ITypeRecord record) {
			Contract.Requires(writer != null, "writer");
			Contract.Requires(record != null, "record");

			writer.WriteStartElement("type");
			writer.WriteAttributeString("signature", record.Signature);

			WriteCustomAttributeList(writer, record.CustomAttributes);

			WriteGenericArgumentList(writer, record.GenericArguments);

			writer.WriteElementList(record.Methods, "methods", WriteMethodRecord);

			writer.WriteEndElement();
		}

		private static void WriteEventRecord(XmlWriter writer, IEventRecord record) {
			Contract.Requires(writer != null, "writer");
			Contract.Requires(record != null, "record");

			writer.WriteStartElement("event");
			writer.WriteAttributeString("signature", record.Signature);

			if (record.AddMethodId.HasValue) {
				writer.WriteAttributeString("add", record.AddMethodId.Value.ToStorageString());
			}

			if (record.RemoveMethodId.HasValue) {
				writer.WriteAttributeString("remove", record.RemoveMethodId.Value.ToStorageString());
			}

			if (record.FireMethodId.HasValue) {
				writer.WriteAttributeString("fire", record.FireMethodId.Value.ToStorageString());
			}

			var otherMethodIdStr = string.Join(";", record.OtherMethodIds.Select(id => id.ToStorageString()).ToArray());
			writer.WriteAttributeString("others", otherMethodIdStr);

			WriteCustomAttributeList(writer, record.CustomAttributes);

			writer.WriteEndElement();
		}

		private static void WriteMethodRecord(XmlWriter writer, IMethodRecord record) {
			Contract.Requires(writer != null, "writer");
			Contract.Requires(record != null, "record");

			writer.WriteStartElement("method");
			writer.WriteAttributeString("id", record.Id.ToStorageString());
			writer.WriteAttributeString("signature", record.Signature);

			if (!string.IsNullOrEmpty(record.Hash)) {
				writer.WriteAttributeString("hash", record.Hash);
				writer.WriteAttributeString("hashAlgorithm", record.HashAlgorithm.ToString());
			}

			var sourceFileIdStr = string.Join(";", record.SourceFileIds.Select(id => id.ToStorageString()).ToArray());
			if (!string.IsNullOrEmpty(sourceFileIdStr)) {
				writer.WriteAttributeString("sourceFileIds", sourceFileIdStr);
			}

			WriteCustomAttributeList(writer, record.CustomAttributes);

			WriteGenericArgumentList(writer, record.GenericArguments);

			writer.WriteElementList(record.Points, "points", (wr, point) => WritePointRecord(wr, point));

			writer.WriteEndElement();
		}

		private static void WritePointRecord(XmlWriter writer, IPointRecord record) {
			Contract.Requires(writer != null);
			Contract.Requires(record != null);

			writer.WriteStartElement("point");

			writer.WriteAttributeString("id", record.Id.ToStorageString());

			if (record.SourceFileId != null) {
				writer.WriteAttributeString("sourceFileId", record.SourceFileId.Value.ToStorageString());
				writer.WriteAttributeString("startLine", record.StartLine.Value.ToStorageString());
				writer.WriteAttributeString("startColumn", record.StartColumn.Value.ToStorageString());
				writer.WriteAttributeString("endLine", record.EndLine.Value.ToStorageString());
				writer.WriteAttributeString("endColumn", record.EndColumn.Value.ToStorageString());
			}

			writer.WriteEndElement();
		}

		private static void WritePropertyRecord(XmlWriter writer, IPropertyRecord record) {
			Contract.Requires(writer != null, "writer");
			Contract.Requires(record != null, "record");

			writer.WriteStartElement("property");
			writer.WriteAttributeString("signature", record.Signature);

			if (record.GetMethodId.HasValue) {
				writer.WriteAttributeString("get", record.GetMethodId.Value.ToStorageString());
			}
			if (record.SetMethodId.HasValue) {
				writer.WriteAttributeString("set", record.SetMethodId.Value.ToStorageString());
			}

			var otherMethodIdStr = string.Join(";", record.OtherMethodIds.Select(id => id.ToStorageString()).ToArray());
			writer.WriteAttributeString("others", otherMethodIdStr);

			WriteCustomAttributeList(writer, record.CustomAttributes);

			writer.WriteEndElement();
		}

		private static void WriteCustomAttributeList(XmlWriter writer, IEnumerable<string> customAttributes) {
			writer.WriteElementList(customAttributes, CustomAttributes,
				(w, str) => w.WriteElementString(CustomAttribute, str));
		}

		private static void WriteGenericArgumentList(XmlWriter writer, IEnumerable<string> genericArguments) {
			writer.WriteElementList(genericArguments, GenericArguments,
				(w, str) => w.WriteElementString(GenericArgument, str));
		}

		#endregion write methods
	}
}
