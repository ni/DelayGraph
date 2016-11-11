using System;
using System.Collections.Generic;
using RegisterPlacement.DelayGraph;

namespace RegisterPlacement.LatencyAssignment
{
    internal class LatencyAssignmentAsap : ILatencyAssignment
    {
        internal LatencyAssignmentAsap()
        {
            Graph = null;
            TargetPeriod = 0;
            DelayMap = new Dictionary<DelayGraphVertex, int>();
            RegisteredTerminals = new HashSet<DelayGraphVertex>();
        }

        private DelayGraph.DelayGraph Graph { get; set; }
        private int TargetPeriod { get; set; }

        private Dictionary<DelayGraphVertex, int> DelayMap { get; set; }
        private HashSet<DelayGraphVertex> RegisteredTerminals { get; set; }

        public HashSet<DelayGraphVertex> Execute(
            DelayGraph.DelayGraph delayGraph,
            int targetPeriod)
        {
            Graph = delayGraph;
            InitializeDelayMap();
            RegisteredTerminals.Clear();
            
            Traverse(targetPeriod);
            Traverse(targetPeriod); // helps with feedback paths to call twice but the delay on the second pass is likely inaccurate. Chicken and egg problem
            return RegisteredTerminals;
        }

        private void InitializeDelayMap()
        {
            DelayMap.Clear();
            foreach (var vertex in Graph.Vertices)
            {
                if (vertex.IsRegistered)
                {
                    DelayMap[vertex] = 0;
                }
            }
        }

        private void Traverse(int targetPeriod)
        {
            foreach (var vertex in Graph.Vertices) // assuming semi topological order (cycles exist)
            {
                if (vertex.IsRegistered)
                {
                    continue;
                }
                int maxDelayIn = 0;
                IEnumerable<DelayGraphEdge> inEdges;
                if (Graph.TryGetInEdges(vertex, out inEdges))
                {
                    foreach (var upstreamEdge in inEdges)
                    {
                        int delayUpstream;
                        if (!DelayMap.TryGetValue(upstreamEdge.Source, out delayUpstream))
                        {
                            delayUpstream = 0; // cycles might exist, have to start somewhere
                        }
                        int delayAlongEdge = upstreamEdge.Delay + delayUpstream;
                        maxDelayIn = Math.Max(maxDelayIn, delayAlongEdge);
                    }
                }

                int maxDelayOut = 0;
                long maxThroughputCostDownstream = 0;
                long maxLatencyCostDownstream = 0;
                long maxRegisterCostDownstream = 0;
                IEnumerable<DelayGraphEdge> outEdges;
                if (Graph.TryGetOutEdges(vertex, out outEdges))
                {
                    foreach (var downstreamEdge in outEdges)
                    {
                        maxDelayOut = Math.Max(maxDelayOut, downstreamEdge.Delay);
                        maxThroughputCostDownstream = Math.Max(maxThroughputCostDownstream, downstreamEdge.Target.ThroughputCostIfRegistered);
                        maxLatencyCostDownstream = Math.Max(maxLatencyCostDownstream, downstreamEdge.Target.LatencyCostIfRegistered);
                        maxRegisterCostDownstream = Math.Max(maxRegisterCostDownstream, downstreamEdge.Target.RegisterCostIfRegistered);
                    }
                }

                long throughputCostHere = vertex.ThroughputCostIfRegistered;
                long latencyCostHere = vertex.LatencyCostIfRegistered;
                long registerCostHere = vertex.RegisterCostIfRegistered;

                if ((maxDelayIn + maxDelayOut) > targetPeriod)
                {
                    RegisteredTerminals.Add(vertex);
                    DelayMap[vertex] = 0;
                }
                else if ((maxDelayIn > 0) &&
                    ((maxThroughputCostDownstream > throughputCostHere) ||
                    (maxLatencyCostDownstream > latencyCostHere && throughputCostHere == maxThroughputCostDownstream) ||
                    (maxRegisterCostDownstream > registerCostHere && throughputCostHere == maxThroughputCostDownstream && latencyCostHere == maxLatencyCostDownstream)))
                {
                    RegisteredTerminals.Add(vertex); // half hearted attempt to keep latency out of loops or other places where we get possible qor degredation
                    DelayMap[vertex] = 0;
                }
                else
                {
                    RegisteredTerminals.Remove(vertex);
                    DelayMap[vertex] = maxDelayIn;
                }
            }
        }
    }
}
