using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Orbi.Setup;

/// <summary>
/// Entrada do instalador. Sem App.xaml, sem StartupUri: qualquer falha de
/// carregamento aparece numa janela crua em vez de "nada acontece".
/// </summary>
public class App : Application
{
    public static readonly Color Ink = Color.FromRgb(0x14, 0x10, 0x0C);
    public static readonly Color Paper = Color.FromRgb(0xF2, 0xED, 0xE3);
    public static readonly Color Signal = Color.FromRgb(0xC7, 0x45, 0x2A);
    public static readonly Color Warm = Color.FromRgb(0x6A, 0x61, 0x52);

    public Window? Janela;
    public TextBlock? LogBox;

    public void AppendLog(string line)
    {
        if (LogBox is null) return;
        LogBox.Text += line + Environment.NewLine;
    }

    /// <summary>Log da instalação sem interface (usado pelo CI e por suporte).</summary>
    public static string SilentLog => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Orbi-setup.log");

    [STAThread]
    public static void Main(string[] args)
    {
        var app = new App();

        if (args.Contains("--silent"))
        {
            app.Startup += async (_, _) => await ModoSilencioso(args, app);
            app.Run();
            return;
        }

        app.Run(new SetupWindow(args));
    }

    /// <summary>
    /// Instala (ou desinstala) sem mostrar nada. Exit code 0 = deu certo.
    /// Sem isso, a única forma de conferir o instalador seria abrir uma janela.
    /// </summary>
    private static async Task ModoSilencioso(string[] args, Application app)
    {
        var log = new List<string>();
        void Anotar(string linha)
        {
            log.Add(linha);
            try { File.AppendAllText(SilentLog, linha + Environment.NewLine); } catch { }
        }

        try
        {
            try { File.Delete(SilentLog); } catch { }
            Anotar($"Orbi setup {Installer.Versao} · {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            if (args.Contains("--uninstall"))
            {
                Installer.Uninstall();
                Anotar($"desinstalado de {Paths.Target}");
            }
            else
            {
                await Installer.RunAsync(new Progress<double>(_ => { }), CancellationToken.None);
                Anotar($"instalado em {Paths.Target}");
            }

            foreach (var linha in Installer.Log) Anotar("  " + linha);
            Anotar("ok");
            Environment.ExitCode = 0;
        }
        catch (Exception ex)
        {
            Anotar("falhou: " + ex);
            Environment.ExitCode = 1;
        }

        app.Shutdown();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.ToString(), "Orbi — erro no instalador");
            args.Handled = true;
        };
        base.OnStartup(e);
    }
}
