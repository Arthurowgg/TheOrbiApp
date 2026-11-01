using System.IO;
using System.Text;
using System.Text.Json;

namespace Orbi.Services;

/// <summary>Uma coisa que aconteceu: um pedido, uma nota, um app aberto.</summary>
public sealed class MemoryEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public DateTime At { get; set; } = DateTime.Now;
    public string Kind { get; set; } = "pedido";   // pedido · nota · lembrete · rotina
    public string Text { get; set; } = "";
    public string? Answer { get; set; }
}

/// <summary>
/// Memória do Orbi: ele guarda o que você pediu, o que foi feito e o que você
/// anotou — e usa isso para responder perguntas sobre você ("o que eu tava
/// fazendo ontem?", "qual era o site que eu abri?").
///
/// Fica em %APPDATA%\Orbi\memoria.json (últimos 500 registros) e, quando você
/// está na conta, sobe para o Firestore como as outras preferências.
/// </summary>
public static class Memory
{
    private const int Max = 500;
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private static List<MemoryEntry> _entries = new();
    private static bool _loaded;

    private static string Path => System.IO.Path.Combine(Store.Root, "memoria.json");

    public static IReadOnlyList<MemoryEntry> All
    {
        get { Load(); lock (Gate) return _entries.ToList(); }
    }

    private static void Load()
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            if (File.Exists(Path))
            {
                _entries = JsonSerializer.Deserialize<List<MemoryEntry>>(File.ReadAllText(Path, Encoding.UTF8)) ?? new();
            }
        }
        catch
        {
            _entries = new(); // arquivo corrompido: começa limpo
        }
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(Store.Root);
            lock (Gate) File.WriteAllText(Path, JsonSerializer.Serialize(_entries, Options), Encoding.UTF8);
        }
        catch
        {
            // memória é conveniência: falhar aqui não pode derrubar o app
        }
    }

    /// <summary>Registra algo que aconteceu. Silencioso e sem repetição exata.</summary>
    public static void Remember(string text, string kind = "pedido", string? answer = null)
    {
        text = (text ?? "").Trim();
        if (text.Length == 0) return;
        if (text.Length > 400) text = text[..400];

        Load();
        lock (Gate)
        {
            // o mesmo pedido repetido em menos de 2 minutos não vira duas linhas
            var last = _entries.FirstOrDefault();
            if (last is not null && last.Text == text && (DateTime.Now - last.At).TotalMinutes < 2) return;

            _entries.Insert(0, new MemoryEntry { Text = text, Kind = kind, Answer = answer });
            if (_entries.Count > Max) _entries.RemoveRange(Max, _entries.Count - Max);
        }
        Save();
    }

    public static void Forget()
    {
        Load();
        lock (Gate) _entries.Clear();
        Save();
    }

    /// <summary>Recebe a memória que veio do Firestore e junta com a local.</summary>
    public static void Adopt(IEnumerable<MemoryEntry> remotas)
    {
        Load();
        lock (Gate)
        {
            foreach (var remota in remotas)
            {
                if (string.IsNullOrWhiteSpace(remota.Text)) continue;
                if (_entries.Any(local => local.Text == remota.Text &&
                                          Math.Abs((local.At - remota.At).TotalMinutes) < 2))
                {
                    continue;
                }
                _entries.Add(remota);
            }

            _entries = _entries.OrderByDescending(entry => entry.At).Take(Max).ToList();
        }
        Save();
    }

    /// <summary>Resumo curto do dia — usado pela IA e pelo painel de memória.</summary>
    public static string TodaySummary()
    {
        var hoje = All.Where(entry => entry.At.Date == DateTime.Now.Date).Take(12).ToList();
        if (hoje.Count == 0) return "nada registrado hoje";
        return string.Join(" · ", hoje.Select(entry => $"{entry.At:HH:mm} {entry.Text}"));
    }

    /// <summary>
    /// Responde perguntas sobre a própria memória, sem internet e sem IA.
    /// Ex.: "o que eu fiz ontem?", "o que eu abri hoje?", "me lembra o que anotei".
    /// Devolve null quando a pergunta não é sobre memória.
    /// </summary>
    public static string? Answer(string question)
    {
        var q = question.ToLowerInvariant();
        if (!(q.Contains("o que eu") || q.Contains("que eu fiz") || q.Contains("que eu abri") ||
              q.Contains("que eu pedi") || q.Contains("que eu anotei") || q.Contains("meu dia") ||
              q.Contains("minha memoria") || q.Contains("minha memória") || q.Contains("historico") ||
              q.Contains("histórico") || q.Contains("lembra do que")))
        {
            return null;
        }

        var alvo = q.Contains("ontem")
            ? DateTime.Now.Date.AddDays(-1)
            : q.Contains("semana") ? DateTime.MinValue : DateTime.Now.Date;

        var entradas = All
            .Where(entry => alvo == DateTime.MinValue
                ? entry.At >= DateTime.Now.AddDays(-7)
                : entry.At.Date == alvo)
            .Take(10)
            .ToList();

        if (entradas.Count == 0)
        {
            return alvo == DateTime.Now.Date
                ? "Ainda não registrei nada hoje. Quando você pedir alguma coisa, eu anoto."
                : "Não tenho nada registrado desse período.";
        }

        var titulo = alvo == DateTime.Now.Date ? "Hoje eu registrei isto:"
            : alvo == DateTime.MinValue ? "Nesta semana eu registrei:"
            : $"Sobre {alvo:dd/MM}, eu registrei:";

        var linhas = new StringBuilder(titulo);
        foreach (var entry in entradas)
        {
            linhas.AppendLine();
            linhas.Append($"· {entry.At:HH:mm} — {entry.Text}");
            if (!string.IsNullOrWhiteSpace(entry.Answer)) linhas.Append($" ({entry.Answer})");
        }
        return linhas.ToString();
    }

    /// <summary>Perguntas prontas que a barra sugere (mostram que a memória existe).</summary>
    public static IReadOnlyList<string> Suggestions()
    {
        var sugestoes = new List<string> { "o que eu fiz hoje?", "o que eu fiz ontem?" };
        if (All.Count(entry => entry.Kind == "nota") > 0) sugestoes.Add("o que eu anotei?");
        return sugestoes;
    }
}
