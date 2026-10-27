// =============================================================================
// Complaint.cs — Core Domain Model
// Represents a single student grievance with priority scoring and composite key.
// =============================================================================

using System;

namespace ResolveHub.Core.Models
{
    /// <summary>
    /// Possible life-cycle states for a complaint.
    /// </summary>
    public enum ComplaintStatus
    {
        Pending    = 0,
        InProgress = 1,
        Resolved   = 2,
        Rejected   = 3
    }

    /// <summary>
    /// Urgency levels mapped to a numeric base-score used in priority calculation.
    /// </summary>
    public enum UrgencyLevel
    {
        Low      = 1,
        Medium   = 2,
        High     = 3,
        Critical = 4
    }

    /// <summary>
    /// Immutable-ish domain model for a student grievance.
    /// The <see cref="Priority"/> property is recomputed on demand so the
    /// max-heap always has fresh scores when DuplicateCount changes.
    /// </summary>
    public class Complaint
    {
        // -----------------------------------------------------------------
        // Identity & metadata
        // -----------------------------------------------------------------

        /// <summary>Unique auto-incrementing integer from SQLite (0 = not yet persisted).</summary>
        public int    Id             { get; set; }

        /// <summary>Human-readable complaint number shown in UI, e.g. "HC-1048".</summary>
        public string ComplaintNumber { get; set; } = string.Empty;

        /// <summary>Short title entered by the student.</summary>
        public string Title          { get; set; } = string.Empty;

        /// <summary>Full description of the issue.</summary>
        public string Description    { get; set; } = string.Empty;

        // -----------------------------------------------------------------
        // Categorisation
        // -----------------------------------------------------------------

        public string       Category   { get; set; } = string.Empty;
        public string       HostelName { get; set; } = string.Empty;
        public string       FloorName  { get; set; } = string.Empty;
        public string       RoomNumber { get; set; } = string.Empty;

        // -----------------------------------------------------------------
        // Priority & deduplication
        // -----------------------------------------------------------------

        public UrgencyLevel Urgency       { get; set; } = UrgencyLevel.Medium;

        /// <summary>
        /// How many additional students reported the same category+location.
        /// Starts at 0 (one reporter); each duplicate increments this by 1.
        /// </summary>
        public int          DuplicateCount { get; set; } = 0;

        // -----------------------------------------------------------------
        // Status & timing
        // -----------------------------------------------------------------

        public ComplaintStatus Status    { get; set; } = ComplaintStatus.Pending;
        public DateTime        CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime?       UpdatedAt { get; set; }

        // -----------------------------------------------------------------
        // Submitter
        // -----------------------------------------------------------------

        /// <summary>Foreign key linking back to the Users table.</summary>
        public int    StudentId   { get; set; }
        public string StudentName { get; set; } = string.Empty;

        // -----------------------------------------------------------------
        // Computed properties
        // -----------------------------------------------------------------

        /// <summary>
        /// Composite key used by the custom hash table for O(1) duplicate detection.
        /// Format: "category:hostelname:roomnumber"
        /// </summary>
        public string CompositeKey =>
            $"{Category.Trim().ToLower()}:{HostelName.Trim().ToLower()}:{RoomNumber.Trim().ToLower()}";

        /// <summary>
        /// Hours elapsed since this complaint was submitted.
        /// </summary>
        public double HoursPending =>
            (DateTime.UtcNow - CreatedAt).TotalHours;

        /// <summary>
        /// Priority score formula (higher = more urgent in the max-heap).
        /// Formula: (BaseUrgency * 10) + (HoursPending * 1.5) + (DuplicateCount * 5)
        /// </summary>
        public double Priority =>
            ((int)Urgency * 10.0)
            + (HoursPending * 1.5)
            + (DuplicateCount * 5.0);
    }
}
