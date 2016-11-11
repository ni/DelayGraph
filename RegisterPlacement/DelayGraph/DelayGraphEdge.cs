using System;

namespace RegisterPlacement.DelayGraph
{
    /// <summary>
    /// Edges in the DelayGraph hold the delay between the vertices.
    /// Register placement algorithms should place registers to try and
    /// keep the delay between registers less than the clock period.
    /// </summary>
    [Serializable]
    internal class DelayGraphEdge : DirectedEdge<DelayGraphVertex>
    {
        /// <summary>
        /// Delay between vertices
        /// </summary>
        [GraphmlAttribute("int")]
        public int Delay { get; private set; }

        /// <summary>
        /// Feedback edge makes cycles in otherwise forward data flow graphs
        /// </summary>
        [GraphmlAttribute("boolean")]
        public bool IsFeedback { get; private set; }

        /// <summary>
        /// constructor for a delay graph edge
        /// </summary>
        /// <param name="source">vertex that is source of edge</param>
        /// <param name="target">vertex that is target of edge</param>
        /// <param name="delay">number specifying delay on this edge in ps</param>
        /// <param name="isFeedback">true iff this edge represents a feedback edge</param>
        internal DelayGraphEdge(DelayGraphVertex source, DelayGraphVertex target, int delay, bool isFeedback)
            : base(source, target)
        {
            Delay = delay;
            IsFeedback = isFeedback;
        }
    }
}
