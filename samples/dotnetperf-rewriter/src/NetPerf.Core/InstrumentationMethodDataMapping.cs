using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetPerf.Core.Interfaces;

namespace NetPerf.Core {
    
    /// <summary>
    /// A Map of all three objects in the graph for easier obtaining of the data for reporting reasons.
    /// </summary>
    public class InstrumentationMethodDataMapping {

        public InstrumentationMethodData Method {
            get;
            set;
        }

        public InstrumentationTypeData Type {
            get;
            set;
        }

        public InstrumentationModuleData Module {
            get;
            set;
        }

    }
}
