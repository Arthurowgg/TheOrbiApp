using System.IO;
using System.Text;
using System.Windows;
using Orbi.Services;

namespace Orbi;

/// <summary>
/// Relatório que responde "por que não abre?" sem precisar de depurador:
/// versão, telas, DPI, bandeja, atalho, se a barra foi criada, estado da conta
/// e as últimas linhas do crash.log.
/// </summary>
public partial class DiagnosticoWindow : Window
{
    public DiagnosticoWindow()
    {
        InitializeComponent();
        Reload();
    }

    private void Reload()
    {
        var report = new StringBuilder();

        void Line(string label, string value) =>
            report.AppendLine($"{label,-22}{value}");

        Line("Versão do app", typeof(DiagnosticoWindow).Assembly.GetName().Version?.ToString() ?? "?");
        Line("Windows", Environment.OSVersion.VersionString);
        Line("64 bits", Environment.Is64BitProcess ? "sim" : "não");
        Line(".NET", Environment.Version.ToString());
        Line("Pasta de dados", Store.Root);
        Line("Arquivo de dados", File.Exists(Path.Combine(Store.Root, "orbi.json")) ? "existe" : "ainda não criado");
        Line("Chave do Gemini", Store.HasApiKey() ? "configurada" : "nenhuma (modo local)");

        report.AppendLine();

        var screens = System.Windows.Forms.Screen.AllScreens;
        Line("Telas", screens.Length.ToString());
        foreach (var screen in screens)
        {
            var dpi = screen.Bounds;
            Line("  · " + (screen.Primary ? "principal" : "secundária"),
                $"{dpi.Width}×{dpi.Height} px · área útil {screen.WorkingArea.Width}×{screen.WorkingArea.Height}");
        }

        report.AppendLine();

        var app = System.Windows.Application.Current as App;
        Line("Barra criada", app?.BarCreated == true ? "sim" : "não (ou falhou)");
        Line("Atalho global", string.IsNullOrEmpty(HotKey.Combination) ? "não registrado" : HotKey.Combination);
        Line("Bandeja", app?.TrayVisible == true ? "visível" : "não criada");
        Line("Conta", Firebase.SignedIn ? Firebase.Email ?? "(conectada)" : "sem conta — funciona local");

        report.AppendLine();
        report.AppendLine("Últimos erros (crash.log)");
        report.AppendLine(new string('-', 46));
        try
        {
            if (File.Exists(Diagnostics.LogPath))
            {
                var lines = File.ReadAllLines(Diagnostics.LogPath);
                foreach (var line in lines.TakeLast(25)) report.AppendLine(line);
            }
            else
            {
                report.AppendLine("nenhum erro registrado — o app não chegou a falhar.");
            }
        }
        catch (Exception error)
        {
            report.AppendLine("não consegui ler o log: " + error.Message);
        }

        Report.Text = report.ToString();
    }

    private void Reload_Click(object sender, RoutedEventArgs e) => Reload();

    private void OpenBar_Click(object sender, RoutedEventArgs e)
    {
        (System.Windows.Application.Current as App)?.ShowBar();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Windows.Clipboard.SetText(Report.Text);
            MessageBox.Show("Relatório copiado. Cole na conversa com o suporte.", "Orbi");
        }
        catch
        {
            MessageBox.Show("Não consegui copiar. Selecione o texto e use Ctrl+C.", "Orbi");
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
