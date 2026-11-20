namespace GameLocker.ConfigUI;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        using var icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        using var window = new Form1 { Icon = icon };
        Application.Run(window);
    }    
}
