using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Orbi.Services;

namespace Orbi;

/// <summary>
/// Autoteste que prova que a interface FUNCIONA, sem depender de olhar a tela.
///
/// Roda com: Orbi.exe --selftest   (ou pelo atalho "Orbi (autoteste)")
///
/// Cria cada janela de verdade, força o layout, mede o tamanho e escreve o
/// resultado em %APPDATA%\Orbi\selftest.txt. Se uma janela falhar, o erro
/// aparece na íntegra — é o diagnóstico que faltava.
/// </summary>
public static class Smoke
{
    public static string ReportPath => Path.Combine(Store.Root, "selftest.txt");

    public static void Run(Func<Window?> showBar)
    {
        var log = new StringBuilder();
        void Line(string text)
        {
            log.AppendLine(text);
            ConsoleLog.Step("[selftest] " + text);
        }

        Line($"Orbi — autoteste · {typeof(Smoke).Assembly.GetName().Version}");
        Line($"Windows: {Environment.OSVersion.VersionString} · .NET {Environment.Version}");
        Line($"Pasta: {Store.Root}");
        Line("");

        try
        {
            var settings = Store.Current;
            Line($"Dados locais: ok (nome \"{settings.UserName}\", {Memory.All.Count} registros de memória, {settings.Reminders.Count} lembretes)");
        }
        catch (Exception error)
        {
            Line($"Dados locais: FALHOU — {error.GetType().Name}: {error.Message}");
        }

        TestWindow("Barra de comando", () =>
        {
            var bar = showBar();
            return bar;
        }, Line);

        TestWindow("Ajustes", () => new SettingsWindow(), Line);
        TestWindow("Diagnóstico", () => new DiagnosticoWindow(), Line);
        TestWindow("Login", () => new LoginWindow(), Line);

        // ícone da bandeja
        try
        {
            var icon = TrayIcon.Load();
            Line($"Ícone da bandeja: ok ({icon.Width}×{icon.Height})");
        }
        catch (Exception error)
        {
            Line($"Ícone da bandeja: FALHOU — {error.GetType().Name}: {error.Message}");
        }

        Line($"Atalho global: {(string.IsNullOrEmpty(HotKey.Combination) ? "NÃO registrado" : HotKey.Combination)}");
        Line("");
        Line("RESULTADO: " + (log.ToString().Contains("FALHOU") ? "há falhas (veja acima)" : "tudo abriu"));

        try
        {
            Directory.CreateDirectory(Store.Root);
            File.WriteAllText(ReportPath, log.ToString(), Encoding.UTF8);
        }
        catch
        {
            // sem relatório: o console ainda tem tudo
        }
    }

    private static void TestWindow(string name, Func<Window?> factory, Action<string> line)
    {
        Window? window = null;
        try
        {
            window = factory();
            if (window is null)
            {
                line($"{name}: FALHOU — o app devolveu null");
                return;
            }

            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = -20000;   // fora da tela: não atrapalha o teste
            window.Top = -20000;
            window.ShowInTaskbar = false;
            window.Show();
            window.UpdateLayout();

            // Força a renderização da árvore para pegar exceções de layout.
            if (window.Content is FrameworkElement root)
            {
                root.Measure(new Size(window.Width, window.Height));
                root.Arrange(new Rect(0, 0, window.Width, window.Height));
                root.UpdateLayout();
            }

            var size = window.ActualWidth > 0 && window.ActualHeight > 0
                ? $"{window.ActualWidth:0}×{window.ActualHeight:0}"
                : "SEM TAMANHO";

            var texto = VisualTreeHelper.GetChildrenCount(window) > 0 ? "renderizada" : "vazia";
            line($"{name}: ok — {size}, {texto}");
        }
        catch (Exception error)
        {
            var inner = error;
            while (inner.InnerException is not null) inner = inner.InnerException;
            line($"{name}: FALHOU — {error.GetType().Name}: {error.Message}");
            if (!ReferenceEquals(inner, error)) line($"   causa raiz: {inner.GetType().Name}: {inner.Message}");
        }
        finally
        {
            try { window?.Hide(); } catch { /* ignore */ }
        }
    }
}
