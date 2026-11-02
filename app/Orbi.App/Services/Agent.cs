using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Orbi.Services;

/// <summary>Um passo do agente, como aparece no chat.</summary>
public sealed record AgentStep(string Title, string Detail, bool Ok = true, string? Icone = null);

/// <summary>Eventos do agente para a interface (chat animado em tempo real).</summary>
public sealed class AgentEvents
{
    /// <summary>Texto que o modelo está pensando/juntando (aparece enquanto chega).</summary>
    public Action<string>? Pensando { get; init; }
    /// <summary>Resposta em streaming.</summary>
    public Action<string>? Escrevendo { get; init; }
    /// <summary>Começou um passo (ferramenta ou controle).</summary>
    public Action<AgentStep>? Passo { get; init; }
}

/// <summary>
/// O agente do Orbi: uma IA com 598 ferramentas, function calling e vários passos.
///
/// O laço é o clássico de agente:
///   1. manda a conversa + as declarações de função;
///   2. o modelo responde com texto (resposta) e/ou <c>functionCall</c>;
///   3. o app executa cada chamada e devolve o resultado como
///      <c>functionResponse</c>;
///   4. volta ao passo 1 com o histórico atualizado, até o modelo parar de pedir
///      ferramentas (ou bater o teto de passos).
///
/// Nada é adivinhado: o resultado de cada ferramenta volta para o modelo antes do
/// próximo passo — é isso que permite “abrir o site, procurar o preço, anotar e
/// me avisar” funcionar em sequência.
/// </summary>
public static class Agent
{
    /// <summary>Quantas rodadas de ferramenta antes de pedir ao modelo que conclua.</summary>
    public const int MaxSteps = 10;

    /// <summary>Histórico da conversa (fica no app; sobe só a memória resumida).</summary>
    private static readonly List<(string Role, string Text)> Historico = new();

    /// <summary>Ferramentas já usadas nesta conversa: continuam declaradas.</summary>
    private static readonly HashSet<string> Usadas = new(StringComparer.OrdinalIgnoreCase);

    private static void GuardarUsada(string nomeFuncao)
    {
        var id = ToolSchema.Resolver(nomeFuncao);
        if (id is not null) Usadas.Add(id);
    }

    public static void LimparHistorico()
    {
        Historico.Clear();
        Usadas.Clear();
    }

    public static async Task<(string Answer, List<AgentStep> Steps, bool Ok)> RunAsync(
        string pedido,
        AgentEvents? eventos = null,
        CancellationToken token = default)
    {
        var passos = new List<AgentStep>();
        if (!Gemini.Available)
        {
            return (Gemini.Explicar("sem_chave"), passos, false);
        }

        var contents = new JsonArray();
        foreach (var (papel, texto) in Historico.TakeLast(8))
        {
            contents.Add(new JsonObject
            {
                ["role"] = papel == "user" ? "user" : "model",
                ["parts"] = new JsonArray(new JsonObject { ["text"] = texto }),
            });
        }
        contents.Add(new JsonObject
        {
            ["role"] = "user",
            ["parts"] = new JsonArray(new JsonObject { ["text"] = pedido }),
        });

        // O teto da API é 128 funções: vão as de controle + as que casam com o pedido.
        var declaracoes = ToolSchema.Declaracoes(pedido, Usadas);
        var sistema = Sistema(pedido);
        var nomesDeclarados = declaracoes
            .Select(item => item!["name"]!.GetValue<string>())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var textoFinal = new StringBuilder();
        var erroFinal = (string?)null;
        var usouFerramenta = false;

        try
        {
            for (var rodada = 0; rodada <= MaxSteps; rodada++)
            {
                token.ThrowIfCancellationRequested();

                var pedacos = new StringBuilder();
                var resposta = await Gemini.ConversarAsync(
                    contents, declaracoes, sistema,
                    aoPensar: eventos?.Pensando,
                    aoEscrever: trecho =>
                    {
                        pedacos.Append(trecho);
                        eventos?.Escrevendo?.Invoke(trecho);
                    },
                    token);

                if (!resposta.Ok)
                {
                    erroFinal = resposta.Erro;
                    break;
                }

                var texto = resposta.Texto.Trim();

                if (resposta.Chamadas.Count == 0)
                {
                    if (texto.Length > 0) textoFinal.Append(texto);
                    break;
                }

                // O modelo pediu ferramentas: guarda o que ele disse e devolve os resultados.
                var partesModelo = new JsonArray();
                if (texto.Length > 0)
                {
                    partesModelo.Add(new JsonObject { ["text"] = texto });
                }
                foreach (var chamada in resposta.Chamadas)
                {
                    partesModelo.Add(new JsonObject
                    {
                        ["functionCall"] = new JsonObject
                        {
                            ["name"] = chamada.Nome,
                            ["args"] = JsonNode.Parse(chamada.Argumentos) ?? new JsonObject(),
                        },
                    });
                }
                contents.Add(new JsonObject { ["role"] = "model", ["parts"] = partesModelo });

                var respostas = new JsonArray();
                foreach (var chamada in resposta.Chamadas)
                {
                    token.ThrowIfCancellationRequested();
                    var (passo, saida) = Executar(chamada.Nome, chamada.Argumentos);
                    passos.Add(passo);
                    GuardarUsada(chamada.Nome);
                    usouFerramenta = true;
                    eventos?.Passo?.Invoke(passo);

                    respostas.Add(new JsonObject
                    {
                        ["functionResponse"] = new JsonObject
                        {
                            ["name"] = chamada.Nome,
                            ["response"] = new JsonObject
                            {
                                ["resultado"] = saida.Resultado,
                                ["ok"] = saida.Ok,
                            },
                        },
                    });
                }
                contents.Add(new JsonObject { ["role"] = "user", ["parts"] = respostas });

                // Teto de passos: pede a conclusão em vez de cortar seco.
                if (rodada == MaxSteps)
                {
                    contents.Add(new JsonObject
                    {
                        ["role"] = "user",
                        ["parts"] = new JsonArray(new JsonObject
                        {
                            ["text"] = "Chega de ferramentas por agora: responda à pessoa com o que você já tem.",
                        }),
                    });
                    var ultimo = await Gemini.ConversarAsync(contents, null, sistema, null, null, token);
                    if (ultimo.Ok && ultimo.Texto.Trim().Length > 0) textoFinal.Append(ultimo.Texto.Trim());
                }
            }
        }
        catch (OperationCanceledException)
        {
            erroFinal = "cancelado";
        }

        var fala = textoFinal.ToString().Trim();
        if (fala.Length == 0)
        {
            fala = erroFinal is not null
                ? Gemini.Explicar(erroFinal)
                : usouFerramenta ? "Feito." : "Não tenho o que responder sobre isso.";
        }

        // Conversa vira memória: é o que permite “o que eu fazia ontem?”.
        Historico.Add(("user", pedido));
        Historico.Add(("assistant", fala));
        if (Historico.Count > 16) Historico.RemoveRange(0, Historico.Count - 16);
        Memory.Remember(pedido, usouFerramenta ? "ação" : "conversa", fala);

        return (fala, passos, erroFinal is null);
    }

    /* ------------------------------ execução ------------------------------ */

    private static (AgentStep Passo, (string Resultado, bool Ok) Saida) Executar(string nome, string argumentosJson)
    {
        JsonObject args;
        try { args = JsonNode.Parse(argumentosJson)?.AsObject() ?? new JsonObject(); }
        catch { args = new JsonObject(); }

        string Texto(string chave) => args[chave]?.GetValue<string>() ?? "";
        int Numero(string chave) =>
            int.TryParse(args[chave]?.ToString(), out var valor) ? valor : 0;

        try
        {
            switch (nome.ToLowerInvariant())
            {
                case "pc_ver_a_tela":
                {
                    var descricao = Pc.DescribeScreen();
                    return (new AgentStep("olhei a tela", PrimeiraLinha(descricao), true, "eye"),
                            (descricao, true));
                }

                case "pc_capturar_a_tela":
                {
                    var caminho = Pc.Capture();
                    return (new AgentStep("tirei uma foto da tela", caminho, true, "camera"),
                            ("Captura em " + caminho + ". " + Pc.ActiveWindowTitle(), true));
                }

                case "pc_clicar":
                {
                    var x = Numero("x");
                    var y = Numero("y");
                    var botao = string.IsNullOrWhiteSpace(Texto("botao")) ? "left" : Texto("botao");
                    if (x <= 0 || y <= 0) return (new AgentStep("clicar", "sem posição", false, "cursor"),
                                                     ("Preciso das coordenadas x e y.", false));
                    Pc.Click(x, y, botao);
                    return (new AgentStep("cliquei", $"{x},{y} ({botao})", true, "cursor"), ("feito", true));
                }

                case "pc_digitar":
                {
                    var texto = Texto("texto");
                    if (texto.Length == 0) return (new AgentStep("digitar", "sem texto", false, "keyboard"),
                                                   ("O texto veio vazio.", false));
                    Pc.Type(texto);
                    return (new AgentStep("digitei", Curto(texto), true, "keyboard"), ("feito", true));
                }

                case "pc_atalho":
                {
                    var teclas = Texto("teclas");
                    if (teclas.Length == 0) return (new AgentStep("atalho", "sem teclas", false, "keyboard"),
                                                    ("A combinação veio vazia.", false));
                    Pc.Hotkey(teclas);
                    return (new AgentStep("atalho " + teclas, "enviado", true, "keyboard"), ("feito", true));
                }

                case "pc_focar_janela":
                {
                    var alvo = Texto("alvo");
                    var ok = Pc.Focus(alvo);
                    return (new AgentStep("trouxe a janela", alvo, ok, "window"),
                            (ok ? "A janela “" + alvo + "” está na frente." : "Não achei uma janela com “" + alvo + "”.", ok));
                }

                case "pc_pesquisar_na_web":
                {
                    var consulta = Texto("consulta");
                    var (titulo, trecho, ok) = Web.PesquisarAsync(consulta).GetAwaiter().GetResult();
                    var saida = ok
                        ? $"Resultado principal: {titulo}. {trecho}"
                        : "A pesquisa não voltou resultados: " + trecho;
                    return (new AgentStep("pesquisei", consulta, ok, "search"), (saida, ok));
                }

                case "pc_ler_pagina":
                {
                    var endereco = Texto("endereco");
                    var texto = Web.LerAsync(endereco).GetAwaiter().GetResult();
                    var ok = !texto.StartsWith("Não consegui", StringComparison.OrdinalIgnoreCase);
                    var titulo = texto.Split('\n').FirstOrDefault() ?? "";
                    return (new AgentStep("li a página", Curto(titulo), ok, "page"), (texto, ok));
                }

                case "pc_abrir_no_navegador":
                {
                    var endereco = Texto("endereco");
                    var resolvido = Browser.Open(endereco);
                    return (new AgentStep("abri no navegador", resolvido, true, "globe"), ("aberto: " + resolvido, true));
                }

                case "pc_lembrar":
                {
                    var texto = Texto("texto");
                    var tipo = string.IsNullOrWhiteSpace(Texto("tipo")) ? "fato" : Texto("tipo");
                    Memory.Remember(texto, tipo);
                    return (new AgentStep("guardei na memória", Curto(texto), true, "book"), ("guardado", true));
                }

                case "pc_consultar_memoria":
                {
                    var termo = Texto("termo").ToLowerInvariant();
                    var achados = Memory.All
                        .Where(entrada => entrada.Text.ToLowerInvariant().Contains(termo) ||
                                          (entrada.Answer ?? "").ToLowerInvariant().Contains(termo))
                        .TakeLast(6)
                        .Select(entrada => $"{entrada.At:dd/MM HH:mm} · {entrada.Text} → {entrada.Answer}")
                        .ToList();
                    return (new AgentStep("consultei a memória", termo, true, "book"),
                            (achados.Count == 0 ? "Nada na memória sobre “" + termo + "”." : string.Join("\n", achados), true));
                }

                case "pc_usar_ferramenta":
                {
                    var id = Texto("id").Trim();
                    var dado = Texto("dado");
                    var ferramenta = Tools.Find(id) ?? Tools.Find(id.Replace('_', '-'));
                    if (ferramenta is null)
                    {
                        var achados = Tools.Search(id.Replace('_', ' ')).Take(5)
                            .Select(item => item.Id).ToList();
                        return (new AgentStep("não achei essa ferramenta", id, false, "question"),
                                ("Não existe ferramenta com esse id. " +
                                 (achados.Count > 0
                                     ? "Parecidas: " + string.Join(", ", achados) + "."
                                     : "Use pc_buscar_no_catalogo para achar a certa."), false));
                    }
                    var saiu = Tools.Run(ferramenta.Id, dado);
                    var foi = !saiu.StartsWith("Não consegui", StringComparison.OrdinalIgnoreCase) &&
                              !saiu.StartsWith("Preciso do dado", StringComparison.OrdinalIgnoreCase);
                    return (new AgentStep(ferramenta.Name, Curto(saiu), foi, IconeDe(ferramenta.Group)),
                            (saiu, foi));
                }

                case "pc_buscar_no_catalogo":
                {
                    var assunto = Texto("assunto");
                    var achados = Tools.Search(assunto).Take(12)
                        .Select(ferramenta => $"{ferramenta.Id} — {ferramenta.Name}" +
                                              (ferramenta.NeedsArg ? $" (dado: {ferramenta.Placeholder})" : ""))
                        .ToList();
                    return (new AgentStep("busquei no catálogo", assunto, true, "grid"),
                            (achados.Count == 0 ? "Nenhuma ferramenta para “" + assunto + "”." : string.Join("\n", achados), true));
                }
            }

            // Catálogo: nome da função → id da ferramenta
            if (!nomesDeclarados.Contains(nome))
            {
                // veio do pacote de outra conversa (ou o modelo inventou): o resolver ainda acha
                Log.Add("agente: função fora do pacote desta conversa: " + nome);
            }
            var id = ToolSchema.Resolver(nome);
            if (id is null)
            {
                return (new AgentStep("ferramenta desconhecida", nome, false, "question"),
                        ("Não existe ferramenta com esse nome. Use pc_buscar_no_catalogo para achar a certa.", false));
            }

            var ferramenta = Tools.Find(id);
            if (ferramenta is null)
            {
                return (new AgentStep(nome, "não está no catálogo", false, "question"), ("Ferramenta indisponível.", false));
            }

            var dado = args["dado"]?.ToString() ?? "";
            if (ferramenta.NeedsArg && string.IsNullOrWhiteSpace(dado))
            {
                var falta = "Preciso do dado: " + (ferramenta.Placeholder ?? "o complemento");
                return (new AgentStep(ferramenta.Name, falta, false, IconeDe(ferramenta.Group)), (falta, false));
            }

            var resultado = Tools.Run(ferramenta.Id, dado);
            var deuCerto = !resultado.StartsWith("Não consegui", StringComparison.OrdinalIgnoreCase) &&
                           !resultado.StartsWith("Preciso do dado", StringComparison.OrdinalIgnoreCase);
            return (new AgentStep(ferramenta.Name, Curto(resultado), deuCerto, IconeDe(ferramenta.Group)),
                    (resultado, deuCerto));
        }
        catch (Exception error)
        {
            Log.Error("agente " + nome, error);
            return (new AgentStep(nome, "falhou", false, "alert"),
                    ("A ferramenta falhou: " + error.Message, false));
        }
    }

    private static string IconeDe(string grupo) => grupo switch
    {
        "Arquivos e pastas" or "Pastas" => "folder",
        "Rede e internet" or "Rede" => "wifi",
        "Web e pesquisa" => "globe",
        "Cálculos" => "calc",
        "Datas" => "calendar",
        "Texto" or "Texto e números" or "Área de transferência" => "text",
        "Janelas e mouse" or "Janelas" => "window",
        "Tela" or "Tela e captura" => "eye",
        "Som e mídia" or "Áudio e vídeo" => "volume",
        "Configurações do Windows" or "Painéis" => "sliders",
        "Aplicativos" or "Apps" => "grid",
        "Energia e bateria" or "Energia" => "battery",
        "Sistema" => "chip",
        _ => "spark",
    };

    private static string Curto(string texto)
    {
        var limpo = texto.Replace("\r", " ").Replace("\n", " · ").Trim();
        return limpo.Length > 90 ? limpo[..90] + "…" : limpo;
    }

    private static string PrimeiraLinha(string texto)
    {
        var linha = texto.Split('\n').FirstOrDefault(l => l.Trim().Length > 0) ?? "";
        return Curto(linha);
    }

    /* ------------------------------ sistema ------------------------------ */

    private static string Sistema(string pedido)
    {
        var nome = string.IsNullOrWhiteSpace(Store.Current.UserName)
            ? "amigo"
            : Store.Current.UserName.Split(' ')[0];
        var janela = Pc.ActiveWindowTitle();
        var memoria = Memory.TodaySummary();
        var total = Tools.All.Count;
        var grupos = string.Join(", ", Tools.Groups.OrderBy(grupo => grupo));

        var texto = new StringBuilder();
        texto.AppendLine("Você é o Orbi, um agente que vive no Windows ao lado de " + nome + ".");
        texto.AppendLine("Fale português do Brasil, próximo e direto. Nada de “claro!”, nada de repetir o pedido.");
        texto.AppendLine();
        texto.AppendLine("## O que você pode fazer");
        texto.AppendLine($"O Orbi tem {total} ferramentas do Windows, em {Tools.Groups.Count()} grupos: " + grupos + ".");
        texto.AppendLine($"Nesta conversa, as mais prováveis para este pedido estão declaradas como funções ({ToolSchema.MaxFuncoes} é o máximo que a API aceita por pedido). " +
                         "As outras continuam ao seu alcance de dois jeitos: pc_buscar_no_catalogo acha os ids por assunto " +
                         "e pc_usar_ferramenta executa qualquer uma pelo id. Nunca diga que não sabe fazer algo antes de procurar no catálogo.");
        texto.AppendLine();
        texto.AppendLine("## Como agir (multi-passos)");
        texto.AppendLine("1. Se o pedido é pergunta ou conversa, responda direto, sem chamar função.");
        texto.AppendLine("2. Se precisa fazer algo no PC, chame a função certa. Se não houver função " +
                         "para isso, chame pc_buscar_no_catalogo e depois pc_usar_ferramenta com o id — " +
                         "não existe tarefa do Windows fora do catálogo.");
        texto.AppendLine("3. Cada resultado volta para você: leia, e só então decida o próximo passo. " +
                         "Vários passos são normais (abrir, esperar, ver a tela, clicar, digitar).");
        texto.AppendLine("4. Antes de clicar em algo que não sabe onde está, chame pc_ver_a_tela; use as " +
                         "coordenadas e o tamanho das janelas que voltarem.");
        texto.AppendLine("5. Quando precisar de informação de fora (preço, notícia, clima, documentação), " +
                         "pc_pesquisar_na_web e, se precisar de detalhe, pc_ler_pagina antes de responder.");
        texto.AppendLine("6. Se uma ferramenta falhar, tente outra parecida uma vez; se continuar, diga o " +
                         "que aconteceu em uma frase e o que a pessoa pode tentar.");
        texto.AppendLine("7. Só chame pc_lembrar quando a pessoa disser algo que vale guardar (gosto, rotina, " +
                         "recado). Não anote tudo.");
        texto.AppendLine("8. No fim, responda em 1 a 3 frases contando o que ficou pronto. Sem listas, " +
                         "sem markdown, sem emoji de enfeite.");
        texto.AppendLine();
        texto.AppendLine($"Agora: {DateTime.Now:dddd, dd/MM/yyyy HH:mm}.");
        if (janela.Length > 0) texto.AppendLine($"A janela em foco é “{janela}”.");
        texto.AppendLine("Memória do dia: " + (string.IsNullOrWhiteSpace(memoria) ? "nada registrado ainda." : memoria));
        return texto.ToString();
    }
}
