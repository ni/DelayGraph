using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RegisterPlacement.DelayGraph
{
    /// <summary>
    /// Represents one solution to the latency assignment problem. 
    /// Calulates and returns the quality of the solution
    /// </summary>
    internal class DelayGraphSolution
    {
        /// <summary>
        /// constructor for build DG solution
        /// </summary>
        /// <param name="graph">a delay graph</param>
        /// <param name="registeredTerminals">a hashset of nodes needing registered</param>
        internal DelayGraphSolution(
            DelayGraph graph,
            HashSet<DelayGraphVertex> registeredTerminals)
        {
            // for testing purposes
            Graph = graph;
            RegisteredTerminals = registeredTerminals;
            FoundComboCycle = false;
            Slack = 0;
            Score = null;
        }

        /// <summary>
        /// Constructor for <see cref="DelayGraphSolution"/>.
        /// </summary>
        /// <param name="solutionName">The name of this solution</param>
        /// <param name="graph">The <see cref="DelayGraph"/> that is being solved by the terminals marked as registered in <paramref name="registeredTerminals"/>.</param>
        /// <param name="registeredTerminals">A set of vertices that are to be marked as registered in this solution. These registers </param>
        /// <param name="targetClockPeriod">The target clock period in PS for this soluton.</param>
        internal DelayGraphSolution(
            string solutionName,
            DelayGraph graph,
            HashSet<DelayGraphVertex> registeredTerminals,
            int targetClockPeriod)
        {
            Graph = graph;
            RegisteredTerminals = registeredTerminals;

            FixSolutionForSiblingVertices();
            
            bool foundCycle;
            int clockPeriod = EstimatePeriod(out foundCycle);
            if (foundCycle)
            {
                // try to fix cycles and then recalc period
                FixSolutionForComboCycles();
                clockPeriod = EstimatePeriod(out foundCycle);
                Console.WriteLine("Warning: Fixing combo cycles in " + solutionName);
            }

            FoundComboCycle = foundCycle;
            Slack = targetClockPeriod - clockPeriod;

            long throughput, latency, registers;
            SumCosts(out throughput, out latency, out registers);

            Score = new ScoreCard(throughput, latency, registers);
        }
        /// <summary>
        /// estimate period
        /// </summary>
        /// <param name="foundCycle">return iff found a combinational cycle</param>
        /// <returns>estimated clock period</returns>
        internal int EstimatePeriod(out bool foundCycle)
        {
            int maxPeriod = 0;
            foundCycle = false;
            var computedDelays = new Dictionary<DelayGraphVertex, int>();

            // first, start with registers and vertices without incoming edges
            foreach (var vertex in Graph.Vertices)
            {
                IEnumerable<DelayGraphEdge> edges;
                if (Registered(vertex) ||
                    !Graph.TryGetInEdges(vertex, out edges) ||
                    edges == null ||
                    !edges.Any())
                {
                    bool cycle;
                    var period = FindClockPeriod(vertex, computedDelays, out cycle);
                    //// Verifier.Verify(!cycle, ExceptionGenerator.InvalidState, "Found a Combinational Cycle in Solution");
                    maxPeriod = Math.Max(maxPeriod, period);
                    if (cycle)
                    {
                        foundCycle = true;
                    }
                }
            }

            // second, in case of circular vertices
            foreach (var vertex in Graph.Vertices)
            {
                if (!computedDelays.ContainsKey(vertex))
                {
                    bool cycle;
                    var period = FindClockPeriod(vertex, computedDelays, out cycle);
                    //// Verifier.Verify(!cycle, ExceptionGenerator.InvalidState, "Found a Combinational Cycle in Solution");
                    maxPeriod = Math.Max(maxPeriod, period);
                    if (cycle)
                    {
                        foundCycle = true;
                    }
                }
            }

            return maxPeriod;
        }

        private int FindClockPeriod(DelayGraphVertex vertex, Dictionary<DelayGraphVertex, int> computedDelays, out bool foundCycle)
        {
            int periodEstimatedFromThisTerminalForward =
                TraverseForwardCalculatingPeriodIterative(vertex, computedDelays, out foundCycle);
            return periodEstimatedFromThisTerminalForward;
        }

        /// <summary>
        /// return true iff x is better tham y
        /// </summary>
        /// <param name="x">a solution</param>
        /// <param name="y">a different solution</param>
        /// <returns>true iff x is better than y</returns>
        internal static bool IsBetterThan(DelayGraphSolution x, DelayGraphSolution y)
        {
            if (!x.FoundComboCycle && y.FoundComboCycle)
            {
                return true;
            }
            if (x.FoundComboCycle && !y.FoundComboCycle)
            {
                return false;
            }
#if false
            if (x.Slack >= 0 && y.Slack < 0)
            {
                return true;
            }
            if (y.Slack >= 0 && x.Slack < 0)
            {
                return false;
            }
#endif
            return ScoreCard.IsBetterThan(x.Score, y.Score);
        }

        /// <summary>
        /// print the dotty file
        /// </summary>
        /// <param name="pathName">path name</param>
        /// <returns>file name</returns>
        internal String PrintDotFile(String pathName)
        {
            using (var writer = new StreamWriter(path: pathName, append: false))
            {
                var dotString = Graph.GetDotString(RegisteredTerminals);
                writer.Write(dotString);
            }
            return pathName;
        }

        /// <summary>
        /// minimum clock period due to edge delay values
        /// </summary>
        /// <param name="graph">a delay graph</param>
        /// <returns>a value</returns>
        internal static int MinClockPeriod(DelayGraph graph)
        {
            int maxPeriod = 0;
            foreach (var edge in graph.Edges)
            {
                maxPeriod = Math.Max(maxPeriod, edge.Delay);
            }
            return maxPeriod;
        }

        /// <summary>
        ///  prune edges from same source/target, keeping the strongr edge
        /// </summary>
        /// <param name="graph">a delay graph</param>
        /// <returns>whether DG has changed</returns>
        internal static bool PruneEdges(DelayGraph graph)
        {
            bool changed = false;

            foreach (var vertex in graph.Vertices)
            {
                IEnumerable<DelayGraphEdge> edges;
                if (!graph.TryGetOutEdges(vertex, out edges))
                {
                    continue;
                }
                if (edges.Count() < 2)
                {
                    continue;
                }
                // check for redundant out edges
                var targets = new Dictionary<DelayGraphVertex, DelayGraphEdge>();
                foreach (var edge in edges.ToList())
                {
                    DelayGraphEdge prevEdge;
                    if (targets.TryGetValue(edge.Target, out prevEdge))
                    {
                        // decide which edge to keep and which to delete
                        if (prevEdge.Delay >= edge.Delay)
                        {
                            // keep prev edge
                            graph.RemoveEdge(edge);
                        }
                        else
                        {
                            targets[edge.Target] = edge;
                            graph.RemoveEdge(prevEdge);
                        }
                        changed = true;
                    }
                    else
                    {
                        targets[edge.Target] = edge;
                    }
                }
            }
            return changed;
        }

        /// <summary>
        /// sum costs over all nodes
        /// </summary>
        /// <param name="throughputTotalCost">a cost</param>
        /// <param name="latencyTotalCost">a cost</param>
        /// <param name="registersTotalCost">a cost</param>
        internal void SumCosts(
            out long throughputTotalCost,
            out long latencyTotalCost,
            out long registersTotalCost)
        {
            var sort = DelayGraphAlgorithms.TopologicalSort(Graph).ToList();
            throughputTotalCost = DelayGraphAlgorithms.FindMaxThroughputCostCore(sort, Graph, RegisteredTerminals);
            latencyTotalCost = DelayGraphAlgorithms.FindMaxLatencyCore(sort, Graph, RegisteredTerminals);
            registersTotalCost = 0;
            foreach (var vertex in Graph.Vertices.Where(Registered))
            {
                registersTotalCost += vertex.RegisterCostIfRegistered;
            }
        }

        /// <summary>
        /// try to insert registers in cyclic paths starting at (1) feedbackInputNode's input terminals and (2) RightShiftRegister's output terminal
        /// </summary>
        private void FixSolutionForComboCycles()
        {
            foreach (var v in Graph.Vertices.Where(v => v.IsTerminal() &&
                                                        !Registered(v) &&
                                                        (v.NodeType == DelayGraphNodeType.FeedbackInputNode ||
                                                         (v.NodeType == DelayGraphNodeType.RightShiftRegister &&
                                                          v.IsOutputTerminal))))
            {
                foreach (var e in Graph.GetFeedbackOutEdges(v))
                {
                    var next = e.Target;
                    if (Registered(next))
                    {
                        continue;
                    }
                    var visited = new HashSet<DelayGraphVertex>();
                    if (FindForwardPath(next, v, visited))
                    {
                        // there is a combo path from v to itself via next - fix the cycle by registering next
                        if (v.DisallowRegister)
                        {
                            if (v.NodeType == DelayGraphNodeType.FeedbackInputNode)
                            {
                                // put registers on incoming forward edge's source, unless it is already registered or register is not allowed
                                foreach (var edge in Graph.GetForwardInEdges(v))
                                {
                                    // register s iff ok to do so
                                    var s = edge.Source;
                                    if (!s.DisallowRegister && !Registered(s))
                                    {
                                        RegisteredTerminals.Add(s);
                                    }
                                }
                            }
                        }
                        else
                        {
                            RegisteredTerminals.Add(v);
                        }
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// return true iff there is at least one forward combinational path from v to target (ie, a path consisting of forward edges and no registered vertices along the way)
        /// </summary>
        /// <param name="v">the source vertex</param>
        /// <param name="target">the target vertex</param>
        /// <param name="visited">a cache for visited vertices</param>
        /// <returns>true iff there is a combinational forward path</returns>
        private bool FindForwardPath(DelayGraphVertex v, DelayGraphVertex target, HashSet<DelayGraphVertex> visited)
        {
            if (v == target)
            {
                return true;
            }
            if (!visited.Add(v))
            {
                // previously visited
                return false;
            }
            foreach (var e in Graph.GetForwardOutEdges(v))
            {
                if (Registered(e.Target))
                {
                    continue;
                }
                if (FindForwardPath(e.Target, target, visited))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Fix up sibling nodes that came from the same original source node before extraction. 
        /// If any sibling is registered, all must be registered.
        /// </summary>
        /// <returns>a count of number of nodes that changed</returns>
        private int FixSolutionForSiblingVertices()
        {
            int count = 0;
            HashSet<DelayGraphVertex> grouped;
            var listOfSiblings = DelayGraphAlgorithms.FindVertexGroups(Graph, out grouped);
            foreach (var siblings in listOfSiblings)
            {
                if (siblings.Any(Registered) && siblings.Count() > 1)
                {
                    // at least one in sibling is registered
                    var verticesWhichAreNotRegistered = siblings.Where(v => !Registered(v));
                    count += verticesWhichAreNotRegistered.Count();
                    foreach (var v in verticesWhichAreNotRegistered)
                    {
                        RegisteredTerminals.Add(v);
                    }
                }
            }
            return count;
        }

        private void RetimeRegister(DelayGraphVertex v)
        {
            if (v.IsInputTerminal)
            {
                // move v's register to incoming edges
                foreach (var e in Graph.GetForwardInEdges(v))
                {
                    RegisteredTerminals.Add(e.Source);
                }
            }
            else
            {
                // move v's register to outgoing edges
                foreach (var e in Graph.GetForwardOutEdges(v))
                {
                    RegisteredTerminals.Add(e.Source);
                }
            }
            RegisteredTerminals.Remove(v);
        }

        /// <summary>
        /// the delay graph
        /// </summary>
        internal DelayGraph Graph { get; set; }

        /// <summary>
        /// the registered vertices
        /// </summary>
        internal HashSet<DelayGraphVertex> RegisteredTerminals { get; set; }

        private int Slack { get; set; }

        private bool FoundComboCycle { get; set; }

        private ScoreCard Score { get; set; }

        private bool Registered(DelayGraphVertex v)
        {
            return v.IsRegistered || RegisteredTerminals.Contains(v);
        }

        private int TraverseForwardCalculatingPeriod(
            DelayGraphVertex startingVertex, out bool foundCycle)
        {
            var computedDelays = new Dictionary<DelayGraphVertex, int>();
            var visiting = new HashSet<DelayGraphVertex>();
            return TraverseForwardCalculatingPeriodRecursive(startingVertex, out foundCycle, visiting, computedDelays);
        }

        private int TraverseForwardCalculatingPeriodRecursive(DelayGraphVertex startingVertex, out bool foundCycle,
                                                              HashSet<DelayGraphVertex> visiting,
                                                              Dictionary<DelayGraphVertex, int> computedDelays)
        {
            foundCycle = false;
            int maxAmongOutgoingEdges = 0;
            if (computedDelays.TryGetValue(startingVertex, out maxAmongOutgoingEdges))
            {
                return maxAmongOutgoingEdges;
            }
            if (visiting.Contains(startingVertex)) // stop recursing if we hit a cycle
            {
                foundCycle = true;
                return 0;
            }

            // set state to visiting for starting vertex
            visiting.Add(startingVertex);
            IEnumerable<DelayGraphEdge> foundOutgoingEdges;
            if (Graph.TryGetOutEdges(startingVertex, out foundOutgoingEdges))
            {
                foreach (var outEdge in foundOutgoingEdges)
                {
                    int delayOnThisPath;
                    if (Registered(outEdge.Target)) // stop recursing when we hit a register
                    {
                        delayOnThisPath = outEdge.Delay;
                    }
                    else
                    {
                        bool anyCycle;
                        delayOnThisPath = outEdge.Delay + TraverseForwardCalculatingPeriodRecursive(
                            startingVertex: outEdge.Target,
                            foundCycle: out anyCycle,
                            visiting: visiting,
                            computedDelays: computedDelays);
                        if (anyCycle)
                        {
                            foundCycle = true;
                        }
                    }
                    maxAmongOutgoingEdges = Math.Max(maxAmongOutgoingEdges, delayOnThisPath);
                }
            }
            // finished visiting startingVertex - enter into ComputeDelays - which means visited is done
            // and remove it from visiting to be technically correct
            computedDelays[startingVertex] = maxAmongOutgoingEdges;
            visiting.Remove(startingVertex);
            return maxAmongOutgoingEdges;
        }
#if false
        private class PeriodTraverseState
        {
            internal DelayGraphVertex Vertex { get; set; }
            internal int DelayReturned { get; set; }
            internal Queue<DelayGraphEdge> UnfinishedEdges { get; set; }
            internal int MaxAmongOutgoingEdges { get; set; }
            internal PeriodTraverseState StateWaitingOn { get; set; }

            internal PeriodTraverseState(
                DelayGraphVertex vertex,
                int delayReturned,
                Queue<DelayGraphEdge> unfinishedEdges,
                int maxAmongOutgoingEdges)
            {
                Vertex = vertex;
                DelayReturned = delayReturned;
                UnfinishedEdges = unfinishedEdges;
                MaxAmongOutgoingEdges = maxAmongOutgoingEdges;
                StateWaitingOn = null;
            }

            internal PeriodTraverseState(
                DelayGraphVertex vertex) 
                : this(vertex, 0, null, 0)
            {
            }

        }

        private int TraverseForwardCalculatingPeriodIterative2(
            DelayGraphVertex startingVertex, out bool foundCycle)
        {
            Stack<PeriodTraverseState> stack = new Stack<PeriodTraverseState>();
            stack.Push(new PeriodTraverseState(startingVertex));

            PeriodTraverseState currentState = null;
            foundCycle = false;
            while (stack.Any())
            {
                currentState = stack.Peek();

                int alreadyComputedDelay = 0;
                if (ComputedDelays.TryGetValue(currentState.Vertex, out alreadyComputedDelay)) // put in for reconvergent paths
                {
                    currentState.DelayReturned = alreadyComputedDelay;
                    stack.Pop();
                    continue;
                }
                if (currentState.StateWaitingOn == null) // this state is used here just to tell if we are coming along here b/c we need to attend to unfinished business where we might have already set 'visited'
                {
                    if (Visiting.Contains(currentState.Vertex)) // stop recursing if we're going along a loop, but still add up the delays of the loop
                    {
                        currentState.DelayReturned = 0;
                        foundCycle = true;
                        stack.Pop();
                        continue;
                    }
                    Visiting.Add(currentState.Vertex);
                }
                if (currentState.UnfinishedEdges == null)
                {
                    IEnumerable<DelayGraphEdge> foundOutgoingEdges;
                    if (Graph.TryGetOutEdges(currentState.Vertex, out foundOutgoingEdges))
                    {
                        currentState.UnfinishedEdges = new Queue<DelayGraphEdge>(foundOutgoingEdges);
                    }
                }
                bool done = true;
                while (currentState.UnfinishedEdges != null && currentState.UnfinishedEdges.Any())
                {
                    var outEdge = currentState.UnfinishedEdges.Peek();
                    int delayOnThisPath;
                    if (Registered(outEdge.Target)) // stop recursing when we hit a register
                    {
                        delayOnThisPath = outEdge.Delay;
                    }
                    else if (Visiting.Contains(outEdge.Target))
                    {
                        // found a cycle
                        foundCycle = true;
                        delayOnThisPath = outEdge.Delay;
                    }
                    else
                    {
                        if (currentState.StateWaitingOn == null)
                        {
                            currentState.StateWaitingOn = new PeriodTraverseState(outEdge.Target);
                            stack.Push(currentState.StateWaitingOn);
                            done = false; // need to process what we just pushed
                            break;
                        }
                        else
                        {
                            delayOnThisPath = outEdge.Delay + currentState.StateWaitingOn.DelayReturned;
                            currentState.StateWaitingOn = null;
                        }
                    }
                    currentState.UnfinishedEdges.Dequeue();
                    currentState.MaxAmongOutgoingEdges = Math.Max(currentState.MaxAmongOutgoingEdges, delayOnThisPath);
                }
                if (done)
                {
                    ComputedDelays[currentState.Vertex] = currentState.MaxAmongOutgoingEdges;
                    currentState.DelayReturned = currentState.MaxAmongOutgoingEdges;
                    Visiting.Remove(currentState.Vertex);
                    stack.Pop();
                }
            }
            return currentState.DelayReturned;
        }
#endif

        private enum VisitState
        {
            Queued = 0,
            Visiting = 1,
            Visited = 2,
        }

        private int TraverseForwardCalculatingPeriodIterative(
            DelayGraphVertex startingVertex,
            Dictionary<DelayGraphVertex, int> computedDelays,
            out bool foundCycle)
        {
            Stack<DelayGraphVertex> stack = new Stack<DelayGraphVertex>();
            var visited = new Dictionary<DelayGraphVertex, VisitState>();

            stack.Push(startingVertex);
            visited[startingVertex] = VisitState.Queued;
            foundCycle = false;

            while (stack.Any())
            {
                DelayGraphVertex currentVertex = stack.Peek();
                var visitStatus = visited[currentVertex];   // it is an error if currentVertex is not found in visited dictionary

                if (visitStatus == VisitState.Queued)
                {
                    // first time processing currentVertex - queue all outgoing edges
                    visited[currentVertex] = VisitState.Visiting;
                    IEnumerable<DelayGraphEdge> foundOutgoingEdges;
                    if (Graph.TryGetOutEdges(currentVertex, out foundOutgoingEdges))
                    {
                        var todo = foundOutgoingEdges.Where(e => !Registered(e.Target)).Select(e => e.Target);
                        todo.Reverse();
                        foreach (var next in todo)
                        {
                            VisitState status;
                            if (!visited.TryGetValue(next, out status))
                            {
                                visited[next] = VisitState.Queued;
                                stack.Push(next);
                            }
                            else if (status == VisitState.Queued)
                            {
                                // next has been previously queued - have to push it again so this is processed before parent's second visit
                                stack.Push(next);
                            }
                            else if (status == VisitState.Visiting)
                            {
                                // found a cycle
                                foundCycle = true;
                                computedDelays[next] = 0;
                            }
                            // ok if status is Visited - no need to re-schedule next
                        }
                    }
                }
                else if (visitStatus == VisitState.Visiting)
                {
                    // second visit - all fanout have been processed already
                    int maxDelay = 0;
                    IEnumerable<DelayGraphEdge> foundOutgoingEdges;
                    if (Graph.TryGetOutEdges(currentVertex, out foundOutgoingEdges))
                    {
                        foreach (var edge in foundOutgoingEdges)
                        {
                            var next = edge.Target;
                            int nextDelay = 0;
                            if (!Registered(next))
                            {
                                if (!computedDelays.TryGetValue(next, out nextDelay))
                                {
                                    throw new Exception("Cannot find Key: next should have been processed and found in ComputeDelays");
                                }
                            }
                            maxDelay = Math.Max(maxDelay, edge.Delay + nextDelay);
                        }
                    }
                    computedDelays[currentVertex] = maxDelay;
                    visited[currentVertex] = VisitState.Visited;
                    stack.Pop();
                }
                else
                {
                    // visited - due to reconvergence - just pop it
                    stack.Pop();
                }
            }
            return computedDelays[startingVertex];
        }

        /// <summary>
        /// score card subclass
        /// </summary>
        internal class ScoreCard
        {
            /// <summary>
            /// constructor thats produces a new ScoreCard
            /// </summary>
            /// <param name="throughputCost">a cost 1</param>
            /// <param name="latencyCost">a cost 2</param>
            /// <param name="registerCost">a cost 3</param>
            internal ScoreCard(
                long throughputCost,
                long latencyCost,
                long registerCost)
            {
                ThroughputCosts = throughputCost;
                LatencyCosts = latencyCost;
                RegisterCosts = registerCost;
            }

            private long ThroughputCosts { get; set; }
            private long LatencyCosts { get; set; }
            private long RegisterCosts { get; set; }

            /// <summary>
            /// return true iff score x is better than score y
            /// </summary>
            /// <param name="x">score 1</param>
            /// <param name="y">score 2</param>
            /// <returns>true or false</returns>
            internal static bool IsBetterThan(ScoreCard x, ScoreCard y)
            {
                if (x.ThroughputCosts < y.ThroughputCosts ||
                    (x.ThroughputCosts == y.ThroughputCosts &&
                     (x.LatencyCosts < y.LatencyCosts ||
                      (x.LatencyCosts == y.LatencyCosts &&
                       x.RegisterCosts < y.RegisterCosts))))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
    }
}
