using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Orbi.Services;

/// <summary>Uma chamada de função que o modelo pediu.</summary>
public sealed record FuncaoChamada(string Nome, string Argumentos);

/// <summary>Como terminou uma rodada de conversa.</summary>
public sealed record RespostaModelo(string Texto, List<FuncaoChamada> Chamadas, bool Ok, string? Erro);

/// <summary>
/// O cérebro do Orbi: Gemini, direto da API do Google AI Studio.
///
/// - <b>function calling nativo</b>: o modelo recebe as ferramentas como funções
///   declaradas (não como texto no prompt) e responde com
///   <c>functionCall</c> — quem executa é o app;
/// - <b>streaming</b>: o texto aparece conforme chega (SSE do
///   <c>:streamGenerateContent</c>);
/// - <b>várias rodadas</b>: cada resultado de ferramenta volta para o modelo,
///   que decide o próximo passo (o agente em <see cref="Orbi"/> cuida do laço);
/// - <b>chave do usuário</b>: a chave é dele, fica cifrada no PC e nunca sobe
///   para outro lugar que não seja o próprio Google.
/// </summary>
public static class Gemini
{
    /// <summary>Modelos do AI Studio, do mais capaz ao mais econômico.</summary>
    public static readonly string[] Models =
    {
        "gemini-2.5-flash",
        "gemini-2.5-flash-lite",
        "gemini-2.0-flash",
    };

    public const string KeyPage = "https://aistudio.google.com/apikey";

    private const string Base = "https://generativelanguage.googleapis.com/v1beta/models/";

    private static readonly HttpClient Cliente = Criar();

    private static HttpClient Criar()
    {
        var cliente = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        cliente.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Orbi/" + Versao);
        return cliente;
    }

    private static string Versao => typeof(Gemini).Assembly.GetName().Version?.ToString(3) ?? "1.2";

    public static bool HasKey => !string.IsNullOrWhiteSpace(Store.LoadApiKey());

    /// <summary>IA-first: sem chave não há resposta livre; as ferramentas locais continuam.</summary>
    public static bool Available => HasKey;

    public static string Mode => HasKey
        ? (string.IsNullOrWhiteSpace(Store.Current.GeminiModel) ? Models[0] : Store.Current.GeminiModel)
        : "sem chave";

    private static string Chave() => Store.LoadApiKey()?.Trim() ?? "";

    /* ------------------------------ conversa ------------------------------ */

    /// <summary>
    /// Uma rodada de conversa com o modelo. <paramref name="contents"/> já vem no
    /// formato do Gemini (histórico + mensagem nova) e é devolvido atualizado?
    /// Não: quem mantém o histórico é o agente — aqui só respondemos uma vez.
    /// </summary>
    public static async Task<RespostaModelo> ConversarAsync(
        JsonArray contents,
        JsonArray? funcoes,
        string sistema,
        Action<string>? aoPensar = null,
        Action<string>? aoEscrever = null,
        CancellationToken token = default)
    {
        if (!Available) return new RespostaModelo("", new(), false, "sem_chave");

        string? ultimoErro = null;
        foreach (var modelo in Ordem())
        {
            var (resposta, erro, podeTentarOutro) = await UmaVezAsync(
                modelo, contents, funcoes, sistema, aoPensar, aoEscrever, token);

            if (resposta is not null)
            {
                if (Store.Current.GeminiModel != modelo)
                {
                    Store.Current.GeminiModel = modelo;
                    Store.Save();
                }
                return resposta;
            }

            ultimoErro = erro ?? ultimoErro;

            // chave inválida ou limite diário: outro modelo gratuito pode responder,
            // mas se a chave está errada nenhum responde — não insiste.
            if (erro is "chave_invalida") break;
            if (!podeTentarOutro) break;
        }

        return new RespostaModelo("", new(), false, ultimoErro ?? "sem_resposta");
    }

    private static async Task<(RespostaModelo? Resposta, string? Erro, bool PodeTentarOutro)> UmaVezAsync(
        string modelo, JsonArray contents, JsonArray? funcoes, string sistema,
        Action<string>? aoPensar, Action<string>? aoEscrever, CancellationToken token)
    {
        var corpo = new JsonObject
        {
            ["system_instruction"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["text"] = sistema }) },
            ["contents"] = contents.DeepClone(),
            ["generationConfig"] = new JsonObject
            {
                ["temperature"] = 0.7,
                ["topP"] = 0.95,
                ["maxOutputTokens"] = 2048,
            },
        };
        if (funcoes is { Count: > 0 })
        {
            corpo["tools"] = new JsonArray(new JsonObject { ["functionDeclarations"] = funcoes.DeepClone() });
            corpo["toolConfig"] = new JsonObject
            {
                ["functionCallingConfig"] = new JsonObject { ["mode"] = "AUTO" },
            };
        }

        var url = $"{Base}{modelo}:streamGenerateContent?alt=sse&key={Uri.EscapeDataString(Chave())}";
        try
        {
            using var pedido = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(corpo.ToJsonString(), Encoding.UTF8, "application/json"),
            };
            pedido.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

            using var resposta = await Cliente.SendAsync(pedido, HttpCompletionOption.ResponseHeadersRead, token);
            if (!resposta.IsSuccessStatusCode)
            {
                var erro = await resposta.Content.ReadAsStringAsync(token);
                var (codigo, tentarOutro) = LerErro(erro, (int)resposta.StatusCode);
                Log.Add($"gemini {modelo}: HTTP {(int)resposta.StatusCode} {codigo}");
                return (null, codigo, tentarOutro);
            }

            var texto = new StringBuilder();
            var pensamento = new StringBuilder();
            var chamadas = new List<FuncaoChamada>();

            await using var fluxo = await resposta.Content.ReadAsStreamAsync(token);
            using var leitor = new StreamReader(fluxo, Encoding.UTF8);
            while (await leitor.ReadLineAsync(token) is { } linha)
            {
                token.ThrowIfCancellationRequested();
                if (!linha.StartsWith("data:", StringComparison.Ordinal)) continue;
                var dados = linha[5..].Trim();
                if (dados.Length == 0 || dados == "[DONE]") continue;

                JsonDocument pedaco;
                try { pedaco = JsonDocument.Parse(dados); }
                catch { continue; }

                using (pedaco)
                {
                    if (!pedaco.RootElement.TryGetProperty("candidates", out var candidatos)) continue;
                    if (candidatos.GetArrayLength() == 0) continue;
                    var candidato = candidatos[0];
                    if (!candidato.TryGetProperty("content", out var conteudo)) continue;
                    if (!conteudo.TryGetProperty("parts", out var partes)) continue;

                    foreach (var parte in partes.EnumerateArray())
                    {
                        if (parte.TryGetProperty("functionCall", out var chamada))
                        {
                            var nome = chamada.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                            var argumentos = chamada.TryGetProperty("args", out var a) ? a.GetRawText() : "{}";
                            if (nome.Length > 0) chamadas.Add(new FuncaoChamada(nome, argumentos));
                            continue;
                        }

                        if (!parte.TryGetProperty("text", out var trecho)) continue;
                        var valor = trecho.GetString() ?? "";
                        if (valor.Length == 0) continue;

                        var ePensamento = parte.TryGetProperty("thought", out var t) && t.ValueKind == JsonValueKind.True;
                        if (ePensamento)
                        {
                            pensamento.Append(valor);
                            aoPensar?.Invoke(valor);
                        }
                        else
                        {
                            texto.Append(valor);
                            aoEscrever?.Invoke(valor);
                        }
                    }
                }
            }

            var pronto = (texto.Length > 0 ? texto.ToString() : (pensamento.Length > 0 ? pensamento.ToString() : ""));
            return (new RespostaModelo(pronto, chamadas, true, null), null, false);
        }
        catch (OperationCanceledException)
        {
            return (new RespostaModelo("", new(), false, "cancelado"), "cancelado", false);
        }
        catch (Exception error)
        {
            Log.Error("gemini " + modelo, error);
            return (null, "rede", true);
        }
    }

    /// <summary>Traduz o erro do Google para algo que dê para mostrar na tela.</summary>
    private static (string Codigo, bool TentarOutro) LerErro(string json, int status)
    {
        var mensagem = "";
        try
        {
            using var documento = JsonDocument.Parse(json);
            if (documento.RootElement.TryGetProperty("error", out var erro) &&
                erro.TryGetProperty("message", out var texto))
            {
                mensagem = texto.GetString() ?? "";
            }
        }
        catch { }

        var minusculo = mensagem.ToLowerInvariant();
        if (minusculo.Contains("api key not valid") || minusculo.Contains("api_key_invalid"))
        {
            return ("chave_invalida", false);
        }
        if (minusculo.Contains("quota") || status == 429)
        {
            return ("limite", true);
        }
        if (minusculo.Contains("not found") || status == 404)
        {
            return ("modelo_indisponivel", true);
        }
        if (status is 500 or 502 or 503 or 504)
        {
            return ("google_fora", true);
        }
        return (status == 400 ? "pedido_invalido" : "falha", true);
    }

    private static List<string> Ordem()
    {
        var preferido = Store.Current.GeminiModel;
        return Models.Where(modelo => modelo != preferido).Prepend(preferido)
            .Where(modelo => !string.IsNullOrWhiteSpace(modelo))
            .Distinct()
            .ToList();
    }

    /// <summary>Mensagem pronta para a interface a partir do código de erro.</summary>
    public static string Explicar(string? erro) => erro switch
    {
        "sem_chave" => "Falta a chave da IA. Abra Ajustes ▸ IA e cole a sua chave do Google AI Studio (é gratuita).",
        "chave_invalida" => "O Google recusou a chave. Confira se copiou inteira (começa com AIza) e se a Generative Language API está ativa.",
        "limite" => "Bati no limite gratuito por agora. Espere um minuto ou troque o modelo em Ajustes ▸ IA.",
        "modelo_indisponivel" => "Esse modelo não está disponível para a sua chave. Troque o modelo em Ajustes ▸ IA.",
        "google_fora" => "O Google não respondeu agora. Tente de novo em instantes.",
        "rede" => "Sem conexão com o Google. Confira a internet.",
        "cancelado" => "Parei a pedido seu.",
        _ => "Não consegui pensar agora. Tente de novo em instantes.",
    };

    /* ------------------------------ compatibilidade ------------------------------ */

    /// <summary>Pergunta simples (sem ferramentas) — usada por telas que só querem um texto.</summary>
    public static async Task<string?> AskAsync(string prompt, IEnumerable<(string Role, string Text)>? history = null,
        string? memory = null)
    {
        if (!Available) return null;
        var contents = new JsonArray();
        if (history is not null)
        {
            foreach (var (papel, texto) in history.TakeLast(6))
            {
                contents.Add(new JsonObject
                {
                    ["role"] = papel == "user" ? "user" : "model",
                    ["parts"] = new JsonArray(new JsonObject { ["text"] = texto }),
                });
            }
        }
        contents.Add(new JsonObject
        {
            ["role"] = "user",
            ["parts"] = new JsonArray(new JsonObject { ["text"] = prompt }),
        });

        var resposta = await ConversarAsync(contents, null, Sistema(memory));
        return resposta.Ok && resposta.Texto.Length > 0 ? resposta.Texto : null;
    }

    /// <summary>Uma chamada de texto para quem precisa de algo curto (resumos).</summary>
    public static async Task<string?> CompleteAsync(string prompt, string? system, bool json = false,
        int maxTokens = 400, CancellationToken token = default)
    {
        if (!Available) return null;
        var contents = new JsonArray(new JsonObject
        {
            ["role"] = "user",
            ["parts"] = new JsonArray(new JsonObject { ["text"] = prompt }),
        });
        var resposta = await ConversarAsync(contents, null, system ?? Sistema(null),
            null, null, token);
        if (!resposta.Ok) return null;
        var texto = resposta.Texto.Trim();
        if (json)
        {
            var inicio = texto.IndexOf('{');
            var fim = texto.LastIndexOf('}');
            if (inicio >= 0 && fim > inicio) texto = texto[inicio..(fim + 1)];
        }
        return texto.Length == 0 ? null : texto;
    }

    public static string Sistema(string? memory)
    {
        var nome = string.IsNullOrWhiteSpace(Store.Current.UserName) ? "amigo" : Store.Current.UserName;
        return "Você é o Orbi, o assistente do Windows de " + nome + ". " +
               "Responda em português do Brasil, direto, no máximo 3 frases. " +
               $"Hoje é {DateTime.Now:dd/MM/yyyy}, agora são {DateTime.Now:HH:mm}. " +
               "Memória do dia: " + (string.IsNullOrWhiteSpace(memory) ? "nada registrado ainda." : memory);
    }

    /// <summary>Testa a chave com a chamada mais barata possível.</summary>
    public static async Task<(bool Ok, string Message)> TestAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return (false, "Cole a chave antes de testar.");

        foreach (var modelo in Models)
        {
            var corpo = new JsonObject
            {
                ["contents"] = new JsonArray(new JsonObject
                {
                    ["role"] = "user",
                    ["parts"] = new JsonArray(new JsonObject { ["text"] = "Responda apenas: ok" }),
                }),
                ["generationConfig"] = new JsonObject { ["maxOutputTokens"] = 16 },
            };
            try
            {
                using var pedido = new HttpRequestMessage(HttpMethod.Post,
                    $"{Base}{modelo}:generateContent?key={Uri.EscapeDataString(key.Trim())}")
                {
                    Content = new StringContent(corpo.ToJsonString(), Encoding.UTF8, "application/json"),
                };
                using var resposta = await Cliente.SendAsync(pedido);
                var json = await resposta.Content.ReadAsStringAsync();

                if (resposta.IsSuccessStatusCode)
                {
                    Store.SaveApiKey(key.Trim());
                    Store.Current.GeminiModel = modelo;
                    Store.Save();
                    return (true, $"Chave válida. O Orbi pensa com {modelo}.");
                }

                var (codigo, _) = LerErro(json, (int)resposta.StatusCode);
                if (codigo == "chave_invalida") return (false, Explicar(codigo));
            }
            catch (Exception error)
            {
                Log.Error("testar chave", error);
                return (false, "Sem conexão com o Google agora.");
            }
        }

        return (false, "A chave foi aceita pela conta, mas nenhum modelo respondeu. Tente de novo em instantes.");
    }
}
