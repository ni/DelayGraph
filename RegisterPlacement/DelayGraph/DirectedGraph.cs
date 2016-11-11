using System;
using System.Collections.Generic;
using System.Linq;

namespace RegisterPlacement.DelayGraph
{
    /// <summary>
    /// Home brew version of a simple graph data structure to replace limited usage of QuickGraph
    /// </summary>
    /// <typeparam name="TVertex">this is the vertex data type</typeparam>
    /// <typeparam name="TEdge">this is the edge data type</typeparam>
    [Serializable]
    internal class DirectedGraph<TVertex, TEdge> where TEdge : DirectedEdge<TVertex>
    {
        /// <summary>
        /// private ordered hashset of vertices, so we can return list of vertices in stable order (according to their add order)
        /// </summary>
        private readonly OrderedHashSet<TVertex> _vertices;

        /// <summary>
        /// private ordered hashset of edges, so we can return a list of edges in stable order (according to their add order)
        /// </summary>
        private readonly OrderedHashSet<TEdge> _edges;

        /// <summary>
        /// this is a map of vertex to vertex connection info VertexInfo is a private class that keeps in and out edges for each vertex
        /// </summary>
        private readonly Dictionary<TVertex, VertexInfo> _vertexMap;

        /// <summary>
        /// constructor - initialize all internal data structures
        /// </summary>
        internal DirectedGraph()
        {
            _vertices = new OrderedHashSet<TVertex>();
            _edges = new OrderedHashSet<TEdge>();
            _vertexMap = new Dictionary<TVertex, VertexInfo>();
        }

        /// <summary>
        /// Property get for Vertices - bridge OrderedHashset to IEnumerable
        /// </summary>
        internal IEnumerable<TVertex> Vertices
        {
            get
            {
                foreach (var v in _vertices)
                {
                    yield return v;
                }
            }
        }

        /// <summary>
        /// Property get for Edges - bridge OrderedHashset to IEnumerable
        /// </summary>
        internal IEnumerable<TEdge> Edges
        {
            get
            {
                foreach (var e in _edges)
                {
                    yield return e;
                }
            }
        }

        /// <summary>
        /// Get Incoming edges of vertex v
        /// </summary>
        /// <param name="v">this is a vertex</param>
        /// <param name="edges">return edges, null if not found</param>
        /// <returns>true iff vertex v is found</returns>
        internal bool TryGetInEdges(TVertex v, out IEnumerable<TEdge> edges)
        {
            VertexInfo info;
            edges = null;

            if (_vertexMap.TryGetValue(v, out info))
            {
                edges = info.InEdges;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Get Outgoing edges of vertex v
        /// </summary>
        /// <param name="v">this is a vertex</param>
        /// <param name="edges">return edges, null if not found</param>
        /// <returns>true iff vertex v is found</returns>
        internal bool TryGetOutEdges(TVertex v, out IEnumerable<TEdge> edges)
        {
            VertexInfo info;
            edges = null;

            if (_vertexMap.TryGetValue(v, out info))
            {
                edges = info.OutEdges;
                return true;
            }
            return false;
        }

        /// <summary>
        /// add a vertex v to graph
        /// </summary>
        /// <param name="v">this is a vertex</param>
        /// <returns>true if properly added, false if already exists</returns>
        internal bool AddVertex(TVertex v)
        {
            // add v to end of _vertices
            if (_vertices.AddLast(v))
            {
                var info = new VertexInfo();
                _vertexMap[v] = info;
                return true;
            }
            return false;
        }

        /// <summary>
        /// add edge e to graph - it is expected that vertices have been added to graph before adding this edge
        /// </summary>
        /// <param name="e">this is an edge</param>
        /// <returns>true if properly added, false if already exists</returns>
        internal bool AddEdge(TEdge e)
        {
            if (_edges.AddLast(e))
            {
                return UpdateEdgeConnections(e);
            }
            return false;
        }

        /// <summary>
        /// register edge connectivity to VertexMap
        /// </summary>
        /// <param name="e">this is an edge</param>
        /// <returns>true if everything is ok, false if one or more vertices not added to graph yet</returns>
        private bool UpdateEdgeConnections(TEdge e)
        {
            VertexInfo srcInfo, targetInfo;
            if (_vertexMap.TryGetValue(e.Source, out srcInfo) &&
                _vertexMap.TryGetValue(e.Target, out targetInfo))
            {
                srcInfo.OutEdges.Add(e);
                targetInfo.InEdges.Add(e);
                return true;
            }
            return false;
        }

        private void RemoveEdgeConnections(TEdge e)
        {
            VertexInfo srcInfo, targetInfo;
            if (_vertexMap.TryGetValue(e.Source, out srcInfo))
            {
                srcInfo.OutEdges.Remove(e);
            }
            if (_vertexMap.TryGetValue(e.Target, out targetInfo))
            {
                targetInfo.InEdges.Remove(e);
            }
        }

        /// <summary>
        /// add edge e and its source and target vertices (if not already added)
        /// </summary>
        /// <param name="e">this is an edge</param>
        /// <returns>true iff edge is properly added</returns>
        internal bool AddVerticesAndEdge(TEdge e)
        {
            // add source and target vertices in case they have not been added to graph
            AddVertex(e.Source);
            AddVertex(e.Target);
            // then add edge itself (and update connections)
            return AddEdge(e);
        }

        /// <summary>
        /// property of number of edges
        /// </summary>
        internal int EdgeCount => _edges.Count();

        /// <summary>
        /// property of number of vertices
        /// </summary>
        internal int VertexCount => _vertices.Count();

        /// <summary>
        /// get outedges of vertex v
        /// </summary>
        /// <param name="v">this is a vertex</param>
        /// <returns>enumerable out edges, empty if not found</returns>
        internal IEnumerable<TEdge> OutEdges(TVertex v)
        {
            IEnumerable<TEdge> edges;
            if (TryGetOutEdges(v, out edges))
            {
                return edges;
            }
            return Enumerable.Empty<TEdge>();
        }

        /// <summary>
        /// return incomig edges for vertex v
        /// </summary>
        /// <param name="v">this is a vertex</param>
        /// <returns>enumerable in edges, empty if not found</returns>
        internal IEnumerable<TEdge> InEdges(TVertex v)
        {
            IEnumerable<TEdge> edges;
            if (TryGetInEdges(v, out edges))
            {
                return edges;
            }
            return Enumerable.Empty<TEdge>();
        }

        /// <summary>
        /// remove edge and its connections from graph - note this does not remove vertex from the graph
        /// </summary>
        /// <param name="e">this is an edge</param>
        internal void RemoveEdge(TEdge e)
        {
            RemoveEdgeConnections(e);
            _edges.Remove(e);
        }

        /// <summary>
        /// find and return an edge with source and target vertices
        /// this returns the first edge found, in case of having many edges from same source and target vertices
        /// </summary>
        /// <param name="source">this is the source vertex</param>
        /// <param name="target">this is the target vertex</param>
        /// <param name="edge">this is output for edge found, null if not found</param>
        /// <returns>true iff edge is found</returns>
        internal bool TryGetEdge(TVertex source, TVertex target, out TEdge edge)
        {
            foreach (var e in OutEdges(source))
            {
                if (e.Target.Equals(target))
                {
                    edge = e;
                    return true;
                }
            }
            edge = null;
            return false;
        }

        #region Private Class

        /// <summary>
        /// this is a data structure of incoming and outgoing edges for a vertex
        /// </summary>
        [Serializable]
        private class VertexInfo
        {
            /// <summary>
            /// a collection of Incoming edges
            /// </summary>
            internal ICollection<TEdge> InEdges { get; }

            /// <summary>
            /// a collection of outgoing edges
            /// </summary>
            internal ICollection<TEdge> OutEdges { get; }

            /// <summary>
            /// constructor - initialize with lists of incoming and outgoing edges
            /// </summary>
            internal VertexInfo()
            {
                InEdges = new List<TEdge>();
                OutEdges = new List<TEdge>();
            }
        }
        #endregion
    }
}
