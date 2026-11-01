using System.Text.Json.Nodes;
using System.Windows.Threading;

namespace Orbi.Services;

/// <summary>
/// Sincronização com o Firestore — a mesma coleção `users/{uid}` que o site usa.
///
/// - PULL: traz apelidos, atalhos, rotinas, preferências e o nome da conta.
/// - PUSH: manda o que foi editado no app (só campos que o app controla),
///   quase sempre com updateMask parcial, para nunca sobrescrever o site.
/// - LOOP: a cada 45 s confere a versão remota; se mudou, aplica.
///
/// Nada sensível trafega: notas, área de transferência, lembretes e a chave
/// do Gemini continuam só no PC.
/// </summary>
public static class Sync
{
    private static readonly DispatcherTimer Timer = new() { Interval = TimeSpan.FromSeconds(45) };
    private static DateTime _lastPush = DateTime.MinValue;
    private static bool _running;

    public static string LastStatus { get; private set; } = "Sem conta vinculada.";
    public static DateTime? LastSync { get; private set; }
    public static event Action? Changed;

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

    private static void Notify() => Changed?.Invoke();

    private static async Task TickAsync()
    {
        if (!Firebase.SignedIn) return;
        // Depois de um push, só conferimos o remoto de tempos em tempos.
        if ((DateTime.Now - _lastPush).TotalSeconds < 30) return;
        await PullAsync(false);
    }

    /* ------------------------------ PULL ------------------------------ */
    public static async Task PullAsync(bool announce)
    {
        if (!Firebase.SignedIn) return;
        var fields = await Firebase.GetDocAsync();
        if (fields is null)
        {
            LastStatus = "Documento ainda não existe no Firestore.";
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

        // apelidos
        var aliases = Firebase.ReadList(fields, "aliases")
            .Select(item => (
                Alias: item["mapValue"]?["fields"]?["alias"]?["stringValue"]?.GetValue<string>() ?? "",
                Target: item["mapValue"]?["fields"]?["target"]?["stringValue"]?.GetValue<string>() ?? ""))
            .Where(item => !string.IsNullOrWhiteSpace(item.Alias) && !string.IsNullOrWhiteSpace(item.Target))
            .ToList();
        // Presença do campo (e não quantidade) define quem manda: se você apagar
        // todos os apelidos no site, o app também limpa a lista local.
        if (fields["aliases"] is not null)
        {
            settings.Aliases = aliases.Select(item => new AliasEntry { Alias = item.Alias, Target = item.Target }).ToList();
            changes++;
        }

        // atalhos de texto
        var shortcuts = Firebase.ReadList(fields, "shortcuts")
            .Select(item => (
                Trigger: item["mapValue"]?["fields"]?["trigger"]?["stringValue"]?.GetValue<string>() ?? "",
                Expansion: item["mapValue"]?["fields"]?["expansion"]?["stringValue"]?.GetValue<string>() ?? ""))
            .Where(item => !string.IsNullOrWhiteSpace(item.Trigger) && !string.IsNullOrWhiteSpace(item.Expansion))
            .ToList();
        if (fields["shortcuts"] is not null)
        {
            settings.Shortcuts = shortcuts
                .Select(item => new TextShortcut { Trigger = item.Trigger, Expansion = item.Expansion })
                .ToList();
            changes++;
        }

        // rotinas
        var routines = Firebase.ReadList(fields, "routines").ToList();
        if (fields["routines"] is not null)
        {
            settings.Routines = routines
                .Select(node =>
                {
                    var fields2 = node["mapValue"]?["fields"];
                    var name = fields2?["name"]?["stringValue"]?.GetValue<string>() ?? "";
                    var commands = fields2?["commands"]?["arrayValue"]?["values"]?.AsArray()
                        .Select(command => command?["stringValue"]?.GetValue<string>() ?? "")
                        .Where(command => !string.IsNullOrWhiteSpace(command))
                        .ToList() ?? new List<string>();
                    return new Routine { Name = name, Commands = commands };
                })
                .Where(routine => !string.IsNullOrWhiteSpace(routine.Name) && routine.Commands.Count > 0)
                .ToList();
            changes++;
        }

        // preferências do app
        var prefs = Firebase.ReadMap(fields, "appPrefs");
        if (prefs is not null)
        {
            settings.StartWithWindows = Firebase.ReadBool(prefs, "startWithWindows", settings.StartWithWindows);
            settings.Volume = Firebase.ReadInt(prefs, "volume", settings.Volume);
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
            LastStatus = "Conta vinculada — nada novo para importar.";
            Notify();
        }
    }

    /* ------------------------------ PUSH ------------------------------ */
    public static async Task<(bool Ok, string Message)> PushAsync()
    {
        if (!Firebase.SignedIn) return (false, "Entre na conta para sincronizar.");

        var settings = Store.Current;
        var fields = new JsonObject
        {
            ["name"] = Firebase.Str(settings.UserName),
            ["lastSyncAt"] = Firebase.Timestamp(DateTime.UtcNow),
            ["aliases"] = Firebase.List(settings.Aliases.Select(alias => Firebase.Map(new JsonObject
            {
                ["alias"] = Firebase.Str(alias.Alias),
                ["target"] = Firebase.Str(alias.Target),
            }))),
            ["shortcuts"] = Firebase.List(settings.Shortcuts.Select(shortcut => Firebase.Map(new JsonObject
            {
                ["trigger"] = Firebase.Str(shortcut.Trigger),
                ["expansion"] = Firebase.Str(shortcut.Expansion),
            }))),
            ["routines"] = Firebase.List(settings.Routines.Select(routine => Firebase.Map(new JsonObject
            {
                ["name"] = Firebase.Str(routine.Name),
                ["commands"] = Firebase.List(routine.Commands.Select(command => Firebase.Str(command))),
            }))),
            ["appPrefs"] = Firebase.Map(new JsonObject
            {
                ["startWithWindows"] = Firebase.Bool(settings.StartWithWindows),
                ["volume"] = Firebase.Num(settings.Volume),
                ["lockEnabled"] = Firebase.Bool(settings.LockEnabled),
                ["lockAfterMinutes"] = Firebase.Num(settings.LockAfterMinutes),
                ["geminiModel"] = Firebase.Str(settings.GeminiModel),
            }),
        };

        // `color` é da cor escolhida no site (Perfil). O app só manda uma cor
        // padrão quando suspeita que o documento ainda não existe — numa conta
        // já criada, o PATCH com updateMask não toca no campo.
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

    /// <summary>Envia um campo específico (ex.: depois de mudar uma preferência).</summary>
    public static async Task PushFieldsAsync(JsonObject fields)
    {
        if (!Firebase.SignedIn) return;
        var (ok, message) = await Firebase.PatchAsync(fields);
        LastStatus = ok ? $"Enviado às {DateTime.Now:HH:mm}." : message;
        if (ok) _lastPush = DateTime.Now;
        Notify();
    }
}
