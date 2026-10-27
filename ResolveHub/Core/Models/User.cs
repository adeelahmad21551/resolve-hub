// =============================================================================
// User.cs — Student account domain model
// =============================================================================

using System;

namespace ResolveHub.Core.Models
{
    /// <summary>Role enumeration — extensible for Admin/Staff in later milestones.</summary>
    public enum UserRole
    {
        Student = 0,
        Staff   = 1,
        Admin   = 2
    }

    /// <summary>
    /// Represents a registered application user.
    /// PasswordHash stores the PBKDF2 output from SecureHasher — never plain text.
    /// </summary>
    public class User
    {
        public int      Id           { get; set; }
        public string   FullName     { get; set; } = string.Empty;
        public string   Email        { get; set; } = string.Empty;
        public string   StudentId    { get; set; } = string.Empty;   // e.g. "2024-CS-042"
        public string   PasswordHash { get; set; } = string.Empty;   // PBKDF2 hash — never plain text
        public string   HostelName   { get; set; } = string.Empty;
        public string   RoomNumber   { get; set; } = string.Empty;
        public UserRole Role         { get; set; } = UserRole.Student;
        public DateTime CreatedAt    { get; set; } = DateTime.UtcNow;
        public bool     IsActive     { get; set; } = true;
    }
}
