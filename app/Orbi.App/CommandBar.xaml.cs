using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Orbi.Services;

namespace Orbi;

/// <summary>Uma linha de sugestão/resposta na barra.</summary>
public sealed class ResultRow
{
    public string Glyph { get; init; } = "\u2022";
    public string Text { get; init; } = "";
    public string? Detail { get; init; }
    public string Badge { get; init; } = "";
    public string BadgeBrush { get; init; } = "#1FFFFFFF";
    public string BadgeTextBrush { get; init; } = "#99F1EBDF";
    public string? Command { get; init; }
    public bool IsConfirm { get; init; }

    public Visibility DetailVisibility => string.IsNullOrWhiteSpace(Detail) ? Visibility.Collapsed : Visibility.Visible;
}

public partial class CommandBar : Window
{
    private readonly ObservableCollection<ResultRow> _rows = new();
    private readonly List<(string Role, string Text)> _history = new();
    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(260) };
    private bool _thinking;

    public CommandBar()
    {
        InitializeComponent();
        Results.ItemsSource = _rows;
        _debounce.Tick += async (_, _) =>
        {
            _debounce.Stop();
            await AskAiAsync(Input.Text);
        };
        AiStatus.Text = Gemini.Available ? "gemini pronto" : "modo local";
        AiStatus.Foreground = Gemini.Available
            ? new SolidColorBrush(Color.FromRgb(0xC7, 0x45, 0x2A))
            : new SolidColorBrush(Color.FromArgb(0x80, 0xF1, 0xEB, 0xDF));
    }

    /* ----------------------------- abrir / fechar ----------------------------- */
    public void RefreshGreeting() => ShowGreeting();

    public void ShowBar()
    {
        // Redimensiona para caber na tela (notebook pequeno, escala 150%…).
        var screen = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position);
        var area = screen.WorkingArea;
        var scale = VisualTreeHelper.GetDpi(this);

        var width = Math.Min(Width, area.Width / scale.DpiScaleX - 40);
        var height = Math.Min(Height, area.Height / scale.DpiScaleY - 40);
        if (width > 300) Width = width;
        if (height > 200) Height = height;

        // WorkingArea e Left/Top: ambos em pixels físicos (unidades nativas).
        Left = area.Left + (area.Width - Width * scale.DpiScaleX) / 2;
        Top = area.Top + area.Height * 0.24;

        if (!IsVisible) Show();

        Input.Text = "";
        _rows.Clear();
        ShowGreeting();
        UpdateFooter();

        Topmost = true;
        Activate();
        Focus();
        Input.Focus();
        Keyboard.Focus(Input);
    }

    /// <summary>Mostra o atalho que está realmente funcionando.</summary>
    private void UpdateFooter()
    {
        var keys = string.IsNullOrEmpty(HotKey.Combination) ? "use o ícone na bandeja" : HotKey.Combination;
        Footer.Text = $"{keys} abre · Enter executa · Esc esconde";
    }

    /// <summary>
    /// Fechar a janela (X, Alt+F4) só esconde — o app vive na bandeja.
    /// Só o menu "Sair" encerra de verdade.
    /// </summary>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        var app = System.Windows.Application.Current as App;
        if (app is null || !app.ClosingForReal)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }

    public void HideBar()
    {
        Hide();
    }

    /// <summary>Arrasta a janela pela faixa de entrada.</summary>
    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try { DragMove(); }
            catch { /* clique simples, sem arrasto */ }
        }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key == Key.Escape)
        {
            // Esc esconde a barra; o app continua vivo na bandeja.
            Hide();
            e.Handled = true;
        }
    }

    /* ----------------------------- saudação ----------------------------- */
    private void ShowGreeting()
    {
        var first = string.IsNullOrWhiteSpace(Store.Current.UserName)
            ? ""
            : ", " + Store.Current.UserName.Split(' ')[0];
        var pending = Store.Current.Reminders.Count(r => !r.Done && r.DueAt > DateTime.Now);
        var detail = pending > 0
            ? $"Você tem {pending} lembrete(s) pendente(s)."
            : ShortcutHint();

        Add(new ResultRow
        {
            Glyph = "\u25CF",
            Text = $"Oi{first}. O que você precisa?",
            Detail = detail,
            Badge = Gemini.Available ? "IA" : "local",
            BadgeBrush = Gemini.Available ? "#FFC7452A" : "#1FFFFFFF",
            BadgeTextBrush = Gemini.Available ? "#FFFFFFFF" : "#99F1EBDF",
        });

        var suggestions = new[]
        {
            ("Abre o zap", "apps"),
            ("Volume em 30", "sliders"),
            ("Me lembra em 20 min de beber água", "bell"),
            ("Status do PC", "monitor"),
        };

        foreach (var (command, _) in suggestions)
        {
            Add(new ResultRow { Glyph = "\u2192", Text = command, Command = command, Badge = "exemplo" });
        }

        var recent = Store.Current.Commands.Take(3).ToList();
        foreach (var command in recent)
        {
            Add(new ResultRow { Glyph = "\u21BB", Text = command, Command = command, Badge = "recente" });
        }
    }

    private static string ShortcutHint() =>
        string.IsNullOrEmpty(HotKey.Combination)
            ? "Abra pelo ícone do Orbi na bandeja."
            : $"{HotKey.Combination} abre em qualquer programa.";

    private void Add(ResultRow row) => _rows.Add(row);

    /* ----------------------------- digitação ----------------------------- */
    private void Input_TextChanged(object sender, TextChangedEventArgs e)
    {
        Hint.Visibility = string.IsNullOrEmpty(Input.Text) ? Visibility.Visible : Visibility.Collapsed;
        var text = Input.Text.Trim();
        _rows.Clear();

        if (string.IsNullOrWhiteSpace(text))
        {
            _debounce.Stop();
            ShowGreeting();
            return;
        }

        // Sugestão ao vivo: mostra o que vai acontecer antes de apertar Enter.
        var preview = Interpreter.Interpret(text);
        if (preview.Kind != ReplyKind.NeedsAi)
        {
            _debounce.Stop();
            Add(ToRow(preview));

            // 1º item selecionado: Enter executa sem precisar clicar.
            Results.SelectedIndex = 0;
            if (preview.Kind == ReplyKind.Confirm)
            {
                Add(new ResultRow
                {
                    Glyph = "\u2713",
                    Text = "Sim, executar",
                    Detail = "Enter confirma. Esc cancela.",
                    Badge = "confirmar",
                    BadgeBrush = "#FFC7452A",
                    BadgeTextBrush = "#FFFFFFFF",
                    Command = text,
                    IsConfirm = true,
                });
            }
            return;
        }

        if (!Gemini.Available)
        {
            Results.SelectedIndex = 0;
            Add(new ResultRow
            {
                Glyph = "?",
                Text = "Isso eu respondo com o Gemini.",
                Detail = "Adicione sua chave gratuita em Ajustes ▸ IA — ela fica cifrada neste PC. Sem ela, os comandos locais continuam funcionando.",
                Badge = "ajustes",
            });
            return;
        }

        Add(new ResultRow
        {
            Glyph = "?",
            Text = "Perguntar ao Gemini…",
            Detail = "Pausa na digitação para não gastar chamadas.",
            Badge = "IA",
            BadgeBrush = "#FFC7452A",
            BadgeTextBrush = "#FFFFFFFF",
        });
        _debounce.Stop();
        _debounce.Start();
    }

    private static ResultRow ToRow(OrbiReply reply) => new()
    {
        Glyph = reply.Kind switch
        {
            ReplyKind.Confirm => "\u26A0",
            ReplyKind.Private => "\u25A0",
            ReplyKind.NeedsAi => "?",
            _ => "\u2713",
        },
        Text = reply.Text,
        Detail = reply.Detail,
        Badge = reply.Kind switch
        {
            ReplyKind.Confirm => "confirmar",
            ReplyKind.Private => "privado",
            ReplyKind.NeedsAi => "IA",
            _ => "no seu PC",
        },
        BadgeBrush = reply.Kind is ReplyKind.Confirm or ReplyKind.NeedsAi ? "#FFC7452A" : "#1FFFFFFF",
        BadgeTextBrush = reply.Kind is ReplyKind.Confirm or ReplyKind.NeedsAi ? "#FFFFFFFF" : "#99F1EBDF",
    };

    /* ----------------------------- IA ----------------------------- */
    private async Task AskAiAsync(string prompt)
    {
        if (_thinking || string.IsNullOrWhiteSpace(prompt)) return;
        _thinking = true;
        Footer.Text = "consultando o Gemini…";
        try
        {
            var answer = await Gemini.AskAsync(prompt, _history);
            _rows.Clear();
            if (answer is null)
            {
                Add(new ResultRow
                {
                    Glyph = "!",
                    Text = "Não consegui falar com o Gemini agora.",
                    Detail = "A chave está certa? Os comandos locais seguem funcionando.",
                    Badge = "offline",
                });
            }
            else
            {
                Add(new ResultRow
                {
                    Glyph = "\u2726",
                    Text = answer,
                    Detail = "Respondido pelo Gemini.",
                    Badge = "IA",
                    BadgeBrush = "#FFC7452A",
                    BadgeTextBrush = "#FFFFFFFF",
                });
                _history.Add(("user", prompt));
                _history.Add(("assistant", answer));
                if (_history.Count > 12) _history.RemoveRange(0, _history.Count - 12);
            }
        }
        finally
        {
            _thinking = false;
            UpdateFooter();
        }
    }

    /* ----------------------------- execução ----------------------------- */
    private void Input_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Run();
        }
        else if (e.Key == Key.Down && _rows.Count > 0)
        {
            Results.SelectedIndex = Math.Min(_rows.Count - 1, Results.SelectedIndex + 1);
            e.Handled = true;
        }
        else if (e.Key == Key.Up && _rows.Count > 0)
        {
            Results.SelectedIndex = Math.Max(0, Results.SelectedIndex - 1);
            e.Handled = true;
        }
    }

    private void Input_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Tab completa o primeiro item executável.
        if (e.Key == Key.Tab)
        {
            var first = _rows.FirstOrDefault(row => !string.IsNullOrWhiteSpace(row.Command));
            if (first?.Command is { } command)
            {
                Input.Text = command;
                Input.CaretIndex = Input.Text.Length;
                e.Handled = true;
            }
        }
    }

    private void Run()
    {
        var selected = Results.SelectedItem as ResultRow;
        var text = Input.Text.Trim();

        if (selected?.IsConfirm == true)
        {
            ExecuteSelected(text);
            return;
        }

        // Texto digitado sempre vence: Enter executa o que está na caixa.
        // A seleção só é usada quando a caixa está vazia (exemplos/recentes).
        if (string.IsNullOrWhiteSpace(text) && selected?.Command is { } picked)
        {
            ExecuteSelected(picked);
            return;
        }

        if (string.IsNullOrWhiteSpace(text)) return;
        ExecuteSelected(text);
    }

    private void ExecuteSelected(string text)
    {
        Store.PushCommand(text);
        var reply = Interpreter.Execute(text);

        _rows.Clear();
        Add(ToRow(reply));

        if (reply.Kind == ReplyKind.Confirm)
        {
            Add(new ResultRow
            {
                Glyph = "\u2713",
                Text = "Sim, executar",
                Detail = "Enter confirma. Esc cancela.",
                Badge = "confirmar",
                BadgeBrush = "#FFC7452A",
                BadgeTextBrush = "#FFFFFFFF",
                Command = text,
                IsConfirm = true,
            });
        }

        Input.Text = "";
        Input.Focus();
    }

    private void Results_Click(object sender, MouseButtonEventArgs e)
    {
        if (Results.SelectedItem is not ResultRow row) return;
        if (row.Command is { } command)
        {
            Input.Text = command;
            ExecuteSelected(command);
        }
    }

    private void Results_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Results.SelectedItem is ResultRow { Command: { } command })
        {
            ExecuteSelected(command);
        }
    }
}
