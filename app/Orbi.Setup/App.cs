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

        try
        {
            app.Run(new SetupWindow(args));
        }
        catch (Exception error)
        {
            Note("falha ao abrir a janela: " + error);
            MessageBox.Show("O instalador não conseguiu abrir: " + error.Message,
                "Orbi — instalador", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Registra o que aconteceu em %LOCALAPPDATA%\Orbi-setup.log. É o que permite
    /// descobrir depois por que uma janela abriu em branco em algum PC.
    /// </summary>
    public static void Note(string line)
    {
        try { File.AppendAllText(SilentLog, $"{DateTime.Now:HH:mm:ss} {line}{Environment.NewLine}"); }
        catch { }
    }

    /// <summary>Valor de uma opção: --payload=caminho ou --payload caminho.</summary>
    private static string? Flag(string[] args, string name)
    {
        foreach (var arg in args)
        {
            if (arg.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
            {
                return arg[(name.Length + 1)..].Trim('"');
            }
        }

        var index = Array.FindIndex(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
        if (index < 0 || index + 1 >= args.Length) return null;
        return args[index + 1].Trim('"');
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
            Anotar("argumentos: " + string.Join(" ", args));

            // Pacote local (CI e instalação sem internet): nada é baixado.
            var local = Flag(args, "--payload") ?? Environment.GetEnvironmentVariable("ORBI_PAYLOAD");
            if (!string.IsNullOrWhiteSpace(local))
            {
                if (File.Exists(local)) Payload.UseLocal(local!);
                else Anotar("pacote local não encontrado: " + local);
            }

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
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Note("erro não tratado: " + args.ExceptionObject);

        DispatcherUnhandledException += (_, args) =>
        {
            Note("erro na interface: " + args.Exception);
            MessageBox.Show(args.Exception.ToString(), "Orbi — erro no instalador");
            args.Handled = true;
        };
        base.OnStartup(e);
    }
}
