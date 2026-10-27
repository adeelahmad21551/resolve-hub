// =============================================================================
// DatabaseService.cs — SQLite Data Access Layer
// All DB interaction is centralised here; the rest of the app is DB-agnostic.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using ResolveHub.Core.Models;
using ResolveHub.Core.Security;
using ResolveHub.Config;

namespace ResolveHub.Data
{
    /// <summary>
    /// Wraps all SQLite operations for the application.
    ///
    /// Decision: Microsoft.Data.Sqlite was chosen over Entity Framework Core because
    /// this milestone is an educational application — raw SQL keeps the data layer
    /// transparent and avoids the overhead of an ORM for a simple schema.
    ///
    /// Connection security:
    ///   • The database file lives in AppData\Roaming\ResolveHub\ so it is outside
    ///     the project working directory and is not accidentally committed to version control.
    ///   • No connection string is hard-coded; it is built at runtime from AppSettings.
    ///   • Parameterised queries are used everywhere to prevent SQL injection.
    /// </summary>
    public class DatabaseService : IDisposable
    {
        // -----------------------------------------------------------------
        // Connection
        // -----------------------------------------------------------------
        private readonly SqliteConnection _connection;
        private bool _disposed;

        // -----------------------------------------------------------------
        // Constructor — initialise DB, run migrations, seed demo data
        // -----------------------------------------------------------------
        public DatabaseService()
        {
            string dbPath = AppSettings.DatabasePath;

            // Ensure the parent directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

            string connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode       = SqliteOpenMode.ReadWriteCreate,
                Cache      = SqliteCacheMode.Shared          // safe for single-process use
            }.ToString();

            _connection = new SqliteConnection(connectionString);
            _connection.Open();

            RunMigrations();
            SeedDemoData();
        }

        // -----------------------------------------------------------------
        // DDL — Table creation (idempotent)
        // -----------------------------------------------------------------
        private void RunMigrations()
        {
            ExecuteNonQuery(@"
                PRAGMA journal_mode = WAL;   -- better concurrency
                PRAGMA foreign_keys = ON;    -- enforce FK constraints

                CREATE TABLE IF NOT EXISTS Users (
                    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                    FullName     TEXT    NOT NULL,
                    Email        TEXT    NOT NULL UNIQUE,
                    StudentId    TEXT    NOT NULL UNIQUE,
                    PasswordHash TEXT    NOT NULL,
                    HostelName   TEXT    NOT NULL DEFAULT '',
                    RoomNumber   TEXT    NOT NULL DEFAULT '',
                    Role         INTEGER NOT NULL DEFAULT 0,
                    CreatedAt    TEXT    NOT NULL DEFAULT (datetime('now')),
                    IsActive     INTEGER NOT NULL DEFAULT 1
                );

                CREATE TABLE IF NOT EXISTS Complaints (
                    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                    ComplaintNumber TEXT    NOT NULL UNIQUE,
                    Title           TEXT    NOT NULL,
                    Description     TEXT    NOT NULL DEFAULT '',
                    Category        TEXT    NOT NULL,
                    HostelName      TEXT    NOT NULL,
                    FloorName       TEXT    NOT NULL DEFAULT '',
                    RoomNumber      TEXT    NOT NULL,
                    Urgency         INTEGER NOT NULL DEFAULT 2,
                    DuplicateCount  INTEGER NOT NULL DEFAULT 0,
                    Status          INTEGER NOT NULL DEFAULT 0,
                    StudentId       INTEGER NOT NULL REFERENCES Users(Id),
                    StudentName     TEXT    NOT NULL DEFAULT '',
                    CreatedAt       TEXT    NOT NULL DEFAULT (datetime('now')),
                    UpdatedAt       TEXT
                );

                CREATE INDEX IF NOT EXISTS idx_complaints_student ON Complaints(StudentId);
                CREATE INDEX IF NOT EXISTS idx_complaints_status  ON Complaints(Status);
            ");
        }

        // -----------------------------------------------------------------
        // Seeding — one demo student so the app is usable out of the box
        // -----------------------------------------------------------------
        private void SeedDemoData()
        {
            // Only seed if Users table is empty
            long userCount = (long)(ExecuteScalar("SELECT COUNT(*) FROM Users;") ?? 0L);
            if (userCount > 0) return;

            // Demo student: Aarav Sharma / student@resolvehub.com / Password123!
            string hash = SecureHasher.Hash("Password123!");

            ExecuteNonQuery(@"
                INSERT INTO Users (FullName, Email, StudentId, PasswordHash, HostelName, RoomNumber, Role)
                VALUES (@name, @email, @sid, @hash, @hostel, @room, 0);",
                new SqliteParameter("@name",   "Aarav Sharma"),
                new SqliteParameter("@email",  "student@resolvehub.com"),
                new SqliteParameter("@sid",    "2024-CS-042"),
                new SqliteParameter("@hash",   hash),
                new SqliteParameter("@hostel", "Hostel B"),
                new SqliteParameter("@room",   "204"));

            // Seed sample complaints
            long studentId = (long)(ExecuteScalar("SELECT Id FROM Users WHERE Email = 'student@resolvehub.com';") ?? 1L);

            var sampleComplaints = new (string num, string title, string cat, string hostel, string floor, string room, int urgency, int status, int dupCount, string createdAt)[]
            {
                ("HC-1022", "Mess food quality",      "Mess issues",    "Hostel B", "Ground", "204", 2, 2, 2, "2026-10-20"),
                ("HC-1036", "Wi-Fi connection problem","Wi-Fi",          "Hostel B", "Floor 2","204", 2, 0, 1, "2026-10-22"),
                ("HC-1041", "Room light not working", "Electricity",    "Hostel B", "Floor 2","204", 3, 1, 0, "2026-10-23"),
                ("HC-1048", "Water supply issue",     "Water supply",   "Hostel B", "Floor 2","204", 3, 0, 0, "2026-10-24"),
            };

            foreach (var c in sampleComplaints)
            {
                ExecuteNonQuery(@"
                    INSERT OR IGNORE INTO Complaints
                        (ComplaintNumber, Title, Category, HostelName, FloorName, RoomNumber,
                         Urgency, Status, DuplicateCount, StudentId, StudentName, CreatedAt)
                    VALUES (@num,@title,@cat,@hostel,@floor,@room,@urg,@stat,@dup,@sid,@sname,@cat2);",
                    new SqliteParameter("@num",   c.num),
                    new SqliteParameter("@title", c.title),
                    new SqliteParameter("@cat",   c.cat),
                    new SqliteParameter("@hostel",c.hostel),
                    new SqliteParameter("@floor", c.floor),
                    new SqliteParameter("@room",  c.room),
                    new SqliteParameter("@urg",   c.urgency),
                    new SqliteParameter("@stat",  c.status),
                    new SqliteParameter("@dup",   c.dupCount),
                    new SqliteParameter("@sid",   studentId),
                    new SqliteParameter("@sname", "Aarav Sharma"),
                    new SqliteParameter("@cat2",  c.createdAt));
            }
        }

        // -----------------------------------------------------------------
        // User operations
        // -----------------------------------------------------------------

        /// <summary>Persists a new user and returns the auto-generated Id.</summary>
        public int CreateUser(User user)
        {
            ExecuteNonQuery(@"
                INSERT INTO Users (FullName, Email, StudentId, PasswordHash, HostelName, RoomNumber, Role)
                VALUES (@name, @email, @sid, @hash, @hostel, @room, @role);",
                new SqliteParameter("@name",   user.FullName),
                new SqliteParameter("@email",  user.Email),
                new SqliteParameter("@sid",    user.StudentId),
                new SqliteParameter("@hash",   user.PasswordHash),
                new SqliteParameter("@hostel", user.HostelName),
                new SqliteParameter("@room",   user.RoomNumber),
                new SqliteParameter("@role",   (int)user.Role));

            return (int)(long)(ExecuteScalar("SELECT last_insert_rowid();") ?? 0L);
        }

        /// <summary>Finds a user by email for login.</summary>
        public User? FindUserByEmail(string email)
        {
            using var cmd = CreateCommand(
                "SELECT * FROM Users WHERE Email = @email AND IsActive = 1;",
                new SqliteParameter("@email", email));

            using var reader = cmd.ExecuteReader();
            return reader.Read() ? MapUser(reader) : null;
        }

        /// <summary>Checks whether an email address is already registered.</summary>
        public bool EmailExists(string email)
        {
            long count = (long)(ExecuteScalar(
                "SELECT COUNT(*) FROM Users WHERE Email = @email;",
                new SqliteParameter("@email", email)) ?? 0L);
            return count > 0;
        }

        /// <summary>Checks whether a student ID is already registered.</summary>
        public bool StudentIdExists(string studentId)
        {
            long count = (long)(ExecuteScalar(
                "SELECT COUNT(*) FROM Users WHERE StudentId = @sid;",
                new SqliteParameter("@sid", studentId)) ?? 0L);
            return count > 0;
        }

        // -----------------------------------------------------------------
        // Complaint operations
        // -----------------------------------------------------------------

        /// <summary>Inserts a new complaint and returns the generated Id.</summary>
        public int CreateComplaint(Complaint complaint)
        {
            ExecuteNonQuery(@"
                INSERT INTO Complaints
                    (ComplaintNumber, Title, Description, Category, HostelName, FloorName,
                     RoomNumber, Urgency, DuplicateCount, Status, StudentId, StudentName, CreatedAt)
                VALUES
                    (@num, @title, @desc, @cat, @hostel, @floor,
                     @room, @urg, @dup, @stat, @sid, @sname, @created);",
                new SqliteParameter("@num",     complaint.ComplaintNumber),
                new SqliteParameter("@title",   complaint.Title),
                new SqliteParameter("@desc",    complaint.Description),
                new SqliteParameter("@cat",     complaint.Category),
                new SqliteParameter("@hostel",  complaint.HostelName),
                new SqliteParameter("@floor",   complaint.FloorName),
                new SqliteParameter("@room",    complaint.RoomNumber),
                new SqliteParameter("@urg",     (int)complaint.Urgency),
                new SqliteParameter("@dup",     complaint.DuplicateCount),
                new SqliteParameter("@stat",    (int)complaint.Status),
                new SqliteParameter("@sid",     complaint.StudentId),
                new SqliteParameter("@sname",   complaint.StudentName),
                new SqliteParameter("@created", complaint.CreatedAt.ToString("o")));

            return (int)(long)(ExecuteScalar("SELECT last_insert_rowid();") ?? 0L);
        }

        /// <summary>Increments the DuplicateCount for an existing complaint.</summary>
        public void IncrementDuplicateCount(int complaintId)
        {
            ExecuteNonQuery(@"
                UPDATE Complaints
                SET DuplicateCount = DuplicateCount + 1,
                    UpdatedAt      = datetime('now')
                WHERE Id = @id;",
                new SqliteParameter("@id", complaintId));
        }

        /// <summary>Returns all complaints for a specific student, newest first.</summary>
        public List<Complaint> GetComplaintsByStudent(int studentId)
        {
            using var cmd = CreateCommand(@"
                SELECT * FROM Complaints
                WHERE StudentId = @sid
                ORDER BY CreatedAt DESC;",
                new SqliteParameter("@sid", studentId));

            var list = new List<Complaint>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(MapComplaint(reader));
            return list;
        }

        /// <summary>Returns the next sequential complaint number (HC-XXXX format).</summary>
        public string GenerateComplaintNumber()
        {
            long max = (long)(ExecuteScalar("SELECT COUNT(*) FROM Complaints;") ?? 0L);
            return $"HC-{1000 + max + 1}";
        }

        // -----------------------------------------------------------------
        // Mapping helpers
        // -----------------------------------------------------------------
        private static User MapUser(SqliteDataReader r) => new()
        {
            Id           = r.GetInt32(r.GetOrdinal("Id")),
            FullName     = r.GetString(r.GetOrdinal("FullName")),
            Email        = r.GetString(r.GetOrdinal("Email")),
            StudentId    = r.GetString(r.GetOrdinal("StudentId")),
            PasswordHash = r.GetString(r.GetOrdinal("PasswordHash")),
            HostelName   = r.GetString(r.GetOrdinal("HostelName")),
            RoomNumber   = r.GetString(r.GetOrdinal("RoomNumber")),
            Role         = (UserRole)r.GetInt32(r.GetOrdinal("Role")),
            IsActive     = r.GetInt32(r.GetOrdinal("IsActive")) == 1
        };

        private static Complaint MapComplaint(SqliteDataReader r) => new()
        {
            Id              = r.GetInt32(r.GetOrdinal("Id")),
            ComplaintNumber = r.GetString(r.GetOrdinal("ComplaintNumber")),
            Title           = r.GetString(r.GetOrdinal("Title")),
            Description     = r.IsDBNull(r.GetOrdinal("Description")) ? "" : r.GetString(r.GetOrdinal("Description")),
            Category        = r.GetString(r.GetOrdinal("Category")),
            HostelName      = r.GetString(r.GetOrdinal("HostelName")),
            FloorName       = r.IsDBNull(r.GetOrdinal("FloorName")) ? "" : r.GetString(r.GetOrdinal("FloorName")),
            RoomNumber      = r.GetString(r.GetOrdinal("RoomNumber")),
            Urgency         = (UrgencyLevel)r.GetInt32(r.GetOrdinal("Urgency")),
            DuplicateCount  = r.GetInt32(r.GetOrdinal("DuplicateCount")),
            Status          = (ComplaintStatus)r.GetInt32(r.GetOrdinal("Status")),
            StudentId       = r.GetInt32(r.GetOrdinal("StudentId")),
            StudentName     = r.IsDBNull(r.GetOrdinal("StudentName")) ? "" : r.GetString(r.GetOrdinal("StudentName")),
            CreatedAt       = DateTime.Parse(r.GetString(r.GetOrdinal("CreatedAt")))
        };

        // -----------------------------------------------------------------
        // Low-level helpers
        // -----------------------------------------------------------------
        private void ExecuteNonQuery(string sql, params SqliteParameter[] parameters)
        {
            using var cmd = CreateCommand(sql, parameters);
            cmd.ExecuteNonQuery();
        }

        private object? ExecuteScalar(string sql, params SqliteParameter[] parameters)
        {
            using var cmd = CreateCommand(sql, parameters);
            return cmd.ExecuteScalar();
        }

        private SqliteCommand CreateCommand(string sql, params SqliteParameter[] parameters)
        {
            var cmd = _connection.CreateCommand();
            cmd.CommandText = sql;
            foreach (var p in parameters) cmd.Parameters.Add(p);
            return cmd;
        }

        // -----------------------------------------------------------------
        // IDisposable
        // -----------------------------------------------------------------
        public void Dispose()
        {
            if (_disposed) return;
            _connection.Close();
            _connection.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
