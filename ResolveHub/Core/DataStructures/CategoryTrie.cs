// =============================================================================
// CategoryTrie.cs — Prefix Trie for Auto-Complete
// Enables instant suggestion of categories and locations as the student types.
// =============================================================================

using System;
using System.Collections.Generic;

namespace ResolveHub.Core.DataStructures
{
    /// <summary>
    /// A standard 26-child (lower-case alphabet) prefix trie augmented with
    /// a wildcard child for digits and special characters (space, hyphen).
    ///
    /// Decision: A trie was selected over binary search on a sorted list because
    /// auto-complete is a prefix operation — tries provide O(m) search where m
    /// is the query length, independent of the vocabulary size.
    /// </summary>
    public class CategoryTrie
    {
        // -----------------------------------------------------------------
        // Internal node
        // -----------------------------------------------------------------
        private class TrieNode
        {
            public Dictionary<char, TrieNode> Children { get; } = new();
            public bool IsEndOfWord { get; set; }
            public string? Word { get; set; }   // full word cached at terminal node
        }

        // -----------------------------------------------------------------
        // Root
        // -----------------------------------------------------------------
        private readonly TrieNode _root = new();

        // -----------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------

        /// <summary>Inserts a word into the trie (case-insensitive).</summary>
        public void Insert(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) return;

            word = word.Trim().ToLower();
            TrieNode current = _root;

            foreach (char ch in word)
            {
                if (!current.Children.ContainsKey(ch))
                    current.Children[ch] = new TrieNode();
                current = current.Children[ch];
            }

            current.IsEndOfWord = true;
            current.Word        = word;
        }

        /// <summary>
        /// Returns up to <paramref name="maxResults"/> completions for the given prefix.
        /// O(m + k) where m is prefix length and k is the number of matches.
        /// </summary>
        public List<string> AutoComplete(string prefix, int maxResults = 10)
        {
            var results = new List<string>();
            if (string.IsNullOrWhiteSpace(prefix)) return results;

            prefix = prefix.Trim().ToLower();
            TrieNode current = _root;

            // Navigate to the end of the prefix
            foreach (char ch in prefix)
            {
                if (!current.Children.TryGetValue(ch, out TrieNode? next))
                    return results;   // prefix not found — return empty
                current = next;
            }

            // DFS from that node to collect all words under it
            CollectWords(current, results, maxResults);
            return results;
        }

        /// <summary>Returns true if the exact word exists in the trie.</summary>
        public bool Contains(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) return false;
            word = word.Trim().ToLower();
            TrieNode current = _root;
            foreach (char ch in word)
            {
                if (!current.Children.TryGetValue(ch, out TrieNode? next))
                    return false;
                current = next;
            }
            return current.IsEndOfWord;
        }

        // -----------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------

        private static void CollectWords(TrieNode node, List<string> results, int maxResults)
        {
            if (results.Count >= maxResults) return;

            if (node.IsEndOfWord && node.Word is not null)
                results.Add(node.Word);

            foreach (var child in node.Children.Values)
                CollectWords(child, results, maxResults);
        }
    }
}
