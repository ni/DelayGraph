using System.Collections.Generic;
using RegisterPlacement.DelayGraph;

namespace RegisterPlacement.LatencyAssignment
{
    /// <summary>
    /// Defines the call pattern for a latency assignment algorithm
    /// </summary>
    internal interface ILatencyAssignment
    {
        /// <summary>
        /// Execute the algorithm on the given graph with the given target period and return the resuling set of <see cref="DelayGraphVertex"/> 
        /// that should be registered to solve the problem.
        /// </summary>
        /// <param name="delayGraph">The initial <see cref="DelayGraph"/>.</param>
        /// <param name="targetPeriod">The target period in PS</param>
        /// <returns>The set of <see cref="DelayGraphVertex"/> that should be registered in this design to meet the desired <paramref name="targetPeriod"/>.</returns>
        HashSet<DelayGraphVertex> Execute(DelayGraph.DelayGraph delayGraph, int targetPeriod);
    }
}
