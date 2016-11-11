using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace RegisterPlacement.DelayGraph
{
    /// <summary>
    /// 
    /// Represents an ordered (not sorted) collection. The values can be added at the start or
    /// at the end of the sequence only but they can be removed from everywhere.
    /// 
    /// Two inner data structures are used to provide fast operations: Dictionary and LinkedList.
    /// The list is used to maintain the order of the values while the dictionary is used for random
    /// fast access in between the limits of the list.
    /// 
    /// <![CDATA[
    /// 
    /// 
    ///                        +-----------------+                      
    ///                        |    Dictionary   |                      
    ///                        |  value to node  |                      
    ///                        +-----------------+                      
    ///                                                                 
    ///                        /        |        \                      
    ///                       /         |         \                     
    ///                      /          |          \                    
    ///                     /           v           \                   
    ///  First                                                     Last 
    ///           +---------+      +---------+      +---------+         
    ///      +--> |         | +--> |         | +--> |         | <--+     
    ///           | value 1 |      | value 2 |      | value 3 |         
    ///           |         | <--+ |         | <--+ |         |         
    ///           +---------+      +---------+      +---------+         
    ///
    ///
    /// ]]>
    /// 
    /// </summary>
    /// <typeparam name="T">It can be either a reference type or an enum.</typeparam>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix", Justification = "The sufix HashSet represents much better what this class is doing.")]
    [Serializable]
    public class OrderedHashSet<T> : IEnumerable<T>, ISerializable
    {
        /// <summary>
        /// this is the constructor that will initialize internal data structures
        /// </summary>
        public OrderedHashSet()
        {
            _valueList = new LinkedList<T>();
            _valueToNodeMap = new Dictionary<T, LinkedListNode<T>>();
        }

        /// <summary>
        /// This property is an O(1) operation.
        /// </summary>
        public int Count => _valueList.Count;

        /// <summary>
        /// The first value of the sequence. This is an O(1) operation.
        /// </summary>
        public T First => _valueList.First != null ? _valueList.First.Value : default(T);

        /// <summary>
        /// The last value of the sequence. This is an O(1) operation.
        /// </summary>
        public T Last => _valueList.Last != null ? _valueList.Last.Value : default(T);

        /// <inheritdoc/>
        public IEnumerator<T> GetEnumerator()
        {
            return _valueList.GetEnumerator();
        }

        /// <inheritdoc/>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _valueList.GetEnumerator();
        }

        /// <summary>
        /// Adds the value at the start of the sequence.
        /// This is an O(log n) operation.
        /// </summary>
        /// <param name="value">The value to add at the start of the sequence.</param>
        /// <exception cref="InvalidOperationException">Thrown if the value is already present.</exception>
        /// <returns>true if added successfully, false if there was conflict and did not add</returns>
        public bool AddFirst(T value)
        {
            if (!_valueToNodeMap.ContainsKey(value))
            {
                var newNode = _valueList.AddFirst(value);
                _valueToNodeMap.Add(value, newNode);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Adds the value at the end of the sequence.
        /// This is an O(log n) operation.
        /// </summary>
        /// <param name="value">The value to add at the end of the sequence.</param>
        /// <exception cref="InvalidOperationException">Thrown if the value is already present.</exception>
        /// <returns>true if added successfully, false if there was conflict and did not add</returns>
        public bool AddLast(T value)
        {
            if (!_valueToNodeMap.ContainsKey(value))
            {
                var newNode = _valueList.AddLast(value);
                _valueToNodeMap.Add(value, newNode);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Removes all the values from the collection.
        /// This is an O(n) operation.
        /// </summary>
        public void Clear()
        {
            _valueList.Clear();
            _valueToNodeMap.Clear();
        }

        /// <summary>
        /// Looks for a value in the collection.
        /// This is an O(1) operation.
        /// </summary>
        /// <param name="value">Determines whether the collection contains the specified value.</param>
        /// <returns>true if the collection contains the specified value; otherwise, false.</returns>
        public bool Contains(T value)
        {
            return _valueToNodeMap.ContainsKey(value);
        }

        /// <summary>
        /// Removes a value from the collection.
        /// This is an O(log n) operation.
        /// </summary>
        /// <param name="value">The value to be removed.</param>
        /// <returns>true iff value is found in hashset</returns>
        public bool Remove(T value)
        {
            LinkedListNode<T> node;
            if (_valueToNodeMap.TryGetValue(value, out node))
            {
                _valueList.Remove(node);
                _valueToNodeMap.Remove(value);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Removes the value at the start of the collection.
        /// This is an O(1) operation.
        /// </summary>
        /// <returns>true if successfully removed first item, false if there is no first item</returns>
        public bool RemoveFirst()
        {
            var firstNode = _valueList.First;
            if (firstNode != null)
            {
                _valueToNodeMap.Remove(firstNode.Value);
                _valueList.RemoveFirst();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Removes the value at the end of the collection.
        /// This is an O(1) operation.
        /// </summary>
        /// <returns>true if successfully removed first item, false if there is no first item</returns>
        public bool RemoveLast()
        {
            var lastNode = _valueList.Last;
            if (lastNode != null)
            {
                _valueToNodeMap.Remove(lastNode.Value);
                _valueList.RemoveLast();
                return true;
            }
            return false;
        }

        private readonly LinkedList<T> _valueList;

        private readonly Dictionary<T, LinkedListNode<T>> _valueToNodeMap;

        /// <summary>
        /// Reconstruct the ordered hashset upon deserialization
        /// </summary>
        /// <param name="info">serialization info</param>
        /// <param name="context">streaming context</param>
        protected OrderedHashSet(SerializationInfo info, StreamingContext context)
        {
            _valueList = new LinkedList<T>();
            _valueToNodeMap = new Dictionary<T, LinkedListNode<T>>();
            var listSize = info.GetInt32("listSize");

            for (int i = 0; i < listSize; i++)
            {
                var element = (T)info.GetValue("value" + i, typeof(T));
                var node = _valueList.AddLast(element);
                _valueToNodeMap.Add(element, node);
            }
        }

        /// <summary>
        /// Constructor for deserializing an OrderedHashSet object. Do not call.
        /// </summary>
        /// <param name="info">Serialization info</param>
        /// <param name="context">Streaming context</param>
        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            int i = 0;
            info.AddValue("listSize", _valueList.Count);
            foreach (var value in _valueList)
            {
                info.AddValue("value" + i, value, typeof(T));
                i++;
            }

            // info.AddValue("values", _valueList, typeof(LinkedList<T>));
        }
    }
}