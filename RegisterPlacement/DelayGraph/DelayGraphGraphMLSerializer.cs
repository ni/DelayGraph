using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace RegisterPlacement.DelayGraph
{
    /// <summary>
    /// Object to serialize a DelayGraph into GraphML format.    
    /// GraphML is a common XML format to describe graph objects, which
    /// many popular graph packages have parsers for. All properties to serialize
    /// must be tagged with the [GraphmlAttribute] attribute; 
    /// </summary>
    internal class DelayGraphGraphMlSerializer
    {
        private DelayGraph _graph;

        /// <summary>
        /// Serializes a DelayGraph object to GraphML format.
        /// </summary>
        /// <param name="graph">DelayGraph to serialize</param>
        /// <param name="filePath">Path to write serialized delay graph</param>
        internal void Serialize(DelayGraph graph, string filePath)
        {
            _graph = graph; 

            if (Path.GetExtension(filePath) != ".graphml")
            {
                filePath += ".graphml";
            }

            using (StreamWriter outfile = new StreamWriter(filePath))
            {
                // add file XML file header, graph header, properties, and parse info
                outfile.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
                outfile.WriteLine("<!-- This file was generated from the DelayGraph object. -->");
                outfile.WriteLine("<graphml xmlns=\"http://graphml.graphdrawing.org/xmlns\"");
                outfile.WriteLine("            xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"");
                outfile.WriteLine("            xsi:schemaLocation=\"http://graphml.graphdrawing.org/xmlns");
                outfile.WriteLine("                                http://graphml.graphdrawing.org/xmlns/1.0/graphml.xsd\">");

                SerializeVertexAndEdgeProperties(outfile);

                outfile.WriteLine("  <graph id=\"G\" edgedefault=\"directed\"");
                outfile.WriteLine(String.Format(CultureInfo.InvariantCulture, "          parse.nodes=\"{0}\" parse.edges=\"{1}\"", _graph.VertexCount, _graph.EdgeCount));
                outfile.WriteLine("          parse.nodeids=\"canonical\" parse.edgeids=\"canonical\"");
                // not including max indegree and max outdegree of nodes...I don't think we need to include this
                outfile.WriteLine("          parse.order=\"nodesfirst\">");

                SerializeVertices(outfile);
                SerializeEdges(outfile);

                outfile.WriteLine("  </graph>");
                outfile.WriteLine("</graphml>");
            }
        }

        private static void SerializeVertexAndEdgeProperties(StreamWriter outfile)
        {
            foreach (var property in typeof(DelayGraphVertex).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                SerializePropertyAttribute(outfile, property, "node");
            }

            // add edge properties to graph
            foreach (var property in typeof(DelayGraphEdge).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                SerializePropertyAttribute(outfile, property, "edge");
            }
        }

        private static void SerializePropertyAttribute(StreamWriter outfile, PropertyInfo property, string forType)
        {
            GraphmlAttributeAttribute attr;
            if (TryGetGraphmlAttribute(property, out attr))
            {
                var name = property.Name;
                var type = attr.GraphmlType;
                outfile.WriteLine(String.Format(CultureInfo.InvariantCulture, "  <key id=\"{0}\" for=\"{1}\" attr.name=\"{0}\" attr.type=\"{2}\"/>", name, forType, type));
            }
        }

        private void SerializeVertices(StreamWriter outfile)
        {
            foreach (var vertex in _graph.Vertices)
            {
                var inDegree = _graph.InEdges(vertex).Count();
                var outDegree = _graph.OutEdges(vertex).Count();

                outfile.WriteLine(String.Format(CultureInfo.InvariantCulture, "    <node id=\"n{0}\" parse.indegree=\"{1}\" parse.outdegree=\"{2}\">",
                                                vertex.VertexId, inDegree, outDegree));
                SerializePropertyValues(outfile, vertex);
                outfile.WriteLine("    </node>");
            }
        }

        private void SerializeEdges(StreamWriter outfile)
        {
            int i = 0;
            foreach (var edge in _graph.Edges)
            {
                outfile.WriteLine(String.Format(CultureInfo.InvariantCulture, "    <edge id=\"e{0}\" source=\"n{1}\" target=\"n{2}\">", i++, edge.Source.VertexId, edge.Target.VertexId));
                SerializePropertyValues(outfile, edge);
                outfile.WriteLine("    </edge>");
            }
        }

        private void SerializePropertyValues(StreamWriter outfile, Object obj)
        {
            foreach (var property in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                // only serialize properties that are marked
                GraphmlAttributeAttribute attr;
                if (!TryGetGraphmlAttribute(property, out attr))
                {
                    continue;
                }

                outfile.WriteLine(String.Format(CultureInfo.InvariantCulture, "      <data key=\"{0}\">{1}</data>", property.Name, GetPropertyValue(obj, property)));
            }
        }

        private static string GetPropertyValue(Object obj, PropertyInfo prop)
        {
            if (prop.PropertyType.IsEnum)
            {
                var tmp = (int)prop.GetValue(obj);
                return tmp.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                var tmp = prop.GetValue(obj);
                return tmp.ToString();
            }
        }

        private static bool TryGetGraphmlAttribute(PropertyInfo prop, out GraphmlAttributeAttribute attr)
        {
            var attributes = prop.GetCustomAttributes(typeof(GraphmlAttributeAttribute)).ToList();

            if (attributes.Any())
            {
                attr = (GraphmlAttributeAttribute)attributes[0];
                return true;
            }

            attr = null;
            return false;
        }

        /// <summary>
        /// Recreates a DelayGraph object from a graphml file.
        /// TODO: verify the graphml against a schema
        /// </summary>
        /// <param name="graphml">location of the graphml file on disk</param>
        /// <returns>Deserialized DelayGraph object</returns>
        internal static DelayGraph DeserializeFromGraphMl(string graphml)
        {
            XDocument doc = XDocument.Load(graphml);

            var nmsp = "{http://graphml.graphdrawing.org/xmlns}";

            DelayGraph delayGraph = new DelayGraph();
            if (doc.Root != null)
            {
                var graph = doc.Root.Element(nmsp + "graph");

                var nodeIdToVertexMap = RecreateVertices(graph, nmsp, delayGraph);
                RecreateEdges(graph, nmsp, nodeIdToVertexMap, delayGraph);
            }

            return delayGraph;
        }

        private static Dictionary<String, DelayGraphVertex> RecreateVertices(XElement graph, String nmsp, DelayGraph delayGraph)
        {
            var nodeIdToVertexMap = new Dictionary<String, DelayGraphVertex>();
            var vertexType = typeof(DelayGraphVertex);

            // todo: update this map if new properties are added...or use "CreatePropertyTypeMap" below 
            var propertyToTypeMap = new Dictionary<String, Type>()
            {
                { "VertexId", typeof(int) },
                { "NodeType", typeof(int) },
                { "NodeUniqueId", typeof(int) },
                { "ThroughputCostIfRegistered", typeof(long) },
                { "LatencyCostIfRegistered", typeof(long) },
                { "RegisterCostIfRegistered", typeof(long) },
                { "IsRegistered", typeof(bool) },
                { "IsInputTerminal", typeof(bool) },
                { "IsOutputTerminal", typeof(bool) },
                { "DisallowRegister", typeof(bool) }
            };

            // get the nodes
            foreach (var node in graph.Elements(nmsp + "node"))
            {
                // create new vertex
                var vertex = new DelayGraphVertex();
                var id = (string)node.Attribute("id");
                nodeIdToVertexMap.Add(id, vertex);

                // populate the fields of the node
                foreach (var data in node.Elements())
                {
                    var key = (string)data.Attribute("key");
                    dynamic contents = Convert.ChangeType(data.Value, propertyToTypeMap[key], CultureInfo.InvariantCulture);
                    vertexType.GetProperty(key).SetValue(vertex, contents);
                }

                // add the node to the graph
                delayGraph.AddVertex(vertex);
            }
            return nodeIdToVertexMap;
        }

        private static void RecreateEdges(XElement graph, String nmsp, Dictionary<String, DelayGraphVertex> nodeIdToVertexMap, DelayGraph delayGraph)
        {
            // get the edges
            foreach (var edge in graph.Elements(nmsp + "edge"))
            {
                var source = nodeIdToVertexMap[(string)edge.Attribute("source")];
                var sink = nodeIdToVertexMap[(string)edge.Attribute("target")];
                int delay = 0;
                bool isFeedback = false;
                var isDelay = true;

                // this should only execute twice
                foreach (var data in edge.Elements())
                {
                    if (isDelay)
                    {
                        delay = (int)data;
                        isDelay = false;
                        continue;
                    }
                    isFeedback = (bool)data;
                }
                var newEdge = new DelayGraphEdge(source, sink, delay, isFeedback);
                delayGraph.AddEdge(newEdge);
            }
        }

        // Function that could be used to create the property type map
        private Dictionary<string, Type> CreatePropertyTypeMap(XDocument doc, String nmsp)
        {
            var propertyToTypeMap = new Dictionary<String, Type>();

            foreach (var key in doc.Root.Elements(nmsp + "key"))
            {
                var property = (string)key.Attribute("id");
                var type = (string)key.Attribute("attr.type");
                switch (type)
                {
                    case "int":
                        propertyToTypeMap.Add(property, typeof(int));
                        break;
                    case "long":
                        propertyToTypeMap.Add(property, typeof(long));
                        break;
                    case "boolean":
                        propertyToTypeMap.Add(property, typeof(bool));
                        break;
                    case "string":
                        propertyToTypeMap.Add(property, typeof(string));
                        break;
                    case "double":
                        propertyToTypeMap.Add(property, typeof(double));
                        break;
                    case "single":
                        propertyToTypeMap.Add(property, typeof(float));
                        break;
                    default:
                        throw new InvalidOperationException("Invalid data type found in graphml file");
                }
            }
            return propertyToTypeMap;
        }

    }

    #region AtrributeTags

    /// <summary>
    /// Class used to tag attributes for GraphML serialization
    /// </summary>
    [AttributeUsage(AttributeTargets.All)]
    internal sealed class GraphmlAttributeAttribute : Attribute
    {
        /// <summary>
        /// Corresponding GraphML type for a C# type
        /// </summary>
        internal String GraphmlType { get; private set; }

        /// <summary>
        /// Constructor for a Graphml Attribute
        /// </summary>
        /// <param name="type">GraphML type</param>
        internal GraphmlAttributeAttribute(String type)
        {
            GraphmlType = type;
        }
    }

    #endregion
}
