using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Xml;

namespace NetPerf.Core {

    [DebuggerDisplay("TotalTime:{TotalExecutionTimeTaken} TotalWithoutChildrenTime:{TotalWithoutChildrenTime} TotalMethodTime:{TotalMethodTime} TimingElements:{TimingElements.Count}")]
    public class TimingDataInformation {

        /// <summary>
        /// The total time taken from the first event to the last event
        /// </summary>
        public long TotalExecutionTimeTaken {
            get;
            set;
        }

        /// <summary>
        /// The toal time recorded by children in a tree. (will be larger than the sum currently)
        /// </summary>
        public long TotalChildTime {
            get;
            set;
        }

        /// <summary>
        /// The toal time recorded by methods being tracked.
        /// </summary>
        public long TotalMethodTime {
            get;
            set;
        }

        /// <summary>
        /// The toal time recorded by children in a tree. (will be larger than the sum currently)
        /// </summary>
        public long TotalSumChildTime {
            get;
            set;
        }

        /// <summary>
        /// The toal time recorded by methods being tracked.
        /// </summary>
        public long TotalSumMethodTime {
            get;
            set;
        }

        /// <summary>
        /// The toal time recorded by methods being tracked.
        /// </summary>
        public long TotalTimeWithChildren {
            get {
                return TotalMethodTime + TotalChildTime;
            }
        }

        public List<TimingData> TimingElements {
            get;
            private set;
        }

        public TimingDataInformation() {
            TimingElements = new List<TimingData>();
        }

        /// <summary>
        /// Base implementation of the Timing Report
        /// </summary>
        /// <param name="output">The output stream</param>
        public string ToXmlReport(IInstrumentationDataMapAndInstances map) {
            using (var ms = new MemoryStream()) {
                ToXmlReport(map, ms);
                ms.Position = 0;
                using (var sr = new StreamReader(ms)) {
                    return sr.ReadToEnd();
                }
            }
        }

        /// <summary>
        /// Base implementation of the Timing Report
        /// </summary>
        /// <param name="output">The output stream</param>
        public void ToXmlReport(IInstrumentationDataMapAndInstances map, Stream output) {
            if (output == null) {
                throw new ArgumentNullException("output");
            }

            //we use this because for every parent it has all the child that also have its full calculation
            //this causes a very large number somewhere around 3x of the original number if there is a deep graph.
            //we ended up with .999994 due to using only 6 dec places. This is close enough to 1.0 (100%)
            var totalTimeSum = (double)(this.TotalSumMethodTime + this.TotalSumChildTime);
            var totalSumMethodTimeDouble = (double)this.TotalSumMethodTime;
            var totalSumChildTimeDouble = (double)this.TotalSumChildTime;


            var totalTime = (double)this.TotalTimeWithChildren;
            var totalMethodTime = ((double)(this.TotalMethodTime * 100) / totalTime).ToString("F4");
            var totalChildTime = ((double)(this.TotalChildTime * 100) / totalTime).ToString("F4");
            //the math.max will make sure we don't fall below 0 if by some chance the total time is ~ 100%
            var totalOverhead = "0";
            //if this is zero, something blew up but we can't stop from reporting the overhead.
            if (TotalExecutionTimeTaken > 0) {
                Math.Max((double)0, ((1 - ((double)this.TotalTimeWithChildren / (double)this.TotalExecutionTimeTaken)) * 100)).ToString("F4");
            }
            
            //JIT and timing overhead can be calculated by this number.
            //I guess it depends on how you want this number. TotalExecutionTimeTaken is from the first mark till the last
            //and TotalTimeWithChildren is the total time taken within methods that are tracked. Other items calling us
            //could change this number or jit time or overhead of the app itself.
            //1 - ((double)timing.TotalTimeWithChildren  / (double)timing.TotalExecutionTimeTaken)	0.11711028109702715	double

            var settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.IndentChars = "    ";
            settings.NewLineChars = Environment.NewLine;

            var xw = XmlTextWriter.Create(output, settings);

            xw.WriteStartDocument();
            xw.WriteStartElement("timingdata");
            xw.WriteAttributeString("fileSchemaType", "Free");
            xw.WriteAttributeString("fileSchemaVersion", "1");
            xw.WriteAttributeString("generatedTimeDate", DateTime.Now.ToString());
            xw.WriteAttributeString("generatedTimeTicks", DateTime.Now.Ticks.ToString());
            xw.WriteAttributeString("profilerVersion", VersionInformation.GetFileVersion(System.Reflection.Assembly.GetExecutingAssembly()));

            var sb = new StringBuilder();
            sb.AppendLine(string.Empty);
            sb.Append("DotNetPerf Timing Xml Data File v");
            sb.AppendLine(VersionInformation.GetFileVersion(System.Reflection.Assembly.GetExecutingAssembly()));
            sb.AppendLine(VersionInformation.Copyright(System.Reflection.Assembly.GetExecutingAssembly()));
            sb.AppendLine("http://www.dotnetperf.com or follow @dotnetperf for more information.");

            xw.WriteComment(sb.ToString());

            OutputProcessInformation(map, xw);

            OutputThreadInformation(map, xw);

            OutputAssemblyInformation(map, xw);

            OutputModuleInformation(map, xw);

            OutputTypeInformation(map, TimingElements, totalTimeSum, totalSumMethodTimeDouble, totalSumChildTimeDouble, xw);

            OutputFileInformation(map, TimingElements, totalTimeSum, totalSumMethodTimeDouble, totalSumChildTimeDouble, xw);

            ProcessMethodInformation(map, totalTimeSum, totalSumMethodTimeDouble, totalSumChildTimeDouble, totalMethodTime, totalChildTime, totalOverhead, xw);

            xw.WriteEndElement(); //timingdata
            xw.WriteEndDocument();

            xw.Flush();

        }

        private static void OutputAssemblyInformation(IInstrumentationDataMapAndInstances map, XmlWriter xw) {
            xw.WriteStartElement("assemblies");

            foreach (var instance in map.KnownInstances()) {
                if (instance.Value != null && instance.Value.assemblies != null) {
                    foreach (var assembly in instance.Value.assemblies) {
                        xw.WriteStartElement("assembly");
                        xw.WriteAttributeString("assemblyId", assembly.id);
                        xw.WriteAttributeString("assemblyIdentity", assembly.identity);
                        xw.WriteAttributeString("preinstrumentHash", assembly.preInstrumentedHash);
                        xw.WriteAttributeString("preinstrumentHashAlgorithm", assembly.preInstrumentedHashAlgorithm);
                        xw.WriteEndElement(); //assembly
                    }
                }
            }

            xw.WriteEndElement(); //assemblies
        }

        private static void OutputModuleInformation(IInstrumentationDataMapAndInstances map, XmlWriter xw) {
            xw.WriteStartElement("modules");

            foreach (var instance in map.KnownInstances()) {
                if (instance.Value != null && instance.Value.assemblies != null) {
                    foreach (var assembly in instance.Value.assemblies) {
                        foreach (var module in assembly.modules) {
                            xw.WriteStartElement("module");
                            xw.WriteAttributeString("assemblyId", assembly.id);
                            xw.WriteAttributeString("moduleId", module.id);
                            xw.WriteAttributeString("moduleName", module.name);
                            xw.WriteEndElement(); //module
                        }
                    }
                }
            }

            xw.WriteEndElement(); //modules
        }

        private static void OutputThreadInformation(IInstrumentationDataMapAndInstances map, XmlWriter xw) {
            xw.WriteStartElement("threads");

            xw.WriteComment(@"Note: threadId is not unique even to a process. 
For example: ThreadPool reuses the same thread to execute many items.
Even if you spin up your own threads, the id may be reused. Use named threads for better tracking.");

            foreach (var thread in map.ThreadList()) {
                xw.WriteStartElement("thread");
                xw.WriteAttributeString("processId", thread.ProcessId);
                xw.WriteAttributeString("threadId", thread.ThreadId);
                xw.WriteAttributeString("threadName", thread.ThreadName);
                //xw.WriteAttributeString("timeStamp", process.TimeStamp.ToString());
                xw.WriteEndElement();
            }

            xw.WriteEndElement(); //threads
        }

        private static void OutputProcessInformation(IInstrumentationDataMapAndInstances map, XmlWriter xw) {
            xw.WriteStartElement("processes");

            foreach (var process in map.ProcessList()) {
                xw.WriteStartElement("process");
                xw.WriteAttributeString("machineName", process.MachineName);
                xw.WriteAttributeString("processId", process.ProcessId);
                xw.WriteAttributeString("processName", process.ProcessName);
                xw.WriteAttributeString("ticksPerSecond", process.TicksPerSecond);
                //xw.WriteAttributeString("timeStamp", process.TimeStamp.ToString());
                xw.WriteEndElement();
            }

            xw.WriteEndElement(); //processes
        }

        private static void OutputFileInformation(IInstrumentationDataMapAndInstances map, List<TimingData> timingElements, double totalTimeSum,
            double totalSumMethodTimeDouble, double totalSumChildTimeDouble, XmlWriter xw) {
            xw.WriteStartElement("files");
            xw.WriteComment(@"Note: Not all methods of a type belong to a document so the % may not equal 100%.
In the future we will attempt to auto discover the document that the method belongs.
Example items are dynamically created ctors and automatic properties.");

            foreach (var instance in map.KnownInstances()) {
                if (instance.Value != null && instance.Value.assemblies != null) {
                    foreach (var assembly in instance.Value.assemblies) {
                        foreach (var module in assembly.modules) {
                            if (module.sourceFiles == null) {
                                continue;
                            }
                            //get the list of file id's that exist for the module.
                            var fileIdDictionary = (from type in module.types
                                                    from method in type.methods
                                                    where method.sourceFileIds != null
                                                    select new {
                                                        Method = method,
                                                        List = method.sourceFileIds.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                                    }).ToList();

                            foreach (var file in module.sourceFiles) {
                                xw.WriteStartElement("file");
                                xw.WriteAttributeString("fileId", file.id);
                                xw.WriteAttributeString("language", file.language);
                                xw.WriteAttributeString("moduleId", module.id);
                                xw.WriteAttributeString("uri", file.uri);
                                xw.WriteAttributeString("visited", "1"); //allow us to add all of the metadata later if we decide to keep everything

                                var fileIdString = file.id.ToString();
                                var validMethodIds = (from dict in fileIdDictionary
                                                      from a in dict.List
                                                      where a == fileIdString
                                                      select dict.Method.id).ToList();

                                //get all of the methods for the document
                                //yes there is a better way to select but we are looking for any item that has a fileId in the array.
                                //we could take the list and build a dictionary from it but this is close enough.
                                var fileMethods = timingElements.Where(item => item.ModuleId == module.id
                                                                        && validMethodIds.Any(m => m == item.MethodId)).ToList();

                                xw.WriteAttributeString("childPercent", fileMethods.Sum(item => item.ChildPercent(totalSumChildTimeDouble)).ToString("F4"));
                                xw.WriteAttributeString("methodPercent", fileMethods.Sum(item => item.MethodPercent(totalSumMethodTimeDouble)).ToString("F4"));
                                //xw.WriteAttributeString("totalPercent", fileMethods.Sum(item => item.TotalPercent(totalTimeSum)).ToString("F4"));

                                xw.WriteEndElement(); //file
                            }
                        }
                    }
                }
            }

            xw.WriteEndElement(); //files
        }

        private static void OutputTypeInformation(IInstrumentationDataMapAndInstances map, List<TimingData> timingElements, double totalTimeSum,
            double totalSumMethodTimeDouble, double totalSumChildTimeDouble, XmlWriter xw) {
            xw.WriteStartElement("types");

            foreach (var instance in map.KnownInstances()) {
                if (instance.Value != null && instance.Value.assemblies != null) {
                    foreach (var assembly in instance.Value.assemblies) {
                        foreach (var module in assembly.modules) {
                            foreach (var type in module.types) {
                                xw.WriteStartElement("type");
                                xw.WriteAttributeString("moduleId", module.id);
                                xw.WriteAttributeString("typeId", type.id);
                                xw.WriteAttributeString("typeSignature", type.signature);
                                var nameSpace = string.Empty;
                                var name = string.Empty;
                                try {
                                    var lastIndex = type.signature.LastIndexOf('.');
                                    if (lastIndex > -1) {
                                        nameSpace = type.signature.Substring(0, lastIndex);
                                        name = type.signature.Substring(lastIndex + 1);
                                    }
                                    else {
                                        name = type.signature; //no namespace
                                    }
                                }
                                catch (Exception ex) {
                                    Debug.WriteLine(string.Format("Error Parsing Type For NameSpace {0} Reason:{1}", type.signature, ex.ToString()));
                                }
                                xw.WriteAttributeString("typeName", name);
                                xw.WriteAttributeString("typeNameSpace", nameSpace);
                                xw.WriteAttributeString("visited", "1"); //allow us to add all of the metadata later if we decide to keep everything

                                //Calculate how much time was spent on the type

                                //get all of the methods for the type
                                var typeMethods = timingElements.Where(item => item.ModuleId == module.id
                                                                        && type.methods.Any(tm => tm.id == item.MethodId)).ToList();

                                xw.WriteAttributeString("childPercent", typeMethods.Sum(item => item.ChildPercent(totalSumChildTimeDouble)).ToString("F4"));
                                xw.WriteAttributeString("methodPercent", typeMethods.Sum(item => item.MethodPercent(totalSumMethodTimeDouble)).ToString("F4"));
                                //xw.WriteAttributeString("totalPercent", typeMethods.Sum(item => item.TotalPercent(totalTimeSum)).ToString("F4"));

                                xw.WriteEndElement(); //type
                            }
                        }
                    }
                }
            }

            xw.WriteEndElement(); //types
        }

        private void ProcessMethodInformation(IInstrumentationDataMapAndInstances map, double totalTimeSum, double totalSumMethodTimeDouble,
            double totalSumChildTimeDouble, string totalMethodTime, string totalChildTime, string totalOverhead, XmlWriter xw) {
            xw.WriteStartElement("methods");

            //Do not output the time, it can be used to calculate the wall clock. NOT FOR FREE.
            //xw.WriteAttributeString("totalTime", totalTime);
            xw.WriteAttributeString("totalMethodPercent", totalMethodTime);
            xw.WriteAttributeString("totalChildPercent", totalChildTime);
            xw.WriteAttributeString("nonInstrumented", totalOverhead);

            xw.WriteComment(@"Note: The key you must select on for types is moduleId and typeId.
The key you must select on for Files is moduleId and sourceFileIds.
The CLR spec states a method may exist in multiple source files and therefore they may be "";"" separated. Example ""13;4"".
This is rare and as of v4, the c sharp and vb.net compiler are not able to produce these methods.");

            //see if we have the same file in two processes combined.

            //foreach (var time in TimingElements) {
            //    //Method time is not a sum of the children so we use the actual number,
            //    //but for children, the number includes the children and the number is larger than the actual time
            //    //so we need to ensure we perform the math on the correct elements.
            //    time.WriteTimingData(map, xw, totalTimeSum, totalSumMethodTimeDouble, totalSumChildTimeDouble); //because the total is larger than the original number do the duplicate values.
            //}

            //we do not want to report on the data per process in the free edition.
            //so we are going to group the information by methodId

            var methodGroups = TimingElements.GroupBy<TimingData, int>(item => item.MethodId);

            foreach (var methodGroup in methodGroups) {
                //grab the first item so we have one to go by;
                var methodItem = methodGroup.First();

                xw.WriteStartElement("method");

                var method = map.InstrumentDataForMethod(methodItem.ModuleId, methodItem.MethodId);

                if (method != null) {
                    xw.WriteAttributeString("typeId", method.Type.id);
                    //xw.WriteAttributeString("typeSignature", method.Type.signature);

                    xw.WriteAttributeString("methodSignature", method.Method.signature);
                    var parts = method.Method.signature.Split(new string[] { "::" }, StringSplitOptions.RemoveEmptyEntries);
                    var methodNameWithParams = string.Empty;
                    var methodName = string.Empty;
                    if (parts.Length == 2) {
                        methodNameWithParams = parts[1];
                        try {
                            var parenIndex = methodNameWithParams.IndexOf('(');
                            if (parenIndex > -1) {
                                methodName = methodNameWithParams.Substring(0, parenIndex);
                            }
                            else {
                                methodName = methodNameWithParams;
                            }
                        }
                        catch (Exception ex) {
                            Debug.WriteLine(string.Format("Error Parsing Method Signature {0} Reason:{1}", methodNameWithParams, ex.ToString()));
                        }

                    }
                    //xw.WriteAttributeString("name", parts[0]);
                    xw.WriteAttributeString("methodName", methodName);
                    xw.WriteAttributeString("methodNameWithParams", methodNameWithParams);

                    var fileIds = method.Method.sourceFileIds ?? string.Empty;
                    //we need to do this because of the .net serializer, If you are not in the us, numbers may use , for a .
                    //and if we do that, it will come back as a float and not a string.
                    if (fileIds.IndexOf(",") > -1) {
                        fileIds = fileIds.Replace(',', ';');
                    }
                    xw.WriteAttributeString("sourceFileIds", fileIds);
                }
                else {
                    //if you get here, there is an issue with the xml being sent to the client.
                    xw.WriteAttributeString("typeId", string.Empty);
                    //xw.WriteAttributeString("typeSignature", string.Empty);

                    xw.WriteAttributeString("methodSignature", string.Empty);
                    xw.WriteAttributeString("methodName", string.Empty);
                    xw.WriteAttributeString("methodNameWithParams", string.Empty);
                    xw.WriteAttributeString("sourceFileIds", string.Empty);
                }

                xw.WriteAttributeString("methodId", methodItem.MethodId);
                xw.WriteAttributeString("moduleId", methodItem.ModuleId);
                xw.WriteAttributeString("visited", "1"); //allow us to add all of the metadata later if we decide to keep everything

                xw.WriteAttributeString("processId", "0"); //the number for every process.
                xw.WriteAttributeString("childPercent", methodGroup.Sum(item => item.ChildPercent(totalSumChildTimeDouble)).ToString("F4"));
                xw.WriteAttributeString("methodPercent", methodGroup.Sum(item => item.MethodPercent(totalSumMethodTimeDouble)).ToString("F4"));
                //xw.WriteAttributeString("totalPercent", methodGroup.Sum(item => item.TotalPercent(totalTimeSum)).ToString("F4"));

                //xw.WriteAttributeString("processId", ProcessId);
                //xw.WriteAttributeString("childPercent", ChildPercent(childTime).ToString("F4"));
                //xw.WriteAttributeString("methodPercent", MethodPercent(methodTime).ToString("F4"));
                //xw.WriteAttributeString("totalPercent", TotalPercent(totalTime).ToString("F4"));

                xw.WriteEndElement(); //method

            } //methodGroups


            xw.WriteEndElement(); //methods
        }

    }
}
