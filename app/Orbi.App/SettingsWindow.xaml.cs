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
        LockToggle.IsChecked = settings.LockEnabled;
        LockMinutes.Text = settings.LockAfterMinutes.ToString();
        ApiKeyBox.Password = Store.LoadApiKey() ?? "";
        GeminiState.Text = Store.HasApiKey()
            ? "Chave configurada e cifrada neste PC."
            : "Nenhuma chave ainda: o Orbi não responde perguntas livres sem ela.";
        RefreshAccount();
        LoadHotkeys();
        ModelBox.SelectedIndex = Math.Max(0, Gemini.Models.ToList().IndexOf(settings.GeminiModel));

        LoadTools();
        RenderMemory();
    }

    private void OpenKeyPage_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(Gemini.KeyPage) { UseShellExecute = true });
        }
        catch (Exception error)
        {
            KeyStatus.Text = "Não abri o navegador: " + error.Message;
        }
    }

    /* ----------------------------- ferramentas ----------------------------- */
    private void LoadTools()
    {
        ToolsCount.Text = $"{Tools.All.Count} ações";
        RenderTools(Tools.All);
    }

    private void RenderTools(IEnumerable<Tool> tools)
    {
        ToolsList.ItemsSource = tools
            .Select(tool => new { tool.Group, tool.Name, tool.Id })
            .ToList();
    }

    private void ToolsSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = ToolsSearch.Text.Trim();
        RenderTools(string.IsNullOrWhiteSpace(query) ? Tools.All : Tools.Search(query));
    }

    private void ToolsList_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Enter) return;
        RunSelectedTool();
        e.Handled = true;
    }

    private void ToolsList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) => RunSelectedTool();

    private void RunSelectedTool()
    {
        if (ToolsList.SelectedItem is null) return;
        var id = ToolsList.SelectedItem.GetType().GetProperty("Id")?.GetValue(ToolsList.SelectedItem) as string;
        if (string.IsNullOrWhiteSpace(id)) return;
        ToolsStatus.Text = Tools.Run(id);
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
        AccountLine.Text = signedIn ? Firebase.Email ?? "conta" : "sem conta";
        AccountBox.Text = signedIn
            ? $"{Store.Current.UserName} · {Firebase.Email}"
            : "Sem conta vinculada";
        SyncStatus.Text = signedIn ? Sync.LastStatus : "O Orbi funciona sem conta.";

        LoginButton.Content = signedIn ? "Trocar" : "Entrar";
        AccountHint.Text = signedIn ? "" : "Use a mesma conta do site.";
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
        SyncStatus.Text = "Sincronizando…";
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
        foreach (var page in new[] { PageGeral, PageFerramentas, PageIa, PageConta, PageMemoria, PageSeguranca, PageSobre })
        {
            page.Visibility = Visibility.Collapsed;
        }
        var target = tag switch
        {
            "ferramentas" => PageFerramentas,
            "ia" => PageIa,
            "conta" => PageConta,
            "memoria" or "lembretes" or "notas" or "atalhos" => PageMemoria,
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
        Store.Save();
        KeyStatus.Text = "Chave salva e cifrada com DPAPI.";
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
        GeminiState.Text = "Chave removida. Cole outra para o Orbi voltar a responder.";
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

    private void ForgetMemory_Click(object sender, RoutedEventArgs e)
    {
        Memory.Forget();
        RenderMemory();
        MemoriaStatus.Text = "Memória apagada.";
    }

    /* ----------------------------- listas ----------------------------- */
    private void RenderMemory()
    {
        var entradas = Memory.All;
        MemoriaList.Items.Clear();
        MemoriaResumo.Text = entradas.Count == 0
            ? "Ainda não há nada registrado. Peça alguma coisa na barra e volte aqui."
            : $"{entradas.Count} registros · os mais recentes aparecem primeiro.";

        foreach (var entry in entradas.Take(80))
        {
            MemoriaList.Items.Add(new TextBlock
            {
                Margin = new Thickness(0, 4, 0, 4),
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)FindResource("Ink2"),
                Text = $"{entry.At:dd/MM HH:mm} · {entry.Kind} · {entry.Text}",
            });
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
