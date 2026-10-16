using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace NetPerf.Core {

    [DebuggerDisplay("MethodId:{MethodId} Children:{Children.Count} TimeStamp:{EnterMethod.TimeStamp} ModuleId:{ModuleId}")]
    public sealed class TimingMethodNode {

        private List<TimingMethodNode> _children;

        public List<TimingMethodNode> Children {
            get {
                if (_children == null) {
                    _children = new List<TimingMethodNode>();
                }
                return _children;
            }
        }

        private long _childTime;

        public long ChildTime {
            get {
                if (_childTime == 0 && Children.Count > 0) {
                    //add the TotalTime of all of the children
                    _childTime = Children.Sum(item => item.TotalTime);
                }
                return _childTime;
            }
        }

        public TimingMethod EnterMethod {
            get;
            private set;
        }

        public TimingMethod ExitMethod {
            get;
            private set;
        }

        public long MethodTime {
            get {
                return TotalTime - ChildTime;
            }
        }

        public int ModuleId {
            get {
                return EnterMethod.ModuleId;
            }
        }

        public int MethodId {
            get {
                return EnterMethod.MethodId;
            }
        }

        public int ProcessId {
            get {
                return EnterMethod.ProcessId;
            }
        }

        //we are not tracking threads yet.
        //public int ThreadId {
        //    get {
        //        return EnterMethod.ThreadId;
        //    }
        //}

        public long TotalTime {
            get {
                //TODO determine better way to work with items that have no exit aka they threw an exception.
                if (ExitMethod == null) {
                    return 1;
                }
                return ExitMethod.TimeStamp - EnterMethod.TimeStamp;
            }
        }

        //public int? _classTypeId;

        //public int? ClassTypeId {
        //    get {
        //        return _classTypeId;
        //    }
        //    set {
        //        _classTypeId = value;
        //    }
        //}

        //public int? _methodTypeId;

        //public int? MethodTypeId {
        //    get {
        //        return _methodTypeId;
        //    }
        //    set {
        //        _methodTypeId = value;
        //    }
        //}

        public TimingMethodNode(TimingMethod method) {
            EnterMethod = method;
        }

        public void AddMethod(TimingMethodNode method) {
            Children.Add(method);
        }

        public void Exit(TimingMethod method) {
            ExitMethod = method;
        }
    }
}
