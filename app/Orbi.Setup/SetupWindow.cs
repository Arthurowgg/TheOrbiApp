using System.IO;
using static Orbi.Setup.Win32;

namespace Orbi.Setup;

/// <summary>
/// A janela do instalador, desenhada com Win32: faixa escura à esquerda com a
/// marca, título, o que vai ser instalado, uma barra de progresso e o botão.
/// Sem WPF — é isso que deixa o setup em poucos MB.
/// </summary>
internal sealed class SetupWindow
{
    private const int Width = 720;
    private const int Height = 430;

    private const int IdInstall = 1001;
    private const int IdQuit = 1002;
    private const int IdLog = 1003;
    private const int IdProgress = 1004;
    private const int IdStatus = 1005;

    private readonly IntPtr _window;
    private readonly IntPtr _install;
    private readonly IntPtr _quit;
    private readonly IntPtr _log;
    private readonly IntPtr _progress;
    private readonly IntPtr _status;

    private readonly IntPtr _fontTitle;
    private readonly IntPtr _fontBody;
    private readonly IntPtr _fontLabel;
    private readonly IntPtr _fontMono;
    private readonly IntPtr _brushPaper;
    private readonly IntPtr _brushFilm;
    private readonly IntPtr _brushSignal;

    private bool _busy;
    private bool _done;
    private string _side = "INSTALAÇÃO";
    private readonly List<string> _lines = new();
    private readonly System.Windows.Forms.Timer _clock = new() { Interval = 200 };

    private static readonly string[] Etapas =
    {
        "Conferindo o .NET",
        "Baixando o Orbi",
        "Copiando os arquivos",
        "Criando os atalhos",
    };

    private int _step;

    public SetupWindow()
    {
        var instance = GetModuleHandleW(null);
        var style = WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX;
        var screenWidth = 1280;
        var screenHeight = 800;
        var x = Math.Max(0, (screenWidth - Width) / 2);
        var y = Math.Max(0, (screenHeight - Height) / 2);

        _window = CreateWindowExW(0, "OrbiSetup", "Orbi — instalação", style, x, y, Width, Height,
            IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero);

        _fontTitle = CreateFontW(-30, 0, 0, 0, FW_SEMIBOLD, 0, 0, 0, DEFAULT_CHARSET, 0, 0, 5, 0,
            "Bahnschrift SemiBold");
        _fontBody = CreateFontW(-17, 0, 0, 0, FW_NORMAL, 0, 0, 0, DEFAULT_CHARSET, 0, 0, 5, 0, "Segoe UI");
        _fontLabel = CreateFontW(-13, 0, 0, 0, FW_NORMAL, 0, 0, 0, DEFAULT_CHARSET, 0, 0, 5, 0, "Consolas");
        _fontMono = CreateFontW(-14, 0, 0, 0, FW_NORMAL, 0, 0, 0, DEFAULT_CHARSET, 0, 0, 5, 0, "Consolas");
        _brushPaper = CreateSolidBrush(Paper);
        _brushFilm = CreateSolidBrush(Film);
        _brushSignal = CreateSolidBrush(Signal);

        _install = CreateWindowExW(0, "BUTTON", "Instalar o Orbi",
            WS_CHILD | WS_VISIBLE | WS_TABSTOP | BS_DEFPUSHBUTTON,
            Width - 300, Height - 78, 168, 38, _window, IdInstall, instance, IntPtr.Zero);
        _quit = CreateWindowExW(0, "BUTTON", "Cancelar",
            WS_CHILD | WS_VISIBLE | WS_TABSTOP | BS_PUSHBUTTON,
            Width - 122, Height - 78, 96, 38, _window, IdQuit, instance, IntPtr.Zero);

        _progress = CreateWindowExW(0, "msctls_progress32", "",
            WS_CHILD | WS_VISIBLE, 268, Height - 128, Width - 300, 14, _window, IdProgress, instance, IntPtr.Zero);

        _status = CreateWindowExW(0, "STATIC", "Pronto para instalar.",
            WS_CHILD | WS_VISIBLE | SS_LEFT | SS_NOPREFIX,
            268, Height - 108, Width - 300, 22, _window, IdStatus, instance, IntPtr.Zero);

        _log = CreateWindowExW(0, "EDIT", "",
            WS_CHILD | WS_VISIBLE | ES_MULTILINE | ES_READONLY | ES_AUTOVSCROLL | WS_VSCROLL | WS_BORDER,
            268, 196, Width - 300, Height - 340, _window, IdLog, instance, IntPtr.Zero);

        foreach (var handle in new[] { _status, _log })
        {
            SendMessageW(handle, WM_SETFONT, handle == _log ? _fontMono : _fontBody, IntPtr.Zero);
        }
        SendMessageW(_install, WM_SETFONT, _fontBody, IntPtr.Zero);
        SendMessageW(_quit, WM_SETFONT, _fontBody, IntPtr.Zero);

        _clock.Tick += (_, _) => Repaint();
        _clock.Start();
    }

    public IntPtr Handle => _window;

    public void Show()
    {
        ShowWindow(_window, SW_SHOW);
        UpdateWindow(_window);
    }

    /// <summary>Clique em instalar: dispara o trabalho em segundo plano.</summary>
    public void StartInstall()
    {
        if (_busy) return;
        _busy = true;
        EnableWindow(_install, false);
        SetWindowTextW(_install, "Instalando…");
        AddLine("Iniciando a instalação");

        var progress = new Progress<double>(value => SetProgress(value));
        Task.Run(async () =>
        {
            try
            {
                await Installer.RunAsync(progress, CancellationToken.None);
                _done = true;
                AddLine("Tudo pronto.");
                SetStatus("O Orbi está instalado.");
                SetProgress(1);
                SetWindowTextW(_install, "Abrir o Orbi");
                EnableWindow(_install, true);
                _installText = "Abrir o Orbi";
            }
            catch (Exception error)
            {
                AddLine("Falhou: " + error.Message);
                SetStatus("Não deu para instalar: " + error.Message);
                SetWindowTextW(_install, "Tentar de novo");
                EnableWindow(_install, true);
                _installText = "Tentar de novo";
                _failed = true;
            }
            finally
            {
                _busy = false;
            }
        });
    }

    private string _installText = "Instalar o Orbi";
    private bool _failed;

    public void AppendLog(string line) => AddLine(line);

    private void AddLine(string line)
    {
        if (_window == IntPtr.Zero) return;
        _lines.Add(line);
        if (_lines.Count > 200) _lines.RemoveAt(0);
        var text = string.Join("\r\n", _lines);
        SetWindowTextW(_log, text);
        SendMessageW(_log, (uint)EM_LINESCROLL, IntPtr.Zero, IntPtr.Zero);
    }

    private void SetStatus(string text)
    {
        if (_window == IntPtr.Zero) return;
        SetWindowTextW(_status, text);
    }

    private void SetProgress(double value)
    {
        if (_progress == IntPtr.Zero) return;
        var percent = (int)Math.Round(Math.Clamp(value, 0, 1) * 1000);
        SendMessageW(_progress, (uint)PBM_SETPOS, percent, IntPtr.Zero);
        _progressValue = percent;
    }

    private int _progressValue;

    /// <summary>Desenha a faixa escura, a marca e os textos fixos.</summary>
    public void Paint(IntPtr dc)
    {
        var sideWidth = 240;

        var film = new RECT { Left = 0, Top = 0, Right = sideWidth, Bottom = Height };
        FillRect(dc, ref film, _brushFilm);

        var dot = new RECT { Left = 28, Top = 34, Right = 52, Bottom = 58 };
        FillRect(dc, ref dot, _brushSignal);

        var title = new RECT { Left = 28, Top = 74, Right = sideWidth - 20, Bottom = 122 };
        var old = SelectObject(dc, _fontTitle);
        SetTextColor(dc, Bone);
        SetBkColor(dc, Film);
        DrawTextW(dc, "ORBI", -1, ref title, DT_LEFT | DT_NOPREFIX);

        var version = new RECT { Left = 28, Top = 126, Right = sideWidth - 20, Bottom = 200 };
        SelectObject(dc, _fontLabel);
        SetTextColor(dc, Rgb(0x9A, 0x90, 0x80));
        DrawTextW(dc, "\r\n" + _side + "\r\nv" + Installer.Versao, -1, ref version, DT_LEFT | DT_WORDBREAK | DT_NOPREFIX);

        var where = new RECT { Left = 28, Top = Height - 120, Right = sideWidth - 20, Bottom = Height - 30 };
        SetTextColor(dc, Rgb(0x7A, 0x72, 0x64));
        DrawTextW(dc, "\r\nInstala só para você.\r\nSem administrador.", -1, ref where, DT_LEFT | DT_WORDBREAK | DT_NOPREFIX);

        var paper = new RECT { Left = sideWidth, Top = 0, Right = Width, Bottom = Height };
        FillRect(dc, ref paper, _brushPaper);

        var heading = new RECT { Left = 268, Top = 30, Right = Width - 30, Bottom = 70 };
        SelectObject(dc, _fontTitle);
        SetTextColor(dc, Ink);
        SetBkColor(dc, Paper);
        DrawTextW(dc, _done ? "Instalado." : _failed ? "Algo deu errado." : "Instalar o Orbi", -1, ref heading, DT_LEFT | DT_NOPREFIX);

        var rows = new RECT { Left = 268, Top = 74, Right = Width - 30, Bottom = 190 };
        SelectObject(dc, _fontBody);
        SetTextColor(dc, Warm);
        var detalhe = _done
            ? "Pronto. A barra abre com Alt+Shift+O.\r\nSeus dados ficam em %APPDATA%\\Orbi."
            : "Instalador leve: o app é baixado aqui.\r\nSem conta obrigatória. Sem administrador.";
        DrawTextW(dc, detalhe, -1, ref rows, DT_LEFT | DT_WORDBREAK | DT_NOPREFIX);

        var steps = new RECT { Left = 268, Top = Height - 96, Right = Width - 30, Bottom = Height - 74 };
        SelectObject(dc, _fontLabel);
        SetTextColor(dc, Rgb(0x9A, 0x90, 0x80));
        DrawTextW(dc, _done ? "concluído" : _busy ? Etapas[Math.Min(_step, Etapas.Length - 1)] : "pronto para começar", -1, ref steps, DT_LEFT | DT_NOPREFIX);

        SelectObject(dc, old);
    }

    private void Repaint()
    {
        if (_window == IntPtr.Zero) return;
        InvalidateRect(_window, IntPtr.Zero, false);
    }

    public void Advance(string line)
    {
        if (line.Contains("runtime", StringComparison.OrdinalIgnoreCase)) _step = 0;
        else if (line.Contains("Baixando", StringComparison.OrdinalIgnoreCase)) _step = 1;
        else if (line.Contains("Instalando os arquivos", StringComparison.OrdinalIgnoreCase)) _step = 2;
        else if (line.Contains("atalhos", StringComparison.OrdinalIgnoreCase)) _step = 3;
        AddLine(line);
    }

    public void HandleCommand(int id)
    {
        if (id == IdInstall)
        {
            if (_done)
            {
                Installer.Launch();
                DestroyWindow(_window);
                return;
            }
            if (_failed) _failed = false;
            StartInstall();
            return;
        }
        if (id == IdQuit)
        {
            if (_busy)
            {
                var answer = MessageBoxW(_window, "Cancelar a instalação?", "Orbi",
                    MB_YESNO | MB_ICONQUESTION);
                if (answer != IDYES) return;
            }
            DestroyWindow(_window);
        }
    }

    public void Dispose()
    {
        _clock.Stop();
        _clock.Dispose();
        foreach (var brush in new[] { _brushPaper, _brushFilm, _brushSignal }) DeleteObject(brush);
        foreach (var font in new[] { _fontTitle, _fontBody, _fontLabel, _fontMono }) DeleteObject(font);
    }
}
