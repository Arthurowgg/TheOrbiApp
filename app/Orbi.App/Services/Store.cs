using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Orbi.Services;

/// <summary>
/// O que o app guarda em %APPDATA%\Orbi\orbi.json. Com conta, a memória e as
/// preferências acompanham você (Services/Sync.cs).
/// </summary>
public sealed class OrbiSettings
{
    public string UserName { get; set; } = "";
    public string? AccountEmail { get; set; }

    // Sessão da conta, cifrada com DPAPI junto da chave da IA.
    public string? IdToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? Uid { get; set; }
    public long TokenExpiresAt { get; set; }

    // Última sincronização com a conta (texto curto mostrado nos painéis).
    public string LastSyncLabel { get; set; } = "";

    // Cliente OAuth do Google usado pelo login dentro do app. É público (não é
    // segredo): o que protege o login é o PKCE e o retorno só em 127.0.0.1.
    public string GoogleClientId { get; set; } = "";

    // Marcador de que o documento da conta já existe (evita criar de novo).
    public string RemoteCreatedAt { get; set; } = "";
    public bool StartWithWindows { get; set; } = true;
    public string GeminiModel { get; set; } = "gemini-2.0-flash";
    public int Volume { get; set; } = 50;
    public int LockAfterMinutes { get; set; } = 5;
    public bool LockEnabled { get; set; }
    public List<Reminder> Reminders { get; set; } = new();
    public List<string> ClipboardHistory { get; set; } = new();
    public List<TextShortcut> Shortcuts { get; set; } = new();
    public List<string> Commands { get; set; } = new();

    // Já avisamos que o catálogo funciona sem chave e como ligar a conversa?
    public bool ToldAboutCloud { get; set; }

    // Já mostramos a janela de boas-vindas ("O Orbi está aberto")?
    public bool BoasVindas { get; set; }

    // Atalho global preferido ("Alt+Shift+O", "Ctrl+Shift+O", ...). Vazio = padrão.
    public string Hotkey { get; set; } = "Alt+Shift+O";

    // Onde a barra ficou da última vez (DIP). Null = ainda não posicionada.
    public double? BarLeft { get; set; }
    public double? BarTop { get; set; }
    public double BarWidth { get; set; } = 660;
    public double BarHeight { get; set; } = 62;
}

public sealed class Reminder
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Text { get; set; } = "";
    public DateTime DueAt { get; set; }
    public bool Done { get; set; }
}

public sealed class TextShortcut
{
    public string Trigger { get; set; } = "";
    public string Expansion { get; set; } = "";
}

/// <summary>Persistência em JSON + segredos no DPAPI (por usuário).</summary>
public static class Store
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Orbi");

    private static string SettingsPath => Path.Combine(Root, "orbi.json");
    private static string SecretPath => Path.Combine(Root, "gemini.bin");

    private static OrbiSettings? _cached;

    public static OrbiSettings Current
    {
        get
        {
            if (_cached is not null) return _cached;
            try
            {
                Directory.CreateDirectory(Root);
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath, Encoding.UTF8);
                    _cached = JsonSerializer.Deserialize<OrbiSettings>(json, Options) ?? new OrbiSettings();
                }
            }
            catch
            {
                // Arquivo corrompido: começamos limpo em vez de travar o app.
            }
            _cached ??= new OrbiSettings();
            if (string.IsNullOrWhiteSpace(_cached.UserName))
            {
                _cached.UserName = Environment.UserName;
            }
            return _cached;
        }
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Root);
            var json = JsonSerializer.Serialize(Current, Options);
            var temp = SettingsPath + ".tmp";
            File.WriteAllText(temp, json, Encoding.UTF8);
            File.Move(temp, SettingsPath, true);
        }
        catch
        {
            // Sem permissão de escrita: o app continua funcionando em memória.
        }
    }

    /// <summary>Guarda a chave do Gemini cifrada com DPAPI (só este usuário do Windows lê).</summary>
    public static void SaveApiKey(string key)
    {
        try
        {
            Directory.CreateDirectory(Root);
            if (string.IsNullOrWhiteSpace(key))
            {
                if (File.Exists(SecretPath)) File.Delete(SecretPath);
                return;
            }
            var bytes = Encoding.UTF8.GetBytes(key.Trim());
            var cipher = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(SecretPath, cipher);
        }
        catch
        {
            // Sem DPAPI disponível, a chave simplesmente não é guardada.
        }
    }

    public static string? LoadApiKey()
    {
        try
        {
            if (!File.Exists(SecretPath)) return null;
            var cipher = File.ReadAllBytes(SecretPath);
            var bytes = ProtectedData.Unprotect(cipher, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return null;
        }
    }

    public static bool HasApiKey() => !string.IsNullOrWhiteSpace(LoadApiKey());

    /* ---------------------- sessão da conta (DPAPI) ---------------------- */
    private static string SessionPath => Path.Combine(Root, "session.bin");

    public static void SaveSession(FirebaseSession? session)
    {
        try
        {
            Directory.CreateDirectory(Root);
            if (session is null || string.IsNullOrWhiteSpace(session.RefreshToken))
            {
                if (File.Exists(SessionPath)) File.Delete(SessionPath);
                var settings = Current;
                settings.IdToken = null;
                settings.RefreshToken = null;
                settings.Uid = null;
                settings.TokenExpiresAt = 0;
                Save();
                return;
            }

            var json = JsonSerializer.Serialize(session, Options);
            var cipher = ProtectedData.Protect(Encoding.UTF8.GetBytes(json), null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(SessionPath, cipher);
        }
        catch
        {
            // sem DPAPI: a sessão não persiste entre reinícios
        }
    }

    public static FirebaseSession? LoadSession()
    {
        try
        {
            if (!File.Exists(SessionPath)) return null;
            var cipher = File.ReadAllBytes(SessionPath);
            var bytes = ProtectedData.Unprotect(cipher, null, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<FirebaseSession>(Encoding.UTF8.GetString(bytes), Options);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Texto curto de quando a conta foi sincronizada ("agora", "14:32").</summary>
    public static string LastSyncText
    {
        get
        {
            var rotulo = Current.LastSyncLabel;
            return string.IsNullOrWhiteSpace(rotulo) ? "aguardando" : "às " + rotulo;
        }
    }

    public static void PushClipboard(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var list = Current.ClipboardHistory;
        list.RemoveAll(item => string.Equals(item, text, StringComparison.Ordinal));
        list.Insert(0, text.Length > 2000 ? text[..2000] : text);
        if (list.Count > 30) list.RemoveRange(30, list.Count - 30);
        Save();
    }

    public static void PushCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return;
        var list = Current.Commands;
        list.RemoveAll(item => string.Equals(item, command, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, command.Trim());
        if (list.Count > 40) list.RemoveRange(40, list.Count - 40);
        Save();
    }
}
