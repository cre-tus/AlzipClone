using AlzipClone.Core;

namespace AlzipClone;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        EncodingHelper.EnsureRegistered();
        string? openPath = args.FirstOrDefault(File.Exists);
        Application.Run(new MainForm(openPath));
    }
}
