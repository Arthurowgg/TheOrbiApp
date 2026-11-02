using System.Text.Json.Nodes;

namespace Orbi.Services;

/// <summary>
/// O catálogo do app virando <b>declaração de função</b> do Gemini.
///
/// Nada de "aqui está uma lista de comandos, responda com JSON": o modelo recebe
/// as 597 ferramentas como funções de verdade, com nome, descrição e o dado que
/// cada uma espera. É isso que faz a IA saber todas elas e escolher a certa.
///
/// Além do catálogo, existem funções de controle que só o agente tem:
/// ver a tela, clicar, digitar, atalho, focar janela, pesquisar na web, ler
/// página, abrir link, lembrar, consultar a memória e buscar no catálogo.
/// </summary>
public static class ToolSchema
{
    /// <summary>Nome da função no Gemini → id real da ferramenta no app.</summary>
    private static readonly Dictionary<string, string> Mapa = new(StringComparer.OrdinalIgnoreCase);

    private static JsonArray? _cache;
    private static string _assinatura = "";

    /// <summary>Prefixo das funções que não vêm do catálogo.</summary>
    public const string Ns = "";

    /* ------------------------- controle do PC ------------------------- */

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
            "Traz para a frente a janela que combina com o título ou o nome do programa.",
            new JsonObject { ["alvo"] = Prop("string", "parte do título ou o nome do processo, tipo \"chrome\"") },
            new[] { "alvo" });

        yield return Funcao("pc_pesquisar_na_web",
            "Pesquisa na internet e devolve os primeiros resultados (título e resumo). " +
            "Use para preço, notícia, clima, documentação, qualquer coisa fora deste PC.",
            new JsonObject { ["consulta"] = Prop("string", "o que pesquisar") },
            new[] { "consulta" });

        yield return Funcao("pc_ler_pagina",
            "Abre um endereço e devolve o título e o começo do texto da página, para você ler de verdade " +
            "em vez de adivinhar pelo título.",
            new JsonObject { ["endereco"] = Prop("string", "URL completa, começando com https://") },
            new[] { "endereco" });

        yield return Funcao("pc_abrir_no_navegador",
            "Abre um endereço no navegador padrão do Windows.",
            new JsonObject { ["endereco"] = Prop("string", "URL ou site") },
            new[] { "endereco" });

        yield return Funcao("pc_lembrar",
            "Guarda uma informação na memória da pessoa (ela vê depois em “o que eu fiz hoje”). " +
            "Use quando ela disser algo que vale lembrar.",
            new JsonObject
            {
                ["texto"] = Prop("string", "o que guardar"),
                ["tipo"] = Prop("string", "categoria curta", new[] { "preferencia", "fato", "recado" }),
            },
            new[] { "texto" });

        yield return Funcao("pc_consultar_memoria",
            "Procura na memória do Orbi o que já foi dito, pedido ou guardado antes.",
            new JsonObject { ["termo"] = Prop("string", "palavra ou assunto a procurar") },
            new[] { "termo" });

        yield return Funcao("pc_buscar_no_catalogo",
            "Procura ferramentas do Orbi por assunto — use quando não tiver certeza do nome exato " +
            "de uma ferramenta. Devolve id, nome e o dado que ela pede.",
            new JsonObject { ["assunto"] = Prop("string", "o que você quer fazer") },
            new[] { "assunto" });
    }

    /* ------------------------- catálogo do app ------------------------- */

    private static string NomeFuncao(string id) => id.Replace('-', '_').Replace('.', '_');

    private static string Descricao(Tool ferramenta)
    {
        var texto = ferramenta.Name + ". Grupo: " + ferramenta.Group + ".";
        if (ferramenta.Keywords.Length > 0)
        {
            texto += " Quando usar: " + string.Join(", ", ferramenta.Keywords.Take(6)) + ".";
        }
        if (ferramenta.NeedsArg)
        {
            texto += " Dado obrigatório: " + (ferramenta.Placeholder ?? "o complemento") + ".";
        }
        return texto;
    }

    /// <summary>Declarações prontas para o campo <c>tools</c> da API.</summary>
    public static JsonArray Declaracoes()
    {
        // O catálogo não muda em execução; o cache é só para não remontar a cada passo.
        var assinatura = Tools.All.Count + ":" + Tools.All[^1].Id;
        if (_cache is not null && assinatura == _assinatura) return (JsonArray)_cache.DeepClone();

        var lista = new JsonArray();
        foreach (var funcao in Controle()) lista.Add(funcao);

        foreach (var ferramenta in Tools.All)
        {
            if (ferramenta.Id.StartsWith("control-", StringComparison.OrdinalIgnoreCase))
            {
                // o controle do PC já está nas funções acima, com argumentos de verdade
                continue;
            }

            var nome = NomeFuncao(ferramenta.Id);
            Mapa[nome] = ferramenta.Id;

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

            lista.Add(Funcao(nome, Descricao(ferramenta), propriedades, obrigatorios));
        }

        // As ferramentas de controle também precisam de nome reservado.
        foreach (var reservado in Reservados) Mapa[NomeFuncao(reservado)] = reservado;

        _cache = lista;
        _assinatura = assinatura;
        return (JsonArray)lista.DeepClone();
    }

    /// <summary>Nomes que o agente trata fora do catálogo.</summary>
    public static readonly string[] Reservados =
    {
        "pc_ver_a_tela", "pc_capturar_a_tela", "pc_clicar", "pc_digitar", "pc_atalho", "pc_focar_janela",
        "pc_pesquisar_na_web", "pc_ler_pagina", "pc_abrir_no_navegador", "pc_lembrar", "pc_consultar_memoria",
        "pc_buscar_no_catalogo",
    };

    public static bool EhReservada(string nome) =>
        Reservados.Contains(nome, StringComparer.OrdinalIgnoreCase);

    /// <summary>Id do catálogo por trás do nome da função (ou null).</summary>
    public static string? Resolver(string nomeFuncao)
    {
        if (Mapa.Count == 0) Declaracoes();
        var chave = NomeFuncao(nomeFuncao);
        if (Mapa.TryGetValue(chave, out var id)) return id;

        // o modelo às vezes troca _ por - ou manda o id cru
        var direto = Tools.All.FirstOrDefault(ferramenta =>
            string.Equals(ferramenta.Id, nomeFuncao, StringComparison.OrdinalIgnoreCase));
        return direto?.Id;
    }

    /// <summary>Quantas funções o modelo enxerga (catálogo + controle).</summary>
    public static int Total => Declaracoes().Count;
}
