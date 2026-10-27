// =============================================================================
// ComplaintHashTable.cs — Custom Separate-Chaining Hash Table
// Provides O(1) average-case duplicate complaint detection.
// =============================================================================

using System;
using System.Collections.Generic;
using ResolveHub.Core.Models;

namespace ResolveHub.Core.DataStructures
{
    /// <summary>
    /// A fixed-bucket hash table with separate chaining (linked lists per bucket).
    /// Initial capacity: 1 000 buckets — sized for typical hostel complaint volumes.
    ///
    /// Decision: A custom hash table was implemented instead of Dictionary&lt;T,T&gt;
    /// to demonstrate the DSA requirement and give fine-grained control over the
    /// collision strategy. Separate chaining was chosen over open addressing
    /// because it degrades gracefully under high load.
    /// </summary>
    public class ComplaintHashTable
    {
        // -----------------------------------------------------------------
        // Internal node — each bucket is a singly linked list of nodes
        // -----------------------------------------------------------------
        private class Node
        {
            public string    Key      { get; }
            public Complaint Value    { get; set; }
            public Node?     Next     { get; set; }

            public Node(string key, Complaint value)
            {
                Key   = key;
                Value = value;
            }
        }

        // -----------------------------------------------------------------
        // Fields
        // -----------------------------------------------------------------
        private readonly Node?[] _buckets;
        private readonly int     _capacity;
        private int              _count;

        // -----------------------------------------------------------------
        // Constructor
        // -----------------------------------------------------------------

        /// <param name="capacity">Initial bucket count (default 1 000).</param>
        public ComplaintHashTable(int capacity = 1_000)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
            _buckets  = new Node[_capacity];
            _count    = 0;
        }

        // -----------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------

        /// <summary>Number of unique keys stored.</summary>
        public int Count => _count;

        /// <summary>
        /// Inserts a new complaint or, if the composite key already exists,
        /// increments the existing complaint's <see cref="Complaint.DuplicateCount"/>.
        /// </summary>
        /// <returns>
        /// True  → new entry inserted.
        /// False → duplicate found; DuplicateCount on the existing entry was incremented.
        /// </returns>
        public bool InsertOrMergeDuplicate(Complaint complaint)
        {
            if (complaint is null) throw new ArgumentNullException(nameof(complaint));

            string key    = complaint.CompositeKey;
            int    index  = GetBucketIndex(key);
            Node?  cursor = _buckets[index];

            // Walk the chain — O(k) where k is chain length, typically O(1)
            while (cursor is not null)
            {
                if (cursor.Key == key)
                {
                    // Duplicate detected — merge instead of insert
                    cursor.Value.DuplicateCount++;
                    cursor.Value.UpdatedAt = DateTime.UtcNow;
                    return false;
                }
                cursor = cursor.Next;
            }

            // No duplicate — prepend to bucket chain (O(1))
            var newNode = new Node(key, complaint)
            {
                Next = _buckets[index]
            };
            _buckets[index] = newNode;
            _count++;
            return true;
        }

        /// <summary>Retrieves a complaint by composite key, or null if not found.</summary>
        public Complaint? Get(string compositeKey)
        {
            int   index  = GetBucketIndex(compositeKey);
            Node? cursor = _buckets[index];
            while (cursor is not null)
            {
                if (cursor.Key == compositeKey) return cursor.Value;
                cursor = cursor.Next;
            }
            return null;
        }

        /// <summary>Returns true if the composite key exists.</summary>
        public bool Contains(string compositeKey) => Get(compositeKey) is not null;

        /// <summary>Removes a complaint by composite key.</summary>
        public bool Remove(string compositeKey)
        {
            int   index  = GetBucketIndex(compositeKey);
            Node? cursor = _buckets[index];
            Node? prev   = null;

            while (cursor is not null)
            {
                if (cursor.Key == compositeKey)
                {
                    if (prev is null)
                        _buckets[index] = cursor.Next;
                    else
                        prev.Next = cursor.Next;

                    _count--;
                    return true;
                }
                prev   = cursor;
                cursor = cursor.Next;
            }
            return false;
        }

        /// <summary>Iterates all stored complaints (order not guaranteed).</summary>
        public IEnumerable<Complaint> AllComplaints()
        {
            foreach (var head in _buckets)
            {
                Node? cursor = head;
                while (cursor is not null)
                {
                    yield return cursor.Value;
                    cursor = cursor.Next;
                }
            }
        }

        // -----------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------

        /// <summary>Maps a string key to a bucket index using polynomial rolling hash.</summary>
        private int GetBucketIndex(string key)
        {
            // Using built-in GetHashCode is acceptable for in-process tables.
            // We mask the sign bit to ensure a non-negative index.
            return (key.GetHashCode() & 0x7FFFFFFF) % _capacity;
        }
    }
}
