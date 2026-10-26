namespace Compactador;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        if (args.Length == 2 && args[0] == "--self-test") { SelfTest.Run(args[1]).GetAwaiter().GetResult(); return; }
        using var instance = new Mutex(true, "Local\\CompactadorFilmes.SingleInstance", out var first);
        if (!first) { MessageBox.Show("O Compactador de Filmes já está aberto."); return; }
        ApplicationConfiguration.Initialize();
        Application.Run(new MainWindow());
    }
}
