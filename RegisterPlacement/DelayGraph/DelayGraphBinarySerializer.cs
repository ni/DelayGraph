using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace RegisterPlacement.DelayGraph
{
    /// <summary>
    /// Object to serialize a DelayGraph into Binary Format
    /// </summary>
    internal class DelayGraphBinarySerializer
    {
        private DelayGraph _graph;

        /// <summary>
        /// Serializes a delay graph into a binary file using native .NET formatters. 
        /// </summary>
        /// <param name="graph">DelayGraph to serialize</param>
        /// <param name="filePath">Path to write serialized delay graph</param>
        internal void Serialize(DelayGraph graph, string filePath)
        {
            _graph = graph;

            if (Path.GetExtension(filePath) != ".bin")
            {
                filePath += ".bin";
            }

            IFormatter formatter = new BinaryFormatter();
            Stream stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            formatter.Serialize(stream, _graph);
            stream.Close();
        }

        /// <summary>
        /// Loads a DelayGraph object into memory from the specified serialized file.
        /// Uses native .NET formatters to deserialize the object.  
        /// </summary>
        /// <param name="filepath">Path to a serialized DelayGraph object. This should be a binary file</param>
        /// <returns>Reconstructed DelayGraph object</returns>
        internal static DelayGraph DeserializeFromBinary(string filepath)
        {
            IFormatter formatter = new BinaryFormatter();
            Stream stream = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.Read);
            DelayGraph dg = (DelayGraph)formatter.Deserialize(stream);
            stream.Close();
            return dg;
        }
    }
}
