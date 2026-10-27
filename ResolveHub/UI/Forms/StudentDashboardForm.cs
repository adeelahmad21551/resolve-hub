// =============================================================================
// StudentDashboardForm.cs — Main Application Shell
// Sidebar navigation + content panels for Dashboard, Submit, My Complaints.
// Matches the provided UI screenshot exactly.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using ResolveHub.Config;
using ResolveHub.Core.Engine;
using ResolveHub.Core.Models;
using ResolveHub.Data;
using ResolveHub.Network;
using ResolveHub.UI.Theme;

namespace ResolveHub.UI.Forms
{
    /// <summary>
    /// The main shell form that the student sees after logging in.
    /// Layout:
    ///   ┌──────────┬──────────────────────────────────────────┐
    ///   │ Sidebar  │  Top Bar                                 │
    ///   │          ├──────────────────────────────────────────┤
    ///   │  Nav     │  Content Area (swappable panels)         │
    ///   └──────────┴──────────────────────────────────────────┘
    /// </summary>
    public sealed class StudentDashboardForm : Form
    {
        // -----------------------------------------------------------------
        // Services & state
        // -----------------------------------------------------------------
        private readonly DatabaseService  _db;
        private readonly User             _currentUser;
        private readonly ComplaintEngine  _engine  = new();
        private readonly LocalTcpServer   _server  = new();
        private List<Complaint>           _complaints = [];

        // -----------------------------------------------------------------
        // Layout containers
        // -----------------------------------------------------------------
        private Panel _contentArea    = null!;
        private Panel _activeSideBtn  = null!;

        // Stat card labels (updated when data reloads)
        private Label _statTotal      = null!;
        private Label _statPending    = null!;
        private Label _statInProgress = null!;
        private Label _statResolved   = null!;

        // -----------------------------------------------------------------
        // Constructor
        // -----------------------------------------------------------------
        public StudentDashboardForm(DatabaseService db, User user)
        {
            _db          = db   ?? throw new ArgumentNullException(nameof(db));
            _currentUser = user ?? throw new ArgumentNullException(nameof(user));
            InitializeComponent();
            LoadComplaints();
            _server.Start();          // start loopback server when dashboard opens
        }

        // -----------------------------------------------------------------
        // Form setup
        // -----------------------------------------------------------------
        private void InitializeComponent()
        {
            Text            = $"Hostel Complaint Desk — Student Dashboard";
            Size            = new Size(1200, 760);
            MinimumSize     = new Size(900, 620);
            StartPosition   = FormStartPosition.CenterScreen;
            BackColor       = AppTheme.Background;
            FormBorderStyle = FormBorderStyle.Sizable;
            DoubleBuffered  = true;

            // Root: sidebar | main
            var root = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 2,
                RowCount    = 1
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, AppTheme.SidebarWidth));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            Controls.Add(root);

            // Sidebar
            root.Controls.Add(BuildSidebar(), 0, 0);

            // Main column: topbar + content
            var mainCol = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                RowCount    = 2,
                BackColor   = AppTheme.Background
            };
            mainCol.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            mainCol.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.Controls.Add(mainCol, 1, 0);

            mainCol.Controls.Add(BuildTopBar(), 0, 0);

            _contentArea = new Panel
            {
                Dock       = DockStyle.Fill,
                AutoScroll = true,
                BackColor  = AppTheme.Background,
                Padding    = new Padding(AppTheme.SpaceLG)
            };
            mainCol.Controls.Add(_contentArea, 0, 1);

            // Start on Dashboard panel
            ShowDashboard();
        }

        // -----------------------------------------------------------------
        // Sidebar
        // -----------------------------------------------------------------
        private Panel BuildSidebar()
        {
            var sidebar = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = AppTheme.Secondary
            };

            var layout = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                RowCount    = 3,
                BackColor   = Color.Transparent
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120)); // Brand
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // Nav
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));  // User profile
            sidebar.Controls.Add(layout);

            // Brand block
            var brand = new Panel { Dock = DockStyle.Fill, Padding = new Padding(AppTheme.SpaceMD) };
            brand.Controls.Add(new Label
            {
                Text      = "🏫  Resolve Hub",
                Font      = new Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize  = false,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            });
            layout.Controls.Add(brand, 0, 0);

            // Nav links
            var nav = new FlowLayoutPanel
            {
                Dock        = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents  = false,
                Padding       = new Padding(AppTheme.SpaceSM, 0, AppTheme.SpaceSM, 0),
                BackColor     = Color.Transparent
            };

            _activeSideBtn = AddNavItem(nav, "📊  Dashboard",        () => ShowDashboard(),       true);
                             AddNavItem(nav, "📝  Submit Complaint",  () => ShowSubmitPanel());
                             AddNavItem(nav, "📋  My Complaints",     () => ShowMyComplaints());
                             AddNavItem(nav, "🔔  Notifications",     () => ShowNotifications());
                             AddNavItem(nav, "⚙️  Settings",         () => ShowSettings());
            layout.Controls.Add(nav, 0, 1);

            // User block
            var userPanel = new Panel
            {
                Dock      = DockStyle.Fill,
                Padding   = new Padding(AppTheme.SpaceMD),
                BackColor = AppTheme.PrimaryDark
            };
            var userLayout = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                RowCount    = 2,
                BackColor   = Color.Transparent
            };
            userLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 55f));
            userLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 45f));
            userPanel.Controls.Add(userLayout);

            userLayout.Controls.Add(new Label
            {
                Text      = _currentUser.FullName,
                Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize  = false,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft
            }, 0, 0);

            userLayout.Controls.Add(new Label
            {
                Text      = $"Student ID: {_currentUser.StudentId}",
                Font      = AppTheme.FontCaption,
                ForeColor = ColorTranslator.FromHtml("#A8D5CC"),
                AutoSize  = false,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft
            }, 0, 1);

            layout.Controls.Add(userPanel, 0, 2);
            return sidebar;
        }

        // -----------------------------------------------------------------
        // Top bar
        // -----------------------------------------------------------------
        private Panel BuildTopBar()
        {
            var bar = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = AppTheme.Surface,
                Padding   = new Padding(AppTheme.SpaceLG, 0, AppTheme.SpaceLG, 0)
            };
            bar.Paint += (s, e) =>
            {
                using var pen = new Pen(AppTheme.Border, 1);
                e.Graphics.DrawLine(pen, 0, bar.Height - 1, bar.Width, bar.Height - 1);
            };

            var layout = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 2,
                RowCount    = 1,
                BackColor   = Color.Transparent
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40f));
            bar.Controls.Add(layout);

            layout.Controls.Add(new Label
            {
                Text      = $"Signed in as {_currentUser.FullName}  •  {_currentUser.HostelName}, Room {_currentUser.RoomNumber}",
                Font      = AppTheme.FontBodySmall,
                ForeColor = AppTheme.TextMedium,
                AutoSize  = false,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);

            layout.Controls.Add(new Label
            {
                Text      = $"Resolve Hub  |  Version {AppSettings.AppVersion}",
                Font      = AppTheme.FontBodySmall,
                ForeColor = AppTheme.TextLight,
                AutoSize  = false,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight
            }, 1, 0);

            return bar;
        }

        // -----------------------------------------------------------------
        // Navigation helper — creates a sidebar button
        // -----------------------------------------------------------------
        private Panel AddNavItem(FlowLayoutPanel parent, string text, Action onClick, bool isActive = false)
        {
            var btn = new Panel
            {
                Width     = AppTheme.SidebarWidth - AppTheme.SpaceMD,
                Height    = 44,
                BackColor = isActive ? AppTheme.Primary : Color.Transparent,
                Cursor    = Cursors.Hand,
                Margin    = new Padding(0, 2, 0, 2)
            };

            var lbl = new Label
            {
                Text      = text,
                Font      = new Font("Segoe UI", 9.5f, isActive ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = isActive ? Color.White : ColorTranslator.FromHtml("#A8D5CC"),
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(AppTheme.SpaceMD, 0, 0, 0)
            };
            btn.Controls.Add(lbl);

            // Hover
            btn.MouseEnter += (_, _) => { if (btn != _activeSideBtn) btn.BackColor = ColorTranslator.FromHtml("#1A4A42"); };
            btn.MouseLeave += (_, _) => { if (btn != _activeSideBtn) btn.BackColor = Color.Transparent; };
            lbl.MouseEnter += (_, _) => { if (btn != _activeSideBtn) btn.BackColor = ColorTranslator.FromHtml("#1A4A42"); };
            lbl.MouseLeave += (_, _) => { if (btn != _activeSideBtn) btn.BackColor = Color.Transparent; };

            void Activate(object? s, EventArgs e)
            {
                // Deactivate previous
                if (_activeSideBtn != null)
                {
                    _activeSideBtn.BackColor = Color.Transparent;
                    foreach (Control c in _activeSideBtn.Controls)
                        if (c is Label l) { l.Font = new Font(l.Font, FontStyle.Regular); l.ForeColor = ColorTranslator.FromHtml("#A8D5CC"); }
                }
                // Activate current
                btn.BackColor = AppTheme.Primary;
                lbl.Font      = new Font(lbl.Font, FontStyle.Bold);
                lbl.ForeColor = Color.White;
                _activeSideBtn = btn;
                onClick();
            }

            btn.Click += Activate;
            lbl.Click += Activate;

            parent.Controls.Add(btn);
            return btn;
        }

        // =================================================================
        // CONTENT PANELS
        // =================================================================

        // -----------------------------------------------------------------
        // Dashboard panel
        // -----------------------------------------------------------------
        private void ShowDashboard()
        {
            _contentArea.Controls.Clear();

            var outer = new TableLayoutPanel
            {
                Dock        = DockStyle.Top,
                ColumnCount = 1,
                RowCount    = 4,
                AutoSize    = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor   = Color.Transparent
            };

            // Page heading
            outer.Controls.Add(PageHeading(
                "Student Dashboard",
                $"Welcome, {_currentUser.FullName.Split(' ')[0]}. Submit a hostel issue and track your complaints here.",
                DateTime.Now.ToString("dddd, dd MMM yyyy")));

            outer.Controls.Add(Spacer(AppTheme.SpaceLG));

            // Stat cards row
            outer.Controls.Add(BuildStatCards());

            outer.Controls.Add(Spacer(AppTheme.SpaceLG));

            // Bottom: Submit form | Recent complaints (side-by-side)
            var bottomRow = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount    = 1,
                AutoSize    = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor   = Color.Transparent
            };
            bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42f));
            bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58f));

            bottomRow.Controls.Add(BuildQuickSubmitCard(), 0, 0);
            bottomRow.Controls.Add(BuildRecentComplaintsCard(), 1, 0);
            outer.Controls.Add(bottomRow);

            _contentArea.Controls.Add(outer);
            RefreshStats();
        }

        // -----------------------------------------------------------------
        // Stat cards
        // -----------------------------------------------------------------
        private TableLayoutPanel BuildStatCards()
        {
            var row = new TableLayoutPanel
            {
                ColumnCount = 4,
                RowCount    = 1,
                AutoSize    = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor   = Color.Transparent
            };
            for (int i = 0; i < 4; i++)
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));

            _statTotal      = AddStatCard(row, 0, "Total Complaints", "📋");
            _statPending    = AddStatCard(row, 1, "Pending",           "⏳");
            _statInProgress = AddStatCard(row, 2, "In Progress",       "🔧");
            _statResolved   = AddStatCard(row, 3, "Resolved",          "✅");

            return row;
        }

        private Label AddStatCard(TableLayoutPanel parent, int col, string caption, string icon)
        {
            var card = new Panel
            {
                BackColor = AppTheme.Surface,
                Margin    = new Padding(0, 0, AppTheme.SpaceMD, 0),
                Height    = 100,
                Dock      = DockStyle.Fill
            };
            card.Paint += PaintCardShadow;

            var layout = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 2,
                RowCount    = 2,
                BackColor   = Color.Transparent,
                Padding     = new Padding(AppTheme.SpaceMD)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 75f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));

            var capLbl = new Label
            {
                Text      = caption,
                Font      = AppTheme.FontBodySmall,
                ForeColor = AppTheme.TextMedium,
                AutoSize  = false,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft
            };
            layout.Controls.Add(capLbl, 0, 0);

            var iconLbl = new Label
            {
                Text      = icon,
                Font      = new Font("Segoe UI Emoji", 18f),
                ForeColor = AppTheme.Primary,
                AutoSize  = false,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight
            };
            layout.Controls.Add(iconLbl, 1, 0);

            var numLbl = new Label
            {
                Text      = "0",
                Font      = new Font("Segoe UI", 28f, FontStyle.Bold),
                ForeColor = AppTheme.TextDark,
                AutoSize  = false,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft
            };
            layout.Controls.Add(numLbl, 0, 1);

            card.Controls.Add(layout);
            parent.Controls.Add(card, col, 0);
            return numLbl;
        }

        // -----------------------------------------------------------------
        // Quick submit card (left column on dashboard)
        // -----------------------------------------------------------------
        private Panel BuildQuickSubmitCard()
        {
            var card = new Panel
            {
                BackColor = AppTheme.Surface,
                Margin    = new Padding(0, 0, AppTheme.SpaceMD, 0),
                Dock      = DockStyle.Fill,
                MinimumSize = new Size(0, 420)
            };
            card.Paint += PaintCardShadow;

            var layout = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                RowCount    = 10,
                BackColor   = Color.Transparent,
                Padding     = new Padding(AppTheme.SpaceLG)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));  // title
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));  // label
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));  // input
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));  // label
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));  // combo
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));  // label
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));  // room input
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));  // label
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));  // description
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // button

            var header = new Label
            {
                Text      = "📝  Submit a Complaint",
                Font      = AppTheme.FontHeading3,
                ForeColor = AppTheme.TextDark,
                AutoSize  = false,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            layout.Controls.Add(header, 0, 0);

            // Title field
            layout.Controls.Add(FieldLbl("Complaint title"), 0, 1);
            var txtTitle = Input("Enter a short title");
            layout.Controls.Add(txtTitle, 0, 2);

            // Category
            layout.Controls.Add(FieldLbl("Category"), 0, 3);
            var cmbCategory = new ComboBox
            {
                Dock      = DockStyle.Fill,
                Font      = AppTheme.FontBody,
                BackColor = AppTheme.Background,
                ForeColor = AppTheme.TextDark,
                FlatStyle = FlatStyle.Flat,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbCategory.Items.AddRange(["Water supply", "Electricity", "Room maintenance", "Wi-Fi", "Mess issues", "Sanitation", "Security", "Laundry", "Lift", "Plumbing"]);
            cmbCategory.SelectedIndex = 0;
            layout.Controls.Add(cmbCategory, 0, 4);

            // Room number
            layout.Controls.Add(FieldLbl("Room number"), 0, 5);
            var txtRoom = Input(_currentUser.RoomNumber);
            layout.Controls.Add(txtRoom, 0, 6);

            // Description
            layout.Controls.Add(FieldLbl("Description of the problem"), 0, 7);
            var txtDesc = new TextBox
            {
                Dock            = DockStyle.Fill,
                Multiline       = true,
                Font            = AppTheme.FontBody,
                BackColor       = AppTheme.Background,
                ForeColor       = AppTheme.TextDark,
                BorderStyle     = BorderStyle.FixedSingle,
                PlaceholderText = "Describe the issue and where it occurred…"
            };
            layout.Controls.Add(txtDesc, 0, 8);

            // Submit button
            var bottomPad = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(0, AppTheme.SpaceMD, 0, 0) };
            var btnSubmit = AppTheme.PrimaryButton("＋  Submit Complaint");
            btnSubmit.Dock   = DockStyle.Fill;
            btnSubmit.Click += async (_, _) =>
                await SubmitComplaintAsync(txtTitle, cmbCategory, txtRoom, txtDesc, btnSubmit);
            bottomPad.Controls.Add(btnSubmit);
            layout.Controls.Add(bottomPad, 0, 9);

            card.Controls.Add(layout);
            return card;
        }

        // -----------------------------------------------------------------
        // Recent complaints card (right column on dashboard)
        // -----------------------------------------------------------------
        private Panel BuildRecentComplaintsCard()
        {
            var card = new Panel
            {
                BackColor   = AppTheme.Surface,
                Dock        = DockStyle.Fill,
                MinimumSize = new Size(0, 420)
            };
            card.Paint += PaintCardShadow;

            var layout = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                RowCount    = 2,
                BackColor   = Color.Transparent,
                Padding     = new Padding(AppTheme.SpaceLG)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            // Header row
            var headerRow = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 2,
                RowCount    = 2,
                BackColor   = Color.Transparent,
                Margin      = new Padding(0)
            };
            headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70f));
            headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30f));
            headerRow.Controls.Add(new Label { Text = "Recent Complaints", Font = AppTheme.FontHeading3, ForeColor = AppTheme.TextDark, Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft, AutoSize = false }, 0, 0);
            headerRow.Controls.Add(new Label { Text = "View all →", Font = AppTheme.FontBodySmall, ForeColor = AppTheme.Primary, Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomRight, AutoSize = false, Cursor = Cursors.Hand }, 1, 0);
            headerRow.Controls.Add(new Label { Text = "Your submitted complaints and their current status.", Font = AppTheme.FontBodySmall, ForeColor = AppTheme.TextMedium, Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopLeft, AutoSize = false }, 0, 1);
            layout.Controls.Add(headerRow, 0, 0);

            // Complaints grid
            var grid = BuildComplaintsGrid();
            layout.Controls.Add(grid, 0, 1);

            card.Controls.Add(layout);
            return card;
        }

        private DataGridView BuildComplaintsGrid()
        {
            var grid = new DataGridView
            {
                Dock              = DockStyle.Fill,
                ReadOnly          = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                BorderStyle       = BorderStyle.None,
                BackgroundColor   = AppTheme.Surface,
                GridColor         = AppTheme.Border,
                Font              = AppTheme.FontBody,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode     = DataGridViewSelectionMode.FullRowSelect,
                CellBorderStyle   = DataGridViewCellBorderStyle.SingleHorizontal
            };
            grid.DefaultCellStyle.BackColor   = AppTheme.Surface;
            grid.DefaultCellStyle.ForeColor   = AppTheme.TextDark;
            grid.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#E8F5F2");
            grid.DefaultCellStyle.SelectionForeColor = AppTheme.TextDark;
            grid.ColumnHeadersDefaultCellStyle.BackColor   = AppTheme.Background;
            grid.ColumnHeadersDefaultCellStyle.ForeColor   = AppTheme.TextMedium;
            grid.ColumnHeadersDefaultCellStyle.Font        = AppTheme.FontBodySmall;
            grid.ColumnHeadersBorderStyle                  = DataGridViewHeaderBorderStyle.None;
            grid.EnableHeadersVisualStyles = false;

            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colId",      HeaderText = "Complaint ID", FillWeight = 18 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTitle",   HeaderText = "Title",        FillWeight = 32 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCat",     HeaderText = "Category",     FillWeight = 20 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDate",    HeaderText = "Date",         FillWeight = 16 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colStatus",  HeaderText = "Status",       FillWeight = 14 });

            grid.CellFormatting += (_, e) =>
            {
                if (e.ColumnIndex == 4 && e.RowIndex >= 0 && e.Value is string statusStr && e.CellStyle is not null)
                {
                    if (statusStr == "Pending")     { e.CellStyle.ForeColor = AppTheme.Pending;    e.CellStyle.Font = new Font(AppTheme.FontBodySmall, FontStyle.Bold); }
                    if (statusStr == "In Progress") { e.CellStyle.ForeColor = AppTheme.InProgress; e.CellStyle.Font = new Font(AppTheme.FontBodySmall, FontStyle.Bold); }
                    if (statusStr == "Resolved")    { e.CellStyle.ForeColor = AppTheme.Resolved;   e.CellStyle.Font = new Font(AppTheme.FontBodySmall, FontStyle.Bold); }
                }
            };

            PopulateGrid(grid);
            return grid;
        }

        // -----------------------------------------------------------------
        // My Complaints panel (full-screen view)
        // -----------------------------------------------------------------
        private void ShowMyComplaints()
        {
            _contentArea.Controls.Clear();

            var outer = new TableLayoutPanel
            {
                Dock        = DockStyle.Top,
                ColumnCount = 1,
                RowCount    = 3,
                AutoSize    = true,
                BackColor   = Color.Transparent
            };

            outer.Controls.Add(PageHeading("My Complaints", "All complaints you have submitted.", ""));
            outer.Controls.Add(Spacer(AppTheme.SpaceLG));

            var card = new Panel
            {
                BackColor = AppTheme.Surface,
                Height    = 500,
                Dock      = DockStyle.Top
            };
            card.Paint += PaintCardShadow;
            card.Padding = new Padding(AppTheme.SpaceLG);

            var grid = BuildComplaintsGrid();
            card.Controls.Add(grid);
            outer.Controls.Add(card);

            _contentArea.Controls.Add(outer);
        }

        // -----------------------------------------------------------------
        // Submit panel (standalone full page)
        // -----------------------------------------------------------------
        private void ShowSubmitPanel()
        {
            _contentArea.Controls.Clear();

            var outer = new TableLayoutPanel
            {
                Dock        = DockStyle.Top,
                ColumnCount = 1,
                RowCount    = 3,
                AutoSize    = true,
                BackColor   = Color.Transparent
            };

            outer.Controls.Add(PageHeading("Submit a Complaint", "Describe your hostel issue below.", ""));
            outer.Controls.Add(Spacer(AppTheme.SpaceLG));

            var card = BuildQuickSubmitCard();
            card.Margin = Padding.Empty;
            outer.Controls.Add(card);

            _contentArea.Controls.Add(outer);
        }

        // -----------------------------------------------------------------
        // Notifications placeholder
        // -----------------------------------------------------------------
        private void ShowNotifications()
        {
            _contentArea.Controls.Clear();
            _contentArea.Controls.Add(PageHeading("Notifications", "No new notifications.", ""));
        }

        // -----------------------------------------------------------------
        // Settings placeholder
        // -----------------------------------------------------------------
        private void ShowSettings()
        {
            _contentArea.Controls.Clear();
            _contentArea.Controls.Add(PageHeading("Settings", "Account settings coming soon.", ""));
        }

        // =================================================================
        // DATA OPERATIONS
        // =================================================================

        /// <summary>Loads all complaints for the current student from the DB and into the engine.</summary>
        private void LoadComplaints()
        {
            try
            {
                _complaints = _db.GetComplaintsByStudent(_currentUser.Id);
                foreach (var c in _complaints)
                    _engine.Ingest(c);   // hydrate in-memory structures
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not load complaints: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>Handles the submit complaint button click.</summary>
        private async Task SubmitComplaintAsync(
            TextBox txtTitle, ComboBox cmbCat, TextBox txtRoom,
            TextBox txtDesc, Button btnSubmit)
        {
            string title    = txtTitle.Text.Trim();
            string category = cmbCat.SelectedItem?.ToString() ?? "";
            string room     = txtRoom.Text.Trim();
            string desc     = txtDesc.Text.Trim();

            if (string.IsNullOrWhiteSpace(title))
            {
                MessageBox.Show("Please enter a complaint title.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnSubmit.Enabled = false;
            btnSubmit.Text    = "Submitting…";

            try
            {
                var complaint = new Complaint
                {
                    ComplaintNumber = await Task.Run(() => _db.GenerateComplaintNumber()),
                    Title           = title,
                    Description     = desc,
                    Category        = category,
                    HostelName      = _currentUser.HostelName,
                    FloorName       = "",
                    RoomNumber      = string.IsNullOrWhiteSpace(room) ? _currentUser.RoomNumber : room,
                    Urgency         = UrgencyLevel.Medium,
                    Status          = ComplaintStatus.Pending,
                    StudentId       = _currentUser.Id,
                    StudentName     = _currentUser.FullName,
                    CreatedAt       = DateTime.UtcNow
                };

                // Engine checks for duplicates
                var (isNew, result) = _engine.Ingest(complaint);

                if (!isNew)
                {
                    await Task.Run(() => _db.IncrementDuplicateCount(result.Id));
                    MessageBox.Show(
                        $"A similar complaint already exists ({result.ComplaintNumber}).\n" +
                        $"Your report has been merged and the priority has been increased.\n" +
                        $"Duplicate count: {result.DuplicateCount}",
                        "Duplicate Detected",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    int newId = await Task.Run(() => _db.CreateComplaint(complaint));
                    complaint.Id = newId;
                    _complaints.Insert(0, complaint);

                    MessageBox.Show(
                        $"Complaint submitted successfully!\nComplaint ID: {complaint.ComplaintNumber}",
                        "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                // Clear form
                txtTitle.Text = "";
                txtDesc.Text  = "";
                txtRoom.Text  = _currentUser.RoomNumber;

                // Refresh stats
                RefreshStats();
                ShowDashboard();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Submission failed: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnSubmit.Enabled = true;
                btnSubmit.Text    = "＋  Submit Complaint";
            }
        }

        private void RefreshStats()
        {
            if (_statTotal == null) return;

            int total      = _complaints.Count;
            int pending    = _complaints.FindAll(c => c.Status == ComplaintStatus.Pending).Count;
            int inProgress = _complaints.FindAll(c => c.Status == ComplaintStatus.InProgress).Count;
            int resolved   = _complaints.FindAll(c => c.Status == ComplaintStatus.Resolved).Count;

            if (InvokeRequired)
            {
                Invoke(() => UpdateStatLabels(total, pending, inProgress, resolved));
            }
            else
            {
                UpdateStatLabels(total, pending, inProgress, resolved);
            }
        }

        private void UpdateStatLabels(int total, int pending, int inProgress, int resolved)
        {
            _statTotal.Text      = total.ToString();
            _statPending.Text    = pending.ToString();
            _statInProgress.Text = inProgress.ToString();
            _statResolved.Text   = resolved.ToString();
        }

        private void PopulateGrid(DataGridView grid)
        {
            grid.Rows.Clear();
            // Show 4 most recent
            var recent = _complaints.Count > 4 ? _complaints.GetRange(0, 4) : _complaints;
            foreach (var c in recent)
            {
                grid.Rows.Add(
                    c.ComplaintNumber,
                    c.Title,
                    c.Category,
                    c.CreatedAt.ToString("dd MMM yyyy"),
                    AppTheme.StatusLabel(c.Status));
            }
        }

        // =================================================================
        // HELPERS / BUILDERS
        // =================================================================

        private static Panel PageHeading(string title, string subtitle, string dateStr)
        {
            var panel = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 80,
                BackColor = Color.Transparent
            };

            var layout = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 2,
                RowCount    = 2,
                BackColor   = Color.Transparent
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35f));

            layout.Controls.Add(new Label { Text = title, Font = AppTheme.FontHeading1, ForeColor = AppTheme.TextDark, AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft }, 0, 0);

            if (!string.IsNullOrEmpty(dateStr))
                layout.Controls.Add(new Label { Text = $"📅  {dateStr}", Font = AppTheme.FontBody, ForeColor = AppTheme.TextMedium, AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomRight }, 1, 0);

            layout.Controls.Add(new Label { Text = subtitle, Font = AppTheme.FontBody, ForeColor = AppTheme.TextMedium, AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopLeft }, 0, 1);

            panel.Controls.Add(layout);
            return panel;
        }

        private static Panel Spacer(int height) => new()
        {
            Dock      = DockStyle.Top,
            Height    = height,
            BackColor = Color.Transparent
        };

        private static Label FieldLbl(string text) => new()
        {
            Text      = text,
            Font      = AppTheme.FontLabel,
            ForeColor = AppTheme.TextMedium,
            AutoSize  = false,
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft
        };

        private static TextBox Input(string placeholder) => new()
        {
            Dock            = DockStyle.Fill,
            Font            = AppTheme.FontBody,
            BackColor       = AppTheme.Background,
            ForeColor       = AppTheme.TextDark,
            BorderStyle     = BorderStyle.FixedSingle,
            PlaceholderText = placeholder
        };

        private static void PaintCardShadow(object? sender, PaintEventArgs e)
        {
            if (sender is not Panel p) return;
            using var pen = new Pen(AppTheme.Border, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
        }

        // -----------------------------------------------------------------
        // Form close — stop the TCP server cleanly
        // -----------------------------------------------------------------
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _server.Stop();
            _server.Dispose();
            base.OnFormClosed(e);
        }
    }
}
