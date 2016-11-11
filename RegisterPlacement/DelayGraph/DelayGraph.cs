using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace RegisterPlacement.DelayGraph
{
    /// <summary>
    /// Provides a separate type for the graph used to store terminal vertices and the
    /// delays between them on edges.
    /// </summary>
    [Serializable]
    internal class DelayGraph : DirectedGraph<DelayGraphVertex, DelayGraphEdge>
    {
        /// <summary>
        /// return an enumerable of forward in edges to vertex v
        /// </summary>
        /// <param name="v">the vertex</param>
        /// <returns>enumerable of in edges which are forward edges</returns>
        internal IEnumerable<DelayGraphEdge> GetForwardInEdges(DelayGraphVertex v)
        {
            IEnumerable<DelayGraphEdge> inEdges;
            if (TryGetInEdges(v, out inEdges))
            {
                return inEdges.Where(e => !e.IsFeedback);
            }
            return Enumerable.Empty<DelayGraphEdge>();
        }

        /// <summary>
        /// return an enumerable of forward out edges to vertex v
        /// </summary>
        /// <param name="v">the vertex</param>
        /// <returns>enumerable of out edges which are forward edges</returns>
        internal IEnumerable<DelayGraphEdge> GetForwardOutEdges(DelayGraphVertex v)
        {
            IEnumerable<DelayGraphEdge> outEdges;
            if (TryGetOutEdges(v, out outEdges))
            {
                return outEdges.Where(e => !e.IsFeedback);
            }
            return Enumerable.Empty<DelayGraphEdge>();
        }

        /// <summary>
        /// get and return an enumerable of input edges from v which are feedback
        /// </summary>
        /// <param name="v">this is a delay graph vertex</param>
        /// <returns>an enumerable of delay graph edges</returns>
        internal IEnumerable<DelayGraphEdge> GetFeedbackInEdges(DelayGraphVertex v)
        {
            IEnumerable<DelayGraphEdge> inEdges;
            if (TryGetInEdges(v, out inEdges))
            {
                return inEdges.Where(e => e.IsFeedback);
            }
            return Enumerable.Empty<DelayGraphEdge>();
        }

        /// <summary>
        /// get and return an enumerable of output edges from v which are feedback edges
        /// </summary>
        /// <param name="v">a vertex in delay graph</param>
        /// <returns>an enumerable of delay graph edges</returns>
        internal IEnumerable<DelayGraphEdge> GetFeedbackOutEdges(DelayGraphVertex v)
        {
            IEnumerable<DelayGraphEdge> outEdges;
            if (TryGetOutEdges(v, out outEdges))
            {
                return outEdges.Where(e => e.IsFeedback);
            }
            return Enumerable.Empty<DelayGraphEdge>();
        }

        #region DOT File

        /// <summary>
        /// Creates a dot string that can be used to visualize the delay graph
        /// </summary>
        /// <param name="registeredTerminals">A list of terminals in the delay graph where a register has been placed</param>
        /// <returns>A dot string</returns>
        internal string GetDotString(HashSet<DelayGraphVertex> registeredTerminals = null)
        {
            var nodeIds = Zip(Vertices, Enumerable.Range(0, Vertices.Count())).ToDictionary(kv => kv.Key, kv => kv.Value);

            var sb = new StringBuilder();
            sb.AppendLine("digraph G {");

            // cluster vertices and print info to file
            int i = 0;
            foreach (var vertexGroup in Vertices.GroupBy(v => v.NodeUniqueId))
            {
                sb.AppendLine(CreateSubGraph(vertexGroup.ToList(), "cluster" + i++, nodeIds, registeredTerminals));
            }

            // print edge information to dot file
            foreach (var edge in Edges)
            {
                var attrs = FormatEdge(edge).Select(kv => kv.Key + "=\"" + kv.Value + "\"");
                sb.AppendLine("  " + nodeIds[edge.Source] + " -> " + nodeIds[edge.Target] + " [" + String.Join(", ", attrs) + "];");
            }

            sb.AppendLine("}");

            return sb.ToString();
        }

        /// <summary>
        /// Creates an enumerator that produces key,value pairs from two IEnumerable entities.
        /// </summary>
        /// <typeparam name="T0">The type in the IEnumerable used for the keys.</typeparam>
        /// <typeparam name="T1">Type type in the IEnumerable used for the values.</typeparam>
        /// <param name="first">The IEnumerable containing the keys.</param>
        /// <param name="second">The IEnumerable containing the values.</param>
        /// <returns>An IEnumerable of key,value pairs formed from the smaller of the first and second input IEnumerables.</returns>
        private static IEnumerable<KeyValuePair<T0, T1>> Zip<T0, T1>(IEnumerable<T0> first, IEnumerable<T1> second)
        {
            IEnumerator<T0> firstEnum = first.GetEnumerator();
            IEnumerator<T1> secondEnum = second.GetEnumerator();
            while (true)
            {
                bool firstStep = firstEnum.MoveNext();
                bool secondStep = secondEnum.MoveNext();
                if (firstStep && secondStep)
                {
                    yield return new KeyValuePair<T0, T1>(firstEnum.Current, secondEnum.Current);
                }
                else
                {
                    // throw new InvalidOperationException("Zipped IEnumerables must have the same number of elements.");
                    yield break;
                }
            }
        }

        private static string CreateSubGraph(List<DelayGraphVertex> vertices, string name, Dictionary<DelayGraphVertex, int> nodeIds, HashSet<DelayGraphVertex> registeredTerminals)
        {
            StringBuilder sb = new StringBuilder();
            var groupName = String.Format(CultureInfo.InvariantCulture, "\"Node: {0}\"", vertices[0].NodeUniqueId);
            sb.AppendLine(String.Format(CultureInfo.InvariantCulture, "  subgraph {0} {{ ", name));
            sb.AppendLine("    label = " + groupName);
            sb.AppendLine("    style = \"bold\"");

            foreach (var vertex in vertices)
            {
                var attrs = FormatVertex(vertex, registeredTerminals).Select(kv => kv.Key + "=\"" + kv.Value + "\"");
                sb.AppendLine("    " + nodeIds[vertex] + " [" + String.Join(", ", attrs) + "];");
            }
            sb.AppendLine("  }");

            return sb.ToString();
        }

        private static Dictionary<string, string> FormatEdge(DelayGraphEdge edge)
        {
            var attrs = new Dictionary<string, string>();

            if (edge.IsFeedback)
            {
                attrs.Add("label", "[" + edge.Delay.ToString(CultureInfo.InvariantCulture) + "]");
                attrs.Add("color", DotColor.Red);
            }
            else
            {
                attrs.Add("label", edge.Delay.ToString(CultureInfo.InvariantCulture));
            }

            return attrs;
        }

        private static Dictionary<string, string> FormatVertex(DelayGraphVertex vertex, HashSet<DelayGraphVertex> registeredTerminals)
        {
            var attrs = new Dictionary<string, string>();

            attrs.Add("label", "[" + vertex.VertexId + "]");

            if (vertex.IsInputTerminal)
            {
                attrs.Add("shape", "box");
            }

            if (vertex.IsRegistered)
            {
                attrs.Add("style", "filled");
                attrs.Add("fillcolor", DotColor.Red);
            }
            else if (registeredTerminals != null && registeredTerminals.Contains(vertex))
            {
                attrs.Add("style", "filled");
                attrs.Add("fillcolor", DotColor.DarkGray);
            }
            else if (vertex.DisallowRegister)
            {
                attrs.Add("style", "filled");
                attrs.Add("fillcolor", DotColor.LightBlue);
            }

            return attrs;
        }

        /// <summary>
        /// Class that holds string color codes in the format dot files expect. 
        /// Add to this as necessary. 
        /// </summary>
        private static class DotColor
        {
            public const string Red = "#FF0000";
            public const string LightBlue = "#ADD8E6FF";
            public const string YellowGreen = "#9ACD32FF";
            public const string Violet = "#EE82EEFF";
            public const string DarkGray = "#A9A9A9";
        }

        #endregion DOT FILE

    }
}
