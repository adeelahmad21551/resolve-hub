# FLOW.md — Resolve Hub Code Execution Flow

This document traces how execution travels through the codebase from startup
to the key user actions. Read this alongside the source files.

---

## 1. Application Bootstrap

```
Program.cs → Main()
  │
  ├── ApplicationConfiguration.Initialize()      ← DPI + visual styles
  │
  ├── new DatabaseService()                       ← Data/DatabaseService.cs
  │     ├── Opens SQLite connection (AppData\Roaming\ResolveHub\resolvehub.db)
  │     ├── RunMigrations()                       ← CREATE TABLE IF NOT EXISTS ...
  │     └── SeedDemoData()                        ← Inserts Aarav Sharma if DB is empty
  │
  └── Application.Run(new LoginForm(db))          ← UI/Forms/LoginForm.cs
```

---

## 2. Login Flow

```
LoginForm
  │
  ├── User fills email + password → clicks "Sign In"
  │
  ├── BtnLogin_ClickAsync()
  │     ├── Task.Run → DatabaseService.FindUserByEmail(email)
  │     │     └── SELECT * FROM Users WHERE Email = @email
  │     │
  │     ├── SecureHasher.Verify(plainPassword, user.PasswordHash)
  │     │     ├── Split stored "salt:hash" string
  │     │     ├── Rfc2898DeriveBytes.Pbkdf2(password, salt, 100000, SHA256)
  │     │     └── CryptographicOperations.FixedTimeEquals(actual, expected)
  │     │
  │     ├── [FAIL] → ShowError("Invalid email or password.")
  │     │
  │     └── [PASS] → new StudentDashboardForm(db, user).Show()
  │                   LoginForm.Hide()
  │
  └── "Register here" link → RegisterForm.ShowDialog()
```

---

## 3. Registration Flow

```
RegisterForm
  │
  ├── User fills all fields → clicks "Create Account"
  │
  ├── BtnRegister_ClickAsync()
  │     ├── Client-side validation (empty fields, password length, email format)
  │     ├── Task.Run → DatabaseService.EmailExists(email)
  │     ├── Task.Run → DatabaseService.StudentIdExists(studentId)
  │     ├── Task.Run → SecureHasher.Hash(password)
  │     │     ├── RandomNumberGenerator.GetBytes(16)    ← random salt
  │     │     └── Rfc2898DeriveBytes.Pbkdf2(...)        ← derive key
  │     │     └── returns "base64Salt:base64Hash"
  │     ├── Task.Run → DatabaseService.CreateUser(user)
  │     │     └── INSERT INTO Users ...
  │     └── RegistrationSucceeded event → LoginForm pre-fills email
```

---

## 4. Student Dashboard Launch

```
StudentDashboardForm(db, user)
  │
  ├── InitializeComponent()
  │     ├── BuildSidebar()          ← Teal sidebar with nav buttons
  │     ├── BuildTopBar()           ← "Signed in as …" bar
  │     └── ShowDashboard()         ← Default landing panel
  │
  ├── LoadComplaints()
  │     ├── DatabaseService.GetComplaintsByStudent(studentId)
  │     │     └── SELECT * FROM Complaints WHERE StudentId = @sid ORDER BY CreatedAt DESC
  │     └── ComplaintEngine.Ingest(complaint)   ← hydrate in-memory DSA structures
  │           ├── ComplaintHashTable.InsertOrMergeDuplicate(c)
  │           └── ComplaintMaxHeap.Insert(c)
  │
  └── LocalTcpServer.Start()        ← Network/LocalTcpServer.cs
        └── Task.Run(AcceptLoopAsync) ← listens on 127.0.0.1:5000
```

---

## 5. Complaint Submission Flow

```
Dashboard "Submit Complaint" button clicked
  │
  ├── SubmitComplaintAsync()
  │     ├── Validates title is not empty
  │     │
  │     ├── Builds Complaint object with CompositeKey = "category:hostel:room"
  │     │
  │     ├── ComplaintEngine.Ingest(complaint)
  │     │     │
  │     │     ├── ComplaintHashTable.InsertOrMergeDuplicate(complaint)
  │     │     │     ├── GetBucketIndex(compositeKey)  ← polynomial rolling hash
  │     │     │     ├── Walk chain at that bucket
  │     │     │     ├── [DUPLICATE] → DuplicateCount++ → return false
  │     │     │     └── [NEW]       → prepend node     → return true
  │     │     │
  │     │     ├── [NEW]  → ComplaintMaxHeap.Insert(complaint)
  │     │     │             └── SiftUp() to maintain max-heap property
  │     │     │
  │     │     └── [DUPLICATE] → ComplaintMaxHeap.Rebuild()
  │     │                       └── Floyd's build-heap O(n)
  │     │
  │     ├── [NEW]       → DatabaseService.CreateComplaint(complaint)
  │     │                 └── INSERT INTO Complaints ...
  │     │
  │     └── [DUPLICATE] → DatabaseService.IncrementDuplicateCount(existingId)
  │                       └── UPDATE Complaints SET DuplicateCount = DuplicateCount + 1
  │
  └── RefreshStats() → Updates the 4 stat-card labels
```

---

## 6. TCP Loopback Communication Flow

```
LocalTcpServer (running in background Task)
  │
  ├── AcceptLoopAsync()
  │     └── TcpListener.AcceptTcpClientAsync()
  │           └── Task.Run(HandleClientAsync(client))
  │                 ├── Read newline-delimited JSON line
  │                 ├── Deserialise → TcpRequest { Action, Payload }
  │                 ├── DispatchAction(request)
  │                 │     ├── "PING"   → TcpResponse(true, "PONG")
  │                 │     └── "STATUS" → TcpResponse(true, "Server is running.")
  │                 └── Write serialised TcpResponse as newline-delimited JSON

LocalTcpClient.SendAsync("PING")
  │
  ├── Build TcpRequest JSON
  ├── TcpClient.ConnectAsync("127.0.0.1", 5000)
  ├── Write request line
  ├── Read response line
  └── Deserialise → TcpResponse
```

---

## 7. Auto-Complete Flow (Trie)

```
User types in a Category text box (future enhancement hook)
  │
  └── ComplaintEngine.Suggest(prefix)
        └── CategoryTrie.AutoComplete(prefix)
              ├── Navigate trie to end of prefix
              └── DFS to collect all matching words (up to 10)
```

---

## 8. Priority Queue Usage

```
ComplaintEngine.GetAllByPriority()
  │
  └── ComplaintMaxHeap.ToSortedList()
        ├── Snapshot the backing List<Complaint>
        └── Sort by Priority descending
              Priority = (Urgency × 10) + (HoursPending × 1.5) + (DuplicateCount × 5)
```

---

## 9. File & Namespace Map

```
ResolveHub/
├── Program.cs                         ← Entry point
├── Config/
│   └── AppSettings.cs                 ← All constants / paths
├── Core/
│   ├── Models/
│   │   ├── Complaint.cs               ← Domain model + priority formula
│   │   └── User.cs                    ← User domain model
│   ├── Security/
│   │   └── SecureHasher.cs            ← PBKDF2 hash + verify
│   ├── DataStructures/
│   │   ├── ComplaintHashTable.cs      ← Custom O(1) duplicate detection
│   │   ├── ComplaintMaxHeap.cs        ← Binary max-heap priority queue
│   │   └── CategoryTrie.cs            ← Prefix trie for auto-complete
│   └── Engine/
│       └── ComplaintEngine.cs         ← Orchestrates all 3 DSA structures
├── Data/
│   └── DatabaseService.cs             ← SQLite DAL (all DB operations)
├── Network/
│   ├── LocalTcpServer.cs              ← Async loopback TCP server
│   └── LocalTcpClient.cs              ← Stateless async TCP client
├── UI/
│   ├── Theme/
│   │   └── AppTheme.cs                ← Colours, fonts, control factories
│   └── Forms/
│       ├── LoginForm.cs               ← Auth gate
│       ├── RegisterForm.cs            ← New student registration
│       └── StudentDashboardForm.cs    ← Main application shell
└── Docs/
    ├── DECISIONS.md                   ← Architecture decision log
    └── FLOW.md                        ← This file
```
