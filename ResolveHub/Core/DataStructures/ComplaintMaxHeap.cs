// =============================================================================
// ComplaintMaxHeap.cs — Dynamic Array-Backed Binary Max-Heap Priority Queue
// Automatically re-heapifies when a complaint's priority changes.
// =============================================================================

using System;
using System.Collections.Generic;
using ResolveHub.Core.Models;

namespace ResolveHub.Core.DataStructures
{
    /// <summary>
    /// A max-heap priority queue over <see cref="Complaint"/> objects.
    /// The root always holds the complaint with the highest priority score.
    ///
    /// Decision: A binary heap (backed by a List&lt;T&gt; for dynamic resizing) was
    /// chosen over a sorted list because Insert and ExtractMax are both O(log n),
    /// which is superior to a sorted list's O(n) insert for large datasets.
    /// </summary>
    public class ComplaintMaxHeap
    {
        // -----------------------------------------------------------------
        // Storage — index 0 is the heap root
        // -----------------------------------------------------------------
        private readonly List<Complaint> _heap = new();

        // -----------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------

        /// <summary>Number of complaints currently in the heap.</summary>
        public int Count => _heap.Count;

        /// <summary>Peek at the highest-priority complaint without removing it.</summary>
        public Complaint? Peek() => _heap.Count > 0 ? _heap[0] : null;

        /// <summary>
        /// Inserts a new complaint and sifts it up to maintain the heap property.
        /// O(log n)
        /// </summary>
        public void Insert(Complaint complaint)
        {
            if (complaint is null) throw new ArgumentNullException(nameof(complaint));
            _heap.Add(complaint);
            SiftUp(_heap.Count - 1);
        }

        /// <summary>
        /// Removes and returns the complaint with the highest priority.
        /// O(log n)
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the heap is empty.</exception>
        public Complaint ExtractMax()
        {
            if (_heap.Count == 0)
                throw new InvalidOperationException("Priority queue is empty.");

            Complaint max = _heap[0];

            // Move last element to root and sift down
            int last = _heap.Count - 1;
            _heap[0] = _heap[last];
            _heap.RemoveAt(last);

            if (_heap.Count > 0) SiftDown(0);

            return max;
        }

        /// <summary>
        /// Re-heapifies the entire structure.
        /// Call this after a complaint's DuplicateCount (and therefore priority) changes.
        /// O(n log n) but infrequent.
        /// </summary>
        public void Rebuild()
        {
            // Floyd's build-heap algorithm — O(n)
            for (int i = (_heap.Count / 2) - 1; i >= 0; i--)
                SiftDown(i);
        }

        /// <summary>Returns a snapshot of all complaints sorted by priority descending.</summary>
        public IReadOnlyList<Complaint> ToSortedList()
        {
            var snapshot = new List<Complaint>(_heap);
            snapshot.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            return snapshot;
        }

        /// <summary>Returns all complaints (heap order — not sorted).</summary>
        public IReadOnlyList<Complaint> All() => _heap.AsReadOnly();

        // -----------------------------------------------------------------
        // Private heap operations
        // -----------------------------------------------------------------

        /// <summary>Sifts the element at <paramref name="i"/> upward until the heap property is restored.</summary>
        private void SiftUp(int i)
        {
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (_heap[i].Priority > _heap[parent].Priority)
                {
                    Swap(i, parent);
                    i = parent;
                }
                else break;
            }
        }

        /// <summary>Sifts the element at <paramref name="i"/> downward until the heap property is restored.</summary>
        private void SiftDown(int i)
        {
            int n = _heap.Count;
            while (true)
            {
                int largest = i;
                int left    = 2 * i + 1;
                int right   = 2 * i + 2;

                if (left  < n && _heap[left].Priority  > _heap[largest].Priority) largest = left;
                if (right < n && _heap[right].Priority > _heap[largest].Priority) largest = right;

                if (largest == i) break;

                Swap(i, largest);
                i = largest;
            }
        }

        private void Swap(int a, int b)
        {
            (_heap[a], _heap[b]) = (_heap[b], _heap[a]);
        }
    }
}
