using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Orbi.Services;

namespace Orbi;

public partial class SettingsWindow : Window
{

    public SettingsWindow()
    {
        InitializeComponent();
        VersionLine.Text = "Orbi " + (typeof(App).Assembly.GetName().Version?.ToString(3) ?? "?") +
                           " · Windows 11 x64";
        Load();
        MostrarEstadoGoogle();
    }

    /* ------------------------- entrar com o Google ------------------------- */

    private void MostrarEstadoGoogle()
    {
        GoogleClientBox.Text = Store.Current.GoogleClientId;
        GoogleState.Text = GoogleLogin.Disponivel
            ? "Pronto — a conta pode entrar pelo Google dentro do app."
            : string.IsNullOrWhiteSpace(Store.Current.GoogleClientId)
                ? "Falta o identificador do cliente OAuth."
                : "Identificador salvo, mas este PC não tem o componente de navegador (WebView2).";
    }

    private async void SalvarGoogle_Click(object sender, RoutedEventArgs e)
    {
        var texto = GoogleClientBox.Text.Trim();
        if (texto.Length > 0 && !GoogleLogin.IdValido(texto))
        {
            GoogleState.Text = "Isso não parece um identificador do Google (termina em .apps.googleusercontent.com).";
            return;
        }
        GoogleLogin.ClientId = texto;
        MostrarEstadoGoogle();
        if (GoogleLogin.Disponivel) await Sync.PushFieldsAsync(new System.Text.Json.Nodes.JsonObject { ["googleReady"] = Firebase.Bool(true) });
    }

    private async void TestarGoogle_Click(object sender, RoutedEventArgs e)
    {
        if (!GoogleLogin.Disponivel)
        {
            MostrarEstadoGoogle();
            return;
        }
        GoogleState.Text = "Abrindo a janela do Google…";
        // await, e não espera bloqueante: a janela do Google precisa da thread da interface.
        var (ok, mensagem) = await GoogleLogin.EntrarAsync(this);
        GoogleState.Text = mensagem;
        if (ok)
        {
            App.Instance.RefreshAccount();
            _ = Sync.PullAsync(true);
        }
    }


    /* ----------------------------- carregar ----------------------------- */
    private void Load()
    {
        var settings = Store.Current;
        NameBox.Text = settings.UserName;
        StartupToggle.IsChecked = settings.StartWithWindows;
        LockToggle.IsChecked = settings.LockEnabled;
        LockMinutes.Text = settings.LockAfterMinutes.ToString();
        LoadHotkeys();
    }

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
            : $"Em uso: {HotKey.Combination}.";
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
    }

    /* ----------------------------- salvar ----------------------------- */
    private void SaveAll_Click(object sender, RoutedEventArgs e)
    {
        var settings = Store.Current;
        settings.UserName = NameBox.Text.Trim();

        var wantsStartup = StartupToggle.IsChecked == true;
        if (wantsStartup != settings.StartWithWindows)
        {
            SystemActions.StartWithWindows(wantsStartup);
            settings.StartWithWindows = wantsStartup;
        }

        settings.LockEnabled = LockToggle.IsChecked == true;
        if (int.TryParse(LockMinutes.Text.Trim(), out var minutes))
        {
            settings.LockAfterMinutes = Math.Clamp(minutes, 1, 120);
        }

        Store.Save();
        _ = Sync.PushPrefsAsync();
        HotkeyState.Text = "Salvo.";
        App.Instance.ReturnBar();
    }

    /* ----------------------------- utilidades ----------------------------- */
    private void OpenBar_Click(object sender, RoutedEventArgs e) => App.Instance.ReturnBar();

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Store.Save();
        App.Instance.ExitApp();
    }

    /// <summary>
    /// Procura versão nova na release publicada. Se existir, o próprio botão vira
    /// o convite para instalar; se não, diz que está em dia — sem caixa de diálogo.
    /// </summary>
    private async void ProcurarAtualizacao_Click(object sender, RoutedEventArgs e)
    {
        UpdateButton.IsEnabled = false;
        UpdateButton.Content = "Procurando…";
        try
        {
            var nova = await Update.ProcurarAsync();
            if (nova is null)
            {
                UpdateButton.Content = "Você está na " + Update.Instalada + " — em dia";
                return;
            }

            UpdateButton.Content = "Instalar a " + nova.Versao;
            UpdateButton.Tag = nova;
            UpdateButton.Click -= ProcurarAtualizacao_Click;
            UpdateButton.Click += InstalarAtualizacao_Click;
            UpdateButton.IsEnabled = true;
        }
        catch (Exception error)
        {
            Log.Error("procurar atualização", error);
            UpdateButton.Content = "Não consegui verificar agora";
        }
        finally
        {
            UpdateButton.IsEnabled = true;
        }
    }

    private void InstalarAtualizacao_Click(object sender, RoutedEventArgs e)
    {
        if (UpdateButton.Tag is not Update.Disponivel nova) return;
        Update.Instalar(nova);
        UpdateButton.Content = "Instalador aberto — siga a instalação";
        UpdateButton.IsEnabled = false;
    }

    private void OpenSiteReleases_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://orbi-app.web.app/baixar") { UseShellExecute = true });
        }
        catch
        {
            // sem navegador agora
        }
    }
}
