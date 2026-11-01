using System.Windows;
using Orbi.Services;

namespace Orbi;

/// <summary>Entrar / criar conta no app usando o mesmo Firebase do site.</summary>
public partial class LoginWindow : Window
{
    private bool _signup;

    public LoginWindow()
    {
        InitializeComponent();
        EmailBox.Text = Store.Current.AccountEmail ?? "";
        UpdateMode();
        Loaded += (_, _) => EmailBox.Focus();
    }

    private void UpdateMode()
    {
        Headline.Text = _signup
            ? "Crie a conta do Orbi depois de instalar. O que for seu acompanha você."
            : "Entre para a sua memória acompanhar você.";
        PrimaryButton.Content = _signup ? "Criar conta" : "Entrar";
        SecondaryButton.Content = _signup ? "Já tenho conta" : "Criar uma conta";
    }

    private async void Primary_Click(object sender, RoutedEventArgs e)
    {
        await RunAsync(EmailBox.Text, PasswordBox.Password);
    }

    private void Secondary_Click(object sender, RoutedEventArgs e)
    {
        _signup = !_signup;
        ErrorText.Visibility = Visibility.Collapsed;
        UpdateMode();
    }

    private async void Forgot_Click(object sender, RoutedEventArgs e)
    {
        var email = EmailBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            ShowError("Digite o e-mail primeiro — o link vai para ele.");
            return;
        }
        StatusText.Text = "Enviando link…";
        var (ok, message) = await Firebase.SendResetAsync(email);
        StatusText.Text = message;
        if (!ok) ShowError(message);
    }

    private async Task RunAsync(string email, string password)
    {
        ErrorText.Visibility = Visibility.Collapsed;
        SetBusy(true, _signup ? "Criando conta…" : "Entrando…");
        try
        {
            var (ok, message) = _signup
                ? await Firebase.SignUpAsync(email, password)
                : await Firebase.SignInAsync(email, password);

            if (!ok)
            {
                ShowError(message);
                StatusText.Text = "";
                return;
            }

            // Primeira sincronização: traz o que existe na conta.
            await Sync.PullAsync(false);
            await Sync.PushAsync();

            StatusText.Text = $"{message} Conta: {Firebase.Email}.";
            DialogResult = true;
            Close();
        }
        finally
        {
            SetBusy(false, null);
        }
    }

    private void SetBusy(bool busy, string? status)
    {
        PrimaryButton.IsEnabled = !busy;
        SecondaryButton.IsEnabled = !busy;
        PasswordBox.IsEnabled = !busy;
        EmailBox.IsEnabled = !busy;
        if (status is not null) StatusText.Text = status;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
