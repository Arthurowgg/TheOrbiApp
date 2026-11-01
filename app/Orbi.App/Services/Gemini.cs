using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Orbi.Services;

/// <summary>
/// Ponte com o Gemini (Google AI Studio). A chave fica cifrada no PC com DPAPI;
/// o app nunca envia nada além do texto do comando.
/// </summary>
public static class Gemini
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(25) };

    public static bool Available => Store.Current.UseGemini && Store.HasApiKey();

    public static async Task<string?> AskAsync(string prompt, IEnumerable<(string Role, string Text)>? history = null)
    {
        var key = Store.LoadApiKey();
        if (string.IsNullOrWhiteSpace(key)) return null;

        var model = string.IsNullOrWhiteSpace(Store.Current.GeminiModel)
            ? "gemini-2.0-flash"
            : Store.Current.GeminiModel;

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={Uri.EscapeDataString(key)}";

        var contents = new List<object>();
        var system = "Você é a Orbi, assistente do Windows. Responda em português do Brasil, " +
                     "direto, no máximo 3 frases. Se o pedido puder ser resolvido por um comando do app, " +
                     "diga o comando exato (ex.: “abre o zap”, “volume em 30”, “me lembra em 20 min”). " +
                     "Você não executa ações: apenas responde e sugere comandos. " +
                     $"O usuário se chama {Store.Current.UserName}. Data: {DateTime.Now:dd/MM/yyyy}.";

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
            systemInstruction = new { parts = new[] { new { text = system } } },
            contents,
            generationConfig = new { temperature = 0.6, maxOutputTokens = 400 },
        };

        try
        {
            using var body = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await Client.PostAsync(url, body);
            var json = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                // 400/403 costuma ser chave inválida; devolvemos null para cair no modo local.
                return null;
            }

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0) return null;
            var candidate = candidates[0];
            if (!candidate.TryGetProperty("content", out var content)) return null;
            if (!content.TryGetProperty("parts", out var parts) || parts.GetArrayLength() == 0) return null;

            var builder = new StringBuilder();
            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var text)) builder.Append(text.GetString());
            }
            var answer = builder.ToString().Trim();
            return string.IsNullOrWhiteSpace(answer) ? null : answer;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Testa a chave com uma chamada mínima.</summary>
    public static async Task<(bool Ok, string Message)> TestAsync(string key)
    {
        var previous = Store.LoadApiKey();
        Store.SaveApiKey(key);
        try
        {
            var answer = await AskAsync("Responda apenas: ok");
            return answer is null
                ? (false, "A chave não foi aceita pelo Google. Confira se ela foi copiada inteira e se a Generative Language API está ativa.")
                : (true, "Chave válida. O Gemini já pode responder aos comandos que fogem do script.");
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(previous) && previous != key) Store.SaveApiKey(previous);
        }
    }
}
