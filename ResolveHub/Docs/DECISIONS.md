# DECISIONS.md — Resolve Hub Architecture Decision Log

Every meaningful architectural or technical decision made during the building of
this project is recorded here, along with the rationale.

---

## D-001 · Password Hashing: PBKDF2 / HMAC-SHA256

| Field       | Value                                                  |
|-------------|--------------------------------------------------------|
| **Date**    | 2026-10-27                                             |
| **Status**  | Accepted                                               |
| **Files**   | `Core/Security/SecureHasher.cs`                        |

### Decision
Use `Rfc2898DeriveBytes.Pbkdf2` with HMAC-SHA256, 100,000 iterations, and a
16-byte cryptographically secure random salt (`RandomNumberGenerator.GetBytes`).
Store passwords as `base64(salt):base64(hash)` in the SQLite `Users.PasswordHash` column.

### Rationale
- **PBKDF2 is built into .NET** — no external dependency, unlike BCrypt or Argon2.
- **100,000 iterations** meets the NIST SP 800-132 recommendation for 2025+.
- **`CryptographicOperations.FixedTimeEquals`** prevents timing-based brute-force.
- **Never BCrypt** — BCrypt truncates passwords at 72 bytes; PBKDF2 does not.

---

## D-002 · Database: Microsoft.Data.Sqlite (raw SQL, no ORM)

| Field       | Value                                                  |
|-------------|--------------------------------------------------------|
| **Date**    | 2026-10-27                                             |
| **Status**  | Accepted                                               |
| **Files**   | `Data/DatabaseService.cs`, `ResolveHub.csproj`         |

### Decision
Use `Microsoft.Data.Sqlite` 8.0.10 with hand-written, parameterised SQL queries.
No Entity Framework Core, Dapper, or other ORM.

### Rationale
- **Educational clarity** — raw SQL keeps the data layer transparent for students
  learning the stack.
- **Minimal footprint** — the app targets a single local machine; ORM overhead is
  unjustified.
- **Parameterised queries everywhere** — prevents SQL injection.
- **Microsoft.Data.Sqlite** is the official Microsoft package; chosen over
  `System.Data.SQLite` (community) for better .NET 8 integration.

---

## D-003 · Database file location: `AppData\Roaming\ResolveHub\`

| Field       | Value                                                  |
|-------------|--------------------------------------------------------|
| **Date**    | 2026-10-27                                             |
| **Status**  | Accepted                                               |
| **Files**   | `Config/AppSettings.cs`, `Data/DatabaseService.cs`     |

### Decision
The SQLite database file is written to
`%APPDATA%\ResolveHub\resolvehub.db` (resolved at runtime via
`Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)`).

### Rationale
- **Never in the project folder** — prevents the DB from being accidentally
  committed to Git (a common security leak with SQLite).
- **`AppData\Roaming`** is the Windows-standard location for per-user application
  data.
- A `.gitignore` entry for `*.db` is also added as a defence-in-depth measure.

---

## D-004 · Custom Hash Table (separate chaining, 1 000 buckets)

| Field       | Value                                                  |
|-------------|--------------------------------------------------------|
| **Date**    | 2026-10-27                                             |
| **Status**  | Accepted                                               |
| **Files**   | `Core/DataStructures/ComplaintHashTable.cs`            |

### Decision
Implement a custom hash table instead of `Dictionary<string, Complaint>`.
Use separate chaining (linked-list per bucket) with an initial capacity of 1 000.

### Rationale
- **DSA requirement** — the spec explicitly requires a custom hash table.
- **Separate chaining** was preferred over open addressing because it degrades
  gracefully under high load and never needs rehashing for this dataset size.
- **Composite key format** `category:hostel:room` covers all meaningful axes
  of deduplication with a single lookup.

---

## D-005 · Binary Max-Heap Priority Queue (dynamic array-backed)

| Field       | Value                                                  |
|-------------|--------------------------------------------------------|
| **Date**    | 2026-10-27                                             |
| **Status**  | Accepted                                               |
| **Files**   | `Core/DataStructures/ComplaintMaxHeap.cs`              |

### Decision
Implement a binary max-heap backed by a `List<Complaint>` (zero-index root).
Priority formula: `(Urgency × 10) + (HoursPending × 1.5) + (DuplicateCount × 5)`.

### Rationale
- **O(log n) insert and extract** vs O(n) for a sorted list under insertions.
- **`List<T>` backing** gives dynamic resizing without a fixed array.
- **Rebuild on duplicate merge** — when DuplicateCount changes, a full O(n) rebuild
  is triggered. This is acceptable because duplicates are rare events.
- **Priority formula** was designed so that both age and breadth (duplicates)
  influence urgency, incentivising quick resolution.

---

## D-006 · Prefix Trie for Auto-Complete

| Field       | Value                                                  |
|-------------|--------------------------------------------------------|
| **Date**    | 2026-10-27                                             |
| **Status**  | Accepted                                               |
| **Files**   | `Core/DataStructures/CategoryTrie.cs`                  |

### Decision
Implement a standard prefix trie with `Dictionary<char, TrieNode>` children
(supports full Unicode, not just ASCII).

### Rationale
- **O(m) prefix search** where m = query length, independent of vocabulary size.
- **Dictionary children** instead of a fixed `char[26]` array — handles
  categories with spaces, hyphens, and digits (e.g. "wi-fi", "floor 2").

---

## D-007 · TCP Socket: loopback-only, 127.0.0.1:5000

| Field       | Value                                                  |
|-------------|--------------------------------------------------------|
| **Date**    | 2026-10-27                                             |
| **Status**  | Accepted                                               |
| **Files**   | `Network/LocalTcpServer.cs`, `Network/LocalTcpClient.cs` |

### Decision
Bind the TCP server **exclusively to `IPAddress.Loopback` (127.0.0.1)**, not to
`IPAddress.Any` (0.0.0.0).

### Rationale
- **Security** — binding to `127.0.0.1` makes the socket unreachable from the
  LAN or internet; only processes on the same machine can connect.
- **No auth required** — a local attacker with process-level access already has
  higher privileges than the socket grants.
- **Raw TCP** was chosen over SignalR/gRPC because the spec explicitly requires
  socket implementation and the application is single-machine.

---

## D-008 · Configuration: No Hard-Coded Secrets

| Field       | Value                                                  |
|-------------|--------------------------------------------------------|
| **Date**    | 2026-10-27                                             |
| **Status**  | Accepted                                               |
| **Files**   | `Config/AppSettings.cs`                                |

### Decision
All configuration lives in `AppSettings.cs`. No API keys, passwords, or connection
strings appear in source code. The database path is derived at runtime.

### Rationale
- **Zero hard-coded secrets** — there are no external API keys in this milestone.
  If they are added later, they must come from environment variables or an
  encrypted user-secrets store, never from source code.
- **Single point of change** — brand colours, ports, and paths are constants in
  one class.

---

## D-009 · UI: Programmatic WinForms (no Designer)

| Field       | Value                                                  |
|-------------|--------------------------------------------------------|
| **Date**    | 2026-10-27                                             |
| **Status**  | Accepted                                               |
| **Files**   | `UI/Forms/*.cs`                                        |

### Decision
All UI is built in code (`InitializeComponent` method), not with the Visual
Studio Designer (`.Designer.cs` files).

### Rationale
- **Version control friendly** — Designer files generate brittle, hard-to-diff XML.
- **Theme system** — `AppTheme.cs` factory methods ensure every control is styled
  consistently; Designer-built forms scatter style values everywhere.
- **`TableLayoutPanel` + percentage columns** — ensures the layout scales cleanly
  across different screen resolutions.

---

## D-010 · Seeded Demo Data

| Field       | Value                                                  |
|-------------|--------------------------------------------------------|
| **Date**    | 2026-10-27                                             |
| **Status**  | Accepted                                               |
| **Files**   | `Data/DatabaseService.cs`                              |

### Decision
On first launch (empty DB), seed one student and four complaints so the UI
is immediately demonstrable.

| Field        | Value                        |
|--------------|------------------------------|
| Name         | Aarav Sharma                 |
| Email        | student@resolvehub.com       |
| Password     | Password123!                 |
| Student ID   | 2024-CS-042                  |
| Hostel       | Hostel B, Room 204           |

> **Security note**: The demo password is documented here for development only.
> Production deployments must use a different seed or require the admin to set
> the initial password on first run.
