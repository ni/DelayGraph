using System.Collections.Generic;
using RegisterPlacement.DelayGraph;

namespace RegisterPlacement.LatencyAssignment
{
    internal interface ILatencyAssignment
    {
        HashSet<DelayGraphVertex> Execute(
            DelayGraph.DelayGraph delayGraph,
            int targetPeriod);
    }
}
