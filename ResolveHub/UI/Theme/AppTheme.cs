// =============================================================================
// AppTheme.cs — Centralised UI Theme / Design System
// All colours, fonts, and spacing live here — never repeated in form code.
// =============================================================================

using System.Drawing;
using System.Windows.Forms;

namespace ResolveHub.UI.Theme
{
    /// <summary>
    /// The single source of truth for all visual design tokens.
    ///
    /// Decision: A static theme class was chosen over resource dictionaries
    /// (which belong to WPF) because WinForms resolves styles at code-level.
    /// Centralising values here means a brand change touches one file only.
    ///
    /// Font pair: Inter (headings) + Inter (body) — single family, different weights.
    /// Icon set:  Unicode emoji/symbols (no external icon library dependency).
    /// </summary>
    public static class AppTheme
    {
        // -----------------------------------------------------------------
        // Colour palette (from AppSettings brand colours)
        // -----------------------------------------------------------------
        public static readonly Color Primary      = ColorTranslator.FromHtml("#1A7F6E");
        public static readonly Color PrimaryDark  = ColorTranslator.FromHtml("#0D5C4E");
        public static readonly Color Secondary    = ColorTranslator.FromHtml("#0D2B27");
        public static readonly Color Background   = ColorTranslator.FromHtml("#F5F7F6");
        public static readonly Color Surface      = Color.White;
        public static readonly Color TextDark     = ColorTranslator.FromHtml("#1C2B29");
        public static readonly Color TextMedium   = ColorTranslator.FromHtml("#3D5550");
        public static readonly Color TextLight    = ColorTranslator.FromHtml("#6B8A84");
        public static readonly Color Border       = ColorTranslator.FromHtml("#DDE8E5");
        public static readonly Color Success      = ColorTranslator.FromHtml("#27AE85");
        public static readonly Color Warning      = ColorTranslator.FromHtml("#E8A020");
        public static readonly Color Danger       = ColorTranslator.FromHtml("#D94040");
        public static readonly Color Pending      = ColorTranslator.FromHtml("#E8A020");
        public static readonly Color InProgress   = ColorTranslator.FromHtml("#1A7F6E");
        public static readonly Color Resolved     = ColorTranslator.FromHtml("#27AE85");

        // -----------------------------------------------------------------
        // Typography — single font family, two sizes
        // -----------------------------------------------------------------
        public static readonly Font FontHeading1  = new("Segoe UI", 22f, FontStyle.Bold);
        public static readonly Font FontHeading2  = new("Segoe UI", 16f, FontStyle.Bold);
        public static readonly Font FontHeading3  = new("Segoe UI", 13f, FontStyle.Bold);
        public static readonly Font FontBody      = new("Segoe UI",  9.5f, FontStyle.Regular);
        public static readonly Font FontBodySmall = new("Segoe UI",  8.5f, FontStyle.Regular);
        public static readonly Font FontLabel     = new("Segoe UI",  9f,  FontStyle.Regular);
        public static readonly Font FontButton    = new("Segoe UI", 10f, FontStyle.Bold);
        public static readonly Font FontCaption   = new("Segoe UI",  8f,  FontStyle.Regular);

        // -----------------------------------------------------------------
        // Spacing / sizing constants (8-pt grid)
        // -----------------------------------------------------------------
        public const int SpaceXS  =  4;
        public const int SpaceSM  =  8;
        public const int SpaceMD  = 16;
        public const int SpaceLG  = 24;
        public const int SpaceXL  = 32;
        public const int SpaceXXL = 48;

        public const int CornerRadius  = 8;
        public const int InputHeight   = 38;
        public const int ButtonHeight  = 44;
        public const int SidebarWidth  = 220;

        // -----------------------------------------------------------------
        // Factory methods — creates consistently styled controls
        // -----------------------------------------------------------------

        /// <summary>Creates a primary action button (teal, full-width feel).</summary>
        public static Button PrimaryButton(string text)
        {
            var btn = new Button
            {
                Text            = text,
                BackColor       = Primary,
                ForeColor       = Color.White,
                FlatStyle       = FlatStyle.Flat,
                Font            = FontButton,
                Height          = ButtonHeight,
                Cursor          = Cursors.Hand,
                TextAlign       = ContentAlignment.MiddleCenter,
                UseVisualStyleBackColor = false
            };
            btn.FlatAppearance.BorderSize  = 0;
            btn.FlatAppearance.MouseOverBackColor  = PrimaryDark;
            btn.FlatAppearance.MouseDownBackColor  = Secondary;
            return btn;
        }

        /// <summary>Creates a ghost / outlined button.</summary>
        public static Button GhostButton(string text)
        {
            var btn = new Button
            {
                Text      = text,
                BackColor = Color.Transparent,
                ForeColor = Primary,
                FlatStyle = FlatStyle.Flat,
                Font      = FontButton,
                Height    = ButtonHeight,
                Cursor    = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Primary;
            btn.FlatAppearance.BorderSize  = 1;
            return btn;
        }

        /// <summary>Creates a single-line text input with consistent styling.</summary>
        public static TextBox TextInput(string placeholder = "")
        {
            var tb = new TextBox
            {
                Font        = FontBody,
                BackColor   = Surface,
                ForeColor   = TextDark,
                BorderStyle = BorderStyle.FixedSingle,
                Height      = InputHeight
            };
            return tb;
        }

        /// <summary>Creates a styled ComboBox.</summary>
        public static ComboBox ComboInput()
        {
            var cb = new ComboBox
            {
                Font        = FontBody,
                BackColor   = Surface,
                ForeColor   = TextDark,
                FlatStyle   = FlatStyle.Flat,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            return cb;
        }

        /// <summary>Creates a section header label.</summary>
        public static Label SectionLabel(string text)
        {
            return new Label
            {
                Text      = text,
                Font      = FontHeading3,
                ForeColor = TextDark,
                AutoSize  = true
            };
        }

        /// <summary>Creates a field caption label.</summary>
        public static Label FieldLabel(string text)
        {
            return new Label
            {
                Text      = text,
                Font      = FontLabel,
                ForeColor = TextMedium,
                AutoSize  = true
            };
        }

        /// <summary>Returns the colour associated with a complaint status.</summary>
        public static Color StatusColor(ResolveHub.Core.Models.ComplaintStatus status) =>
            status switch
            {
                ResolveHub.Core.Models.ComplaintStatus.Pending    => Pending,
                ResolveHub.Core.Models.ComplaintStatus.InProgress => InProgress,
                ResolveHub.Core.Models.ComplaintStatus.Resolved   => Resolved,
                _                                                  => TextLight
            };

        /// <summary>Returns the display label for a complaint status.</summary>
        public static string StatusLabel(ResolveHub.Core.Models.ComplaintStatus status) =>
            status switch
            {
                ResolveHub.Core.Models.ComplaintStatus.Pending    => "Pending",
                ResolveHub.Core.Models.ComplaintStatus.InProgress => "In Progress",
                ResolveHub.Core.Models.ComplaintStatus.Resolved   => "Resolved",
                ResolveHub.Core.Models.ComplaintStatus.Rejected   => "Rejected",
                _                                                  => "Unknown"
            };
    }
}
