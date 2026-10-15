using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Xml;

namespace NetPerf.Core {

    /// <summary>
    /// Core timing metadata needed for method.
    /// </summary>
    [DebuggerDisplay("MethodId:{MethodId} MethodTime:{MethodTime} ChildTime:{ChildTime} TotalTime:{TotalTime} ModuleId:{ModuleId} ProcessId:{ProcessId}")]
    public class TimingData {

        public long ChildTime {
            get;
            set;
        }

        public int MethodId {
            get;
            set;
        }

        public long MethodTime {
            get;
            set;
        }

        public int ModuleId {
            get;
            set;
        }

        public int ProcessId {
            get;
            set;
        }

        //we are not tracking threads yet.
        //public int ThreadId {
        //    get {
        //        return EnterMethod.ThreadId;
        //    }
        //}

        public long TotalTime {
            get {
                return MethodTime + ChildTime;
            }
        }

        public float ChildPercent(long totalTime) {
            return (float)((double)(ChildTime * 100) / (double)totalTime);
        }

        public float ChildPercent(double totalTime) {
            return (float)((double)(ChildTime * 100) / totalTime);
        }

        public float MethodPercent(long totalTime) {
            return (float)((double)(MethodTime * 100) / (double)totalTime);
        }

        public float MethodPercent(double totalTime) {
            return (float)((double)(MethodTime * 100) / totalTime);
        }

        public float TotalPercent(long totalTime) {
            return (float)((double)(TotalTime * 100) / (double)totalTime);
        }

        public float TotalPercent(double totalTime) {
            return (float)((double)(TotalTime * 100) / totalTime);
        }

    }
}
