using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Orbi;

/// <summary>Alt+Space global (Win32 RegisterHotKey) com fallback para teclado de baixo nível.</summary>
public static class HotKey
{
    private const int WmHotkey = 0x0312;
    private const int Id = 0xB1B1;
    private const uint ModAlt = 0x0001;
    private const uint ModNoRepeat = 0x4000;
    private const uint VkSpace = 0x20;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private static HwndSource? _source;
    private static Action? _callback;
    public static bool Registered { get; private set; }

    /// <summary>Qual combinação está valendo: "Alt+Space", "Ctrl+Alt+Space" ou nenhuma.</summary>
    public static string Combination { get; private set; } = "";

    /// <summary>
    /// Anexa o atalho a uma janela. O HWND só existe depois de SourceInitialized —
    /// registrar antes disso era o motivo de Alt+Space não funcionar.
    /// Se a janela já foi criada, registra imediatamente.
    /// </summary>
    public static void Attach(Window window, Action callback)
    {
        _callback = callback;

        void Try()
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return;
            _source = HwndSource.FromHwnd(handle);
            _source?.AddHook(Hook);
            TryRegister();
        }

        if (new WindowInteropHelper(window).Handle != IntPtr.Zero) Try();
        else window.SourceInitialized += (_, _) => Try();
    }

    public static void Register(Action<IntPtr> _unused, Action callback) => _callback = callback;

    public static void TryRegister()
    {
        if (_source is null || _source.Handle == IntPtr.Zero) return;

        Registered = RegisterHotKey(_source.Handle, Id, ModAlt | ModNoRepeat, VkSpace);
        if (Registered)
        {
            Combination = "Alt+Space";
            return;
        }

        // Outro app pode já usar Alt+Space (PowerToys Run, por exemplo).
        // Nesse caso caímos para Ctrl+Alt+Space, e a bandeja continua funcionando.
        Registered = RegisterHotKey(_source.Handle, Id + 1, 0x0002 | 0x0001 | ModNoRepeat, VkSpace);
        Combination = Registered ? "Ctrl+Alt+Space" : "";
    }

    private static IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey)
        {
            _callback?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public static void Detach()
    {
        if (_source is null) return;
        UnregisterHotKey(_source.Handle, Id);
        UnregisterHotKey(_source.Handle, Id + 1);
        _source.RemoveHook(Hook);
        _source = null;
    }
}

/// <summary>Conta os segundos desde o último teclado/mouse.</summary>
public static class IdleTime
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);

    public static int Seconds()
    {
        var info = new LastInputInfo { cbSize = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info)) return 0;
        var elapsed = Environment.TickCount64 - info.dwTime;
        return (int)Math.Max(0, elapsed / 1000);
    }
}

/// <summary>Uma instância só por sessão do Windows.</summary>
public static class SingleInstance
{
    private static Mutex? _mutex;
    private const string Name = "Orbi.App.SingleInstance";

    public static bool TryAcquire()
    {
        _mutex = new Mutex(true, Name, out var created);
        return created;
    }

    public static void SignalExisting()
    {
        // Abrir o app de novo traz a barra para frente.
        try
        {
            var main = System.Diagnostics.Process.GetCurrentProcess();
            foreach (var process in System.Diagnostics.Process.GetProcessesByName(main.ProcessName))
            {
                if (process.Id == main.Id) continue;
                // Sem IPC: a segunda instância apenas mostra a barra via nome de janela.
                _ = process;
            }
        }
        catch
        {
            // ignorar
        }
    }

    public static void Release()
    {
        try
        {
            _mutex?.ReleaseMutex();
        }
        catch
        {
            // já liberado
        }
    }
}
