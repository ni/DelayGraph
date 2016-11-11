using System;
using System.Collections.Generic;
using System.Linq;
using RegisterPlacement.DelayGraph;

namespace RegisterPlacement.LatencyAssignment
{
    internal class LatencyAssignmentGreedy : ILatencyAssignment
    {
        internal LatencyAssignmentGreedy()
        {
            Graph = null;
            TargetPeriod = 0;
            OutputDelayMap = new Dictionary<DelayGraphVertex, int>();
            InputDelayMap = new Dictionary<DelayGraphVertex, int>();
            FaninRegistersMap = new Dictionary<DelayGraphVertex, HashSet<DelayGraphVertex>>();
            FanoutRegistersMap = new Dictionary<DelayGraphVertex, HashSet<DelayGraphVertex>>();
            RegisterToRegisterDelays = new Dictionary<Tuple<DelayGraphVertex, DelayGraphVertex>, int>();
        }

        private DelayGraph.DelayGraph Graph { get; set; }
        private int TargetPeriod { get; set; }

        private Dictionary<DelayGraphVertex, int> OutputDelayMap { get; set; }

        private Dictionary<DelayGraphVertex, int> InputDelayMap { get; set; }

        private Dictionary<DelayGraphVertex, HashSet<DelayGraphVertex>> FaninRegistersMap { get; set; }

        private Dictionary<DelayGraphVertex, HashSet<DelayGraphVertex>> FanoutRegistersMap { get; set; }

        private Dictionary<Tuple<DelayGraphVertex, DelayGraphVertex>, int> RegisterToRegisterDelays { get; set; }

        public HashSet<DelayGraphVertex> Execute(
            DelayGraph.DelayGraph delayGraph,
            int targetPeriod)
        {
            Graph = delayGraph;
            TargetPeriod = targetPeriod;

            // first, add all vertex to RegisteredTerminals
            var registered = new HashSet<DelayGraphVertex>(Graph.Vertices);
            //initialize all output and input delay maps
            Initialize();

            // next, iteratively remove registered terminals as long as it meets timing
            bool changed;
            do
            {
                changed = false;
                // remove vertex if possible
                var sorted = from vertex in Graph.Vertices
                             where (!vertex.IsRegistered && registered.Contains(vertex))
                             orderby vertex.ThroughputCostIfRegistered descending,
                                     vertex.LatencyCostIfRegistered descending,
                                     vertex.RegisterCostIfRegistered descending
                             select vertex;
                foreach (var vertex in sorted)
                {
                    if (IsSafeToDeRegister(vertex))
                    {
                        changed = true;
                        registered.Remove(vertex);
                    }
                }
            }
            while (changed);
            bool foundCycle;
            int maxDelay = SanityCheckGraph(registered, out foundCycle);
            if (foundCycle)
            {
                Console.WriteLine("Detected Cycles");
            }
            if (maxDelay > TargetPeriod)
            {
                Console.WriteLine("Detected Timing Violation");
            }

            return registered;
        }

        private int SanityCheckGraph(HashSet<DelayGraphVertex> registered, out bool foundCycle)
        {
            // sanity check
            var solution = new DelayGraphSolution(Graph, registered);
            return solution.EstimatePeriod(out foundCycle);
        }

        /// <summary>
        /// assumes all vertex are initialized to be registered, so all edges are between registered vertices
        /// initialize OutputDelayMap to have max of output edge delays for vertex,
        /// and InputDelayMap to have max of input edge delays for vertex,
        /// and initialize FaninRegistersMap to have all inedge's sources,
        /// and FanoutRegistersMap to have all out edges' targets
        /// </summary>
        private void Initialize()
        {
            OutputDelayMap = new Dictionary<DelayGraphVertex, int>();
            InputDelayMap = new Dictionary<DelayGraphVertex, int>();
            FaninRegistersMap = new Dictionary<DelayGraphVertex, HashSet<DelayGraphVertex>>();
            FanoutRegistersMap = new Dictionary<DelayGraphVertex, HashSet<DelayGraphVertex>>();
            RegisterToRegisterDelays = new Dictionary<Tuple<DelayGraphVertex, DelayGraphVertex>, int>();
            foreach (var vertex in Graph.Vertices)
            {
                IEnumerable<DelayGraphEdge> edges;
                if (Graph.TryGetInEdges(vertex, out edges))
                {
                    int delay = 0;
                    foreach (var edge in edges)
                    {
                        delay = Math.Max(delay, edge.Delay);
                    }
                    InputDelayMap[vertex] = delay;
                    FaninRegistersMap[vertex] = new HashSet<DelayGraphVertex>(edges.Select(e => e.Source));

                }
                if (Graph.TryGetOutEdges(vertex, out edges))
                {
                    int delay = 0;
                    foreach (var edge in edges)
                    {
                        delay = Math.Max(delay, edge.Delay);
                    }
                    OutputDelayMap[vertex] = delay;
                    FanoutRegistersMap[vertex] = new HashSet<DelayGraphVertex>(edges.Select(e => e.Target));
                }
            }
            // initialize reg->reg->delay using all edges
            foreach (var edge in Graph.Edges)
            {
                UpdateRegisterRegisterDelay(edge.Source, edge.Target, edge.Delay);
            }
        }

        private void UpdateRegisterRegisterDelay(DelayGraphVertex from, DelayGraphVertex to, int delay)
        {
            var key = Tuple.Create(from, to);
            int origDelay;
            if (!RegisterToRegisterDelays.TryGetValue(key, out origDelay) ||
                origDelay < delay)
            {
                RegisterToRegisterDelays[key] = delay;
            }
        }

        private int GetRegisterToRegisterDelay(DelayGraphVertex from, DelayGraphVertex to)
        {
            var key = Tuple.Create(from, to);
            int delay;
            if (!RegisterToRegisterDelays.TryGetValue(key, out delay))
            {
                throw new Exception("This is not supposed to ever happen");
            }
            return delay;
        }

        private void DeleteRegisterToRegisterDelay(DelayGraphVertex from, DelayGraphVertex to)
        {
            var key = Tuple.Create(from, to);
            if (!RegisterToRegisterDelays.Remove(key))
            {
                throw new Exception("this should not happen");
            }
        }

        private bool IsSafeToDeRegister(DelayGraphVertex vertex)
        {
            if (vertex.IsRegistered)
            {
                return false;
            }
            // get incoming delay
            int incomingDelay;
            if (!InputDelayMap.TryGetValue(vertex, out incomingDelay))
            {
                incomingDelay = 0;
            }
            // get outgoing delay
            int outgoingDelay;
            if (!OutputDelayMap.TryGetValue(vertex, out outgoingDelay))
            {
                outgoingDelay = 0;
            }
            int totalDelay = incomingDelay + outgoingDelay;
            if (totalDelay > TargetPeriod)
            {
                return false;
            }
            // double check for cycles if vertex register is removed
            HashSet<DelayGraphVertex> faninRegisters;
            if (FaninRegistersMap.TryGetValue(vertex, out faninRegisters))
            {
                if (faninRegisters.Contains(vertex))
                {
                    return false;   // vertex is its own fanin register - cannot remove register or there would be a cycle
                }
            }

            HashSet<DelayGraphVertex> fanoutRegisters;
            if (FanoutRegistersMap.TryGetValue(vertex, out fanoutRegisters))
            {
                if (fanoutRegisters.Contains(vertex))
                {
                    return false;   // vertex is its own fanout register - cannot remove register or there would be a cycle
                }
            }

            UpdateFanInRegisters(vertex, outgoingDelay, faninRegisters, fanoutRegisters);

            UpdateFanOutRegisters(vertex, incomingDelay, faninRegisters, fanoutRegisters);

            CleanFromRegisterToRegisterMap(vertex, faninRegisters, fanoutRegisters);

            return true;
        }

        private void UpdateFanInRegisters(DelayGraphVertex vertex, int outgoingDelay, HashSet<DelayGraphVertex> faninRegisters, HashSet<DelayGraphVertex> fanoutRegisters)
        {
            // update fanin and fanout registers maps and delays now that vertex is NOT a register anymore
            if (faninRegisters == null)
            {
                return;
            }
            // update each upstream register vertex
            foreach (var faninReg in faninRegisters)
            {
                int faninDelay = GetRegisterToRegisterDelay(@from: faninReg, to: vertex);
                int totalDelay = faninDelay + outgoingDelay;

                // update faninReg's output delay to be max of total delay and original delay
                int origDelay;
                if (!OutputDelayMap.TryGetValue(faninReg, out origDelay) ||
                    origDelay < totalDelay)
                {
                    OutputDelayMap[faninReg] = totalDelay;
                }

                // remove vertex from reg's fanout registers
                HashSet<DelayGraphVertex> origFanoutRegs;
                if (FanoutRegistersMap.TryGetValue(faninReg, out origFanoutRegs))
                {
                    origFanoutRegs.Remove(vertex);
                }

                // add vertex's fanout registers to reg's fanout registers
                if (fanoutRegisters != null)
                {
                    if (origFanoutRegs == null)
                    {
                        origFanoutRegs = new HashSet<DelayGraphVertex>(fanoutRegisters);
                        FanoutRegistersMap[faninReg] = origFanoutRegs;
                    }
                    else
                    {
                        foreach (var next in fanoutRegisters)
                        {
                            origFanoutRegs.Add(next);
                        }
                    }

                    // update faninReg -> fanoutRegister with faninDelay + v->fanoutRegister delay
                    foreach (var fanoutReg in fanoutRegisters)
                    {
                        int fanoutDelay = GetRegisterToRegisterDelay(@from: vertex, to: fanoutReg);
                        UpdateRegisterRegisterDelay(@from: faninReg, to: fanoutReg, delay: faninDelay + fanoutDelay);
                    }
                }
            }
        }
        private void UpdateFanOutRegisters(DelayGraphVertex vertex, int incomingDelay, HashSet<DelayGraphVertex> faninRegisters, HashSet<DelayGraphVertex> fanoutRegisters)
        {
            // update fanin and fanout registers maps and delays now that vertex is NOT a register anymore
            if (fanoutRegisters == null)
            {
                return;
            }
                // update each downstream register vertex
            foreach (var fanoutReg in fanoutRegisters)
            {
                int fanoutDelay = GetRegisterToRegisterDelay(@from: vertex, to: fanoutReg);
                int totalDelay = incomingDelay + fanoutDelay;
                // update reg's input delay to be max of total delay and original delay
                int origDelay;
                if (!InputDelayMap.TryGetValue(fanoutReg, out origDelay) ||
                    origDelay < totalDelay)
                {
                    InputDelayMap[fanoutReg] = totalDelay;
                }

                // remove vertex from fanout reg's fanin registers
                HashSet<DelayGraphVertex> origFaninRegs;
                if (FaninRegistersMap.TryGetValue(fanoutReg, out origFaninRegs))
                {
                    origFaninRegs.Remove(vertex);
                }

                // add vertex's fanin registers to reg's fanin registers
                if (faninRegisters != null)
                {
                    if (origFaninRegs == null)
                    {
                        origFaninRegs = new HashSet<DelayGraphVertex>(faninRegisters);
                        FaninRegistersMap[fanoutReg] = origFaninRegs;
                    }
                    else
                    {
                        foreach (var next in faninRegisters)
                        {
                            origFaninRegs.Add(next);
                        }
                    }
                    // update faninReg -> fanoutRegister with faninDelay + v->fanoutRegister delay
                    foreach (var faninReg in faninRegisters)
                    {
                        int faninDelay = GetRegisterToRegisterDelay(@from: faninReg, to: vertex);
                        UpdateRegisterRegisterDelay(@from: faninReg, to: fanoutReg, delay: faninDelay + fanoutDelay);
                    }
                }
            }
        }

        /// <summary>
        /// clean up data structures when vertex is no longer a register
        /// </summary>
        /// <param name="vertex"></param>
        /// <param name="faninRegisters"></param>
        /// <param name="fanoutRegisters"></param>
        private void CleanFromRegisterToRegisterMap(DelayGraphVertex vertex, HashSet<DelayGraphVertex> faninRegisters, HashSet<DelayGraphVertex> fanoutRegisters)
        {
            if (faninRegisters != null)
            {
                foreach (var faninReg in faninRegisters)
                {
                    DeleteRegisterToRegisterDelay(@from: faninReg, to: vertex);
                }
            }
            if (fanoutRegisters != null)
            {
                foreach (var fanoutReg in fanoutRegisters)
                {
                    DeleteRegisterToRegisterDelay(@from: vertex, to:fanoutReg);
                }
            }
            FaninRegistersMap.Remove(vertex);
            FanoutRegistersMap.Remove(vertex);
        }
    }
}
