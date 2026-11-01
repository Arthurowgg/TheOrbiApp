using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Orbi.Services;

namespace Orbi;

public partial class SettingsWindow : Window
{

    public SettingsWindow()
    {
        InitializeComponent();
        Nav.SelectedIndex = 0;
        Load();
    }

    /* ----------------------------- carregar ----------------------------- */
    private void Load()
    {
        var settings = Store.Current;
        NameBox.Text = settings.UserName;
        StartupToggle.IsChecked = settings.StartWithWindows;
        GeminiToggle.IsChecked = settings.UseGemini;
        LockToggle.IsChecked = settings.LockEnabled;
        LockMinutes.Text = settings.LockAfterMinutes.ToString();
        ApiKeyBox.Password = Store.LoadApiKey() ?? "";
        GeminiState.Text = Store.HasApiKey()
            ? "Chave configurada e cifrada neste PC."
            : "Sem chave: comandos locais funcionam, perguntas livres não.";
        RefreshAccount();
        LoadHotkeys();
        DataPath.Text = Store.Root;
        ModelBox.SelectedIndex = Math.Max(0, new[] { "gemini-2.0-flash", "gemini-1.5-flash", "gemini-1.5-flash-8b" }
            .ToList().IndexOf(settings.GeminiModel));

        RenderReminders();
        RenderNotes();
        RenderShortcuts();
        RenderAliases();
    }

    /* ----------------------------- conta e sync ----------------------------- */
    /* ----------------------------- atalho global ----------------------------- */
    private bool _loadingHotkeys;

    private void LoadHotkeys()
    {
        _loadingHotkeys = true;
        HotkeyBox.Items.Clear();
        foreach (var combo in HotKey.Available) HotkeyBox.Items.Add(combo.Name);

        var atual = string.IsNullOrWhiteSpace(Store.Current.Hotkey) ? HotKey.Available[0].Name : Store.Current.Hotkey;
        HotkeyBox.SelectedItem = atual;
        if (HotkeyBox.SelectedIndex < 0 && HotkeyBox.Items.Count > 0) HotkeyBox.SelectedIndex = 0;

        HotkeyState.Text = string.IsNullOrEmpty(HotKey.Combination)
            ? "Nenhuma combinação disponível agora (outro app pode estar usando)."
            : $"Em uso: {HotKey.Combination}. Troque se outro app já usa a combinação.";
        _loadingHotkeys = false;
    }

    private void HotkeyBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingHotkeys) return;
        if (HotkeyBox.SelectedItem is not string nome) return;

        Store.Current.Hotkey = nome;
        Store.Save();

        var ok = HotKey.Change(nome);
        HotkeyState.Text = ok
            ? $"Pronto: aperte {nome} em qualquer programa."
            : $"{nome} já está em uso por outro app. Escolha outra combinação.";
        Toast(ok ? $"Atalho alterado para {nome}." : $"Não consegui registrar {nome}.");
    }

    private void RefreshAccount()
    {
        var signedIn = Firebase.SignedIn;
        AccountLine.Text = signedIn ? Firebase.Email ?? "conta conectada" : "Sem conta vinculada";
        AccountBox.Text = signedIn
            ? $"{Store.Current.UserName} · {Firebase.Email}"
            : "Sem conta vinculada";
        SyncStatus.Text = signedIn
            ? Sync.LastStatus
            : "Os comandos funcionam sem conta. Entrar só serve para sincronizar.";
        UidBox.Text = Firebase.Uid is { Length: > 8 } uid
            ? $"uid {uid[..8]}…"
            : "sem sessão";
        LoginButton.Content = signedIn ? "Trocar de conta" : "Entrar na conta";
        AccountHint.Text = signedIn
            ? "Sessão salva com DPAPI: só este usuário do Windows continua conectado."
            : "Use a mesma conta do site. Se ainda não tem, crie aqui mesmo.";
    }

    private void AccountLogin_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)System.Windows.Application.Current;
        if (Firebase.SignedIn) Firebase.SignOut();
        app.ShowLogin();
        RefreshAccount();
    }

    private async void SyncNow_Click(object sender, RoutedEventArgs e)
    {
        SyncStatus.Text = "Buscando no Firestore…";
        await Sync.PullAsync(true);
        RefreshAccount();
        Load();
    }

    private async void PushNow_Click(object sender, RoutedEventArgs e)
    {
        SyncStatus.Text = "Enviando deste PC…";
        var (ok, message) = await Sync.PushAsync();
        SyncStatus.Text = message;
        if (!ok) System.Windows.MessageBox.Show(message, "Orbi — sincronização");
        RefreshAccount();
    }

    private void AccountSignOut_Click(object sender, RoutedEventArgs e)
    {
        Firebase.SignOut();
        Store.SaveSession(null);
        RefreshAccount();
        Toast("Você saiu da conta. O app continua funcionando com os dados locais.");
    }

    /* ----------------------------- navegação ----------------------------- */
    private void Nav_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Pages is null) return;
        var tag = (Nav.SelectedItem as ListBoxItem)?.Tag as string ?? "geral";
        foreach (var page in new[] { PageGeral, PageIa, PageConta, PageLembretes, PageNotas, PageAtalhos, PageApelidos, PageSeguranca, PageSobre })
        {
            page.Visibility = Visibility.Collapsed;
        }
        var target = tag switch
        {
            "ia" => PageIa,
            "conta" => PageConta,
            "lembretes" => PageLembretes,
            "notas" => PageNotas,
            "atalhos" => PageAtalhos,
            "apelidos" => PageApelidos,
            "seguranca" => PageSeguranca,
            "sobre" => PageSobre,
            _ => PageGeral,
        };
        target.Visibility = Visibility.Visible;
    }

    /* ----------------------------- ações ----------------------------- */
    private void SaveGeneral_Click(object sender, RoutedEventArgs e)
    {
        Store.Current.UserName = NameBox.Text.Trim();
        var wantsStartup = StartupToggle.IsChecked == true;
        if (wantsStartup != Store.Current.StartWithWindows)
        {
            SystemActions.StartWithWindows(wantsStartup);
        }
        Store.Current.UseGemini = GeminiToggle.IsChecked == true;
        Store.Save();
        _ = Sync.PushFieldsAsync(new System.Text.Json.Nodes.JsonObject
        {
            ["name"] = Firebase.Str(Store.Current.UserName),
            ["appPrefs"] = Firebase.Map(new System.Text.Json.Nodes.JsonObject
            {
                ["startWithWindows"] = Firebase.Bool(Store.Current.StartWithWindows),
                ["volume"] = Firebase.Num(Store.Current.Volume),
                ["lockEnabled"] = Firebase.Bool(Store.Current.LockEnabled),
                ["lockAfterMinutes"] = Firebase.Num(Store.Current.LockAfterMinutes),
                ["geminiModel"] = Firebase.Str(Store.Current.GeminiModel),
            }),
        });
        Toast("Preferências salvas e enviadas para a conta.");
    }

    private void SaveKey_Click(object sender, RoutedEventArgs e)
    {
        var key = ApiKeyBox.Password.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            KeyStatus.Text = "Cole a chave antes de salvar.";
            return;
        }
        Store.SaveApiKey(key);
        Store.Current.UseGemini = true;
        Store.Save();
        KeyStatus.Text = "Chave salva e cifrada com DPAPI.";
        GeminiToggle.IsChecked = true;
    }

    private async void TestKey_Click(object sender, RoutedEventArgs e)
    {
        var key = ApiKeyBox.Password.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            KeyStatus.Text = "Cole a chave para testar.";
            return;
        }
        KeyStatus.Text = "Testando…";
        var (ok, message) = await Gemini.TestAsync(key);
        KeyStatus.Text = message;
        KeyStatus.Foreground = ok ? Brushes.ForestGreen : Brushes.Firebrick;
    }

    private void RemoveKey_Click(object sender, RoutedEventArgs e)
    {
        Store.SaveApiKey("");
        ApiKeyBox.Password = "";
        KeyStatus.Text = "Chave removida deste PC.";
        GeminiState.Text = "Sem chave: comandos locais funcionam, perguntas livres não.";
    }

    private void SaveSecurity_Click(object sender, RoutedEventArgs e)
    {
        Store.Current.LockEnabled = LockToggle.IsChecked == true;
        if (int.TryParse(LockMinutes.Text.Trim(), out var minutes))
        {
            Store.Current.LockAfterMinutes = Math.Clamp(minutes, 1, 120);
        }
        Store.Save();
        _ = Sync.PushFieldsAsync(new System.Text.Json.Nodes.JsonObject
        {
            ["appPrefs"] = Firebase.Map(new System.Text.Json.Nodes.JsonObject
            {
                ["startWithWindows"] = Firebase.Bool(Store.Current.StartWithWindows),
                ["volume"] = Firebase.Num(Store.Current.Volume),
                ["lockEnabled"] = Firebase.Bool(Store.Current.LockEnabled),
                ["lockAfterMinutes"] = Firebase.Num(Store.Current.LockAfterMinutes),
                ["geminiModel"] = Firebase.Str(Store.Current.GeminiModel),
            }),
        });
        Toast("Segurança atualizada e enviada para a conta.");
    }

    private void OpenSite_Click(object sender, RoutedEventArgs e) => OpenUrl(Firebase.SiteUrl);

    private void OpenReleases_Click(object sender, RoutedEventArgs e) => OpenUrl("https://github.com/Arthurowgg/TheOrbiApp/releases");
    private void OpenBar_Click(object sender, RoutedEventArgs e) => ((App)System.Windows.Application.Current).ShowBar();

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Store.Save();
        ((App)System.Windows.Application.Current).ExitApp();
    }

    private void AddNote_Click(object sender, RoutedEventArgs e)
    {
        var text = NoteBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;
        Store.Current.Notes.Insert(0, new Note { Text = text });
        Store.Save();
        NoteBox.Text = "";
        RenderNotes();
    }

    private void AddShortcut_Click(object sender, RoutedEventArgs e)
    {
        var trigger = ShortcutTrigger.Text.Trim();
        var text = ShortcutText.Text.Trim();
        if (string.IsNullOrWhiteSpace(trigger) || string.IsNullOrWhiteSpace(text)) return;
        Store.Current.Shortcuts.RemoveAll(item => item.Trigger.Equals(trigger, StringComparison.OrdinalIgnoreCase));
        Store.Current.Shortcuts.Add(new TextShortcut { Trigger = trigger, Expansion = text });
        Store.Save();
        ShortcutTrigger.Text = "";
        ShortcutText.Text = "";
        RenderShortcuts();
        _ = Sync.PushAsync();
    }

    private void AddAlias_Click(object sender, RoutedEventArgs e)
    {
        var alias = AliasName.Text.Trim();
        var target = AliasTarget.Text.Trim();
        if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(target)) return;
        Store.Current.Aliases.RemoveAll(item => item.Alias.Equals(alias, StringComparison.OrdinalIgnoreCase));
        Store.Current.Aliases.Add(new AliasEntry { Alias = alias, Target = target });
        Store.Save();
        AliasName.Text = "";
        AliasTarget.Text = "";
        RenderAliases();
        _ = Sync.PushAsync();
    }

    /* ----------------------------- listas ----------------------------- */
    private void RenderReminders()
    {
        RemindersList.Items.Clear();
        var pending = Store.Current.Reminders.OrderBy(item => item.DueAt).ToList();
        if (pending.Count == 0)
        {
            RemindersList.Items.Add(new TextBlock { Text = "Nenhum lembrete. Peça: “me lembra em 20 min de beber água”.", Foreground = (Brush)FindResource("Warm") });
            return;
        }
        foreach (var reminder in pending)
        {
            var panel = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var text = new StackPanel();
            text.Children.Add(new TextBlock
            {
                Text = reminder.Text,
                FontWeight = reminder.Done ? FontWeights.Normal : FontWeights.SemiBold,
                TextDecorations = reminder.Done ? TextDecorations.Strikethrough : null,
                Foreground = (Brush)FindResource("Ink"),
            });
            text.Children.Add(new TextBlock
            {
                Text = reminder.DueAt.ToString("dd/MM HH:mm"),
                FontFamily = (FontFamily)FindResource("Mono"),
                FontSize = 10.5,
                Foreground = (Brush)FindResource("Warm"),
            });
            Grid.SetColumn(text, 0);
            panel.Children.Add(text);

            var remove = new Button
            {
                Content = "remover",
                Style = (Style)FindResource("GhostButton"),
                Tag = reminder.Id,
            };
            remove.Click += (_, _) =>
            {
                Store.Current.Reminders.RemoveAll(item => item.Id == reminder.Id);
                Store.Save();
                RenderReminders();
            };
            Grid.SetColumn(remove, 1);
            panel.Children.Add(remove);

            RemindersList.Items.Add(panel);
        }
    }

    private void RenderNotes()
    {
        NotesList.Items.Clear();
        foreach (var note in Store.Current.Notes.Take(20).ToList())
        {
            var panel = new Grid { Margin = new Thickness(0, 6, 0, 6) };
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var text = new StackPanel();
            text.Children.Add(new TextBlock
            {
                Text = note.Text,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)FindResource("Ink"),
            });
            text.Children.Add(new TextBlock
            {
                Text = note.CreatedAt.ToString("dd/MM HH:mm"),
                FontFamily = (FontFamily)FindResource("Mono"),
                FontSize = 10.5,
                Foreground = (Brush)FindResource("Warm"),
            });
            Grid.SetColumn(text, 0);
            panel.Children.Add(text);

            var remove = new Button { Content = "remover", Style = (Style)FindResource("GhostButton") };
            remove.Click += (_, _) =>
            {
                Store.Current.Notes.RemoveAll(item => item.Id == note.Id);
                Store.Save();
                RenderNotes();
            };
            Grid.SetColumn(remove, 1);
            panel.Children.Add(remove);
            NotesList.Items.Add(panel);
        }
    }

    private void RenderShortcuts()
    {
        ShortcutsList.Items.Clear();
        foreach (var shortcut in Store.Current.Shortcuts.ToList())
        {
            var row = new TextBlock
            {
                Margin = new Thickness(0, 4, 0, 4),
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)FindResource("Ink2"),
                Text = $"“{shortcut.Trigger}” → {shortcut.Expansion}",
            };
            ShortcutsList.Items.Add(row);
        }
        if (ShortcutsList.Items.Count == 0)
        {
            ShortcutsList.Items.Add(new TextBlock { Text = "Nenhum atalho ainda.", Foreground = (Brush)FindResource("Warm") });
        }
    }

    private void RenderAliases()
    {
        AliasesList.Items.Clear();
        foreach (var alias in Store.Current.Aliases.ToList())
        {
            AliasesList.Items.Add(new TextBlock
            {
                Margin = new Thickness(0, 4, 0, 4),
                Foreground = (Brush)FindResource("Ink2"),
                Text = $"“{alias.Alias}” → {alias.Target}",
            });
        }
        if (AliasesList.Items.Count == 0)
        {
            AliasesList.Items.Add(new TextBlock { Text = "Nenhum apelido ainda.", Foreground = (Brush)FindResource("Warm") });
        }
    }

    /* ----------------------------- utilidades ----------------------------- */
    private void Toast(string message)
    {
        KeyStatus.Text = message;
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // ignorar
        }
    }

    public void SelectTab(string tag)
    {
        foreach (ListBoxItem item in Nav.Items)
        {
            if ((item.Tag as string) == tag)
            {
                Nav.SelectedItem = item;
                break;
            }
        }
    }

    public void Refresh() => Load();

}
