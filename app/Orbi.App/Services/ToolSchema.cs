using System.Text.Json.Nodes;

namespace Orbi.Services;

/// <summary>
/// O catálogo do app virando <b>declaração de função</b> do Gemini.
///
/// Nada de "aqui está uma lista de comandos, responda com JSON": o modelo recebe
/// funções de verdade, com nome, descrição e o dado que cada uma espera — e é
/// assim que ele sabe com o que pode contar.
///
/// A doc da API tem um limite duro: <b>no máximo 128 declarações por pedido</b>.
/// Então a estratégia é de agente, não de despejar tudo:
///
/// <list type="number">
/// <item>as 12 funções de controle (ver a tela, clicar, digitar, web, memória…)
/// vão <b>sempre</b> — são as mãos do agente;</item>
/// <item>do catálogo (<see cref="MaxFuncoes"/> menos as de controle, ~116), entram
/// as que casam com o pedido, mais as de <see cref="Sempre"/>;</item>
/// <item>as que já foram usadas nesta conversa continuam disponíveis;</item>
/// <item>e o que ficou de fora não fica inalcançável: <c>pc_buscar_no_catalogo</c>
/// procura as 598 por assunto e <c>pc_usar_ferramenta</c> executa qualquer uma
/// pelo id. O agente chega em todas.</item>
/// </list>
/// </summary>
public static class ToolSchema
{
    /// <summary>Teto da API: mais que isso e o pedido inteiro é recusado.</summary>
    public const int MaxFuncoes = 128;

    /// <summary>Nome da função no Gemini → id real da ferramenta no app.</summary>
    private static readonly Dictionary<string, string> Mapa = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Id da ferramenta → nome da função (para não repetir com sufixo errado).</summary>
    private static readonly Dictionary<string, string> Nomes = new(StringComparer.OrdinalIgnoreCase);

    private static bool _mapaPronto;

    /* ---------------------------- utilidades ---------------------------- */

    private static JsonObject Funcao(string nome, string descricao, JsonObject? propriedades = null,
        string[]? obrigatorios = null)
    {
        var parametros = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = propriedades ?? new JsonObject(),
        };
        if (obrigatorios is { Length: > 0 })
        {
            parametros["required"] = new JsonArray(obrigatorios.Select(item => (JsonNode)item!).ToArray());
        }
        return new JsonObject
        {
            ["name"] = nome,
            ["description"] = descricao,
            ["parameters"] = parametros,
        };
    }

    private static JsonObject Prop(string tipo, string descricao, string[]? valores = null)
    {
        var item = new JsonObject { ["type"] = tipo, ["description"] = descricao };
        if (valores is { Length: > 0 })
        {
            item["enum"] = new JsonArray(valores.Select(valor => (JsonNode)valor!).ToArray());
        }
        return item;
    }

    /// <summary>Nome de função válido para a API (letras, números, _ e -).</summary>
    private static string NomeValido(string texto)
    {
        var limpo = new string(texto.Select(c => char.IsLetterOrDigit(c) || c is '_' or '-' ? c : '_').ToArray());
        if (limpo.Length == 0) return "ferramenta";
        return char.IsDigit(limpo[0]) ? "f_" + limpo : limpo;
    }

    /* ------------------------- controle do PC ------------------------- */

    /// <summary>As funções de controle: o que só um agente de PC faz.</summary>
    private static IEnumerable<JsonObject> Controle()
    {
        yield return Funcao("pc_ver_a_tela",
            "Lê o que está acontecendo agora: resolução, posição do mouse, janela em foco e a lista " +
            "das janelas abertas com posição e tamanho. Use antes de clicar em algo que não sabe onde está.");

        yield return Funcao("pc_capturar_a_tela",
            "Tira uma foto da tela e devolve o caminho do arquivo e o que está em foco. " +
            "Use quando precisar descrever detalhes visuais para a pessoa.");

        yield return Funcao("pc_clicar",
            "Clica em um ponto da tela, em coordenadas de pixel (origem no canto superior esquerdo).",
            new JsonObject
            {
                ["x"] = Prop("integer", "coordenada horizontal em pixels"),
                ["y"] = Prop("integer", "coordenada vertical em pixels"),
                ["botao"] = Prop("string", "botão do mouse", new[] { "left", "right", "double" }),
            },
            new[] { "x", "y" });

        yield return Funcao("pc_digitar",
            "Digita um texto no campo que está em foco (o teclado de verdade, letra por letra).",
            new JsonObject { ["texto"] = Prop("string", "o texto exato a digitar") },
            new[] { "texto" });

        yield return Funcao("pc_atalho",
            "Aperta uma combinação de teclas, tipo \"ctrl+t\", \"alt+tab\", \"ctrl+shift+s\" ou \"win+e\".",
            new JsonObject { ["teclas"] = Prop("string", "a combinação, com + entre as teclas") },
            new[] { "teclas" });

        yield return Funcao("pc_focar_janela",
            "Traz para a frente a janela cujo título contém o texto (sem diferenciar maiúsculas).",
            new JsonObject { ["alvo"] = Prop("string", "parte do título da janela") },
            new[] { "alvo" });

        yield return Funcao("pc_pesquisar_na_web",
            "Pesquisa na web agora e devolve o resultado principal (título e trecho). " +
            "Use para fato atual, preço, notícia, documentação — nunca invente.",
            new JsonObject { ["consulta"] = Prop("string", "o que procurar") },
            new[] { "consulta" });

        yield return Funcao("pc_ler_pagina",
            "Abre um endereço e devolve o texto da página, já limpo. Use depois de pesquisar, " +
            "quando o trecho do resultado não bastar.",
            new JsonObject { ["endereco"] = Prop("string", "URL completa, com https://") },
            new[] { "endereco" });

        yield return Funcao("pc_abrir_no_navegador",
            "Abre um endereço no navegador padrão do Windows.",
            new JsonObject { ["endereco"] = Prop("string", "URL ou domínio") },
            new[] { "endereco" });

        yield return Funcao("pc_lembrar",
            "Guarda um fato sobre a pessoa na memória do Orbi (gosto, rotina, recado, projeto). " +
            "Use só quando valer a pena lembrar depois.",
            new JsonObject
            {
                ["texto"] = Prop("string", "o que guardar, em uma frase"),
                ["tipo"] = Prop("string", "espécie do registro", new[] { "fato", "gosto", "rotina", "recado", "ação" }),
            },
            new[] { "texto" });

        yield return Funcao("pc_consultar_memoria",
            "Procura no que já foi combinado e feito antes (memória do Orbi).",
            new JsonObject { ["termo"] = Prop("string", "assunto a procurar; vazio traz o que é recente") });

        yield return Funcao("pc_buscar_no_catalogo",
            "Procura, entre as centenas de ferramentas do Windows que o Orbi tem, as que servem para " +
            "um assunto. Devolve os ids. Use quando não houver uma função para o que você quer fazer — " +
            "e depois execute com pc_usar_ferramenta.",
            new JsonObject { ["assunto"] = Prop("string", "o que a ferramenta deveria fazer") },
            new[] { "assunto" });

        yield return Funcao("pc_usar_ferramenta",
            "Executa qualquer ferramenta do catálogo pelo id (o que pc_buscar_no_catalogo devolve). " +
            "É o caminho para as ferramentas que não estão declaradas nesta conversa.",
            new JsonObject
            {
                ["id"] = Prop("string", "id da ferramenta, exatamente como no catálogo (com hífens)"),
                ["dado"] = Prop("string", "o dado que a ferramenta pede (site, texto, caminho, número)"),
            },
            new[] { "id" });
    }

    /// <summary>Nomes que o agente trata fora do catálogo.</summary>
    public static readonly string[] Reservados =
    {
        "pc_ver_a_tela", "pc_capturar_a_tela", "pc_clicar", "pc_digitar", "pc_atalho", "pc_focar_janela",
        "pc_pesquisar_na_web", "pc_ler_pagina", "pc_abrir_no_navegador", "pc_lembrar", "pc_consultar_memoria",
        "pc_buscar_no_catalogo", "pc_usar_ferramenta",
    };

    public static bool EhReservada(string nome) =>
        Reservados.Contains(nome, StringComparer.OrdinalIgnoreCase);

    /* --------------------------- o catálogo --------------------------- */

    /// <summary>
    /// Monta o mapa nome-de-função → id de <b>todas</b> as ferramentas, uma vez.
    /// O JSON não é montado aqui: só os nomes, que é o que a execução precisa.
    /// </summary>
    private static void MontarMapa()
    {
        if (_mapaPronto) return;
        Mapa.Clear();
        Nomes.Clear();

        foreach (var ferramenta in Tools.All)
        {
            if (ferramenta.Id.StartsWith("control-", StringComparison.OrdinalIgnoreCase)) continue;

            var nome = NomeValido(ferramenta.Id.Replace('-', '_').Replace('.', '_'));
            if (Mapa.ContainsKey(nome)) nome += "_" + (Mapa.Count + 1);
            Mapa[nome] = ferramenta.Id;
            Nomes[ferramenta.Id] = nome;
        }

        foreach (var reservado in Reservados) Mapa[reservado] = reservado;
        _mapaPronto = true;
    }

    private static string NomeDe(Tool ferramenta)
    {
        MontarMapa();
        return Nomes.TryGetValue(ferramenta.Id, out var nome) ? nome : NomeValido(ferramenta.Id);
    }

    private static string Descricao(Tool ferramenta)
    {
        var texto = $"{ferramenta.Name} (grupo: {ferramenta.Group}).";
        if (ferramenta.NeedsArg)
        {
            texto += $" Precisa do dado: {ferramenta.Placeholder ?? "o complemento"}.";
        }
        return texto;
    }

    private static JsonObject DeclaracaoDa(Tool ferramenta)
    {
        JsonObject? propriedades = null;
        string[]? obrigatorios = null;
        if (ferramenta.NeedsArg)
        {
            propriedades = new JsonObject
            {
                ["dado"] = Prop("string", ferramenta.Placeholder ?? "o dado que a ferramenta pede"),
            };
            obrigatorios = new[] { "dado" };
        }
        return Funcao(NomeDe(ferramenta), Descricao(ferramenta), propriedades, obrigatorios);
    }

    /* ------------------------- escolha por pedido ------------------------- */

    private static readonly string[] Ruido =
    {
        "de", "da", "do", "das", "dos", "a", "o", "as", "os", "um", "uma", "e", "em", "no", "na",
        "para", "por", "com", "que", "meu", "minha", "me", "favor", "aqui", "isso", "meu", "quero",
        "preciso", "você", "voce", "pode", "fazer", "faz", "agora", "agora", "porque",
    };

    private static IEnumerable<string> Termos(string texto) =>
        texto.ToLowerInvariant()
            .Split(new[] { ' ', ',', '.', '!', '?', '\n', '\t', ';', ':', '/', '(', ')', '"', '\'' },
                StringSplitOptions.RemoveEmptyEntries)
            .Where(palavra => palavra.Length > 2 && !Ruido.Contains(palavra))
            .Distinct();

    /// <summary>Grupos que sempre rendem: o agente mexe neles em quase todo pedido.</summary>
    private static bool Sempre(Tool ferramenta) =>
        ferramenta.Id is "orbi-chat" or "orbi-bar" ||
        ferramenta.Group.Contains("Janelas", StringComparison.OrdinalIgnoreCase) ||
        ferramenta.Group.Contains("Tela", StringComparison.OrdinalIgnoreCase) ||
        ferramenta.Group.Contains("Web", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Quais ferramentas do catálogo entram nas declarações deste pedido, em ordem
    /// de relevância, até caber no teto da API.
    /// </summary>
    public static List<Tool> Escolher(string? pedido, IReadOnlyCollection<string>? usadas = null)
    {
        MontarMapa();
        var vagas = MaxFuncoes - Reservados.Length;

        var pontos = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var termos = string.IsNullOrWhiteSpace(pedido) ? Array.Empty<string>() : Termos(pedido).ToArray();

        foreach (var ferramenta in Tools.All)
        {
            if (ferramenta.Id.StartsWith("control-", StringComparison.OrdinalIgnoreCase)) continue;

            var alvo = (ferramenta.Id + " " + ferramenta.Name + " " + ferramenta.Group + " " +
                        string.Join(" ", ferramenta.Keywords) + " " + (ferramenta.Placeholder ?? ""))
                .ToLowerInvariant();

            var acertos = termos.Count(termo => alvo.Contains(termo, StringComparison.Ordinal));
            if (acertos > 0) pontos[ferramenta.Id] = acertos;
        }

        var escolhidas = new List<Tool>();
        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Por(Tool ferramenta)
        {
            if (escolhidas.Count >= vagas || !vistos.Add(ferramenta.Id)) return;
            escolhidas.Add(ferramenta);
        }

        // 1. as que já foram usadas nesta conversa continuam à mão
        if (usadas is not null)
        {
            foreach (var id in usadas)
            {
                var ferramenta = Tools.Find(id);
                if (ferramenta is not null) Por(ferramenta);
            }
        }

        // 2. o que casa com o pedido, primeiro quem mais casa, e no máximo 4 por grupo
        //    (senão "arquivos" enche a lista com 30 variações e nada mais entra)
        var porGrupo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var ferramenta in Tools.All
                     .Where(item => pontos.ContainsKey(item.Id))
                     .OrderByDescending(item => pontos[item.Id])
                     .ThenBy(item => item.Name))
        {
            var grupo = ferramenta.Group;
            porGrupo.TryGetValue(grupo, out var quantas);
            if (quantas >= 4) continue;
            porGrupo[grupo] = quantas + 1;
            Por(ferramenta);
        }

        // 3. o resto do que costuma ser útil, até encher
        foreach (var ferramenta in Tools.All.Where(Sempre))
        {
            Por(ferramenta);
        }

        // 4. ainda sobrou espaço? completa com o começo do catálogo
        foreach (var ferramenta in Tools.All)
        {
            if (escolhidas.Count >= vagas) break;
            if (ferramenta.Id.StartsWith("control-", StringComparison.OrdinalIgnoreCase)) continue;
            Por(ferramenta);
        }

        return escolhidas;
    }

    /// <summary>
    /// As declarações prontas para o campo <c>tools</c> da API: controle + as do
    /// pedido, nunca passando de <see cref="MaxFuncoes"/>.
    /// </summary>
    public static JsonArray Declaracoes(string? pedido = null, IReadOnlyCollection<string>? usadas = null)
    {
        var lista = new JsonArray();
        foreach (var funcao in Controle()) lista.Add(funcao);
        foreach (var ferramenta in Escolher(pedido, usadas)) lista.Add(DeclaracaoDa(ferramenta));
        return lista;
    }

    /// <summary>Todo o catálogo em declarações — usado só para conferência, não na API.</summary>
    public static JsonArray Todos()
    {
        var lista = new JsonArray();
        foreach (var funcao in Controle()) lista.Add(funcao);
        foreach (var ferramenta in Tools.All)
        {
            if (ferramenta.Id.StartsWith("control-", StringComparison.OrdinalIgnoreCase)) continue;
            lista.Add(DeclaracaoDa(ferramenta));
        }
        return lista;
    }

    /// <summary>Id do catálogo por trás do nome da função (ou null).</summary>
    public static string? Resolver(string nomeFuncao)
    {
        // funções de controle não estão no catálogo: o nome já é o destino
        if (EhReservada(nomeFuncao)) return nomeFuncao;

        MontarMapa();
        if (Mapa.TryGetValue(nomeFuncao, out var id)) return id;

        // o modelo às vezes troca _ por -, manda o id cru ou repete com sufixo
        var semSufixo = nomeFuncao.Split('_').Length > 1
            ? nomeFuncao.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '_')
            : nomeFuncao;
        foreach (var candidato in new[] { semSufixo, nomeFuncao.Replace('_', '-'), semSufixo.Replace('_', '-') })
        {
            if (Mapa.TryGetValue(candidato, out var achado)) return achado;
            var direto = Tools.All.FirstOrDefault(ferramenta =>
                string.Equals(ferramenta.Id, candidato, StringComparison.OrdinalIgnoreCase));
            if (direto is not null) return direto.Id;
        }
        return null;
    }

    /// <summary>Quantas ferramentas existem no todo (o que o Orbi sabe fazer).</summary>
    public static int TotalDoCatalogo => Tools.All.Count;

    /// <summary>Quantas funções o modelo vê em um pedido qualquer (respeita o teto).</summary>
    public static int Total => Declaracoes().Count;
}
