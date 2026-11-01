using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Orbi;

/// <summary>Atalho global (Win32 RegisterHotKey), padrão Alt+Shift+O, com alternativas automáticas.</summary>
public static class HotKey
{
    private const int WmHotkey = 0x0312;
    private const int Id = 0xB1B1;
    private const uint ModAlt = 0x0001;
    private const uint ModCtrl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const uint ModNoRepeat = 0x4000;

    /// <summary>Combinações disponíveis, na ordem em que o app tenta.</summary>
    public sealed record Combo(string Name, uint Modifiers, uint Key)
    {
        public override string ToString() => Name;
    }

    private static readonly Combo[] Combos =
    {
        new("Alt+Shift+O", ModAlt | ModShift, 0x4F),      // O — padrão: não conflita com Windows/AI
        new("Ctrl+Shift+O", ModCtrl | ModShift, 0x4F),
        new("Alt+Shift+Space", ModAlt | ModShift, 0x20),
        new("Ctrl+Shift+Space", ModCtrl | ModShift, 0x20),
    };

    public static IReadOnlyList<Combo> Available => Combos;

    /// <summary>Combinação preferida (salva em Ajustes); vazia = primeira disponível.</summary>
    public static string Preferred { get; set; } = "";

    private static Combo? _active;

    /// <summary>Descrição do atalho em uso, ou vazio se nenhum registrou.</summary>
    public static string ActiveName => _active?.Name ?? "";

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private static HwndSource? _source;
    private static Window? _window;
    private static Action? _callback;
    public static bool Registered { get; private set; }

    /// <summary>Qual combinação está valendo (ex.: "Alt+Shift+O") ou vazio se nenhuma.</summary>
    public static string Combination { get; private set; } = "";

    /// <summary>
    /// Anexa o atalho a uma janela. O HWND só existe depois de SourceInitialized —
    /// registrar antes disso era o motivo do atalho não funcionar.
    /// Se a janela já foi criada, registra imediatamente.
    /// </summary>
    public static void Attach(Window window, Action callback)
    {
        _callback = callback;
        _window = window;

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
        // Se a barra ainda não tinha HWND quando Attach foi chamado, tenta agora:
        // o atalho é conferido a cada abertura da barra, então nunca fica "sem
        // atalho" só porque o handle demorou a existir.
        if ((_source is null || _source.Handle == IntPtr.Zero) && _window is not null)
        {
            var handle = new WindowInteropHelper(_window).Handle;
            if (handle != IntPtr.Zero)
            {
                _source = HwndSource.FromHwnd(handle);
                _source?.AddHook(Hook);
            }
        }

        if (_source is null || _source.Handle == IntPtr.Zero) return;

        // Ordem: a combinação preferida (Ajustes) e depois as alternativas.
        var ordem = Combos
            .OrderBy(combo => combo.Name == Preferred ? 0 : 1)
            .ToList();

        foreach (var combo in ordem)
        {
            if (RegisterHotKey(_source.Handle, Id, combo.Modifiers | ModNoRepeat, combo.Key))
            {
                Registered = true;
                _active = combo;
                Combination = combo.Name;
                return;
            }
        }

        Registered = false;
        _active = null;
        Combination = "";
    }

    /// <summary>Troca o atalho em uso (chamado em Ajustes).</summary>
    public static bool Change(string name)
    {
        if (_source is null) return false;
        UnregisterHotKey(_source.Handle, Id);
        _active = null;
        Combination = "";
        Preferred = name;
        TryRegister();
        return Registered;
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
        _window = null;
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

/// <summary>
/// Uma instância só por sessão do Windows, com IPC de verdade:
/// abrir o Orbi de novo (atalho, menu Iniciar) traz a barra para frente em vez
/// de não fazer nada.
/// </summary>
public static class SingleInstance
{
    private static Mutex? _mutex;
    private const string Name = "Orbi.App.SingleInstance";
    private const string ShowBarEvent = "Orbi.App.ShowBar";
    private const string ShowSettingsEvent = "Orbi.App.ShowSettings";
    private const string ShowDiagEvent = "Orbi.App.ShowDiag";

    public static bool TryAcquire()
    {
        _mutex = new Mutex(true, Name, out var created);
        return created;
    }

    /// <summary>Fica ouvindo pedidos das próximas instâncias.</summary>
    public static void Listen(Action showBar, Action showSettings, Action? showDiag = null)
    {
        ListenOn(ShowBarEvent, showBar);
        ListenOn(ShowSettingsEvent, showSettings);
        if (showDiag is not null) ListenOn(ShowDiagEvent, showDiag);
    }

    private static void ListenOn(string name, Action action)
    {
        var thread = new Thread(() =>
        {
            try
            {
                using var handle = new EventWaitHandle(false, EventResetMode.AutoReset, name);
                while (true)
                {
                    handle.WaitOne();
                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(action);
                }
            }
            catch
            {
                // sem IPC: a segunda instância só encerra
            }
        })
        {
            IsBackground = true,
            Name = $"orbi-ipc-{name}",
        };
        thread.Start();
    }

    public static void SignalExisting(bool settings, bool diag = false)
    {
        try
        {
            var nome = diag ? ShowDiagEvent : settings ? ShowSettingsEvent : ShowBarEvent;
            using var handle = EventWaitHandle.OpenExisting(nome);
            handle.Set();
        }
        catch
        {
            // instância antiga sem IPC: nada a fazer
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
