using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
//using SQLite;
using System.Diagnostics;

namespace NetPerf.Core {

    public class TimingProcess {

        //public int TimingProcessId {
        //    get;
        //    set;
        //}

        public int ProcessId {
            get;
            set;
        }

        public string MachineName {
            get;
            set;
        }

        public string ProcessName {
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
    }

    public class TimingThread {

        //public int TimingThreadId {
        //    get;
        //    set;
        //}

        public int ProcessId {
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

        public long TimeStamp {
            get;
            set;
        }
    }

    public class TimingAssembly {

        //public int AssemblyId {
        //    get;
        //    set;
        //}

        public string AssemblyIdentity {
            get;
            set;
        }

        public int ProcessId {
            get;
            set;
        }

        public long TimeStamp {
            get;
            set;
        }
    }

    /// <summary>
    /// These must be inserted first or as they are found
    /// </summary>
    public class TimingGenericParameters {


        public long GenericParameterId {
            get;
            set;
        }

        public string Types {
            get;
            set;
        }
    }

    [DebuggerDisplay("MethodId:{MethodId} Enter:{Enter} TimeStamp:{TimeStamp} ModuleId:{ModuleId}")]
    public class TimingMethod {

        //public int TimingMethodId {
        //    get;
        //    set;
        //}

        public int ModuleId {
            get;
            set;
        }

        /// <summary>
        /// Enter or Exit
        /// </summary>
        public bool Enter {
            get;
            set;
        }

        public int MethodId {
            get;
            set;
        }

        public int ProcessId {
            get;
            set;
        }

        public int ThreadId {
            get;
            set;
        }

        public long TimeStamp {
            get;
            set;
        }

        [CLSCompliant(false)]
        public int? _classTypeId;

        public int? ClassTypeId {
            get {
                if (_classTypes != null) {
                    return (int)_classTypes.GenericParameterId;
                }
                return _classTypeId;
            }
            set {
                _classTypeId = value;
            }
        }

        [CLSCompliant(false)]
        public int? _methodTypeId;

        public int? MethodTypeId {
            get {
                if (_methodTypes != null) {
                    return (int)_methodTypes.GenericParameterId;
                }
                return _methodTypeId;
            }
            set {
                _methodTypeId = value;
            }
        }

        private TimingGenericParameters _classTypes;

        public TimingGenericParameters ClassTypes {
            get {
                if (_classTypes == null && _classTypeId != null) {
                    //TODO load up the Class Type (IOC)
                }
                return _classTypes;
            }
            set {
                _classTypes = value;
            }
        }

        private TimingGenericParameters _methodTypes;

        public TimingGenericParameters MethodTypes {
            get {
                if (_methodTypes == null && _methodTypeId != null) {
                    //TODO load up the Method Type (IOC)
                }
                return _methodTypes;
            }
            set {
                _methodTypes = value;
            }
        }
    }

    [DebuggerDisplay("PointId:{PointId} TimeStamp:{TimeStamp} ModuleId:{ModuleId}")]
    public class TimingPoint {

        //public int TimingPointId {
        //    get;
        //    set;
        //}

        //See if we need a module id on the point call so that we can track many assemblies.

        public int ModuleId {
            get;
            set;
        }

        public int PointId {
            get;
            set;
        }

        public int ProcessId {
            get;
            set;
        }

        public int ThreadId {
            get;
            set;
        }

        public long TimeStamp {
            get;
            set;
        }
    }
}
