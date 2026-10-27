// =============================================================================
// RegisterForm.cs — New Student Registration
// Validates input, checks for duplicates, hashes passwords, persists to SQLite.
// =============================================================================

using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using ResolveHub.Core.Models;
using ResolveHub.Core.Security;
using ResolveHub.Data;
using ResolveHub.UI.Theme;

namespace ResolveHub.UI.Forms
{
    /// <summary>
    /// A modal dialog form for new student registration.
    /// On success, raises <see cref="RegistrationSucceeded"/> so the caller
    /// (LoginForm) can pre-fill the email field.
    /// </summary>
    public sealed class RegisterForm : Form
    {
        // -----------------------------------------------------------------
        // Event raised to LoginForm on success
        // -----------------------------------------------------------------
        public event Action<User>? RegistrationSucceeded;

        // -----------------------------------------------------------------
        // Services
        // -----------------------------------------------------------------
        private readonly DatabaseService _db;

        // -----------------------------------------------------------------
        // Controls
        // -----------------------------------------------------------------
        private TextBox  _txtFullName   = null!;
        private TextBox  _txtStudentId  = null!;
        private TextBox  _txtEmail      = null!;
        private TextBox  _txtPassword   = null!;
        private TextBox  _txtConfirm    = null!;
        private ComboBox _cmbHostel     = null!;
        private TextBox  _txtRoom       = null!;
        private Button   _btnRegister   = null!;
        private Label    _lblError      = null!;

        // -----------------------------------------------------------------
        // Constructor
        // -----------------------------------------------------------------
        public RegisterForm(DatabaseService db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            InitializeComponent();
        }

        // -----------------------------------------------------------------
        // Form initialisation
        // -----------------------------------------------------------------
        private void InitializeComponent()
        {
            Text            = "Resolve Hub — Register";
            Size            = new Size(560, 680);
            MinimumSize     = new Size(500, 640);
            StartPosition   = FormStartPosition.CenterParent;
            BackColor       = AppTheme.Background;
            FormBorderStyle = FormBorderStyle.Sizable;
            DoubleBuffered  = true;

            var scroll = new Panel
            {
                Dock        = DockStyle.Fill,
                AutoScroll  = true,
                Padding     = new Padding(AppTheme.SpaceXL)
            };
            Controls.Add(scroll);

            var layout = new TableLayoutPanel
            {
                Dock        = DockStyle.Top,
                ColumnCount = 1,
                AutoSize    = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            scroll.Controls.Add(layout);

            // Heading
            layout.Controls.Add(HeadingLabel("Create Account"));
            layout.Controls.Add(SubLabel("Fill in your details to register"));
            layout.Controls.Add(Spacer(AppTheme.SpaceMD));

            // Fields
            layout.Controls.Add(AppTheme.FieldLabel("Full Name *"));
            _txtFullName = StyledInput("Aarav Sharma");
            layout.Controls.Add(_txtFullName);
            layout.Controls.Add(Spacer(AppTheme.SpaceSM));

            layout.Controls.Add(AppTheme.FieldLabel("Student ID *"));
            _txtStudentId = StyledInput("2024-CS-042");
            layout.Controls.Add(_txtStudentId);
            layout.Controls.Add(Spacer(AppTheme.SpaceSM));

            layout.Controls.Add(AppTheme.FieldLabel("Email Address *"));
            _txtEmail = StyledInput("your.email@university.edu");
            layout.Controls.Add(_txtEmail);
            layout.Controls.Add(Spacer(AppTheme.SpaceSM));

            layout.Controls.Add(AppTheme.FieldLabel("Hostel"));
            _cmbHostel = new ComboBox
            {
                Dock      = DockStyle.Fill,
                Font      = AppTheme.FontBody,
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextDark,
                FlatStyle = FlatStyle.Flat,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbHostel.Items.AddRange(["Hostel A", "Hostel B", "Hostel C", "Hostel D", "Girls Hostel 1", "Girls Hostel 2"]);
            _cmbHostel.SelectedIndex = 0;
            layout.Controls.Add(_cmbHostel);
            layout.Controls.Add(Spacer(AppTheme.SpaceSM));

            layout.Controls.Add(AppTheme.FieldLabel("Room Number"));
            _txtRoom = StyledInput("e.g. 204");
            layout.Controls.Add(_txtRoom);
            layout.Controls.Add(Spacer(AppTheme.SpaceSM));

            layout.Controls.Add(AppTheme.FieldLabel("Password *"));
            _txtPassword = StyledInput("Min 8 characters");
            _txtPassword.UseSystemPasswordChar = true;
            layout.Controls.Add(_txtPassword);
            layout.Controls.Add(Spacer(AppTheme.SpaceSM));

            layout.Controls.Add(AppTheme.FieldLabel("Confirm Password *"));
            _txtConfirm = StyledInput("Re-enter password");
            _txtConfirm.UseSystemPasswordChar = true;
            layout.Controls.Add(_txtConfirm);
            layout.Controls.Add(Spacer(AppTheme.SpaceMD));

            // Error label
            _lblError = new Label
            {
                Text      = "",
                Font      = AppTheme.FontBodySmall,
                ForeColor = AppTheme.Danger,
                Dock      = DockStyle.Fill,
                AutoSize  = false,
                Height    = 40,
                TextAlign = ContentAlignment.MiddleLeft
            };
            layout.Controls.Add(_lblError);

            // Register button
            _btnRegister = AppTheme.PrimaryButton("Create Account");
            _btnRegister.Dock   = DockStyle.Fill;
            _btnRegister.Height = AppTheme.ButtonHeight;
            _btnRegister.Click += BtnRegister_ClickAsync;
            layout.Controls.Add(_btnRegister);
            layout.Controls.Add(Spacer(AppTheme.SpaceMD));

            // Set each control's Dock inside layout
            foreach (Control c in layout.Controls)
            {
                if (c is not Label { Height: <= 20 })
                    c.Dock = DockStyle.Fill;
            }
        }

        // -----------------------------------------------------------------
        // Register handler
        // -----------------------------------------------------------------
        private async void BtnRegister_ClickAsync(object? sender, EventArgs e)
        {
            _lblError.Text       = "";
            _btnRegister.Enabled = false;
            _btnRegister.Text    = "Creating account…";

            try
            {
                // ── Validation ────────────────────────────────────────────
                string fullName  = _txtFullName.Text.Trim();
                string studentId = _txtStudentId.Text.Trim();
                string email     = _txtEmail.Text.Trim().ToLower();
                string hostel    = _cmbHostel.SelectedItem?.ToString() ?? "";
                string room      = _txtRoom.Text.Trim();
                string password  = _txtPassword.Text;
                string confirm   = _txtConfirm.Text;

                if (string.IsNullOrWhiteSpace(fullName) ||
                    string.IsNullOrWhiteSpace(studentId) ||
                    string.IsNullOrWhiteSpace(email)     ||
                    string.IsNullOrWhiteSpace(password))
                {
                    ShowError("Please fill in all required fields (marked with *).");
                    return;
                }

                if (password.Length < 8)
                {
                    ShowError("Password must be at least 8 characters.");
                    return;
                }

                if (password != confirm)
                {
                    ShowError("Passwords do not match.");
                    return;
                }

                if (!email.Contains('@') || !email.Contains('.'))
                {
                    ShowError("Please enter a valid email address.");
                    return;
                }

                // ── Duplicate checks (async DB calls) ─────────────────────
                bool emailExists     = await Task.Run(() => _db.EmailExists(email));
                bool studentIdExists = await Task.Run(() => _db.StudentIdExists(studentId));

                if (emailExists)
                {
                    ShowError("An account with this email already exists.");
                    return;
                }

                if (studentIdExists)
                {
                    ShowError("This Student ID is already registered.");
                    return;
                }

                // ── Hash password and persist ─────────────────────────────
                string hash = await Task.Run(() => SecureHasher.Hash(password));

                var user = new User
                {
                    FullName     = fullName,
                    StudentId    = studentId,
                    Email        = email,
                    PasswordHash = hash,
                    HostelName   = hostel,
                    RoomNumber   = room,
                    Role         = UserRole.Student
                };

                int newId = await Task.Run(() => _db.CreateUser(user));
                user.Id   = newId;

                // ── Notify caller and close ───────────────────────────────
                RegistrationSucceeded?.Invoke(user);

                MessageBox.Show(
                    $"Account created successfully!\nYou can now sign in with:\n{email}",
                    "Registration Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                Close();
            }
            catch (Exception ex)
            {
                ShowError($"Registration failed: {ex.Message}");
            }
            finally
            {
                _btnRegister.Enabled = true;
                _btnRegister.Text    = "Create Account";
            }
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------
        private void ShowError(string msg) => _lblError.Text = msg;

        private static Label HeadingLabel(string text) => new()
        {
            Text      = text,
            Font      = AppTheme.FontHeading2,
            ForeColor = AppTheme.TextDark,
            AutoSize  = true,
            Margin    = new Padding(0, 0, 0, AppTheme.SpaceXS)
        };

        private static Label SubLabel(string text) => new()
        {
            Text      = text,
            Font      = AppTheme.FontBody,
            ForeColor = AppTheme.TextMedium,
            AutoSize  = true
        };

        private static Panel Spacer(int height) => new()
        {
            Height    = height,
            Dock      = DockStyle.Fill,
            BackColor = Color.Transparent
        };

        private static TextBox StyledInput(string placeholder) => new()
        {
            Font            = AppTheme.FontBody,
            BackColor       = AppTheme.Background,
            ForeColor       = AppTheme.TextDark,
            BorderStyle     = BorderStyle.FixedSingle,
            Height          = AppTheme.InputHeight,
            PlaceholderText = placeholder
        };
    }
}
