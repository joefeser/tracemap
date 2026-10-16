using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NetPerf.Core.Tests.Unit {

	[TestClass]
	public class NamedPipeMessageSerializerTests {

		[TestMethod]
		public void NamedPipeMessageSerializerTests_OnNewProcess_RoundTrip() {

			var writer = new ByteArrayWriter(1000);
			writer.Write(int.MaxValue); //we need garbage to ensure we support non 0 start positions.
			var processor = new TestProcessor();

			var ticks = 2557743342124;
			var timestamp = 124124125235235;
			var processName = "This is a Process";
			var machineName = "This is my machine";

			NamedPipeMessageSerializer.OnNewProcess(writer, 12, ticks, timestamp, processName, machineName);

			//now see if we can get the message back

			var position = NamedPipeMessageSerializer.OnNewProcess(processor, 4, writer.Buffer(), writer.Position);

			Assert.IsNotNull(processor.NewProcess);
			Assert.AreEqual<int>(writer.Position, position);
			Assert.AreEqual<int>(12, processor.NewProcess.ProcessId);
			Assert.AreEqual<long>(ticks, processor.NewProcess.TicksPerSecond);
			Assert.AreEqual<long>(timestamp, processor.NewProcess.TimeStamp);
			Assert.AreEqual<string>(processName, processor.NewProcess.ProcessName);
			Assert.AreEqual<string>(machineName, processor.NewProcess.MachineName);
		}

		[TestMethod]
		public void NamedPipeMessageSerializerTests_OnNewThread_RoundTrip() {
			var writer = new ByteArrayWriter(100);
			writer.Write(int.MaxValue); //we need garbage to ensure we support non 0 start positions.
			var processor = new TestProcessor();

			var threadId = 255742124;
			var timestamp = 124124125235235;
			var threadName = "This is a Thread";

			NamedPipeMessageSerializer.OnNewThread(writer, 12, timestamp, threadId, threadName);

			//now see if we can get the message back

			var position = NamedPipeMessageSerializer.OnNewThread(processor, 4, writer.Buffer(), writer.Position);

			Assert.IsNotNull(processor.NewThread);
			Assert.AreEqual<int>(writer.Position, position);
			Assert.AreEqual<int>(12, processor.NewThread.ProcessId);
			Assert.AreEqual<int>(threadId, processor.NewThread.ThreadId);
			Assert.AreEqual<long>(timestamp, processor.NewThread.TimeStamp);
			Assert.AreEqual<string>(threadName, processor.NewThread.ThreadName);
		}

		[TestMethod]
		public void NamedPipeMessageSerializerTests_OnNewAssembly_RoundTrip() {
			var writer = new ByteArrayWriter(100);
			writer.Write(int.MaxValue); //we need garbage to ensure we support non 0 start positions.
			var processor = new TestProcessor();

			var timestamp = 124124125235235;
			var assemblyName = "ThisIsAnAssembly.dll";

			NamedPipeMessageSerializer.OnNewAssembly(writer, 12, timestamp, assemblyName);

			//now see if we can get the message back

			var position = NamedPipeMessageSerializer.OnNewAssembly(processor, 4, writer.Buffer(), writer.Position);

			Assert.IsNotNull(processor.NewAssembly);
			Assert.AreEqual<int>(writer.Position, position);
			Assert.AreEqual<int>(12, processor.NewAssembly.ProcessId);
			Assert.AreEqual<long>(timestamp, processor.NewAssembly.TimeStamp);
			Assert.AreEqual<string>(assemblyName, processor.NewAssembly.AssemblyIdentity);
		}

		[TestMethod]
		public void NamedPipeMessageSerializerTests_RegisterMetaData_RoundTrip() {
			var writer = new ByteArrayWriter(10000);
			writer.Write(int.MaxValue); //we need garbage to ensure we support non 0 start positions.
			var processor = new TestProcessor();

			var assemblyName = "ThisIsAnAssembly.dll";
			var ilMetaData = @"24124  qwkrl jasfkl jaf 09a fuas0 fua09sf 09asuf 0asu 0fas0f 09asas f9sf 09asf 09as 09as fas f sfas jas fsf jasf asl fja fas093rkn2l rwkf lakf 89a fasfasfklas lasfasfhasfsf
							24124  qwkrl jasfkl jaf 09a fuas0 fua09sf 09asuf 0asu 0fas0f 09asas f9sf 09asf 09as 09as fas ffas f sfas jas fsf jasf asl fja fas093rkn2l rwkf lakf 89a fasfasfklas lasfasfhasfsf
							24124  qwkrl jasfkl jaf 09a fuas0 fua09sf 09asuf 0asu 0fas0f 09asas f9sf 09asf 09as 09as fas ffas f sfas jas fsf jasf asl fja fas093rkn2l rwkf lakf 89a fasfasfklas lasfasfhasfsf
							24124  qwkrl jasfkl jaf 09a fuas0 fua09sf 09asuf 0asu 0fas0f 09asas f9sf 09asf 09as 09as fas ffas f sfas jas fsf jasf asl fja fas093rkn2l rwkf lakf 89a fasfasfklas lasfasfhasfsf
							24124  qwkrl jasfkl jaf 09a fuas0 fua09sf 09asuf 0asu 0fas0f 09asas f9sf 09asf 09as 09as fas ffas f sfas jas fsf jasf asl fja fas093rkn2l rwkf lakf 89a fasfasfklas lasfasfhasfsf
							24124  qwkrl jasfkl jaf 09a fuas0 fua09sf 09asuf 0asu 0fas0f 09asas f9sf 09asf 09as 09as fas ffas f sfas jas fsf jasf asl fja fas093rkn2l rwkf lakf 89a fasfasfklas lasfasfhasfsf
							24124  qwkrl jasfkl jaf 09a fuas0 fua09sf 09asuf 0asu 0fas0f 09asas f9sf 09asf 09as 09as fas ffas f sfas jas fsf jasf asl fja fas093rkn2l rwkf lakf 89a fasfasfklas lasfasfhasfsf
							24124  qwkrl jasfkl jaf 09a fuas0 fua09sf 09asuf 0asu 0fas0f 09asas f9sf 09asf 09as 09as fas ffas f sfas jas fsf jasf asl fja fas093rkn2l rwkf lakf 89a fasfasfklas lasfasfhasfsf
							24124  qwkrl jasfkl jaf 09a fuas0 fua09sf 09asuf 0asu 0fas0f 09asas f9sf 09asf 09as 09as fas ffas f sfas jas fsf jasf asl fja fas093rkn2l rwkf lakf 89a fasfasfklas lasfasfhasfsf
							24124  qwkrl jasfkl jaf 09a fuas0 fua09sf 09asuf 0asu 0fas0f 09asas f9sf 09asf 09as 09as fas ffas f sfas jas fsf jasf asl fja fas093rkn2l rwkf lakf 89a fasfasfklas lasfasfhasfsf
							24124  qwkrl jasfkl jaf 09a fuas0 fua09sf 09asuf 0asu 0fas0f 09asas f9sf 09asf 09as 09as fas ffas f sfas jas fsf jasf asl fja fas093rkn2l rwkf lakf 89a fasfasfklas lasfasfhasfsf
							24124  qwkrl jasfkl jaf 09a fuas0 fua09sf 09asuf 0asu 0fas0f 09asas f9sf 09asf 09as 09as fas ffas f sfas jas fsf jasf asl fja fas093rkn2l rwkf lakf 89a fasfasfklas lasfasfhasfsf
							24124  qwkrl jasfkl jaf 09a fuas0 fua09sf 09asuf 0asu 0fas0f 09asas f9sf 09asf 09as 09as fas ffas f sfas jas fsf jasf asl fja fas093rkn2l rwkf lakf 89a fasfasfklas lasfasfhasfsf
							24124  qwkrl jasfkl jaf 09a fuas0 fua09sf 09asuf 0asu 0fas0f 09asas f9sf 09asf 09as 09as fas ffas f sfas jas fsf jasf asl fja fas093rkn2l rwkf lakf 89a fasfasfklas lasfasfhasfsf
							24124  qwkrl jasfkl jaf 09a fuas0 fua09sf 09asuf 0asu 0fas0f 09asas f9sf 09asf 09as 09as fas ffas f sfas jas fsf jasf asl fja fas093rkn2l rwkf lakf 89a fasfasfklas lasfasfhasfsf
							24124  qwkrl jasfkl jaf 09a fuas0 fua09sf 09asuf 0asu 0fas0f 09asas f9sf 09asf 09as 09as fas ffas f sfas jas fsf jasf asl fja fas093rkn2l rwkf lakf 89a fasfasfklas lasfasfhasfsf
							24124  qwkrl jasfkl jaf 09a fuas0 fua09sf 09asuf 0asu 0fas0f 09asas f9sf 09asf 09as 09as fas ffas f sfas jas fsf jasf asl fja fas093rkn2l rwkf lakf 89a fasfasfklas lasfasfhasfsf
							24124  qwkrl jasfkl jaf 09a fuas0 fua09sf 09asuf 0asu 0fas0f 09asas f9sf 09asf 09as 09as fas ffas f sfas jas fsf jasf asl fja fas093rkn2l rwkf lakf 89a fasfasfklas lasfasfhasfsf
							24124  qwkrl jasfkl jaf 09a fuas0 fua09sf 09asuf 0asu 0fas0f 09asas f9sf 09asf 09as 09as fas ffas f sfas jas fsf jasf asl fja fas093rkn2l rwkf lakf 89a fasfasfklas lasfasfhasfsf
							24124  qwkrl jasfkl jaf 09a fuas0 fua09sf 09asuf 0asu 0fas0f 09asas f9sf 09asf 09as 09as fas ffas f sfas jas fsf jasf asl fja fas093rkn2l rwkf lakf 89a fasfasfklas lasfasfhasfsf
							24124  qwkrl jasfkl jaf 09a fuas0 fua09sf 09asuf 0asu 0fas0f 09asas f9sf 09asf 09as 09as fas ffas f sfas jas fsf jasf asl fja fas093rkn2l rwkf lakf 89a fasfasfklas lasfasfhasfsf
							24124  qwkrl jasfkl jaf 09a fuas0 fua09sf 09asuf 0asu 0fas0f 09asas f9sf 09asf 09as 09as fas ffas f sfas jas fsf jasf asl fja fas093rkn2l rwkf lakf 89a fasfasfklas lasfasfhasfsf
							24124  qwkrl jasfkl jaf 09a fuas0 fua09sf 09asuf 0asu 0fas0f 09asas f9sf 09asf 09as 09as fas ffas f sfas jas fsf jasf asl fja fas093rkn2l rwkf lakf 89a fasfasfklas lasfasfhasfsf
							24124  qwkrl jasfkl jaf 09a fuas0 fua09sf 09asuf 0asu 0fas0f 09asas f9sf 09asf 09as 09as fas ffas f sfas jas fsf jasf asl fja fas093rkn2l rwkf lakf 89a fasfasfklas lasfasfhasfsf
							24124  qwkrl jasfkl jaf 09a fuas0 fua09sf 09asuf 0asu 0fas0f 09asas f9sf 09asf 09as 09as fas ffas f sfas jas fsf jasf asl fja fas093rkn2l rwkf lakf 89a fasfasfklas lasfasfhasfsf
							24124  qwkrl jasfkl jaf 09a fuas0 fua09sf 09asuf 0asu 0fas0f 09asas f9sf 09asf 09as 09as fas ffas f sfas jas fsf jasf asl fja fas093rkn2l rwkf lakf 89a fasfasfklas lasfasfhasfsf
							24124  qwkrl jasfkl jaf 09a fuas0 fua09sf 09asuf 0asu 0fas0f 09asas f9sf 09asf 09as 09as fas ffas f sfas jas fsf jasf asl fja fas093rkn2l rwkf lakf 89a fasfasfklas lasfasfhasfsf
							24124  qwkrl jasfkl jaf 09a fuas0 fua09sf 09asuf 0asu 0fas0f 09asas f9sf 09asf 09as 09as fas ffas f sfas jas fsf jasf asl fja fas093rkn2l rwkf lakf 89a fasfasfklas lasfasfhasfsf
							24124  qwkrl jasfkl jaf 09a fuas0 fua09sf 09asuf 0asu 0fas0f 09asas f9sf 09asf 09as 09as fas ffas f sfas jas fsf jasf asl fja fas093rkn2l rwkf lakf 89a fasfasfklas lasfasfhasfsf
							24124  qwkrl jasfkl jaf 09a fuas0 fua09sf 09asuf 0asu 0fas0f 09asas f9sf 09asf 09as 09as fas ffas f sfas jas fsf jasf asl fja fas093rkn2l rwkf lakf 89a fasfasfklas lasfasfhasfsf
							24124  qwkrl jasfkl jaf 09a fuas0 fua09sf 09asuf 0asu 0fas0f 09asas f9sf 09asf 09as 09as fas ffas f sfas jas fsf jasf asl fja fas093rkn2l rwkf lakf 89a fasfasfklas lasfasfhasfsf
							24124  qwkrl jasfkl jaf 09a fuas0 fua09sf 09asuf 0asu 0fas0f 09asas f9sf 09asf 09as 09as fas ffas f sfas jas fsf jasf asl fja fas093rkn2l rwkf lakf 89a fasfasfklas lasfasfhasfsf
							24124  qwkrl jasfkl jaf 09a fuas0 fua09sf 09asuf 0asu 0fas0f 09asas f9sf 09asf 09as 09as fas ffas f sfas jas fsf jasf asl fja fas093rkn2l rwkf lakf 89a fasfasfklas lasfasfhasfsf";

			NamedPipeMessageSerializer.RegisterMetadata(writer, assemblyName, ilMetaData);

			//now see if we can get the message back

			var position = NamedPipeMessageSerializer.RegisterMetadata(processor, 4, writer.Buffer(), writer.Position);

			Assert.IsNotNull(processor.MetaData);
			Assert.AreEqual<int>(writer.Position, position);
			Assert.AreEqual<string>(assemblyName, processor.MetaData.AssemblyName);
			Assert.AreEqual<string>(ilMetaData, processor.MetaData.IlMetadata);
		}

		[TestMethod]
		public void NamedPipeMessageSerializerTests_OnMethodEntered_RoundTrip() {
			var writer = new ByteArrayWriter(100);
			writer.Write(int.MaxValue); //we need garbage to ensure we support non 0 start positions.
			var processor = new TestProcessor();

			processor.ClearCalls();

			var methodCall = GetGenericTimingMethod();

			NamedPipeMessageSerializer.OnMethodEntered(writer, methodCall.ProcessId, methodCall.ModuleId, methodCall.ThreadId, methodCall.TimeStamp, methodCall.MethodId);

			//now see if we can get the message back

			var position = NamedPipeMessageSerializer.OnMethodEntered(processor, 4, writer.Buffer(), writer.Position);

			var method = processor.MethodEvents.FirstOrDefault();

			Assert.IsNotNull(method);
			Assert.AreEqual<int>(writer.Position, position);

			Assert.AreEqual<int>(methodCall.ProcessId, method.ProcessId);
			Assert.AreEqual<int>(methodCall.ModuleId, method.ModuleId);
			Assert.AreEqual<int>(methodCall.ThreadId, method.ThreadId);
			Assert.AreEqual<long>(methodCall.TimeStamp, method.TimeStamp);
			Assert.AreEqual<int>(methodCall.MethodId, method.MethodId);
			Assert.AreEqual<bool>(true, method.Enter);
		}

		[TestMethod]
		public void NamedPipeMessageSerializerTests_OnGenericMethodEntered_RoundTrip() {
			var writer = new ByteArrayWriter(10000);
			writer.Write(int.MaxValue); //we need garbage to ensure we support non 0 start positions.
			var processor = new TestProcessor();

			processor.ClearCalls();

			var methodCall = GetGenericTimingMethod();

			NamedPipeMessageSerializer.OnGenericMethodEntered(writer, methodCall.ProcessId, methodCall.ModuleId, methodCall.ThreadId, methodCall.TimeStamp,
				methodCall.MethodId, methodCall.ClassTypes.Types.Split(','), methodCall.MethodTypes.Types.Split(','));

			//now see if we can get the message back

			var position = NamedPipeMessageSerializer.OnGenericMethodEntered(processor, 4, writer.Buffer(), writer.Position);

			var method = processor.MethodEvents.FirstOrDefault();

			Assert.IsNotNull(method);
			Assert.AreEqual<int>(writer.Position, position);

			Assert.AreEqual<int>(methodCall.MethodId, method.MethodId);
			Assert.AreEqual<int>(methodCall.ModuleId, method.ModuleId);
			Assert.AreEqual<int>(methodCall.ProcessId, method.ProcessId);
			Assert.AreEqual<int>(methodCall.ThreadId, method.ThreadId);
			Assert.AreEqual<long>(methodCall.TimeStamp, method.TimeStamp);
			Assert.AreEqual<bool>(true, method.Enter);

			Assert.AreEqual<string>(string.Join(",", methodCall.ClassTypes.Types), method.ClassTypes.Types);
			Assert.AreEqual<string>(string.Join(",", methodCall.MethodTypes.Types), method.MethodTypes.Types);
		}

		[TestMethod]
		public void NamedPipeMessageSerializerTests_OnGenericMethodEntered_NoClass_RoundTrip() {
			var writer = new ByteArrayWriter(10000);
			writer.Write(int.MaxValue); //we need garbage to ensure we support non 0 start positions.
			var processor = new TestProcessor();

			processor.ClearCalls();

			var methodCall = GetGenericTimingMethod();

			NamedPipeMessageSerializer.OnGenericMethodEntered(writer, methodCall.ProcessId, methodCall.ModuleId, methodCall.ThreadId, methodCall.TimeStamp,
				methodCall.MethodId, null, methodCall.MethodTypes.Types.Split(','));

			//now see if we can get the message back

			var position = NamedPipeMessageSerializer.OnGenericMethodEntered(processor, 4, writer.Buffer(), writer.Position);

			var method = processor.MethodEvents.FirstOrDefault();

			Assert.IsNotNull(method);
			Assert.AreEqual<int>(writer.Position, position);

			Assert.AreEqual<int>(methodCall.MethodId, method.MethodId);
			Assert.AreEqual<int>(methodCall.ModuleId, method.ModuleId);
			Assert.AreEqual<int>(methodCall.ProcessId, method.ProcessId);
			Assert.AreEqual<int>(methodCall.ThreadId, method.ThreadId);
			Assert.AreEqual<long>(methodCall.TimeStamp, method.TimeStamp);
			Assert.AreEqual<bool>(true, method.Enter);

			Assert.AreEqual<string>(string.Empty, method.ClassTypes.Types);
			Assert.AreEqual<string>(string.Join(",", methodCall.MethodTypes.Types), method.MethodTypes.Types);
		}

		[TestMethod]
		public void NamedPipeMessageSerializerTests_OnGenericMethodEntered_NoMethod_RoundTrip() {
			var writer = new ByteArrayWriter(10000);
			writer.Write(int.MaxValue); //we need garbage to ensure we support non 0 start positions.
			var processor = new TestProcessor();

			processor.ClearCalls();

			var methodCall = GetGenericTimingMethod();

			NamedPipeMessageSerializer.OnGenericMethodEntered(writer, methodCall.ProcessId, methodCall.ModuleId, methodCall.ThreadId, methodCall.TimeStamp,
				methodCall.MethodId, methodCall.ClassTypes.Types.Split(','), null);

			//now see if we can get the message back

			var position = NamedPipeMessageSerializer.OnGenericMethodEntered(processor, 4, writer.Buffer(), writer.Position);

			var method = processor.MethodEvents.FirstOrDefault();

			Assert.IsNotNull(method);
			Assert.AreEqual<int>(writer.Position, position);

			Assert.AreEqual<int>(methodCall.MethodId, method.MethodId);
			Assert.AreEqual<int>(methodCall.ModuleId, method.ModuleId);
			Assert.AreEqual<int>(methodCall.ProcessId, method.ProcessId);
			Assert.AreEqual<int>(methodCall.ThreadId, method.ThreadId);
			Assert.AreEqual<long>(methodCall.TimeStamp, method.TimeStamp);
			Assert.AreEqual<bool>(true, method.Enter);

			Assert.AreEqual<string>(string.Join(",", methodCall.ClassTypes.Types), method.ClassTypes.Types);
			Assert.AreEqual<string>(string.Empty, method.MethodTypes.Types);
		}

		[TestMethod]
		public void NamedPipeMessageSerializerTests_OnMethodExited_RoundTrip() {
			var writer = new ByteArrayWriter(100);
			writer.Write(int.MaxValue); //we need garbage to ensure we support non 0 start positions.
			var processor = new TestProcessor();

			processor.ClearCalls();

			var methodCall = GetGenericTimingMethod();

			NamedPipeMessageSerializer.OnMethodExited(writer, methodCall.ProcessId, methodCall.ModuleId, methodCall.ThreadId, methodCall.TimeStamp, methodCall.MethodId);

			//now see if we can get the message back

			var position = NamedPipeMessageSerializer.OnMethodExited(processor, 4, writer.Buffer(), writer.Position);

			var method = processor.MethodEvents.FirstOrDefault();

			Assert.IsNotNull(method);
			Assert.AreEqual<int>(writer.Position, position);

			Assert.AreEqual<int>(methodCall.ProcessId, method.ProcessId);
			Assert.AreEqual<int>(methodCall.ModuleId, method.ModuleId);
			Assert.AreEqual<int>(methodCall.ThreadId, method.ThreadId);
			Assert.AreEqual<long>(methodCall.TimeStamp, method.TimeStamp);
			Assert.AreEqual<int>(methodCall.MethodId, method.MethodId);
			Assert.AreEqual<bool>(false, method.Enter);

		}

		[TestMethod]
		public void NamedPipeMessageSerializerTests_OnGenericMethodExited_RoundTrip() {
			var writer = new ByteArrayWriter(10000);
			writer.Write(int.MaxValue); //we need garbage to ensure we support non 0 start positions.
			var processor = new TestProcessor();

			processor.ClearCalls();

			var methodCall = GetGenericTimingMethod();

			NamedPipeMessageSerializer.OnGenericMethodExited(writer, methodCall.ProcessId, methodCall.ModuleId, methodCall.ThreadId, methodCall.TimeStamp,
				methodCall.MethodId, methodCall.ClassTypes.Types.Split(','), methodCall.MethodTypes.Types.Split(','));

			//now see if we can get the message back

			var position = NamedPipeMessageSerializer.OnGenericMethodExited(processor, 4, writer.Buffer(), writer.Position);

			var method = processor.MethodEvents.FirstOrDefault();

			Assert.IsNotNull(method);
			Assert.AreEqual<int>(writer.Position, position);

			Assert.AreEqual<int>(methodCall.MethodId, method.MethodId);
			Assert.AreEqual<int>(methodCall.ModuleId, method.ModuleId);
			Assert.AreEqual<int>(methodCall.ProcessId, method.ProcessId);
			Assert.AreEqual<int>(methodCall.ThreadId, method.ThreadId);
			Assert.AreEqual<long>(methodCall.TimeStamp, method.TimeStamp);
			Assert.AreEqual<bool>(false, method.Enter);

			Assert.AreEqual<string>(string.Join(",", methodCall.ClassTypes.Types), method.ClassTypes.Types);
			Assert.AreEqual<string>(string.Join(",", methodCall.MethodTypes.Types), method.MethodTypes.Types);
		}

		[TestMethod]
		public void NamedPipeMessageSerializerTests_OnPointHit_RoundTrip() {
			var writer = new ByteArrayWriter(100);
			writer.Write(int.MaxValue); //we need garbage to ensure we support non 0 start positions.
			var processor = new TestProcessor();

			processor.ClearCalls();

			var methodCall = GetGenericTimingMethod();
			var pointId = 244256;

			NamedPipeMessageSerializer.OnPointHit(writer, methodCall.ProcessId, methodCall.ModuleId, methodCall.ThreadId, methodCall.TimeStamp, pointId);

			//now see if we can get the message back

			var position = NamedPipeMessageSerializer.OnPointHit(processor, 4, writer.Buffer(), writer.Position);

			var pointHit = processor.PointEvents.FirstOrDefault();

			Assert.IsNotNull(pointHit);
			Assert.AreEqual<int>(writer.Position, position);

			Assert.AreEqual<int>(12, pointHit.ProcessId);
			Assert.AreEqual<int>(methodCall.ModuleId, pointHit.ModuleId);
			Assert.AreEqual<int>(methodCall.ThreadId, pointHit.ThreadId);
			Assert.AreEqual<long>(methodCall.TimeStamp, pointHit.TimeStamp);
			Assert.AreEqual<int>(pointId, pointHit.PointId);
		}

		[TestMethod]
		public void NamedPipeMessageSerializerTests_Stop_RoundTrip() {
			var writer = new ByteArrayWriter(100);
			writer.Write(int.MaxValue); //we need garbage to ensure we support non 0 start positions.
			var processor = new TestProcessor();

			processor.ClearCalls();

			var processId = 2458284;
			var timeStamp = 35235235235235235;

			NamedPipeMessageSerializer.Stop(writer, processId, timeStamp);

			//now see if we can get the message back

			var position = NamedPipeMessageSerializer.Stop(processor, 4, writer.Buffer(), writer.Position);


			Assert.IsNotNull(processor.NewStop);
			Assert.AreEqual<int>(writer.Position, position);

			Assert.AreEqual<int>(processId, processor.NewStop.ProcessId);
			Assert.AreEqual<long>(timeStamp, processor.NewStop.TimeStamp);
		}


		private static TimingMethod GetGenericTimingMethod() {
			var methodCall = new TimingMethod() {
				ClassTypes = new TimingGenericParameters(),
				MethodTypes = new TimingGenericParameters(),
				MethodId = 2478235,
				ModuleId = 24235125,
				ProcessId = 12,
				ThreadId = 252,
				TimeStamp = 124124125235235
			};
			methodCall.ClassTypes.Types = string.Join(",", new string[] { "This is a really cool item", "This is another cool item" });
			methodCall.MethodTypes.Types = string.Join(",", new string[] { "Method This is a really cool item", "Method This is another cool item" });
			return methodCall;
		}

		private class TestProcessor : CoreMessageProcessor {

			public TestOnNewProcess NewProcess {
				get;
				set;
			}

			public TestOnNewAssembly NewAssembly {
				get;
				set;
			}

			public TestOnNewThread NewThread {
				get;
				set;
			}

			public TestRegisterMetadata MetaData {
				get;
				set;
			}

			public TestOnStop NewStop {
				get;
				set;
			}

			public override void OnNewAssembly(int processId, long timestamp, string assemblyIdentity) {
				NewAssembly = new TestOnNewAssembly() {
					AssemblyIdentity = assemblyIdentity,
					ProcessId = processId,
					TimeStamp = timestamp
				};
				base.OnNewAssembly(processId, timestamp, assemblyIdentity);
			}

			public override void OnNewProcess(int processId, long ticksPerSecond, long timestamp, string processName, string machineName) {
				NewProcess = new TestOnNewProcess() {
					MachineName = machineName,
					ProcessId = processId,
					ProcessName = processName,
					TicksPerSecond = ticksPerSecond,
					TimeStamp = timestamp
				};
				base.OnNewProcess(processId, ticksPerSecond, timestamp, processName, machineName);
			}

			public override void OnNewThread(int processId, long timestamp, int threadId, string threadName) {
				NewThread = new TestOnNewThread() {
					ProcessId = processId,
					ThreadId = threadId,
					ThreadName = threadName,
					TimeStamp = timestamp
				};
				base.OnNewThread(processId, timestamp, threadId, threadName);
			}

			public override void RegisterMetadata(string assemblyName, string ilMetadata) {
				MetaData = new TestRegisterMetadata() {
					AssemblyName = assemblyName,
					IlMetadata = ilMetadata
				};
				base.RegisterMetadata(assemblyName, ilMetadata);
			}

			public override void Stop(int processId, long timestamp) {
				NewStop = new TestOnStop() {
					ProcessId = processId,
					TimeStamp = timestamp
				};
				base.Stop(processId, timestamp);
			}

		}

		private class TestOnStop {

			public int ProcessId {
				get;
				set;
			}

			public long TimeStamp {
				get;
				set;
			}
		}

		private class TestOnNewProcess {

			public int ProcessId {
				get;
				set;
			}

			public long TicksPerSecond {
				get;
				set;
			}

			public long TimeStamp {
				get;
				set;
			}

			public string ProcessName {
				get;
				set;
			}

			public string MachineName {
				get;
				set;
			}
		}

		private class TestOnNewAssembly {

			public int ProcessId {
				get;
				set;
			}

			public long TimeStamp {
				get;
				set;
			}

			public string AssemblyIdentity {
				get;
				set;
			}
		}

		private class TestOnNewThread {
			//(int processId, long timestamp, int threadId, string threadName)
			public int ProcessId {
				get;
				set;
			}

			public long TimeStamp {
				get;
				set;
			}

			public int ThreadId {
				get;
				set;
			}

			public string ThreadName {
				get;
				set;
			}
		}

		private class TestRegisterMetadata {

			public string AssemblyName {
				get;
				set;
			}

			public string IlMetadata {
				get;
				set;
			}
		}

	}
}
