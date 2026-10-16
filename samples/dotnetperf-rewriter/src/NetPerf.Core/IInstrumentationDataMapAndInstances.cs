using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetPerf.Core.Interfaces;

namespace NetPerf.Core {

    /// <summary>
    /// We use this interface so that the processor can be passed into the reports
    /// </summary>
    public interface IInstrumentationDataMapAndInstances {

        Dictionary<string, Dictionary<int, InstrumentationMethodData>> DataMap();

        Dictionary<string, InstrumentationData> KnownInstances();

        InstrumentationMethodDataMapping InstrumentDataForMethod(int moduleId, int methodId);

        List<TimingProcess> ProcessList();

        List<TimingThread> ThreadList();
    }
}
