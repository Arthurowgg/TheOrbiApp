using System.Text.Json.Nodes;
using System.Windows.Threading;

namespace Orbi.Services;

/// <summary>
/// Mantém a memória e as preferências iguais entre o app e a conta — o mesmo
/// documento `users/{uid}` que o site usa.
///
/// - PULL: traz memória, preferências e o nome da conta.
/// - PUSH: manda o que mudou no app com updateMask parcial, sem sobrescrever
///   outros campos da conta.
/// - LOOP: a cada 45 s confere se a conta mudou.
///
/// A chave da IA, o histórico de cópia e os lembretes não sobem: ficam no app.
/// </summary>
public static class Sync
{
    private static readonly DispatcherTimer Timer = new() { Interval = TimeSpan.FromSeconds(45) };
    private static DateTime _lastPush = DateTime.MinValue;
    private static bool _running;

    public static string LastStatus { get; private set; } = "Sem conta vinculada.";
    public static DateTime? LastSync { get; private set; }
    public static event Action? Changed;

    public static void Now()
    {
        try { Start(); Log.Add("sync manual pedido"); }
        catch (Exception error) { Log.Error("sync manual", error); }
    }

    public static void Start()
    {
        if (_running) return;
        _running = true;
        Firebase.Changed += () =>
        {
            LastStatus = Firebase.SignedIn ? "Conectando…" : "Sem conta vinculada.";
            Notify();
            if (Firebase.SignedIn) _ = PullAsync(true);
        };
        Timer.Tick += async (_, _) => await TickAsync();
        Timer.Start();

        if (Firebase.SignedIn) _ = PullAsync(true);
    }

    private static void Notify()
    {
        Store.Current.LastSyncLabel = LastSync is { } when ? when.ToString("HH:mm") : "";
        Changed?.Invoke();
    }

    private static async Task TickAsync()
    {
        if (!Firebase.SignedIn) return;
        if ((DateTime.Now - _lastPush).TotalSeconds < 30) return;
        await PullAsync(false);
    }

    /* ------------------------------ pull ------------------------------ */
    public static async Task PullAsync(bool announce)
    {
        if (!Firebase.SignedIn) return;
        var fields = await Firebase.GetDocAsync();
        if (fields is null)
        {
            LastStatus = "Ainda não há nada nesta conta.";
            Notify();
            return;
        }

        var settings = Store.Current;
        var changes = 0;

        settings.RemoteCreatedAt = fields["createdAt"]?["timestampValue"]?.GetValue<string>() ?? "";

        var remoteName = Firebase.ReadString(fields, "name");
        if (!string.IsNullOrWhiteSpace(remoteName) && remoteName != settings.UserName)
        {
            settings.UserName = remoteName;
            changes++;
        }

        settings.AccountEmail = Firebase.ReadString(fields, "email", Firebase.Email ?? "");

        // memória
        if (fields["memory"] is not null)
        {
            var lembrancas = Firebase.ReadList(fields, "memory")
                .Select(item =>
                {
                    var campos = item["mapValue"]?["fields"];
                    var quando = campos?["at"]?["timestampValue"]?.GetValue<string>();
                    return new MemoryEntry
                    {
                        Kind = campos?["kind"]?["stringValue"]?.GetValue<string>() ?? "pedido",
                        Text = campos?["text"]?["stringValue"]?.GetValue<string>() ?? "",
                        Answer = campos?["answer"]?["stringValue"]?.GetValue<string>(),
                        At = DateTime.TryParse(quando, out var at) ? at.ToLocalTime() : DateTime.Now,
                    };
                })
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Text))
                .OrderByDescending(entry => entry.At)
                .Take(200)
                .ToList();

            if (lembrancas.Count > 0) Memory.Adopt(lembrancas);
            changes++;
        }

        // preferências
        var prefs = Firebase.ReadMap(fields, "appPrefs");
        if (prefs is not null)
        {
            settings.StartWithWindows = Firebase.ReadBool(prefs, "startWithWindows", settings.StartWithWindows);
            settings.LockEnabled = Firebase.ReadBool(prefs, "lockEnabled", settings.LockEnabled);
            settings.LockAfterMinutes = Firebase.ReadInt(prefs, "lockAfterMinutes", settings.LockAfterMinutes);
            var model = Firebase.ReadString(prefs, "geminiModel", settings.GeminiModel);
            if (!string.IsNullOrWhiteSpace(model)) settings.GeminiModel = model;
            changes++;
        }

        Store.Save();
        LastSync = DateTime.Now;
        LastStatus = $"Sincronizado às {LastSync:HH:mm}.";
        Notify();

        if (announce && changes == 0)
        {
            LastStatus = "Conta vinculada — nada novo por aqui.";
            Notify();
        }
    }

    /* ------------------------------ push ------------------------------ */
    public static async Task<(bool Ok, string Message)> PushAsync()
    {
        if (!Firebase.SignedIn) return (false, "Entre na conta para sincronizar.");

        var settings = Store.Current;
        var fields = new JsonObject
        {
            ["name"] = Firebase.Str(settings.UserName),
            ["lastSyncAt"] = Firebase.Timestamp(DateTime.UtcNow),
            ["memory"] = Firebase.List(Memory.All.Take(120).Select(entry => Firebase.Map(new JsonObject
            {
                ["text"] = Firebase.Str(entry.Text),
                ["kind"] = Firebase.Str(entry.Kind),
                ["answer"] = Firebase.Str(entry.Answer ?? ""),
                ["at"] = Firebase.Timestamp(entry.At.ToUniversalTime()),
            }))),
            ["appPrefs"] = Prefs(),
        };

        if (string.IsNullOrWhiteSpace(Store.Current.RemoteCreatedAt))
        {
            fields["color"] = Firebase.Str("#c7452a");
        }

        var (ok, message) = await Firebase.PatchAsync(fields);
        if (ok)
        {
            _lastPush = DateTime.Now;
            LastSync = DateTime.Now;
            LastStatus = $"Enviado às {LastSync:HH:mm}.";
        }
        else
        {
            LastStatus = message;
        }
        Notify();
        return (ok, message);
    }

    /// <summary>Envia as preferências atuais (depois de mudar algo nos ajustes).</summary>
    public static Task PushFieldsAsync(JsonObject fields)
    {
        if (!Firebase.SignedIn) return Task.CompletedTask;
        return Enviar(fields);
    }

    public static Task PushPrefsAsync() => PushFieldsAsync(new JsonObject { ["appPrefs"] = Prefs() });

    private static async Task Enviar(JsonObject fields)
    {
        var (ok, message) = await Firebase.PatchAsync(fields);
        LastStatus = ok ? $"Enviado às {DateTime.Now:HH:mm}." : message;
        if (ok) _lastPush = DateTime.Now;
        Notify();
    }

    private static JsonNode Prefs() => Firebase.Map(new JsonObject
    {
        ["startWithWindows"] = Firebase.Bool(Store.Current.StartWithWindows),
        ["lockEnabled"] = Firebase.Bool(Store.Current.LockEnabled),
        ["lockAfterMinutes"] = Firebase.Num(Store.Current.LockAfterMinutes),
        ["geminiModel"] = Firebase.Str(Store.Current.GeminiModel),
    });
}
