namespace PdfCompressor;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // Initialize NLog logging system
        Logger.Initialize();

        try
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
        finally
        {
            // Ensure all log messages are written and shutdown properly
            Logger.Flush();
            Logger.Shutdown();
        }
    }
}