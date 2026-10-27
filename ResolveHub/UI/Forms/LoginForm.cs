// =============================================================================
// LoginForm.cs — Authentication Gate (Programmatic WinForms — No Designer File)
// Responsive layout using TableLayoutPanel with percentage columns/rows.
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
    /// The application entry form.  Authenticates the student against the
    /// SQLite Users table using PBKDF2 password verification.
    /// On success it opens <see cref="StudentDashboardForm"/> and hides itself.
    /// </summary>
    public sealed class LoginForm : Form
    {
        // -----------------------------------------------------------------
        // Services
        // -----------------------------------------------------------------
        private readonly DatabaseService _db;

        // -----------------------------------------------------------------
        // Controls (declared as fields for easy event-wiring)
        // -----------------------------------------------------------------
        private TextBox  _txtEmail    = null!;
        private TextBox  _txtPassword = null!;
        private Button   _btnLogin    = null!;
        private Label    _lblError    = null!;
        private CheckBox _chkShowPwd  = null!;
        private LinkLabel _lnkRegister = null!;

        // -----------------------------------------------------------------
        // Constructor
        // -----------------------------------------------------------------
        public LoginForm(DatabaseService db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            InitializeComponent();
        }

        // -----------------------------------------------------------------
        // Form initialisation
        // -----------------------------------------------------------------
        private void InitializeComponent()
        {
            // ── Form properties ──────────────────────────────────────────
            Text            = "Resolve Hub — Login";
            Size            = new Size(1000, 680);
            MinimumSize     = new Size(800, 580);
            StartPosition   = FormStartPosition.CenterScreen;
            BackColor       = AppTheme.Background;
            FormBorderStyle = FormBorderStyle.Sizable;
            DoubleBuffered  = true;

            // ── Root split: left panel (brand) | right panel (form) ──────
            var root = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 2,
                RowCount    = 1,
                Padding     = Padding.Empty
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45f));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            Controls.Add(root);

            // ── Left: brand / hero panel ─────────────────────────────────
            var leftPanel = BuildBrandPanel();
            root.Controls.Add(leftPanel, 0, 0);

            // ── Right: login form ─────────────────────────────────────────
            var rightPanel = BuildFormPanel();
            root.Controls.Add(rightPanel, 1, 0);
        }

        // -----------------------------------------------------------------
        // Brand / hero panel (left side)
        // -----------------------------------------------------------------
        private Panel BuildBrandPanel()
        {
            var panel = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = AppTheme.Secondary,
                Padding   = new Padding(AppTheme.SpaceXL)
            };

            var layout = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                RowCount    = 4,
                BackColor   = Color.Transparent
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 15f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 15f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 20f));
            panel.Controls.Add(layout);

            // Icon / logo text
            var logoLabel = new Label
            {
                Text      = "🏫",
                Font      = new Font("Segoe UI Emoji", 36f),
                ForeColor = Color.White,
                AutoSize  = false,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft
            };
            layout.Controls.Add(logoLabel, 0, 0);

            // App name
            var appName = new Label
            {
                Text      = "Resolve Hub",
                Font      = new Font("Segoe UI", 24f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize  = false,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            layout.Controls.Add(appName, 0, 1);

            // Tagline
            var tagline = new Label
            {
                Text      = "Student Grievance\nManagement System\n\nRaise. Track.\nResolve.",
                Font      = new Font("Segoe UI", 14f, FontStyle.Regular),
                ForeColor = ColorTranslator.FromHtml("#A8D5CC"),
                AutoSize  = false,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            layout.Controls.Add(tagline, 0, 2);

            // Version
            var version = new Label
            {
                Text      = $"Version {Config.AppSettings.AppVersion}",
                Font      = AppTheme.FontCaption,
                ForeColor = ColorTranslator.FromHtml("#6B8A84"),
                AutoSize  = false,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft
            };
            layout.Controls.Add(version, 0, 3);

            return panel;
        }

        // -----------------------------------------------------------------
        // Form panel (right side)
        // -----------------------------------------------------------------
        private Panel BuildFormPanel()
        {
            var outer = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = AppTheme.Background,
                Padding   = new Padding(AppTheme.SpaceXL, 0, AppTheme.SpaceXL, 0)
            };

            // Centre the card vertically using a TableLayoutPanel
            var centreLayout = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                RowCount    = 3,
                BackColor   = Color.Transparent
            };
            centreLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 15f));
            centreLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 70f));
            centreLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 15f));
            outer.Controls.Add(centreLayout);

            // Card
            var card = BuildCard();
            centreLayout.Controls.Add(card, 0, 1);

            return outer;
        }

        private Panel BuildCard()
        {
            var card = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = AppTheme.Surface,
                Padding   = new Padding(AppTheme.SpaceXL),
                Margin    = new Padding(0)
            };
            card.Paint += PaintCardBorder;

            // Layout inside card
            var layout = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                RowCount    = 9,
                BackColor   = Color.Transparent
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));  // heading
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));  // subtitle
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));  // spacer
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));  // email label
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));  // email input
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));  // password label
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));  // password input
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));  // show pwd + error
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // button + link
            card.Controls.Add(layout);

            // Heading
            var heading = new Label
            {
                Text      = "Welcome back",
                Font      = AppTheme.FontHeading1,
                ForeColor = AppTheme.TextDark,
                AutoSize  = false,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft
            };
            layout.Controls.Add(heading, 0, 0);

            // Sub-heading
            var sub = new Label
            {
                Text      = "Sign in to your account",
                Font      = AppTheme.FontBody,
                ForeColor = AppTheme.TextMedium,
                AutoSize  = false,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft
            };
            layout.Controls.Add(sub, 0, 1);

            // Spacer row (empty)
            layout.Controls.Add(new Label { Dock = DockStyle.Fill, BackColor = Color.Transparent }, 0, 2);

            // Email label + input
            var lblEmail = AppTheme.FieldLabel("Email address");
            lblEmail.Dock = DockStyle.Fill;
            layout.Controls.Add(lblEmail, 0, 3);

            _txtEmail = new TextBox
            {
                Dock        = DockStyle.Fill,
                Font        = AppTheme.FontBody,
                BackColor   = AppTheme.Background,
                ForeColor   = AppTheme.TextDark,
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "student@resolvehub.com"
            };
            layout.Controls.Add(_txtEmail, 0, 4);

            // Password label + input
            var lblPwd = AppTheme.FieldLabel("Password");
            lblPwd.Dock = DockStyle.Fill;
            layout.Controls.Add(lblPwd, 0, 5);

            _txtPassword = new TextBox
            {
                Dock            = DockStyle.Fill,
                Font            = AppTheme.FontBody,
                BackColor       = AppTheme.Background,
                ForeColor       = AppTheme.TextDark,
                BorderStyle     = BorderStyle.FixedSingle,
                UseSystemPasswordChar = true,
                PlaceholderText = "Enter your password"
            };
            layout.Controls.Add(_txtPassword, 0, 6);

            // Show password + error row
            var optRow = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 2,
                RowCount    = 1,
                BackColor   = Color.Transparent,
                Margin      = new Padding(0)
            };
            optRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            optRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

            _chkShowPwd = new CheckBox
            {
                Text      = "Show password",
                Font      = AppTheme.FontBodySmall,
                ForeColor = AppTheme.TextMedium,
                Anchor    = AnchorStyles.Left | AnchorStyles.Top,
                AutoSize  = true
            };
            _chkShowPwd.CheckedChanged += (_, _) =>
                _txtPassword.UseSystemPasswordChar = !_chkShowPwd.Checked;
            optRow.Controls.Add(_chkShowPwd, 0, 0);

            _lblError = new Label
            {
                Text      = "",
                Font      = AppTheme.FontBodySmall,
                ForeColor = AppTheme.Danger,
                AutoSize  = false,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight
            };
            optRow.Controls.Add(_lblError, 1, 0);
            layout.Controls.Add(optRow, 0, 7);

            // Bottom row: login button + register link
            var bottomLayout = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                RowCount    = 3,
                BackColor   = Color.Transparent,
                Margin      = new Padding(0)
            };
            bottomLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, AppTheme.SpaceMD));
            bottomLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, AppTheme.ButtonHeight));
            bottomLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            // Spacer
            bottomLayout.Controls.Add(new Label { BackColor = Color.Transparent }, 0, 0);

            // Login button
            _btnLogin = AppTheme.PrimaryButton("Sign In");
            _btnLogin.Dock   = DockStyle.Fill;
            _btnLogin.Click += BtnLogin_ClickAsync;
            bottomLayout.Controls.Add(_btnLogin, 0, 1);

            // Register link
            _lnkRegister = new LinkLabel
            {
                Text      = "Don't have an account? Register here",
                Font      = AppTheme.FontBody,
                LinkColor = AppTheme.Primary,
                ActiveLinkColor = AppTheme.PrimaryDark,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock      = DockStyle.Fill
            };
            _lnkRegister.LinkClicked += (_, _) => OpenRegisterForm();
            bottomLayout.Controls.Add(_lnkRegister, 0, 2);

            layout.Controls.Add(bottomLayout, 0, 8);

            // Enter key submits form
            _txtPassword.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter) BtnLogin_ClickAsync(this, EventArgs.Empty);
            };

            return card;
        }

        // -----------------------------------------------------------------
        // Login handler
        // -----------------------------------------------------------------
        private async void BtnLogin_ClickAsync(object? sender, EventArgs e)
        {
            _lblError.Text  = "";
            _btnLogin.Enabled = false;
            _btnLogin.Text  = "Signing in…";

            try
            {
                string email    = _txtEmail.Text.Trim();
                string password = _txtPassword.Text;

                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                {
                    ShowError("Please enter your email and password.");
                    return;
                }

                // Off-load DB + crypto work to a background thread
                User? user = await Task.Run(() => _db.FindUserByEmail(email));

                if (user is null || !SecureHasher.Verify(password, user.PasswordHash))
                {
                    ShowError("Invalid email or password.");
                    return;
                }

                // Success — open dashboard and hide login form
                var dashboard = new StudentDashboardForm(_db, user);
                dashboard.FormClosed += (_, _) => Close();
                dashboard.Show();
                Hide();
            }
            catch (Exception ex)
            {
                ShowError($"An error occurred: {ex.Message}");
            }
            finally
            {
                _btnLogin.Enabled = true;
                _btnLogin.Text    = "Sign In";
            }
        }

        // -----------------------------------------------------------------
        // Open register form
        // -----------------------------------------------------------------
        private void OpenRegisterForm()
        {
            var register = new RegisterForm(_db);
            register.RegistrationSucceeded += OnRegistrationSucceeded;
            register.ShowDialog(this);
        }

        private void OnRegistrationSucceeded(User user)
        {
            _txtEmail.Text    = user.Email;
            _txtPassword.Text = "";
            _txtPassword.Focus();
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------
        private void ShowError(string message) => _lblError.Text = message;

        private static void PaintCardBorder(object? sender, PaintEventArgs e)
        {
            if (sender is not Panel p) return;
            using var pen = new Pen(AppTheme.Border, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
        }
    }
}
