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
