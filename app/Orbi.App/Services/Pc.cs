using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Orbi.Services;

/// <summary>
/// O que o Orbi precisa para "usar o PC como você": ver a tela, mover o mouse,
/// clicar em um ponto, digitar, apertar teclas e conversar com as janelas.
/// Tudo por Win32, sem instalar nada.
/// </summary>
public static class Pc
{
    /* ------------------------------ mouse ------------------------------ */

    public const uint MOUSEEVENTF_MOVE = 0x0001;
    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;
    public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    public const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    public const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    public const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    public const uint MOUSEEVENTF_WHEEL = 0x0800;
    public const uint MOUSEEVENTF_HWHEEL = 0x1000;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const uint KEYEVENTF_UNICODE = 0x0004;

    public const int SM_XVIRTUALSCREEN = 76;
    public const int SM_YVIRTUALSCREEN = 77;
    public const int SM_CXVIRTUALSCREEN = 78;
    public const int SM_CYVIRTUALSCREEN = 79;

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    public static extern void mouse_event(uint flags, int dx, int dy, int data, UIntPtr extra);

    [DllImport("user32.dll")]
    public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr window, int command);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowTextW(IntPtr window, StringBuilder text, int max);

    [DllImport("user32.dll")]
    public static extern int GetWindowTextLengthW(IntPtr window);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumProc callback, IntPtr extra);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr window, out RECT rect);

    [DllImport("user32.dll")]
    public static extern bool IsZoomed(IntPtr window);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr window);

    [DllImport("user32.dll")]
    public static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindowW(string? className, string? title);

    public delegate bool EnumProc(IntPtr window, IntPtr extra);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    /// <summary>Lê "640,400" / "640 400" / "x=640 y=400" e devolve a posição.</summary>
    public static (int X, int Y)? ParsePoint(string text)
    {
        var numeros = System.Text.RegularExpressions.Regex
            .Matches(text, @"-?\d{1,5}")
            .Select(m => int.Parse(m.Value)).ToArray();
        return numeros.Length >= 2 ? (numeros[0], numeros[1]) : null;
    }

    /* ------------------------------ atalhos ------------------------------ */

    public static void Move(int x, int y) => SetCursorPos(x, y);

    public static void Click(int x, int y, string button = "left", int times = 1)
    {
        SetCursorPos(x, y);
        Thread.Sleep(60);
        var (down, up) = button.ToLowerInvariant() switch
        {
            "right" => (MOUSEEVENTF_RIGHTDOWN, MOUSEEVENTF_RIGHTUP),
            "middle" or "meio" => (MOUSEEVENTF_MIDDLEDOWN, MOUSEEVENTF_MIDDLEUP),
            _ => (MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP),
        };
        for (var i = 0; i < Math.Clamp(times, 1, 3); i++)
        {
            mouse_event(down, 0, 0, 0, UIntPtr.Zero);
            mouse_event(up, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(80);
        }
    }

    public static void DoubleClick(int x, int y) => Click(x, y, "left", 2);

    public static void Scroll(int clicks)
    {
        mouse_event(MOUSEEVENTF_WHEEL, 0, 0, clicks * 120, UIntPtr.Zero);
    }

    /// <summary>Escreve texto como se fosse o teclado (acentos inclusos).</summary>
    public static void Type(string text)
    {
        foreach (var ch in text)
        {
            if (ch == '\n')
            {
                Press(0x0D);
                continue;
            }
            keybd_event(0, 0, KEYEVENTF_UNICODE, (UIntPtr)ch);
            keybd_event(0, 0, KEYEVENTF_UNICODE | KEYEVENTF_KEYUP, (UIntPtr)ch);
            Thread.Sleep(12);
        }
    }

    public static void Press(int virtualKey)
    {
        keybd_event((byte)virtualKey, 0, 0, UIntPtr.Zero);
        keybd_event((byte)virtualKey, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    /// <summary>Atalho: "ctrl+shift+esc", "alt+f4", "win+r".</summary>
    public static void Hotkey(string combo)
    {
        var keys = combo.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var vks = keys.Select(ToVirtualKey).Where(vk => vk > 0).ToArray();
        foreach (var vk in vks) keybd_event((byte)vk, 0, 0, UIntPtr.Zero);
        for (var i = vks.Length - 1; i >= 0; i--) keybd_event((byte)vks[i], 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    /// <summary>Tecla Windows + tecla.</summary>
    public static void WinKey(int virtualKey)
    {
        keybd_event(0x5B, 0, 0, UIntPtr.Zero);
        keybd_event((byte)virtualKey, 0, 0, UIntPtr.Zero);
        keybd_event((byte)virtualKey, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event(0x5B, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    public static int ToVirtualKey(string name) => name.ToLowerInvariant() switch
    {
        "ctrl" or "control" => 0x11,
        "shift" => 0x10,
        "alt" => 0x12,
        "win" or "windows" => 0x5B,
        "enter" => 0x0D,
        "esc" or "escape" => 0x1B,
        "tab" => 0x09,
        "space" => 0x20,
        "back" or "backspace" => 0x08,
        "del" or "delete" => 0x2E,
        "home" => 0x24,
        "end" => 0x23,
        "up" => 0x26,
        "down" => 0x28,
        "left" => 0x25,
        "right" => 0x27,
        "pageup" or "pgup" => 0x21,
        "pagedown" or "pgdn" => 0x22,
        "print" or "printscreen" => 0x2C,
        "insert" => 0x2D,
        "f1" => 0x70, "f2" => 0x71, "f3" => 0x72, "f4" => 0x73, "f5" => 0x74,
        "f6" => 0x75, "f7" => 0x76, "f8" => 0x77, "f9" => 0x78, "f10" => 0x79,
        "f11" => 0x7A, "f12" => 0x7B,
        _ when name.Length == 1 => char.ToUpperInvariant(name[0]),
        _ => 0,
    };

    /* ------------------------------ tela ------------------------------ */

    /// <summary>Captura a tela inteira (ou um retângulo) e devolve o caminho do PNG.</summary>
    public static string Capture(string? path = null, Rectangle? area = null)
    {
        var bounds = area ?? SystemInformation.VirtualScreen;
        using var bitmap = new Bitmap(Math.Max(1, bounds.Width), Math.Max(1, bounds.Height));
        using (var g = Graphics.FromImage(bitmap))
        {
            g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bitmap.Size, CopyPixelOperation.SourceCopy);
        }
        var destino = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            $"orbi-tela-{DateTime.Now:yyyyMMdd-HHmmss}.png");
        Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
        bitmap.Save(destino, System.Drawing.Imaging.ImageFormat.Png);
        return destino;
    }

    /// <summary>Resumo do que está na tela agora, para a IA decidir onde clicar.</summary>
    public static string DescribeScreen()
    {
        var (width, height) = ScreenSize();
        var (x, y) = CursorPosition();
        var builder = new StringBuilder();
        builder.AppendLine($"tela {width}x{height} · mouse em {x},{y}");
        builder.AppendLine("janela ativa: " + ActiveWindowTitle());
        builder.AppendLine("janelas abertas:");
        foreach (var janela in Windows().Take(12))
        {
            builder.AppendLine($"  [{janela.Process}] {janela.Title} · {janela.Left},{janela.Top} {janela.Width}x{janela.Height}");
        }
        return builder.ToString().TrimEnd();
    }

    public static (int Width, int Height) ScreenSize() =>
        (GetSystemMetrics(SM_CXVIRTUALSCREEN), GetSystemMetrics(SM_CYVIRTUALSCREEN));

    public static (int X, int Y) CursorPosition() =>
        GetCursorPos(out var point) ? (point.X, point.Y) : (0, 0);

    public static string ActiveWindowTitle()
    {
        var handle = GetForegroundWindow();
        if (handle == IntPtr.Zero) return "";
        var length = GetWindowTextLengthW(handle);
        if (length <= 0) return "";
        var text = new StringBuilder(length + 1);
        GetWindowTextW(handle, text, text.Capacity);
        return text.ToString();
    }

    public sealed record WindowInfo(IntPtr Handle, string Title, string Process, int Left, int Top, int Width, int Height);

    public static List<WindowInfo> Windows()
    {
        var lista = new List<WindowInfo>();
        EnumWindows((window, _) =>
        {
            if (!IsWindowVisible(window)) return true;
            var length = GetWindowTextLengthW(window);
            if (length <= 0) return true;
            var title = new StringBuilder(length + 1);
            GetWindowTextW(window, title, title.Capacity);
            if (string.IsNullOrWhiteSpace(title.ToString())) return true;
            GetWindowThreadProcessId(window, out var pid);
            var nome = "";
            try { nome = Process.GetProcessById((int)pid).ProcessName; } catch { }
            GetWindowRect(window, out var rect);
            lista.Add(new WindowInfo(window, title.ToString(), nome,
                rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top));
            return true;
        }, IntPtr.Zero);
        return lista;
    }

    /// <summary>Traz uma janela pelo título (ou parte dele) para a frente.</summary>
    public static bool Focus(string title)
    {
        var janela = Windows().FirstOrDefault(w =>
            w.Title.Contains(title, StringComparison.OrdinalIgnoreCase) ||
            w.Process.Contains(title, StringComparison.OrdinalIgnoreCase));
        if (janela is null) return false;
        if (IsIconic(janela.Handle)) ShowWindow(janela.Handle, 9); // SW_RESTORE
        return SetForegroundWindow(janela.Handle);
    }

    public static bool Maximize(string title)
    {
        var janela = Find(title);
        if (janela is null) return false;
        ShowWindow(janela.Value, 3); // SW_MAXIMIZE
        return true;
    }

    public static bool Minimize(string title)
    {
        var janela = Find(title);
        if (janela is null) return false;
        ShowWindow(janela.Value, 6); // SW_MINIMIZE
        return true;
    }

    public static bool Close(string title)
    {
        var janela = Find(title);
        if (janela is null) return false;
        ShowWindow(janela.Value, 0); // SW_HIDE é bruto demais; fecha pela mensagem
        PostMessageW(janela.Value, 0x0010, IntPtr.Zero, IntPtr.Zero); // WM_CLOSE
        return true;
    }

    private static IntPtr? Find(string title) =>
        Windows().FirstOrDefault(w =>
            w.Title.Contains(title, StringComparison.OrdinalIgnoreCase) ||
            w.Process.Contains(title, StringComparison.OrdinalIgnoreCase))?.Handle;

    [DllImport("user32.dll")]
    private static extern bool PostMessageW(IntPtr window, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessageW(IntPtr window, uint msg, IntPtr wParam, IntPtr lParam);

    /// <summary>Mensagem para uma janela (ou para a área de trabalho, com zero).</summary>
    public static IntPtr SendMessageToWindow(IntPtr window, uint msg, IntPtr wParam, IntPtr lParam) =>
        SendMessageW(window, msg, wParam, lParam);
}
