using System;

namespace RegisterPlacement.DelayGraph
{
    /// <summary>
    /// Vertex in the DelayGraph. Latency selection algorithms can decide to 
    /// register vertices. The cost of registering (in throughput, latency, and registers) 
    /// is contained in this class. Other members of this class are mainly for
    /// debugging and visualization and should not have algorithms dependent on them.
    /// </summary>
    [Serializable]
    internal class DelayGraphVertex
    {
        #region Properties

        /// <summary>
        /// Unique id of this vertex
        /// </summary>
        [GraphmlAttribute("int")]
        public int VertexId { get; private set; }

        /// <summary>
        /// Type of node in the original DFIR...used in the solution computation
        /// </summary>
        [GraphmlAttribute("int")]
        public DelayGraphNodeType NodeType { get; private set; }

        /// <summary>
        /// Node UniqueID that the delay graph node is associated with
        /// </summary>
        [GraphmlAttribute("int")]
        public int NodeUniqueId { get; private set; }

        /// <summary>
        /// Throughput cost if this vertex is registered.
        /// </summary>
        [GraphmlAttribute("long")]
        public long ThroughputCostIfRegistered { get; set; }

        /// <summary>
        /// Latency cost if this vertex is registered.
        /// </summary>
        [GraphmlAttribute("long")]
        public long LatencyCostIfRegistered { get; set; }

        /// <summary>
        /// Register cost if this vertex is registered.
        /// </summary>
        [GraphmlAttribute("long")]
        public long RegisterCostIfRegistered { get; set; }

        /// <summary>
        /// Is this a vertex that is registered as part of the initial delay graph because it is an internal register on a component. 
        /// Do not use this property to mark which nodes are registered as part of the <see cref="DelayGraphSolution"/>
        /// </summary>
        [GraphmlAttribute("boolean")]
        public bool IsRegistered { get; set; }

        /// <summary>
        /// Is this an input terminal Vertex.
        /// </summary>
        [GraphmlAttribute("boolean")]
        public bool IsInputTerminal { get; set; }

        /// <summary>
        /// Is this an output terminal Vertex.
        /// </summary>
        [GraphmlAttribute("boolean")]
        public bool IsOutputTerminal { get; set; }

        /// <summary>
        /// Is this a vertex that cannot allow registering
        /// </summary>
        [GraphmlAttribute("boolean")]
        public bool DisallowRegister { get; set; }

        #endregion 

        #region Public Functions

        /// <summary>
        /// Returns true if the vertex represents a DFIR terminal, false otherwise
        /// </summary>
        /// <returns>bool</returns>
        public bool IsTerminal()
        {
            return IsInputTerminal || IsOutputTerminal; 
        }

        /// <summary>
        /// Returns true if the vertex is an internal regiser, false otherwise
        /// </summary>
        /// <returns>bool</returns>
        public bool IsInternalRegister()
        {
            // currently this is true, but this theoretically could change...
            return IsRegistered;
        }

        /// <inheritdot />
        public override string ToString()
        {
            return nameof(VertexId) + ":" + VertexId;
        }

        #endregion

        #region Constructors

        /// <summary>
        /// constructor for a delay graph vertex
        /// </summary>
        /// <param name="vertexId">unique id of the vertex in the graph</param>
        /// <param name="throughputCostIfRegistered">cost for throughput</param>
        /// <param name="latencyCostIfRegistered">latency cost</param>
        /// <param name="registerCostIfRegistered">register cost</param>
        /// <param name="nodeType">type of dfir node in the graph</param>
        /// <param name="nodeUniqueId">Unique node id that this was node was decomposed from</param>
        /// <param name="isInputTerminal">true if the vertex represents an input terminal</param>
        /// <param name="isOutputTerminal">true if the vertex represents an output terminal</param>
        /// <param name="isRegistered">set value to IsRegisted field</param>
        /// <param name="disallowRegister">don't allow this node to be registered</param>
        internal DelayGraphVertex(
            int vertexId,
            long throughputCostIfRegistered,
            long latencyCostIfRegistered,
            long registerCostIfRegistered,
            DelayGraphNodeType nodeType,
            int nodeUniqueId,
            bool isInputTerminal,
            bool isOutputTerminal,
            bool isRegistered,
            bool disallowRegister)
        {
            VertexId = vertexId;
            ThroughputCostIfRegistered = throughputCostIfRegistered;
            LatencyCostIfRegistered = latencyCostIfRegistered;
            RegisterCostIfRegistered = registerCostIfRegistered;
            NodeType = nodeType;
            NodeUniqueId = nodeUniqueId;
            DisallowRegister = disallowRegister; // false;
            IsRegistered = isRegistered; // false;
            IsInputTerminal = isInputTerminal;
            IsOutputTerminal = isOutputTerminal;
        }

        /// <summary>
        /// constructor for a delay graph vertex, set everything to default value
        /// </summary>
        internal DelayGraphVertex()
        {
        }

        #endregion 
    }

    #region Enumerations

    /// <summary>
    /// Enumerations of some Dfir node types that are required for the DelayGraphSolution class. 
    /// Only used while computing quality of solution. Don't use this enumerated type!
    /// </summary>
    internal enum DelayGraphNodeType
    {
        /// <summary>
        /// Represents a FeedbackInputNode node
        /// </summary>
        FeedbackInputNode,

        /// <summary>
        /// Represents a BorderNode node
        /// </summary>
        BorderNode,

        /// <summary>
        /// Represents a LeftShiftRegister node
        /// </summary>
        LeftShiftRegister,

        /// <summary>
        /// Represents a RightShiftRegister node
        /// </summary>
        RightShiftRegister,

        /// <summary>
        /// Represents all other nodes
        /// </summary>
        Other,

        /// <summary>
        /// Represents an unknown node
        /// </summary>
        Unknown
    }

    #endregion
}
