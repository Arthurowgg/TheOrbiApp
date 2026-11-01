using System.IO;
using System.Text;
using System.Windows;

namespace Orbi;

/// <summary>
/// Rede de segurança: qualquer exceção não tratada vira uma janela legível e uma
/// linha em %APPDATA%\Orbi\crash.log (com dica do que fazer).
///
/// Antes disso, um erro na inicialização fechava o app em silêncio: o usuário via
/// "nada acontece". Agora ele vê o motivo e pode copiar o texto.
/// </summary>
public static class Diagnostics
{
    private static bool _shown;

    public static string LogPath => Path.Combine(Store.Root, "crash.log");

    public static void Install()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Report(args.ExceptionObject as Exception, "AppDomain", fatal: true);

        System.Windows.Threading.Dispatcher.CurrentDispatcher.UnhandledException += (_, args) =>
        {
            Report(args.Exception, "Dispatcher", fatal: false);
            args.Handled = true;
        };

        // Exceções de tarefas que ninguém observa (HTTP, sync, etc.).
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Report(args.Exception, "Task", fatal: false);
            args.SetObserved();
        };
    }

    public static void Report(Exception? error, string source, bool fatal)
    {
        var text = new StringBuilder()
            .AppendLine($"--- {DateTime.Now:yyyy-MM-dd HH:mm:ss} · {source} · versão {typeof(Diagnostics).Assembly.GetName().Version}")
            .AppendLine(error?.ToString() ?? "(sem exceção)")
            .ToString();

        try
        {
            Directory.CreateDirectory(Store.Root);
            File.AppendAllText(LogPath, text);
        }
        catch
        {
            // sem log, mas a janela ainda aparece
        }

        if (_shown) return;
        _shown = true;

        try
        {
            var message =
                "O Orbi encontrou um problema.\n\n" +
                (error is null ? "" : $"{error.GetType().Name}: {error.Message}\n\n") +
                $"Detalhes salvos em:\n{LogPath}\n\n" +
                "O app já tentou continuar funcionando. Se ele não abrir, " +
                "envie esse arquivo para o suporte.";

            if (fatal)
            {
                System.Windows.MessageBox.Show(message, "Orbi — problema", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                // Erros não fatais só vão para o log; a barra continua utilizável.
                System.Diagnostics.Debug.WriteLine(message);
            }
        }
        catch
        {
            // último recurso
        }
    }
}
