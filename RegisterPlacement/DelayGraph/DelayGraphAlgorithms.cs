using System;
using System.Collections.Generic;
using System.Linq;

namespace RegisterPlacement.DelayGraph
{
    /// <summary>
    /// collection of algorithms on delay graph
    /// </summary>
    internal class DelayGraphAlgorithms
    {
        /// <summary>
        /// return a topologically sorted ienumerable of vertices - according to only non-feedback edges
        /// </summary>
        /// <param name="graph">the delay graph</param>
        /// <returns>an enumerable of vertices, sorted in forward edge topological ordering</returns>
        internal static IEnumerable<DelayGraphVertex> TopologicalSort(DelayGraph graph)
        {
            var sorted = new LinkedList<DelayGraphVertex>();
            var visited = new Dictionary<DelayGraphVertex, VisitState>();
            var todo = FindTopoSortSeeds(graph);
            foreach (var v in todo)
            {
                if (!TopoIterative(v, sorted, visited, graph))
                {
                    throw new Exception("Unexpected bad topological seed");
                }
            }

            return sorted;
        }

        /// <summary>
        /// return a topologically sorted (according to non-feedback edges only) list of vertices
        /// </summary>
        /// <param name="v">a vertex</param>
        /// <param name="sorted">linked list of sorted list of vertices</param>
        /// <param name="visited">to keep track of vertices that have been visited previously</param>
        /// <param name="graph">the delay graph</param>
        /// <returns>return true iff v is processed as unvisited </returns>
        private static bool TopoRecursive(DelayGraphVertex v, LinkedList<DelayGraphVertex> sorted, HashSet<DelayGraphVertex> visited, DelayGraph graph)
        {
            // add v to visited first, and if it is already visited, then bail
            if (visited.Add(v))
            {
                var outEdges = graph.GetForwardOutEdges(v);
                foreach (var e in outEdges)
                {
                    TopoRecursive(e.Target, sorted, visited, graph);
                }
                // all fanout have been visited and added to sorted list - add v to start of sorted list
                sorted.AddFirst(v);
                return true;
            }
            return false;
        }

        private enum VisitState
        {
            Queued = 0,
            Visiting = 1,
            Visited = 2,
        }

        /// <summary>
        /// Iterative version of topological sort, of forward edges only
        /// </summary>
        /// <param name="v">a delay graph vertex</param>
        /// <param name="sorted">linked list of sorted list of vertices</param>
        /// <param name="visited">to keep track of vertices that have been visited previously</param>
        /// <param name="graph">the delay graph</param>
        /// <returns>return true iff v is processed as unvisited</returns>
        private static bool TopoIterative(DelayGraphVertex v, LinkedList<DelayGraphVertex> sorted, Dictionary<DelayGraphVertex, VisitState> visited, DelayGraph graph)
        {
            // add v to visited first, and if it is already visited, then bail
            if (visited.ContainsKey(v))
            {
                return false;
            }

            var stack = new Stack<DelayGraphVertex>();
            
            visited[v] = VisitState.Queued;
            stack.Push(v);
            
            while (stack.Any())
            {
                var curr = stack.Peek();
                var status = visited[curr];

                if (status == VisitState.Queued)
                {
                    // first ever visit, queue all outgoing vertices
                    visited[curr] = VisitState.Visiting;
                    var outEdges = graph.GetForwardOutEdges(curr);
                    foreach (var e in outEdges.Reverse())
                    {
                        var next = e.Target;
                        VisitState nextStatus;
                        if (!visited.TryGetValue(next, out nextStatus) ||
                            nextStatus == VisitState.Queued)
                        {
                            // not visited yet, or previously queued, queue next
                            visited[next] = VisitState.Queued;
                            stack.Push(next);
                        }
                        else if (nextStatus == VisitState.Visiting)
                        {
                            // found a cycle - not supposed to happen - there is no good topological sort with cycles
                            return false;
                        }
                        // othewise: visited, no need to queue it again
                    }
                }
                else if (status == VisitState.Visiting)
                {
                    // second visit - all fanout have been processed already
                    visited[curr] = VisitState.Visited;
                    sorted.AddFirst(curr);
                    stack.Pop();
                }
                else
                {
                    // visited - previously added to sorted already, just pop it
                    stack.Pop();
                }
            }

            return true;
        }

        /// <summary>
        /// find and return vertices without incoming forward edges
        /// </summary>
        /// <param name="graph">the delay graph</param>
        /// <returns>enumerable of vertices</returns>
        private static IEnumerable<DelayGraphVertex> FindTopoSortSeeds(DelayGraph graph)
        {
            foreach (var v in graph.Vertices)
            {
                if (!graph.GetForwardInEdges(v).Any())
                {
                    yield return v;
                }
            }
        }

        /// <summary>
        /// class that serves to encapsulate data and reference count
        /// </summary>
        /// <typeparam name="T"></typeparam>
        private class Packet<T>
        {
            internal int RefCount { get; set; }
            internal T Data { get; }

            internal Packet(int refCount, T data)
            {
                RefCount = refCount;
                Data = data;
            }
        }

        /// <summary>
        /// wavefront of consumers - based on number of outedges
        /// </summary>
        /// <typeparam name="T"></typeparam>
        private class WaveFrontDictionary<T> : Dictionary<DelayGraphVertex, Packet<T>>
        {
            /// <summary>
            /// allocate and enter a data into WaveFrontDictionary for vertex v, initialized to refCount
            /// </summary>
            /// <param name="v">a vertex</param>
            /// <param name="refCount">the reference count for number of uses before deleting from dictionary</param>
            /// <param name="data">The data for this definition.</param>
            /// <returns>the allocated data</returns>
            internal void Define(DelayGraphVertex v, int refCount, T data)
            {
                var packet = new Packet<T>(refCount, data);
                if (refCount > 0)
                {
                    this[v] = packet;
                }
            }

            /// <summary>
            /// decrement reference count, if 0, remove v from dictinary
            /// </summary>
            /// <param name="v">a vertex</param>
            /// <returns>the data being kept for v in WaveFrontDictionary</returns>
            internal T Use(DelayGraphVertex v)
            {
                Packet<T> packet;
                if (TryGetValue(v, out packet))
                {
                    var data = packet.Data;
                    packet.RefCount--;
                    if (packet.RefCount == 0)
                    {
                        Remove(v);
                    }
                    return data;
                }
                return default(T);
            }
        }

        /// <summary>
        /// find and return the maximum cyclic throughput cost
        /// </summary>
        /// <param name="sort">topological sorted enumerable of vertices</param>
        /// <param name="graph">a delay graph</param>
        /// <param name="registered">a hash set containing registered vertices</param>
        /// <returns>a value representing maximum throughput cost of all cycles</returns>
        internal static long FindMaxThroughputCostCore(IEnumerable<DelayGraphVertex> sort, DelayGraph graph, HashSet<DelayGraphVertex> registered)
        {
            long maxCycleCost = 0;
            var table = new WaveFrontDictionary<Dictionary<DelayGraphVertex, long>>();
            foreach (var v in sort)
            {
                var inEdges = graph.GetForwardInEdges(v);
                var outEdges = graph.GetForwardOutEdges(v);
                var feedbackInEdges = graph.GetFeedbackInEdges(v);
                var feedbackOutEdges = graph.GetFeedbackOutEdges(v);
                int myRefCount = outEdges.Count();
                long cost = GetThroughputCost(v, registered);

                Dictionary<DelayGraphVertex, long> myData = null;
                // first, consume predecessor's wavefront data, if any
                foreach (var e in inEdges)
                {
                    var p = e.Source;
                    var data = table.Use(p);
                    if (data == null)
                    {
                        continue;
                    }
                    // copy data into myData
                    myData = GetOrMakeMyData(table, v, myRefCount, myData);

                    UpdateMaxData(myData, data);
                }

                if (cost > 0 && myData != null)
                {
                    // incr all costs in myData by cost
                    foreach (var p in myData.Keys.ToList())
                    {
                        myData[p] += cost;
                    }
                }

                if (feedbackInEdges.Any())
                {
                    // v is start of cycle - enter v into myData
                    myData = GetOrMakeMyData(table, v, myRefCount, myData);
                    myData[v] = cost;
                }

                if (myData == null)
                {
                    continue;
                }

                // update max cycle cost
                foreach (var e in feedbackOutEdges)
                {
                    var p = e.Target;
                    long cycleCost;
                    if (myData.TryGetValue(p, out cycleCost) &&
                        cycleCost > maxCycleCost)
                    {
                        maxCycleCost = cycleCost;
                    }
                }
            }

            return maxCycleCost;
        }

        /// <summary>
        /// update myData[v] = myData[v] exists? Max(myData[v], data[v]) : data[v]
        /// </summary>
        /// <param name="myData"></param>
        /// <param name="data"></param>
        private static void UpdateMaxData(Dictionary<DelayGraphVertex, long> myData, Dictionary<DelayGraphVertex, long> data)
        {
            foreach (var kv in data)
            {
                var v = kv.Key;
                long cost = kv.Value;
                long myCost;
                if (!myData.TryGetValue(v, out myCost) ||
                    myCost < cost)
                {
                    // update myData with value of cost if it is larger than myCost, or if v is missing from myData
                    myData[v] = cost;
                }
            }
        }

        /// <summary>
        /// if myData is null, create it and register it into table using table.Define() for vertex v
        /// </summary>
        /// <param name="table">the wavefront dictionary</param>
        /// <param name="v">a vertex</param>
        /// <param name="myRefCount">reference count </param>
        /// <param name="myData">original data, could be null</param>
        /// <returns></returns>
        private static Dictionary<DelayGraphVertex, long> GetOrMakeMyData(WaveFrontDictionary<Dictionary<DelayGraphVertex, long>> table, 
                                                                        DelayGraphVertex v, int myRefCount, Dictionary<DelayGraphVertex, long> myData)
        {
            if (myData == null)
            {
                myData = new Dictionary<DelayGraphVertex, long>();
                table.Define(v, myRefCount, myData);
            }
            return myData;
        }

        private static long GetThroughputCost(DelayGraphVertex v, HashSet<DelayGraphVertex> registered)
        {
            if (v.IsRegistered || registered.Contains(v))
            {
                return v.ThroughputCostIfRegistered;
            }
            return 0;
        }

        /// <summary>
        /// compute the max latency cost from input to outputs of all forward paths
        /// </summary>
        /// <param name="graph">a delay graph</param>
        /// <param name="registered">a registered table</param>
        /// <returns>number of latency cycles</returns>
        internal static long FindMaxLatency(DelayGraph graph, HashSet<DelayGraphVertex> registered)
        {
            var sort = TopologicalSort(graph);
            return FindMaxLatencyCore(sort, graph, registered);
        }

        /// <summary>
        /// find the max latency
        /// </summary>
        /// <param name="sort">sorted list of vertices</param>
        /// <param name="graph">delay graph</param>
        /// <param name="registered">hash table of registered vertices</param>
        /// <returns>number of cycles</returns>
        internal static long FindMaxLatencyCore(IEnumerable<DelayGraphVertex> sort, DelayGraph graph, HashSet<DelayGraphVertex> registered)
        {
            long maxLatencyCost = 0;
            var table = new WaveFrontDictionary<long>();
            foreach (var v in sort)
            {
                var inEdges = graph.GetForwardInEdges(v);
                var outEdges = graph.GetForwardOutEdges(v);
                int myRefCount = outEdges.Count();

                // first, consume predecessor's wavefront data, if any
                long myCost = 0;
                foreach (var e in inEdges)
                {
                    var p = e.Source;
                    var c = table.Use(p);   // default for long is 0
                    // copy data into myData
                    myCost = Math.Max(myCost, c);
                }

                // then add v's latency cost
                myCost += GetLatencyCost(v, registered);

                // register myCost into table
                table.Define(v, myRefCount, myCost);

                // update maxLatencyCost if v has no outEdges
                if (myRefCount == 0 && maxLatencyCost < myCost)
                {
                    maxLatencyCost = myCost;
                }
            }
            return maxLatencyCost;
        }

        private static long GetLatencyCost(DelayGraphVertex v, HashSet<DelayGraphVertex> registered)
        {
            if (v.IsRegistered || registered.Contains(v))
            {
                return v.LatencyCostIfRegistered;
            }
            return 0;
        }

        /// <summary>
        /// group vertices which are inputs and outputs of common node ids
        /// </summary>
        /// <param name="graph">the delay graph</param>
        /// <param name="grouped">hash set of vertices which are grouped</param>
        /// <returns>list of lists of related vertices</returns>
        internal static IEnumerable<IEnumerable<DelayGraphVertex>> FindVertexGroups(DelayGraph graph, out HashSet<DelayGraphVertex> grouped)
        {
            // group vertices which are inputs and outputs of common node ids together
            // there are 3 types of vertices: IsRegistered, (!IsRegistered && IsInputTerminal), (!IsRegisted && !IsInputTerminal)
            // ignore IsRegistered vertices, and group the other types by common node id and types
            grouped = new HashSet<DelayGraphVertex>();

            var inputs = graph.Vertices.Where(v => !v.IsRegistered && v.IsInputTerminal);
            var inputGroups = GroupVerticesByNodeId(inputs, grouped);
            var outputs = graph.Vertices.Where(v => !v.IsRegistered && !v.IsInputTerminal);
            var outputGroups = GroupVerticesByNodeId(outputs, grouped);

            return inputGroups.Concat(outputGroups);
        }

        private static IEnumerable<IEnumerable<DelayGraphVertex>> GroupVerticesByNodeId(IEnumerable<DelayGraphVertex> vertices, HashSet<DelayGraphVertex> grouped)
        {
            var result = new List<List<DelayGraphVertex>>();
            var table = new Dictionary<int, List<DelayGraphVertex>>();
            foreach (var v in vertices)
            {
                var nodeId = v.NodeUniqueId;
                if (nodeId < 0)
                {
                    // bad nodeId - bail
                    continue;
                }
                List<DelayGraphVertex> siblings;
                if (!table.TryGetValue(nodeId, out siblings))
                {
                    siblings = new List<DelayGraphVertex>();
                    table[nodeId] = siblings;
                }
                siblings.Add(v);
            }

            foreach (var lst in table.Values.Where(lst => lst.Count > 1))
            {
                if (grouped != null)
                {
                    foreach (var v in lst)
                    {
                        grouped.Add(v);
                    }
                }
                result.Add(lst);
            }

            return result;
        }

        /// <summary>
        /// Detect cycles in the vertices
        /// </summary>
        /// <param name="graph">a delay graph</param>
        /// <returns>list of lists of vertices</returns>
        internal static List<List<DelayGraphVertex>> DetectCycles(DelayGraph graph)
        {
            return TarjanCycleDetect(graph.Vertices, getFanouts: (v => GetFanouts(v, graph)));
        }

        private static IEnumerable<DelayGraphVertex> GetFanouts(DelayGraphVertex v, DelayGraph graph)
        {
            IEnumerable<DelayGraphEdge> edges;
            if (graph.TryGetOutEdges(v, out edges))
            {
                return edges.Select(e => e.Target);
            }
            return Enumerable.Empty<DelayGraphVertex>();
        }

        /// <summary>
        /// class to bundle node info for SCC algorithm
        /// </summary>
        private class SccInfo
        {
            /// <summary>
            /// this is the index
            /// </summary>
            internal int Index { get; }
            /// <summary>
            /// this is the low link index
            /// </summary>
            internal int LowLink { get; set; }
            /// <summary>
            /// this is the status of whether it is still on stack
            /// </summary>
            internal bool OnStack { get; set; }

            internal SccInfo(int index, int lowLink, bool onStack)
            {
                Index = index;
                LowLink = lowLink;
                OnStack = onStack;
            }
        }

        /// <summary>
        /// This is the recursive version of Tarjan's strongly connected component algorithm
        /// this is useful for reference and for testing whether the iterative version is correct
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="index"></param>
        /// <param name="n"></param>
        /// <param name="infoMap"></param>
        /// <param name="stack"></param>
        /// <param name="sccs"></param>
        /// <param name="getFanouts"></param>
        private static void StronglyConnect<T>(ref int index, T n, Dictionary<T, SccInfo> infoMap, Stack<T> stack, List<List<T>> sccs, Func<T, IEnumerable<T>> getFanouts)
        {
            // preprocess - on first visit
            stack.Push(n);
            var info = new SccInfo(index: index, lowLink: index, onStack: true);
            infoMap[n] = info;
            index++;

            foreach (var next in getFanouts(n))
            {
                SccInfo nextInfo;
                if (!infoMap.TryGetValue(next, out nextInfo))
                {
                    // next has not been visited: recurse into it
                    StronglyConnect(ref index, next, infoMap, stack, sccs, getFanouts);
                    info.LowLink = Math.Min(info.LowLink, infoMap[next].LowLink);
                }
                else if (nextInfo.OnStack)
                {
                    // next is still on stack and hence in the current SCC
                    info.LowLink = Math.Min(info.LowLink, nextInfo.Index);
                }
            }
            // post process - after all children have been processed
            if (info.Index == info.LowLink)
            {
                // n is still a root node, pop the stack and generate an SCC
                var cycle = new List<T>();
                T next;
                do
                {
                    next = stack.Pop();
                    infoMap[next].OnStack = false;
                    cycle.Add(next);
                }
                while (((object)next) != ((object)n));

                sccs.Add(cycle);
            }
        }

        /// <summary>
        /// This returns a list of lists of nodes (of type T), which represents the strongly connected components in the graph
        /// for nodes which are not in any cycle, they will be returned in lists of singletons
        /// </summary>
        /// <typeparam name="T">data type of node in a graph</typeparam>
        /// <param name="nodes">list of nodes of interest</param>
        /// <param name="getFanouts">function to enumerate fanouts of a node</param>
        /// <returns>list of stringly connected components, which are lists of nodes</returns>
        internal static List<List<T>> TarjanCycleDetect<T>(IEnumerable<T> nodes, Func<T, IEnumerable<T>> getFanouts)
        {
            var sccs = new List<List<T>>();
            int index = 0;
            var stack = new Stack<T>();
            var infoMap = new Dictionary<T, SccInfo>();

            foreach (var n in nodes)
            {
                if (!infoMap.ContainsKey(n))
                {
                    StronglyConnectIterative(ref index, n, infoMap, stack, sccs, getFanouts);
                }
            }
            return sccs;
        }

        /// <summary>
        /// this is the recursive version of Tarjan's striongly connected components algorithm
        /// kept for validation purposes
        /// </summary>
        /// <typeparam name="T">data type of node in a graph</typeparam>
        /// <param name="nodes">list of nodes of interest</param>
        /// <param name="getFanouts">function to enumerate fanouts of a node</param>
        /// <returns>list of stringly connected components, which are lists of nodes</returns>
        internal static List<List<T>> TarjanCycleDetectRecursive<T>(IEnumerable<T> nodes, Func<T, IEnumerable<T>> getFanouts)
        {
            var sccs = new List<List<T>>();
            int index = 0;
            var stack = new Stack<T>();
            var infoMap = new Dictionary<T, SccInfo>();

            foreach (var n in nodes)
            {
                if (!infoMap.ContainsKey(n))
                {
                    StronglyConnect(ref index, n, infoMap, stack, sccs, getFanouts);
                }
            }
            return sccs;
        }

        /// <summary>
        /// class to bundle recursion info
        /// </summary>
        /// <typeparam name="T">node data type</typeparam>
        private class ActivationRecord<T>
        {
            /// <summary>
            /// current node being visited in the recursion
            /// </summary>
            internal T Node { get; }

            /// <summary>
            /// status to indicate whether we are waiting for return result of a child
            /// </summary>
            internal bool WaitingForChild { get; set; }

            /// <summary>
            /// number of children from node
            /// </summary>
            private readonly int _childrenCount;

            /// <summary>
            /// index of current child being processed
            /// </summary>
            private int _childIndex;

            /// <summary>
            /// list of children of node
            /// </summary>
            private readonly List<T> _children;

            /// <summary>
            /// construction method, default _index to 0, and WaitingforChild to false
            /// </summary>
            /// <param name="node">current node</param>
            /// <param name="children">list of children for this node</param>
            internal ActivationRecord(T node, List<T> children)
            {
                Node = node;
                _children = children;
                _childrenCount = children.Count;
                _childIndex = 0;
                WaitingForChild = false;
            }

            /// <summary>
            /// return whether this is first visit of node
            /// true iff index is 0 AND not waiting for child
            /// </summary>
            /// <returns>true or false</returns>
            internal bool IsFirstVisit()
            {
                return (_childIndex == 0) && (!WaitingForChild);
            }

            /// <summary>
            /// return whether this is last visit of node
            /// true iff index is -1 (overflowed from last child) AND not waiting for child
            /// corner case: when childrenCount is 0, index will be 0 (no chance to incr), so check for that
            /// </summary>
            /// <returns>true or false</returns>
            internal bool IsLastVisit()
            {
                return (_childIndex < 0 || _childrenCount == 0) && (!WaitingForChild);
            }

            /// <summary>
            /// increment child index
            /// corner cases: when out of bounds, index will be set to -1
            /// </summary>
            internal void MoveToNextChild()
            {
                if (_childIndex < 0 || _childIndex >= (_childrenCount - 1))
                {
                    _childIndex = -1;
                }
                else
                {
                    _childIndex++;
                }
            }

            /// <summary>
            /// get the child to be processed according toe index value
            /// corner case: no children if childCount overflowed number of children
            /// </summary>
            /// <param name="child">child to return, default(T) if no child</param>
            /// <returns>true iff child index is in bound</returns>
            internal bool TryGetChild(out T child)
            {
                if (_childIndex < 0 || _childIndex >= _childrenCount)
                {
                    child = default(T);
                    return false;
                }
                else
                {
                    child = _children[_childIndex];
                    return true;
                }
            }
        }

        /// <summary>
        /// create an activation record for node
        /// </summary>
        /// <typeparam name="T">node type</typeparam>
        /// <param name="node">a node in the graph</param>
        /// <param name="getFanouts">function to return fanouts of a node</param>
        /// <returns>an activation record</returns>
        private static ActivationRecord<T> AllocateActivationRecord<T>(T node, Func<T, IEnumerable<T>> getFanouts)
        {
            // very first time - init
            var children = getFanouts(node).ToList();
            var info = new ActivationRecord<T>(node, children);
            return info;
        }

        /// <summary>
        /// Iterative implementation for finding StronglyConnectedComponent
        /// This implements a recursion stack of activation records to get around stack overflow
        /// in each iteration in the while loop, the current activation record on top of stack "todo"
        /// is examined, and processed according to activation record's current state. it progressed in
        /// sequence:
        ///     - first visit, if first child is not visited yet, push first child, set waitingForChild to true,
        ///                    else, incr activation record
        ///     - if waitingForChild, process child's result, incr activation record
        ///       else, if child is not visited yet, push child, set waitingForChild to true
        ///             else, incr activation record
        ///     ...
        ///     - after processing the last child (or if there is no children at all), IsFinalVisit is true, then
        ///       do the post processing, and pop the activation record
        /// </summary>
        /// <typeparam name="T">data type for node</typeparam>
        /// <param name="index">current index for Tarjan's algorithm</param>
        /// <param name="node">node to visit</param>
        /// <param name="infoMap">dictionary to keep track of node's info for Tarjan algoritm</param>
        /// <param name="stack">stack of nodes in Tarjan's algorithm</param>
        /// <param name="sccs">list of strongly connected components found</param>
        /// <param name="getFanouts">function to find fanouts of nodes in the graph</param>
        private static void StronglyConnectIterative<T>(ref int index, T node, Dictionary<T, SccInfo> infoMap, Stack<T> stack, List<List<T>> sccs, Func<T, IEnumerable<T>> getFanouts)
        {
            var todo = new Stack<ActivationRecord<T>>();

            // activation records belong to stack todo: every entry in todo has one activation record
            // created on push, removed on pop
            todo.Push(AllocateActivationRecord(node, getFanouts));

            while (todo.Any())
            {
                SccInfo info;
                var currState = todo.Peek();
                var curr = currState.Node;

                if (currState.IsFirstVisit())
                {
                    // preprocess - on first visit
                    stack.Push(curr);
                    info = new SccInfo(index: index, lowLink: index, onStack: true);
                    infoMap[curr] = info;
                    index++;
                }
                else
                {
                    info = infoMap[curr];
                }

                T child;
                if (currState.TryGetChild(out child))
                {
                    if (currState.WaitingForChild)
                    {
                        // returned from recursing into child previously
                        info.LowLink = Math.Min(info.LowLink, infoMap[child].LowLink);
                        // done with child
                        currState.WaitingForChild = false;
                        currState.MoveToNextChild();
                    }
                    else
                    {
                        // first processing of child
                        SccInfo childInfo;
                        if (!infoMap.TryGetValue(child, out childInfo))
                        {
                            //// child has not been visited: recurse into it
                            //// StronglyConnect(ref index, next, infoMap, stack, sccs);
                            //// info.LowLink = Math.Min(info.LowLink, nextInfo.LowLink);
                            currState.WaitingForChild = true;
                            todo.Push(AllocateActivationRecord(child, getFanouts));
                        }
                        else
                        {
                            if (childInfo.OnStack)
                            {
                                // next is still on stack and hence in the current SCC
                                info.LowLink = Math.Min(info.LowLink, childInfo.Index);
                            }
                            currState.MoveToNextChild();
                        }
                    }
                }

                // allows post processing once the last child is processed
                if (currState.IsLastVisit())
                {
                    // all children have been processed : do post processing in recursion
                    if (info.Index == info.LowLink)
                    {
                        // n is still a root node, pop the stack and generate an SCC
                        var cycle = new List<T>();
                        T next;
                        do
                        {
                            next = stack.Pop();
                            infoMap[next].OnStack = false;
                            cycle.Add(next);
                        } 
                        while (((object)next) != ((object)curr));

                        sccs.Add(cycle);
                    }
                    todo.Pop();
                }
            }
        }
    }
}
