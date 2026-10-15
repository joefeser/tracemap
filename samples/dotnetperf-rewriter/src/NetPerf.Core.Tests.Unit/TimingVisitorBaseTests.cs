using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetPerf.Core.Interfaces;

namespace NetPerf.Core.Tests.Unit {

	[TestClass]
	public class TimingVisitorBaseTests {

		[TestMethod]
		[ExpectedException(typeof(ArgumentNullException))]
		public void TimingVisitorBase_NullCtorParam_ExpectArgumentNullException() {
			var fail = new TimingVisitorBase(null, null);
		}

		[TestMethod]
		public void TimingVisitorBase_PassZeroItems_EnsureVisitDoesNotFail() {
			var processor = new CoreMessageProcessor();
			var works = new TimingVisitorBase(processor, new IInstrumentationDataMapAndInstancesMock());
		}

		#region ThreeMethods

		[TestMethod]
		public void TimingVisitorBase_ThreeMethods_TotalTimeTaken() {
			var timing = GetThreeMethodsTimingData();
			Assert.AreEqual<long>(250, timing.TotalExecutionTimeTaken);
		}

		[TestMethod]
		public void TimingVisitorBase_ThreeMethods_MethodTimeTaken() {
			var timing = GetThreeMethodsTimingData();
			Assert.AreEqual<long>(200, timing.TotalMethodTime);
		}

		[TestMethod]
		public void TimingVisitorBase_ThreeMethods_ThreeEventsExist() {
			var timing = GetThreeMethodsTimingData();
			Assert.AreEqual<int>(3, timing.TimingElements.Count);
		}

		[TestMethod]
		public void TimingVisitorBase_ThreeMethods_VerifyPercents() {
			var timing = GetThreeMethodsTimingData();
			var events = timing.TimingElements;
			var totalTime = (double)timing.TotalTimeWithChildren;
			Assert.AreEqual<float>(50f, events[0].MethodPercent(totalTime));
			Assert.AreEqual<float>(25f, events[1].MethodPercent(totalTime));
			Assert.AreEqual<float>(25f, events[2].MethodPercent(totalTime));
			//Total and Method are the same for flat methods.
			Assert.AreEqual<float>(50f, events[0].TotalPercent(totalTime));
			Assert.AreEqual<float>(25f, events[1].TotalPercent(totalTime));
			Assert.AreEqual<float>(25f, events[2].TotalPercent(totalTime));
		}

		[TestMethod]
		public void TimingVisitorBase_ThreeMethods_VerifyMethodAndTotalMatch() {
			var timing = GetThreeMethodsTimingData();
			var events = timing.TimingElements;
			Assert.AreEqual<long>(events[0].MethodTime, events[0].TotalTime);
			Assert.AreEqual<long>(events[1].MethodTime, events[1].TotalTime);
			Assert.AreEqual<long>(events[2].MethodTime, events[2].TotalTime);
		}

		private TimingDataInformation GetThreeMethodsTimingData() {
			var processor = new CoreMessageProcessor();
			processor.MethodEvents.AddRange(GetSingleLevelCallGraphThreeMethods());
			var works = new TimingVisitorBase(processor, new IInstrumentationDataMapAndInstancesMock());
			var timing = works.GetTimingData();
			return timing;
		}

		///<summary>
		/// Return a call graph that has one level and 3 method calls to no other methods (flat)
		/// </summary>
		/// <returns></returns>
		private List<TimingMethod> GetSingleLevelCallGraphThreeMethods() {
			var retVal = new List<TimingMethod>();

			retVal.Add(CreateEnterMethod(1, 0));
			retVal.Add(CreateExitMethod(1, 100));

			retVal.Add(CreateEnterMethod(2, 125));
			retVal.Add(CreateExitMethod(2, 175));

			retVal.Add(CreateEnterMethod(3, 200));
			retVal.Add(CreateExitMethod(3, 250));

			return retVal;
		}

		#endregion ThreeMethods

		#region ThreeMethodsThreeLevels

		[TestMethod]
		public void TimingVisitorBase_ThreeLevelThreeMethods_TotalTimeTaken() {
			var timing = GetThreeLevelThreeMethodsTimingData();
			Assert.AreEqual<long>(250, timing.TotalExecutionTimeTaken);
		}

		[TestMethod]
		public void TimingVisitorBase_ThreeLevelThreeMethods_TimeWithChildrenTimeTaken() {
			var timing = GetThreeLevelThreeMethodsTimingData();
			Assert.AreEqual<long>(200, timing.TotalTimeWithChildren);
		}

		[TestMethod]
		public void TimingVisitorBase_ThreeLevelThreeMethods_MethodTimeTaken() {
			var timing = GetThreeLevelThreeMethodsTimingData();
			Assert.AreEqual<long>(175, timing.TotalMethodTime);
		}

		[TestMethod]
		public void TimingVisitorBase_ThreeLevelThreeMethods_ThreeEventsExist() {
			var timing = GetThreeLevelThreeMethodsTimingData();
			Assert.AreEqual<int>(5, timing.TimingElements.Count);
		}

		[TestMethod]
		public void TimingVisitorBase_ThreeLevelThreeMethods_VerifyWithAndWithoutChildrenMatchSum() {
			var timing = GetThreeLevelThreeMethodsTimingData();
			var events = timing.TimingElements;
			for (int i = 0; i < events.Count; i++) {
				Assert.AreEqual<long>(events[i].TotalTime, events[i].MethodTime + events[i].ChildTime);
			}
		}

		[TestMethod]
		public void TimingVisitorBase_ThreeLevelThreeMethods_VerifyPercents() {
			var timing = GetThreeLevelThreeMethodsTimingData();
			var events = timing.TimingElements;
			var totalTime = (double)timing.TotalTimeWithChildren;
			Assert.AreEqual<float>(37.5f, events[0].MethodPercent(totalTime));
			Assert.AreEqual<float>(12.5f, events[0].ChildPercent(totalTime));

			Assert.AreEqual<float>(7.5f, events[1].MethodPercent(totalTime));
			Assert.AreEqual<float>(5f, events[1].ChildPercent(totalTime));

			Assert.AreEqual<float>(5f, events[2].MethodPercent(totalTime));
			Assert.AreEqual<float>(25f, events[3].MethodPercent(totalTime));
			Assert.AreEqual<float>(25f, events[4].MethodPercent(totalTime));
		}

		//[TestMethod]
		//public void TimingVisitorBase_ThreeLevelThreeMethods_VerifyCallGraph() {
		//    var timing = GetThreeLevelThreeMethodsTimingData();
		//    var report = timing.ToXmlReport();
		//    Console.WriteLine(report);
		//    //ToXmlReport
		//}

		private TimingDataInformation GetThreeLevelThreeMethodsTimingData() {

			var processor = new CoreMessageProcessor();
			processor.MethodEvents.AddRange(GetThreeLevelCallGraphThreeMethods());
			var works = new TimingVisitorBase(processor, new IInstrumentationDataMapAndInstancesMock());
			var timing = works.GetTimingData();
			return timing;
		}

		///<summary>
		/// Return a call graph that has one level and 3 method calls to no other methods (flat)
		/// </summary>
		/// <returns></returns>
		private List<TimingMethod> GetThreeLevelCallGraphThreeMethods() {
			var retVal = new List<TimingMethod>();

			//first method
			retVal.Add(CreateEnterMethod(1, 0));
			//begin 2nd child
			retVal.Add(CreateEnterMethod(4, 25));
			//begin 3nd child
			retVal.Add(CreateEnterMethod(5, 30));
			retVal.Add(CreateExitMethod(5, 40));
			//end 3nd child
			retVal.Add(CreateExitMethod(4, 50));
			//end 2nd child
			retVal.Add(CreateExitMethod(1, 100));

			//second method
			retVal.Add(CreateEnterMethod(2, 125));
			retVal.Add(CreateExitMethod(2, 175));
			//third method
			retVal.Add(CreateEnterMethod(3, 200));
			retVal.Add(CreateExitMethod(3, 250));

			return retVal;
		}

		#endregion ThreeMethodsThreeLevels

		private static TimingMethod CreateEnterMethod(int methodId, long timestamp) {
			return new TimingMethod() {
				Enter = true,
				MethodId = methodId,
				ModuleId = 1,
				ProcessId = 1,
				ThreadId = 1,
				TimeStamp = timestamp
			};
		}

		private static TimingMethod CreateExitMethod(int methodId, long timestamp) {
			return new TimingMethod() {
				Enter = false,
				MethodId = methodId,
				ModuleId = 1,
				ProcessId = 1,
				ThreadId = 1,
				TimeStamp = timestamp
			};
		}

		private class IInstrumentationDataMapAndInstancesMock : IInstrumentationDataMapAndInstances {

			public Dictionary<string, Dictionary<int, Interfaces.InstrumentationMethodData>> _DataMap {
				get;
				private set;
			}

			public Dictionary<string, Interfaces.InstrumentationData> _KnownInstances {
				get;
				private set;
			}

			public List<TimingProcess> _processList {
				get;
				private set;
			}

			public List<TimingThread> _threadList {
				get;
				private set;
			}

			public Dictionary<string, Dictionary<int, Interfaces.InstrumentationMethodData>> DataMap() {
				return _DataMap;
			}

			public Dictionary<string, Interfaces.InstrumentationData> KnownInstances() {
				return _KnownInstances;
			}

			public InstrumentationMethodDataMapping InstrumentDataForMethod(int moduleId, int methodId) {
				return new InstrumentationMethodDataMapping() {
					Method = new InstrumentationMethodData() {
						signature = "Test"
					},
					Module = new InstrumentationModuleData() {
						name = "Test"
					},
					Type = new InstrumentationTypeData() {
						signature = "Test"
					}
				};
			}

			public List<TimingProcess> ProcessList() {
				return _processList;
			}

			public List<TimingThread> ThreadList() {
				return _threadList;
			}

			public IInstrumentationDataMapAndInstancesMock() {
				_DataMap = new Dictionary<string, Dictionary<int, Interfaces.InstrumentationMethodData>>();
				_KnownInstances = new Dictionary<string, Interfaces.InstrumentationData>();
				_processList = new List<TimingProcess>();
			}

		}

	}
}
