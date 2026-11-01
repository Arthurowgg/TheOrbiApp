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

    [STAThread]
    public static void Main(string[] args)
    {
        var app = new App();
        app.Run(new SetupWindow(args));
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
