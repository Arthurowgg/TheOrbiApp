using System.IO;
using System.Runtime.InteropServices;
using static Orbi.Setup.Win32;

namespace Orbi.Setup;

/// <summary>
/// Entrada do instalador: Win32 puro, NativeAOT. Não depende de WPF nem de
/// WinForms, então o .exe tem poucos MB e abre mesmo em PC sem .NET.
/// </summary>
internal static class App
{
    /// <summary>Log da instalação sem interface (usado pelo CI e por suporte).</summary>
    public static string SilentLog => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Orbi-setup.log");

    private static SetupWindow? _window;
    private static WndProc? _proc;

    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--silent")) return ModoSilencioso(args).GetAwaiter().GetResult();

        try
        {
            RunWindow();
            return 0;
        }
        catch (Exception error)
        {
            MessageBoxW(IntPtr.Zero, "O instalador não conseguiu abrir: " + error.Message,
                "Orbi — erro", MB_OK | MB_ICONERROR);
            return 1;
        }
    }

    /* ------------------------------ janela ------------------------------ */

    private static void RunWindow()
    {
        var instance = GetModuleHandleW(null);
        _proc = WindowProc;

        var wc = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            style = CS_HREDRAW | CS_VREDRAW,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_proc),
            hInstance = instance,
            hCursor = LoadCursorW(IntPtr.Zero, (IntPtr)IDC_ARROW),
            hbrBackground = (IntPtr)(COLOR_WINDOW + 1),
            lpszClassName = "OrbiSetup",
        };
        RegisterClassExW(ref wc);

        _window = new SetupWindow();
        Installer.Sink = line => _window?.Advance(line);
        _window.Show();

        while (GetMessageW(out var msg, IntPtr.Zero, 0, 0))
        {
            DispatchMessageW(ref msg);
        }

        _window.Dispose();
        _window = null;
    }

    private static IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_COMMAND:
                var id = (int)(wParam.ToInt64() & 0xFFFF);
                _window?.HandleCommand(id);
                return IntPtr.Zero;

            case WM_PAINT:
                var dc = GetDC(hWnd);
                _window?.Paint(dc);
                ReleaseDC(hWnd, dc);
                // deixa o DefWindowProc terminar o BeginPaint/EndPaint
                return DefWindowProcW(hWnd, msg, wParam, lParam);

            case WM_CLOSE:
                if (_window is not null) _window.HandleCommand(1002);
                return IntPtr.Zero;

            case WM_DESTROY:
                PostQuitMessage(0);
                return IntPtr.Zero;
        }
        return DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    /* ------------------------------ silencioso ------------------------------ */

    private static async Task<int> ModoSilencioso(string[] args)
    {
        try { File.Delete(SilentLog); } catch { }

        void Anotar(string linha)
        {
            try { File.AppendAllText(SilentLog, linha + Environment.NewLine); } catch { }
        }

        try
        {
            Anotar($"Orbi setup {Installer.Versao} · {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Installer.Sink = Anotar;

            if (args.Contains("--uninstall"))
            {
                Installer.Uninstall();
                Anotar("desinstalado de " + Paths.Target);
            }
            else
            {
                await Installer.RunAsync(new Progress<double>(_ => { }), CancellationToken.None);
                Anotar("instalado em " + Paths.Target);
            }

            Anotar("ok");
            return 0;
        }
        catch (Exception error)
        {
            Anotar("falhou: " + error);
            return 1;
        }
    }
}
