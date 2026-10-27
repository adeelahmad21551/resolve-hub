// =============================================================================
// Program.cs — Application Entry Point
// Bootstraps services and launches the Login form.
// =============================================================================

using ResolveHub.Data;
using ResolveHub.UI.Forms;

namespace ResolveHub;

/// <summary>
/// Application entry point.
/// All services that need to live for the entire application lifetime are
/// created here and passed down via constructor injection (DI without a container).
/// </summary>
static class Program
{
    /// <summary>Main entry point — STA thread required for WinForms.</summary>
    [STAThread]
    static void Main()
    {
        // Enable high-DPI awareness and modern visual styles
        ApplicationConfiguration.Initialize();

        // Create long-lived services (manual DI)
        using var db = new DatabaseService();

        // Show the login form; the dashboard will be launched from inside it.
        Application.Run(new LoginForm(db));
    }
}