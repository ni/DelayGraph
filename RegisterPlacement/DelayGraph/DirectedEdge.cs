using System;

namespace RegisterPlacement.DelayGraph
{
    /// <summary>
    /// This is a class for directed edge, to take the place of Edge in QuickGraph
    /// </summary>
    /// <typeparam name="TVertex">this is type for vertex</typeparam>
    [Serializable]
    internal class DirectedEdge<TVertex>
    {
        /// <summary>
        /// this is the source of directed edge
        /// </summary>
        internal TVertex Source { get; set; }

        /// <summary>
        /// this is the target of directed edge
        /// </summary>
        internal TVertex Target { get; set; }

        /// <summary>
        /// constructor for directed edge
        /// </summary>
        /// <param name="source">this is the source of the edge</param>
        /// <param name="target">this is the target of the edge</param>
        internal DirectedEdge(TVertex source, TVertex target)
        {
            Source = source;
            Target = target;
        }
    }
}