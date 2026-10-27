// =============================================================================
// AppSettings.cs — Centralised Configuration (No Hard-Coded Secrets)
// =============================================================================

using System;
using System.IO;

namespace ResolveHub.Config
{
    /// <summary>
    /// Provides application-wide configuration values.
    ///
    /// Decision: Configuration is kept in a dedicated class rather than scattered
    /// literals so that every setting has a single point of change and can later
    /// be migrated to appsettings.json / environment variables without touching
    /// business logic.
    ///
    /// Security note: No API keys or passwords are stored here.
    /// The database path is derived at runtime from the user's AppData folder —
    /// it is never committed to version control.
    /// </summary>
    public static class AppSettings
    {
        // -----------------------------------------------------------------
        // Application identity
        // -----------------------------------------------------------------
        public const string AppName    = "Resolve Hub";
        public const string AppVersion = "1.0.0";

        // -----------------------------------------------------------------
        // Brand colours — single source of truth for the UI theme
        // -----------------------------------------------------------------
        public const string PrimaryColorHex   = "#1A7F6E";   // Deep teal (from logo)
        public const string SecondaryColorHex = "#0D2B27";   // Dark green-black
        public const string BackgroundHex     = "#F7F9F8";   // Off-white
        public const string SurfaceHex        = "#FFFFFF";
        public const string TextDarkHex       = "#1C2B29";
        public const string TextMediumHex     = "#3D5550";
        public const string AccentGreenHex    = "#27AE85";

        // -----------------------------------------------------------------
        // Database — path is outside the project directory so it is never
        // accidentally included in a git commit.
        // -----------------------------------------------------------------
        public static string DatabasePath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ResolveHub",
                "resolvehub.db");

        // -----------------------------------------------------------------
        // Uploads directory — sibling of the executable, never served publicly
        // -----------------------------------------------------------------
        public static string UploadsDirectory =>
            Path.Combine(AppContext.BaseDirectory, "Uploads");

        // -----------------------------------------------------------------
        // TCP socket settings (loopback only — not accessible externally)
        // -----------------------------------------------------------------
        public const string TcpHost    = "127.0.0.1";
        public const int    TcpPort    = 5000;
        public const int    TcpTimeout = 5_000;   // milliseconds

        // -----------------------------------------------------------------
        // Complaint numbering
        // -----------------------------------------------------------------
        public const int    ComplaintNumberBase = 1000;
    }
}
