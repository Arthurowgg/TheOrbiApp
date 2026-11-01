using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Orbi.Services;

/// <summary>
/// Modo Cloud: o Orbi responde com o Gemini em vez de só com o catálogo local.
///
/// A chave é pessoal e gratuita (Google AI Studio, camada Spark). Ela fica
/// cifrada com DPAPI em %APPDATA%\Orbi e só é usada aqui no PC.
///
/// Sem chave — ou se o Google não responder — o Orbi continua funcionando:
/// 120+ ferramentas executam no PC sem internet nenhuma.
/// </summary>
public static class Gemini
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(20) };

    /// <summary>Modelos gratuitos, do mais novo ao mais antigo — o primeiro que responder ganha.</summary>
    public static readonly string[] Models =
    {
        "gemini-2.5-flash",
        "gemini-2.5-flash-lite",
        "gemini-2.0-flash",
    };

    public const string KeyPage = "https://aistudio.google.com/apikey";

    public static bool HasKey => !string.IsNullOrWhiteSpace(Store.LoadApiKey());

    /// <summary>
    /// O Orbi é IA-first: sem chave ele não adivinha. O catálogo de ferramentas
    /// continua na barra (é sistema, não IA), mas qualquer pedido em texto livre
    /// passa a pedir a chave.
    /// </summary>
    public static bool Available => HasKey;

    public static string Mode => HasKey ? "Gemini" : "sem chave";

    public static async Task<string?> AskAsync(string prompt, IEnumerable<(string Role, string Text)>? history = null,
        string? memory = null)
    {
        if (!Available) return null;
        var key = Store.LoadApiKey();
        if (string.IsNullOrWhiteSpace(key)) return null;

        var preferred = Store.Current.GeminiModel;
        var order = Models.Where(model => model != preferred).Prepend(preferred)
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Distinct()
            .ToList();

        foreach (var model in order)
        {
            var answer = await CallAsync(model, key, prompt, history, memory);
            if (answer is not null)
            {
                Store.Current.GeminiModel = model;
                Store.Save();
                return answer;
            }
        }
        return null;
    }

    private static async Task<string?> CallAsync(string model, string key, string prompt,
        IEnumerable<(string Role, string Text)>? history, string? memory)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent" +
                  $"?key={Uri.EscapeDataString(key)}";

        var contents = new List<object>();
        if (history is not null)
        {
            foreach (var (role, text) in history.TakeLast(6))
            {
                contents.Add(new { role = role == "user" ? "user" : "model", parts = new[] { new { text } } });
            }
        }
        contents.Add(new { role = "user", parts = new[] { new { text = prompt } } });

        var payload = new
        {
            systemInstruction = new { parts = new[] { new { text = System(memory) } } },
            contents,
            generationConfig = new { temperature = 0.5, maxOutputTokens = 400 },
        };

        try
        {
            using var body = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await Client.PostAsync(url, body);
            if (!response.IsSuccessStatusCode)
            {
                Log.Add("gemini recusou", model + " " + (int)response.StatusCode);
                return null;
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = document.RootElement;
            if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0) return null;
            if (!candidates[0].TryGetProperty("content", out var content)) return null;
            if (!content.TryGetProperty("parts", out var parts) || parts.GetArrayLength() == 0) return null;

            var builder = new StringBuilder();
            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var text)) builder.Append(text.GetString());
            }
            var answer = builder.ToString().Trim();
            return string.IsNullOrWhiteSpace(answer) ? null : answer;
        }
        catch (Exception error)
        {
            Log.Error("gemini " + model, error);
            return null;
        }
    }

    private static string System(string? memory)
    {
        var name = string.IsNullOrWhiteSpace(Store.Current.UserName) ? "amigo" : Store.Current.UserName;
        return "Você é o Orbi, o assistente do Windows do " + name + ". " +
               "Responda em português do Brasil, direto, no máximo 3 frases. " +
               "Você roda dentro de um app que executa ações no PC: quando a pessoa pedir algo do sistema, " +
               "responda com o comando exato em uma linha, começando com “/”. " +
               "Ferramentas disponíveis: " + Tools.ForAi() + ". " +
               "Exemplo: para abrir o gerenciador de tarefas, responda “/task-manager — abrindo”. " +
               "Nunca invente comandos fora dessa lista. " +
               $"Hoje é {DateTime.Now:dd/MM/yyyy}, agora são {DateTime.Now:HH:mm}. " +
               "Memória do dia: " + (string.IsNullOrWhiteSpace(memory) ? "nada registrado ainda." : memory);
    }

    /// <summary>Testa a chave com uma chamada mínima e devolve o primeiro modelo que respondeu.</summary>
    public static async Task<(bool Ok, string Message)> TestAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return (false, "Cole a chave antes de testar.");
        foreach (var model in Models)
        {
            var answer = await CallAsync(model, key.Trim(), "Responda apenas: ok", null, null);
            if (answer is not null)
            {
                Store.SaveApiKey(key.Trim());
                Store.Current.GeminiModel = model;
                Store.Save();
                return (true, $"Chave válida. Modo Cloud ativo com {model}.");
            }
        }
        return (false, "O Google não aceitou a chave. Confira se copiou inteira e se a Generative Language API está ativa.");
    }
}
