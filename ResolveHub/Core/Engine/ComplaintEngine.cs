// =============================================================================
// ComplaintEngine.cs — Orchestrates Hash Table + Max-Heap + Trie
// Single entry point for all complaint business logic.
// =============================================================================

using System;
using System.Collections.Generic;
using ResolveHub.Core.DataStructures;
using ResolveHub.Core.Models;

namespace ResolveHub.Core.Engine
{
    /// <summary>
    /// Combines the three core DSA structures into a unified complaint processing engine.
    ///
    /// Responsibilities:
    ///   1. Duplicate detection via the custom hash table (O(1) average).
    ///   2. Priority ordering via the max-heap.
    ///   3. Auto-complete suggestions via the prefix trie.
    ///
    /// This class is designed as a singleton service — instantiate once and inject
    /// wherever needed (currently held by the StudentDashboard form).
    /// </summary>
    public class ComplaintEngine
    {
        // -----------------------------------------------------------------
        // DSA structures
        // -----------------------------------------------------------------
        private readonly ComplaintHashTable _hashTable = new(1_000);
        private readonly ComplaintMaxHeap   _heap      = new();
        private readonly CategoryTrie       _trie      = new();

        // -----------------------------------------------------------------
        // Constructor — seed the trie with known categories and locations
        // -----------------------------------------------------------------
        public ComplaintEngine()
        {
            // Pre-seed trie so auto-complete works immediately
            string[] categories =
            [
                "water supply", "electricity", "room maintenance",
                "wi-fi", "mess issues", "sanitation", "security",
                "laundry", "lift", "plumbing"
            ];

            string[] hostels =
            [
                "hostel a", "hostel b", "hostel c", "hostel d",
                "girls hostel 1", "girls hostel 2"
            ];

            foreach (var cat in categories) _trie.Insert(cat);
            foreach (var loc in hostels)    _trie.Insert(loc);
        }

        // -----------------------------------------------------------------
        // Public API — Complaint ingestion
        // -----------------------------------------------------------------

        /// <summary>
        /// Ingests a new complaint into the engine.
        ///  • If no duplicate exists → inserts into hash table and heap.
        ///  • If a duplicate exists → increments DuplicateCount and rebuilds heap.
        /// </summary>
        /// <returns>
        /// (isNew: true, complaint: newOrExisting)   where isNew indicates a fresh entry.
        /// </returns>
        public (bool IsNew, Complaint Complaint) Ingest(Complaint complaint)
        {
            if (complaint is null) throw new ArgumentNullException(nameof(complaint));

            bool isNew = _hashTable.InsertOrMergeDuplicate(complaint);

            if (isNew)
            {
                _heap.Insert(complaint);
                // Also add its category and location terms to the trie
                _trie.Insert(complaint.Category);
                _trie.Insert(complaint.HostelName);
            }
            else
            {
                // Fetch the existing entry (DuplicateCount was already incremented)
                var existing = _hashTable.Get(complaint.CompositeKey)!;
                // Priority changed — rebuild heap to maintain invariant
                _heap.Rebuild();
                return (false, existing);
            }

            return (true, complaint);
        }

        // -----------------------------------------------------------------
        // Public API — Query
        // -----------------------------------------------------------------

        /// <summary>Returns all complaints ordered by priority descending.</summary>
        public IReadOnlyList<Complaint> GetAllByPriority() => _heap.ToSortedList();

        /// <summary>Returns all complaints (unordered) from the hash table.</summary>
        public IEnumerable<Complaint> GetAll() => _hashTable.AllComplaints();

        /// <summary>Returns the highest-priority unresolved complaint (heap peek).</summary>
        public Complaint? PeekHighestPriority() => _heap.Peek();

        /// <summary>Returns auto-complete suggestions for a given prefix.</summary>
        public List<string> Suggest(string prefix, int maxResults = 8) =>
            _trie.AutoComplete(prefix, maxResults);

        /// <summary>Returns a complaint by its composite key.</summary>
        public Complaint? FindByKey(string compositeKey) => _hashTable.Get(compositeKey);

        // -----------------------------------------------------------------
        // Statistics helpers (used by dashboard stat cards)
        // -----------------------------------------------------------------

        public int TotalCount      => _hashTable.Count;
        public int HeapSize        => _heap.Count;
    }
}
