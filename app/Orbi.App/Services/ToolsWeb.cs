using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace Orbi.Services;

/// <summary>
/// O segundo lote de ações: web com o dado que você disse, pesquisa em arquivos,
/// texto, cálculo, datas, área de transferência, rede e painéis do Windows.
/// Tudo com argumento — a ferramenta pergunta o dado quando falta, e a IA pode
/// passar o dado no passo do agente.
/// </summary>
public static class ToolsWeb
{
    private static readonly CultureInfo Cultura = new("pt-BR");

    public static void Register(List<Tool> tools)
    {
        _ = Cultura;
        void Add(string id, string name, string group, string keywords, Func<string> run, string? arg = null) =>
            tools.Add(new Tool(id, name, group, keywords.Split(',', StringSplitOptions.TrimEntries), run,
                Arg: arg, Placeholder: arg));

        string D() => Args.Current;

        // dado vazio = usa o que está copiado
        string Texto(string valor) => string.IsNullOrWhiteSpace(valor) ? ClipboardSafe() : valor;

        string Copia(string texto)
        {
            var curto = texto.Length > 140 ? texto[..140] + "…" : texto;
            try { System.Windows.Clipboard.SetText(texto); return "Copiado: " + curto; }
            catch { return curto; }
        }


        string Abrir(string alvo)
        {
            try
            {
                if (Directory.Exists(alvo) || File.Exists(alvo))
                {
                    Process.Start(new ProcessStartInfo(alvo) { UseShellExecute = true });
                }
                else
                {
                    var url = alvo.Contains("://") ? alvo : "https://" + alvo;
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                return "Aberto.";
            }
            catch (Exception error)
            {
                return "Não abri: " + error.Message;
            }
        }

        string App(string nome)
        {
            try
            {
                if (nome == "cmd-as-admin")
                {
                    Process.Start(new ProcessStartInfo("cmd.exe")
                    {
                        UseShellExecute = true,
                        Verb = "runas",
                    });
                    return "Prompt aberto como administrador.";
                }
                Process.Start(new ProcessStartInfo(nome) { UseShellExecute = true });
                return "Aberto.";
            }
            catch
            {
                return "Não achei esse app instalado.";
            }
        }

        Add("pesquisar-na-web", "Pesquisar na web", "Web e pesquisa", "pesquisar,web,busca", () => Abrir($"https://duckduckgo.com/?q={Uri.EscapeDataString(d())}"), "o que pesquisar");
        Add("pesquisar-no-google", "Pesquisar no Google", "Web e pesquisa", "google,pesquisar", () => Abrir($"https://www.google.com/search?q={Uri.EscapeDataString(d())}"), "o que pesquisar");
        Add("pesquisar-no-youtube", "Pesquisar no YouTube", "Web e pesquisa", "youtube,video", () => Abrir($"https://www.youtube.com/results?search_query={Uri.EscapeDataString(d())}"), "o que assistir");
        Add("pesquisar-imagens", "Pesquisar imagens", "Web e pesquisa", "imagens,fotos", () => Abrir($"https://www.google.com/search?tbm=isch&q={Uri.EscapeDataString(d())}"), "o que procurar");
        Add("pesquisar-no-github", "Pesquisar no GitHub", "Web e pesquisa", "github,codigo", () => Abrir($"https://github.com/search?q={Uri.EscapeDataString(d())}"), "o que procurar");
        Add("pesquisar-no-stack-overflow", "Pesquisar no Stack Overflow", "Web e pesquisa", "stackoverflow,erro,codigo", () => Abrir($"https://stackoverflow.com/search?q={Uri.EscapeDataString(d())}"), "o erro ou assunto");
        Add("pesquisar-na-wikipedia", "Pesquisar na Wikipedia", "Web e pesquisa", "wikipedia,enciclopedia", () => Abrir($"https://pt.wikipedia.org/w/index.php?search={Uri.EscapeDataString(d())}"), "o assunto");
        Add("pesquisar-no-mercado-livre", "Pesquisar no Mercado Livre", "Web e pesquisa", "comprar,preco,produto", () => Abrir($"https://lista.mercadolivre.com.br/{Uri.EscapeDataString(d())}"), "o produto");
        Add("pesquisar-videos", "Pesquisar vídeos", "Web e pesquisa", "video,assistir", () => Abrir($"https://www.youtube.com/results?search_query={Uri.EscapeDataString(d())}"), "o assunto");
        Add("abrir-um-site", "Abrir um site", "Web e pesquisa", "site,url,navegador", () => Abrir(d().Contains("://") ? d() : "https://" + d()), "o endereço");
        Add("abrir-um-link-do-youtube", "Abrir um link do YouTube", "Web e pesquisa", "youtube,video", () => Abrir(d().Contains("://") ? d() : $"https://www.youtube.com/results?search_query={Uri.EscapeDataString(d())}"), "o endereço ou o nome");
        Add("ver-um-lugar-no-mapa", "Ver um lugar no mapa", "Web e pesquisa", "mapa,lugar,endereco", () => Abrir($"https://www.google.com/maps/search/{Uri.EscapeDataString(d())}"), "o lugar");
        Add("tracar-uma-rota", "Traçar uma rota", "Web e pesquisa", "rota,caminho,ir", () => Abrir($"https://www.google.com/maps/dir/{Uri.EscapeDataString(d())}"), "de onde para onde");
        Add("traduzir-um-texto", "Traduzir um texto", "Web e pesquisa", "traduzir,idioma", () => Abrir($"https://translate.google.com/?sl=auto&tl=pt&text={Uri.EscapeDataString(d())}&op=translate"), "o texto");
        Add("ver-a-previsao-do-tempo", "Ver a previsão do tempo", "Web e pesquisa", "tempo,clima,chuva", () => Abrir($"https://www.google.com/search?q=previs%C3%A3o+do+tempo+{Uri.EscapeDataString(d())}"), "a cidade");
        Add("ver-a-cotacao-do-dolar", "Ver a cotação do dólar", "Web e pesquisa", "dolar,cotacao,moeda", () => Abrir("https://www.google.com/search?q=cota%C3%A7%C3%A3o+hoje"), "a moeda ou deixe vazio");
        Add("ler-as-noticias", "Ler as notícias", "Web e pesquisa", "noticias,jornal", () => Abrir(d().Length > 0 ? $"https://news.google.com/search?q={Uri.EscapeDataString(d())}" : "https://news.google.com"), "o assunto");
        Add("pesquisar-no-reddit", "Pesquisar no Reddit", "Web e pesquisa", "reddit,forum", () => Abrir($"https://www.reddit.com/search/?q={Uri.EscapeDataString(d())}"), "o assunto");
        Add("pesquisar-no-x", "Pesquisar no X", "Web e pesquisa", "x,twitter,post", () => Abrir($"https://x.com/search?q={Uri.EscapeDataString(d())}"), "o assunto");
        Add("pesquisar-um-filme", "Pesquisar um filme", "Web e pesquisa", "filme,serie,onde", () => Abrir($"https://www.google.com/search?q={Uri.EscapeDataString(d())}+onde+assistir"), "o filme");
        Add("pesquisar-uma-musica", "Pesquisar uma música", "Web e pesquisa", "musica,cancao,ouvir", () => Abrir($"https://open.spotify.com/search/{Uri.EscapeDataString(d())}"), "a música");
        Add("pesquisar-uma-receita", "Pesquisar uma receita", "Web e pesquisa", "receita,comida,cozinhar", () => Abrir($"https://www.google.com/search?q=receita+{Uri.EscapeDataString(d())}"), "o prato");
        Add("pesquisar-uma-vaga", "Pesquisar uma vaga", "Web e pesquisa", "emprego,vaga,linkedin", () => Abrir($"https://www.linkedin.com/jobs/search/?keywords={Uri.EscapeDataString(d())}"), "o cargo");
        Add("abrir-o-e-mail", "Abrir o e-mail", "Web e pesquisa", "email,gmail,caixa", () => Abrir("https://mail.google.com"));
        Add("escrever-um-e-mail", "Escrever um e-mail", "Web e pesquisa", "email,escrever", () => Abrir($"https://mail.google.com/mail/?view=cm&tf=1&su={Uri.EscapeDataString(d())}"), "para quem e sobre o quê");
        Add("pesquisar-no-e-mail", "Pesquisar no e-mail", "Web e pesquisa", "email,buscar", () => Abrir($"https://mail.google.com/mail/u/0/#search/{Uri.EscapeDataString(d())}"), "o que procurar");
        Add("abrir-a-agenda", "Abrir a agenda", "Web e pesquisa", "agenda,calendario,compromisso", () => Abrir("https://calendar.google.com"));
        Add("criar-um-evento", "Criar um evento", "Web e pesquisa", "evento,agenda,marcar", () => Abrir($"https://calendar.google.com/calendar/render?action=TEMPLATE&text={Uri.EscapeDataString(d())}"), "o evento");
        Add("abrir-os-documentos", "Abrir os documentos", "Web e pesquisa", "docs,documento", () => Abrir("https://docs.google.com/document/u/0/"));
        Add("abrir-as-planilhas", "Abrir as planilhas", "Web e pesquisa", "planilha,sheets", () => Abrir("https://docs.google.com/spreadsheets/u/0/"));
        Add("abrir-as-apresentacoes", "Abrir as apresentações", "Web e pesquisa", "slides,apresentacao", () => Abrir("https://docs.google.com/presentation/u/0/"));
        Add("abrir-o-drive", "Abrir o Drive", "Web e pesquisa", "drive,arquivos,nuvem", () => Abrir("https://drive.google.com"));
        Add("abrir-o-whatsapp", "Abrir o WhatsApp", "Web e pesquisa", "whatsapp,mensagem", () => Abrir("https://web.whatsapp.com"));
        Add("mandar-mensagem-no-whatsapp", "Mandar mensagem no WhatsApp", "Web e pesquisa", "whatsapp,mensagem,enviar", () => Abrir($"https://wa.me/{new string(d().Where(char.IsDigit).ToArray())}"), "o número");
        Add("abrir-o-telegram", "Abrir o Telegram", "Web e pesquisa", "telegram,mensagem", () => Abrir("https://web.telegram.org"));
        Add("abrir-o-instagram", "Abrir o Instagram", "Web e pesquisa", "instagram,social", () => Abrir("https://www.instagram.com"));
        Add("abrir-o-linkedin", "Abrir o LinkedIn", "Web e pesquisa", "linkedin,emprego", () => Abrir("https://www.linkedin.com"));
        Add("abrir-o-spotify", "Abrir o Spotify", "Web e pesquisa", "spotify,musica", () => Abrir("https://open.spotify.com"));
        Add("abrir-a-netflix", "Abrir a Netflix", "Web e pesquisa", "netflix,filme", () => Abrir("https://www.netflix.com"));
        Add("abrir-o-notion", "Abrir o Notion", "Web e pesquisa", "notion,anotacao", () => Abrir("https://www.notion.so"));
        Add("abrir-o-figma", "Abrir o Figma", "Web e pesquisa", "figma,design", () => Abrir("https://www.figma.com"));
        Add("abrir-o-canva", "Abrir o Canva", "Web e pesquisa", "canva,design,arte", () => Abrir("https://www.canva.com"));
        Add("abrir-um-pdf", "Abrir um PDF", "Web e pesquisa", "pdf,documento", () => Abrir(d()), "o caminho ou o link");
        Add("baixar-um-arquivo", "Baixar um arquivo", "Web e pesquisa", "baixar,download", () => Abrir(d()), "o link");
        Add("procurar-um-arquivo", "Procurar um arquivo", "Arquivos e pastas", "procurar,arquivo,nome", () => Busca(d()), "o nome");
        Add("procurar-uma-pasta", "Procurar uma pasta", "Arquivos e pastas", "procurar,pasta", () => BuscaPasta(d()), "o nome");
        Add("abrir-a-pasta-de-um-arquivo", "Abrir a pasta de um arquivo", "Arquivos e pastas", "abrir,pasta,arquivo", () => AbrirPastaDe(d()), "o nome");
        Add("ver-o-tamanho-de-uma-pasta", "Ver o tamanho de uma pasta", "Arquivos e pastas", "tamanho,pasta", () => Tamanho(d()), "o caminho ou o nome");
        Add("contar-os-arquivos-de-uma-pasta", "Contar os arquivos de uma pasta", "Arquivos e pastas", "contar,arquivos", () => Contar(d()), "o caminho");
        Add("listar-o-que-tem-em-uma-pasta", "Listar o que tem em uma pasta", "Arquivos e pastas", "listar,pasta", () => Listar(d()), "o caminho");
        Add("abrir-um-caminho", "Abrir um caminho", "Arquivos e pastas", "abrir,caminho", () => Abrir(d()), "o caminho");
        Add("criar-uma-pasta", "Criar uma pasta", "Arquivos e pastas", "criar,pasta", () => CriarPasta(d()), "o caminho");
        Add("criar-um-arquivo-de-texto", "Criar um arquivo de texto", "Arquivos e pastas", "criar,arquivo,texto", () => CriarArquivo(d()), "o caminho");
        Add("renomear-um-arquivo", "Renomear um arquivo", "Arquivos e pastas", "renomear,arquivo", () => Renomear(d()), "de → para");
        Add("copiar-um-arquivo", "Copiar um arquivo", "Arquivos e pastas", "copiar,arquivo", () => Copiar(d()), "origem → destino");
        Add("mover-um-arquivo", "Mover um arquivo", "Arquivos e pastas", "mover,arquivo", () => Mover(d()), "origem → destino");
        Add("apagar-um-arquivo", "Apagar um arquivo", "Arquivos e pastas", "apagar,arquivo,excluir", () => Apagar(d()), "o caminho");
        Add("ler-um-arquivo-de-texto", "Ler um arquivo de texto", "Arquivos e pastas", "ler,arquivo,texto", () => Ler(d()), "o caminho");
        Add("contar-as-palavras", "Contar as palavras", "Texto", "contar,palavras", () => Texto(d()).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length + " palavras.", "o texto");
        Add("contar-as-letras", "Contar as letras", "Texto", "contar,letras,caracteres", () => Texto(d()).Length.ToString("N0") + " letras.", "o texto");
        Add("deixar-em-maiusculas", "Deixar em MAIÚSCULAS", "Texto", "maiusculas,texto", () => Copia(Texto(d()).ToUpperInvariant()), "o texto");
        Add("deixar-em-minusculas", "Deixar em minúsculas", "Texto", "minusculas,texto", () => Copia(Texto(d()).ToLowerInvariant()), "o texto");
        Add("deixar-em-titulo", "Deixar em Título", "Texto", "titulo,capitalizar", () => Copia(Cultura.TextInfo.ToTitleCase(Texto(d()).ToLowerInvariant())), "o texto");
        Add("tirar-os-acentos", "Tirar os acentos", "Texto", "acentos,texto", () => Copia(SemAcento(Texto(d()))), "o texto");
        Add("inverter-o-texto", "Inverter o texto", "Texto", "inverter,texto", () => Copia(new string(Texto(d()).Reverse().ToArray())), "o texto");
        Add("ordenar-as-linhas", "Ordenar as linhas", "Texto", "ordenar,linhas", () => Copia(string.Join("\n", Texto(d()).Split('\n').Select(l => l.Trim()).OrderBy(l => l, StringComparer.CurrentCulture))), "o texto");
        Add("remover-linhas-repetidas", "Remover linhas repetidas", "Texto", "repetidas,linhas", () => Copia(string.Join("\n", Texto(d()).Split('\n').Select(l => l.Trim()).Distinct())), "o texto");
        Add("remover-linhas-vazias", "Remover linhas vazias", "Texto", "vazias,linhas", () => Copia(string.Join("\n", Texto(d()).Split('\n').Where(l => l.Trim().Length > 0))), "o texto");
        Add("juntar-as-linhas", "Juntar as linhas", "Texto", "juntar,linhas,paragrafo", () => Copia(string.Join(" ", Texto(d()).Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0))), "o texto");
        Add("numerar-as-linhas", "Numerar as linhas", "Texto", "numerar,linhas", () => Copia(string.Join("\n", Texto(d()).Split('\n').Select((l, i) => $"{i + 1}. {l.Trim()}"))), "o texto");
        Add("colocar-em-lista", "Colocar em lista", "Texto", "lista,marcadores", () => Copia(string.Join("\n", Texto(d()).Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).Select(l => "• " + l))), "o texto");
        Add("tirar-os-espacos-duplos", "Tirar os espaços duplos", "Texto", "espacos,texto", () => Copia(System.Text.RegularExpressions.Regex.Replace(Texto(d()), @"[ \t]{2,}", " ")), "o texto");
        Add("copiar-em-base64", "Copiar em Base64", "Texto", "base64,codificar", () => Copia(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Texto(d())))), "o texto");
        Add("embaralhar-as-linhas", "Embaralhar as linhas", "Texto", "embaralhar,linhas", () => Copia(string.Join("\n", Texto(d()).Split('\n').OrderBy(_ => Guid.NewGuid()))), "o texto");
        Add("sortear-uma-linha", "Sortear uma linha", "Texto", "sortear,escolher,linha", () => Sortear(Texto(d())), "o texto");
        Add("trocar-uma-palavra", "Trocar uma palavra", "Texto", "trocar,substituir,palavra", () => Trocar(d()), "antiga → nova");
        Add("focar-uma-janela", "Focar uma janela", "Janelas e mouse", "focar,janela,trazer", () => Pc.Focus(d()) ? $"Trouxe {d()} para a frente." : $"Não achei uma janela de {d()}.", "o nome da janela");
        Add("maximizar-uma-janela", "Maximizar uma janela", "Janelas e mouse", "maximizar,janela", () => Pc.Maximize(d()) ? $"Maximizei {d()}." : $"Não achei {d()}.", "o nome da janela");
        Add("minimizar-uma-janela", "Minimizar uma janela", "Janelas e mouse", "minimizar,janela", () => Pc.Minimize(d()) ? $"Minimizei {d()}." : $"Não achei {d()}.", "o nome da janela");
        Add("fechar-uma-janela", "Fechar uma janela", "Janelas e mouse", "fechar,janela", () => Pc.Close(d()) ? $"Fechei {d()}." : $"Não achei {d()}.", "o nome da janela");
        Add("mover-o-mouse", "Mover o mouse", "Janelas e mouse", "mover,mouse,cursor", () => Pc.ParsePoint(d()) is { } p ? (Pc.Move(p.X, p.Y), $"Mouse em {p.X},{p.Y}.").Item2 : "Diga a posição como 640,400.", "x,y");
        Add("clicar-em-uma-posicao", "Clicar em uma posição", "Janelas e mouse", "clicar,posicao,mouse", () => Pc.ParsePoint(d()) is { } p ? (Pc.Click(p.X, p.Y), $"Cliquei em {p.X},{p.Y}.").Item2 : "Diga a posição como 640,400.", "x,y");
        Add("clicar-com-o-botao-direito", "Clicar com o botão direito", "Janelas e mouse", "clique,direito", () => Pc.ParsePoint(d()) is { } p ? (Pc.Click(p.X, p.Y, "right"), $"Clique direito em {p.X},{p.Y}.").Item2 : "Diga a posição como 640,400.", "x,y");
        Add("clique-duplo", "Clique duplo", "Janelas e mouse", "clique,duplo", () => Pc.ParsePoint(d()) is { } p ? (Pc.DoubleClick(p.X, p.Y), $"Clique duplo em {p.X},{p.Y}.").Item2 : "Diga a posição como 640,400.", "x,y");
        Add("digitar-um-texto", "Digitar um texto", "Janelas e mouse", "digitar,escrever", () => { Pc.Type(d()); return "Digitei."; }, "o texto");
        Add("apertar-um-atalho", "Apertar um atalho", "Janelas e mouse", "atalho,tecla,aperte", () => { Pc.Hotkey(d()); return $"Atalho {d()} enviado."; }, "ctrl+t");
        Add("rolar-a-tela", "Rolar a tela", "Janelas e mouse", "rolar,scroll", () => int.TryParse(d(), out var n) ? (Pc.Scroll(n), $"Rolei {n}.").Item2 : (Pc.Scroll(3), "Rolei 3.").Item2, "quantos cliques");
        Add("capturar-a-tela", "Capturar a tela", "Tela", "capturar,tela,foto", () => { Copia(Pc.Capture()); return "Captura salva."; });
        Add("capturar-e-guardar-o-caminho", "Capturar e guardar o caminho", "Tela", "capturar,caminho", () => "Captura salva em " + Pc.Capture());
        Add("ver-o-tamanho-da-tela", "Ver o tamanho da tela", "Tela", "tamanho,tela,resolucao", () => $"{Pc.ScreenSize().Width}x{Pc.ScreenSize().Height}.");
        Add("ver-a-posicao-do-mouse", "Ver a posição do mouse", "Tela", "mouse,posicao", () => $"Mouse em {Pc.CursorPosition().X},{Pc.CursorPosition().Y}.");
        Add("abrir-as-configuracoes-de-video", "Abrir as configurações de vídeo", "Configurações do Windows", "video,tela,resolucao", () => Abrir("ms-settings:display"));
        Add("abrir-o-som-do-sistema", "Abrir o som do sistema", "Configurações do Windows", "som,audio,volume", () => Abrir("ms-settings:sound"));
        Add("abrir-as-notificacoes", "Abrir as notificações", "Configurações do Windows", "notificacao,avisos", () => Abrir("ms-settings:notifications"));
        Add("abrir-a-energia", "Abrir a energia", "Configurações do Windows", "energia,bateria", () => Abrir("ms-settings:batterysaver"));
        Add("abrir-o-armazenamento", "Abrir o armazenamento", "Configurações do Windows", "armazenamento,disco", () => Abrir("ms-settings:storagesense"));
        Add("abrir-o-bluetooth", "Abrir o Bluetooth", "Configurações do Windows", "bluetooth,conectar", () => Abrir("ms-settings:bluetooth"));
        Add("abrir-o-wi-fi", "Abrir o Wi-Fi", "Configurações do Windows", "wifi,rede", () => Abrir("ms-settings:network-wifi"));
        Add("abrir-o-modo-aviao", "Abrir o modo avião", "Configurações do Windows", "aviao,rede", () => Abrir("ms-settings:network-airplanemode"));
        Add("abrir-o-proxy", "Abrir o proxy", "Configurações do Windows", "proxy,rede", () => Abrir("ms-settings:network-proxy"));
        Add("abrir-a-vpn", "Abrir a VPN", "Configurações do Windows", "vpn,rede", () => Abrir("ms-settings:network-vpn"));
        Add("abrir-as-impressoras", "Abrir as impressoras", "Configurações do Windows", "impressora,imprimir", () => Abrir("ms-settings:printers"));
        Add("abrir-os-aplicativos-instalados", "Abrir os aplicativos instalados", "Configurações do Windows", "aplicativos,instalados", () => Abrir("ms-settings:appsfeatures"));
        Add("abrir-os-aplicativos-padrao", "Abrir os aplicativos padrão", "Configurações do Windows", "padrao,app", () => Abrir("ms-settings:defaultapps"));
        Add("abrir-os-apps-de-inicializacao", "Abrir os apps de inicialização", "Configurações do Windows", "iniciar,apps", () => Abrir("ms-settings:startupapps"));
        Add("abrir-as-contas", "Abrir as contas", "Configurações do Windows", "conta,usuario", () => Abrir("ms-settings:yourinfo"));
        Add("abrir-a-data-e-hora", "Abrir a data e hora", "Configurações do Windows", "data,hora,relogio", () => Abrir("ms-settings:dateandtime"));
        Add("abrir-o-idioma", "Abrir o idioma", "Configurações do Windows", "idioma,teclado", () => Abrir("ms-settings:regionlanguage"));
        Add("abrir-a-acessibilidade", "Abrir a acessibilidade", "Configurações do Windows", "acessibilidade,facilitar", () => Abrir("ms-settings:easeofaccess"));
        Add("abrir-o-windows-update", "Abrir o Windows Update", "Configurações do Windows", "atualizar,update", () => Abrir("ms-settings:windowsupdate"));
        Add("abrir-a-recuperacao", "Abrir a recuperação", "Configurações do Windows", "recuperar,restaurar", () => Abrir("ms-settings:recovery"));
        Add("abrir-o-modo-de-jogo", "Abrir o modo de jogo", "Configurações do Windows", "jogo,game", () => Abrir("ms-settings:gaming-gamebar"));
        Add("abrir-o-microfone", "Abrir o microfone", "Configurações do Windows", "microfone,som", () => Abrir("ms-settings:privacy-microphone"));
        Add("abrir-a-camera", "Abrir a câmera", "Configurações do Windows", "camera,foto", () => Abrir("ms-settings:privacy-webcam"));
        Add("abrir-a-localizacao", "Abrir a localização", "Configurações do Windows", "localizacao,gps", () => Abrir("ms-settings:privacy-location"));
        Add("abrir-o-modo-escuro", "Abrir o modo escuro", "Configurações do Windows", "escuro,tema", () => Abrir("ms-settings:colors"));
        Add("abrir-o-papel-de-parede", "Abrir o papel de parede", "Configurações do Windows", "papel de parede,fundo", () => Abrir("ms-settings:personalization-background"));
        Add("abrir-a-barra-de-tarefas", "Abrir a barra de tarefas", "Configurações do Windows", "barra de tarefas", () => Abrir("ms-settings:taskbar"));
        Add("abrir-o-multitarefa", "Abrir o multitarefa", "Configurações do Windows", "multitarefa,alt tab", () => Abrir("ms-settings:multitasking"));
        Add("abrir-a-area-de-trabalho-remota", "Abrir a área de trabalho remota", "Configurações do Windows", "remoto,rdp", () => Abrir("ms-settings:remotedesktop"));
        Add("abrir-os-recursos-opcionais", "Abrir os recursos opcionais", "Configurações do Windows", "recursos,opcionais", () => Abrir("ms-settings:optionalfeatures"));
        Add("abrir-o-modo-desenvolvedor", "Abrir o modo desenvolvedor", "Configurações do Windows", "desenvolvedor,dev", () => Abrir("ms-settings:developers"));
        Add("abrir-os-dispositivos", "Abrir os dispositivos", "Configurações do Windows", "dispositivos,usb", () => Abrir("ms-settings:connecteddevices"));
        Add("abrir-as-opcoes-de-indexacao", "Abrir as opções de indexação", "Configurações do Windows", "indexar,busca", () => Abrir("ms-settings:search"));
        Add("abrir-o-historico-de-atividades", "Abrir o histórico de atividades", "Configurações do Windows", "historico,atividade", () => Abrir("ms-settings:privacy-activityhistory"));
        Add("abrir-o-sensor-de-tela", "Abrir o sensor de tela", "Configurações do Windows", "sensor,tela", () => Abrir("ms-settings:display-advancedgraphics"));
        Add("abrir-o-chrome", "Abrir o Chrome", "Aplicativos", "chrome,navegador", () => App("chrome"));
        Add("abrir-o-firefox", "Abrir o Firefox", "Aplicativos", "firefox,navegador", () => App("firefox"));
        Add("abrir-o-edge", "Abrir o Edge", "Aplicativos", "edge,navegador", () => App("msedge"));
        Add("abrir-o-word", "Abrir o Word", "Aplicativos", "word,documento", () => App("winword"));
        Add("abrir-o-excel", "Abrir o Excel", "Aplicativos", "excel,planilha", () => App("excel"));
        Add("abrir-o-powerpoint", "Abrir o PowerPoint", "Aplicativos", "powerpoint,apresentacao", () => App("powerpnt"));
        Add("abrir-o-outlook", "Abrir o Outlook", "Aplicativos", "outlook,email", () => App("outlook"));
        Add("abrir-o-teams", "Abrir o Teams", "Aplicativos", "teams,reuniao", () => App("msteams"));
        Add("abrir-o-vs-code", "Abrir o VS Code", "Aplicativos", "vscode,codigo,editor", () => App("code"));
        Add("abrir-o-visual-studio", "Abrir o Visual Studio", "Aplicativos", "visual studio,codigo", () => App("devenv"));
        Add("abrir-o-discord", "Abrir o Discord", "Aplicativos", "discord,chat", () => App("discord"));
        Add("abrir-o-steam", "Abrir o Steam", "Aplicativos", "steam,jogo", () => App("steam"));
        Add("abrir-o-vlc", "Abrir o VLC", "Aplicativos", "vlc,video,player", () => App("vlc"));
        Add("abrir-o-gimp", "Abrir o GIMP", "Aplicativos", "gimp,imagem", () => App("gimp"));
        Add("abrir-o-photoshop", "Abrir o Photoshop", "Aplicativos", "photoshop,imagem", () => App("photoshop"));
        Add("abrir-o-zoom", "Abrir o Zoom", "Aplicativos", "zoom,reuniao", () => App("zoom"));
        Add("abrir-o-7-zip", "Abrir o 7-Zip", "Aplicativos", "zip,compactar", () => App("7zFM"));
        Add("abrir-o-prompt-como-administrador", "Abrir o Prompt como administrador", "Aplicativos", "cmd,administrador", () => App("cmd-as-admin"));
        Add("somar", "Somar", "Cálculos", "somar,mais,calculadora", () => Calculo("somar", D()), "2+2 ou uma lista de números");
        Add("subtrair", "Subtrair", "Cálculos", "subtrair,menos", () => Calculo("subtrair", D()), "10-3");
        Add("multiplicar", "Multiplicar", "Cálculos", "multiplicar,vezes", () => Calculo("multiplicar", D()), "6*7");
        Add("dividir", "Dividir", "Cálculos", "dividir,por", () => Calculo("dividir", D()), "10/2");
        Add("calcular-uma-porcentagem", "Calcular uma porcentagem", "Cálculos", "porcentagem,desconto", () => Calculo("porcentagem", D()), "20% de 250");
        Add("calcular-um-aumento", "Calcular um aumento", "Cálculos", "aumento,acrescimo", () => Calculo("aumento", D()), "250 + 15%");
        Add("calcular-um-desconto", "Calcular um desconto", "Cálculos", "desconto,off", () => Calculo("desconto", D()), "250 - 15%");
        Add("dividir-uma-conta", "Dividir uma conta", "Cálculos", "dividir,conta,rachar", () => Calculo("rachar", D()), "120/4");
        Add("calcular-a-media", "Calcular a média", "Cálculos", "media,media de", () => Calculo("media", D()), "10, 8, 6");
        Add("calcular-o-imc", "Calcular o IMC", "Cálculos", "imc,peso,altura", () => Conversao("calcular-o-imc", D()), "80 1.80");
        Add("converter-celsius-para-fahrenheit", "Converter Celsius para Fahrenheit", "Cálculos", "celsius,fahrenheit", () => Conversao("converter-celsius-para-fahrenheit", D()), "30");
        Add("converter-fahrenheit-para-celsius", "Converter Fahrenheit para Celsius", "Cálculos", "fahrenheit,celsius", () => Conversao("converter-fahrenheit-para-celsius", D()), "86");
        Add("converter-quilometros-para-milhas", "Converter quilômetros para milhas", "Cálculos", "km,milhas", () => Conversao("converter-quilometros-para-milhas", D()), "42");
        Add("converter-metros-para-pes", "Converter metros para pés", "Cálculos", "metros,pés", () => Conversao("converter-metros-para-pes", D()), "1.80");
        Add("converter-quilos-para-libras", "Converter quilos para libras", "Cálculos", "quilos,libras", () => Conversao("converter-quilos-para-libras", D()), "75");
        Add("converter-litros-para-galoes", "Converter litros para galões", "Cálculos", "litros,galao", () => Conversao("converter-litros-para-galoes", D()), "10");
        Add("converter-reais-para-dolares", "Converter reais para dólares", "Cálculos", "reais,dolar,cambio", () => Conversao("converter-reais-para-dolares", D()), "100");
        Add("calcular-a-regra-de-tres", "Calcular a regra de três", "Cálculos", "regra de tres,proporcao", () => Conversao("calcular-a-regra-de-tres", D()), "a b c");
        Add("calcular-juros-simples", "Calcular juros simples", "Cálculos", "juros,simples", () => Conversao("calcular-juros-simples", D()), "1000 2 12");
        Add("calcular-o-troco", "Calcular o troco", "Cálculos", "troco,resto", () => Conversao("calcular-o-troco", D()), "50-37.20");
        Add("ver-a-data-de-hoje", "Ver a data de hoje", "Datas", "data,hoje", () => Data("ver-a-data-de-hoje"));
        Add("ver-a-hora-agora", "Ver a hora agora", "Datas", "hora,agora", () => Data("ver-a-hora-agora"));
        Add("ver-o-dia-da-semana", "Ver o dia da semana", "Datas", "dia,semana,hoje", () => Data("ver-o-dia-da-semana"));
        Add("ver-quantos-dias-faltam-para-uma-data", "Ver quantos dias faltam para uma data", "Datas", "faltam,dias,data", () => Data("ver-quantos-dias-faltam-para-uma-data"), "25/12");
        Add("ver-quantos-dias-passaram-de-uma-data", "Ver quantos dias passaram de uma data", "Datas", "passaram,desde", () => Data("ver-quantos-dias-passaram-de-uma-data"), "01/01/2024");
        Add("calcular-a-idade", "Calcular a idade", "Datas", "idade,nascimento", () => Data("calcular-a-idade"), "01/05/1990");
        Add("somar-dias-a-hoje", "Somar dias a hoje", "Datas", "somar,dias,hoje", () => Data("somar-dias-a-hoje"), "30");
        Add("ver-a-semana-do-ano", "Ver a semana do ano", "Datas", "semana,ano", () => Data("ver-a-semana-do-ano"));
        Add("ver-o-mes-por-extenso", "Ver o mês por extenso", "Datas", "mes,extenso", () => Data("ver-o-mes-por-extenso"));
        Add("ver-quanto-falta-para-o-fim-do-ano", "Ver quanto falta para o fim do ano", "Datas", "fim do ano,faltam", () => Data("ver-quanto-falta-para-o-fim-do-ano"));
        Add("ver-o-horario-de-verao", "Ver o horário de verão", "Datas", "horario,verao", () => Data("ver-o-horario-de-verao"));
        Add("ver-a-data-da-pascoa-deste-ano", "Ver a data da Páscoa deste ano", "Datas", "pascoa,data", () => Data("ver-a-data-da-pascoa-deste-ano"));
        Add("ver-o-proximo-feriado", "Ver o próximo feriado", "Datas", "feriado,proximo", () => Data("ver-o-proximo-feriado"));
        Add("ver-a-diferenca-entre-duas-datas", "Ver a diferença entre duas datas", "Datas", "diferenca,dentre,datas", () => Data("ver-a-diferenca-entre-duas-datas"), "01/01/2024 01/06/2024");
        Add("formatar-uma-data", "Formatar uma data", "Datas", "formatar,data", () => Data("formatar-uma-data"), "25/12/2026");
        Add("copiar-para-a-area-de-transferencia", "Copiar para a área de transferência", "Área de transferência", "copiar,clipboard", () => Clip("copiar-para-a-area-de-transferencia"), "o texto");
        Add("colar-da-area-de-transferencia", "Colar da área de transferência", "Área de transferência", "colar,clipboard", () => Clip("colar-da-area-de-transferencia"));
        Add("ver-o-que-esta-copiado", "Ver o que está copiado", "Área de transferência", "copiado,clipboard,ver", () => Clip("ver-o-que-esta-copiado"));
        Add("limpar-a-area-de-transferencia", "Limpar a área de transferência", "Área de transferência", "limpar,clipboard", () => Clip("limpar-a-area-de-transferencia"));
        Add("contar-as-palavras-do-texto-copiado", "Contar as palavras do texto copiado", "Área de transferência", "palavras,copiado", () => Clip("contar-as-palavras-do-texto-copiado"));
        Add("guardar-o-texto-copiado", "Guardar o texto copiado", "Área de transferência", "guardar,copiado", () => Clip("guardar-o-texto-copiado"));
        Add("traduzir-o-texto-copiado", "Traduzir o texto copiado", "Área de transferência", "traduzir,copiado", () => Clip("traduzir-o-texto-copiado"));
        Add("pesquisar-o-texto-copiado", "Pesquisar o texto copiado", "Área de transferência", "pesquisar,copiado", () => Clip("pesquisar-o-texto-copiado"));
        Add("ver-o-meu-ip-local", "Ver o meu IP local", "Rede e internet", "ip,local,rede", () => Rede("ver-o-meu-ip-local"));
        Add("ver-o-meu-ip-publico", "Ver o meu IP público", "Rede e internet", "ip,publico,internet", () => Rede("ver-o-meu-ip-publico"));
        Add("testar-a-internet", "Testar a internet", "Rede e internet", "testar,internet,ping", () => Rede("testar-a-internet"));
        Add("fazer-ping-em-um-site", "Fazer ping em um site", "Rede e internet", "ping,site,host", () => Rede("fazer-ping-em-um-site"), "o endereço");
        Add("ver-a-rota-ate-um-site", "Ver a rota até um site", "Rede e internet", "rota,traceroute", () => Rede("ver-a-rota-ate-um-site"), "o endereço");
        Add("testar-uma-porta", "Testar uma porta", "Rede e internet", "porta,testar", () => Rede("testar-uma-porta"), "site:porta");
        Add("ver-os-servidores-dns", "Ver os servidores DNS", "Rede e internet", "dns,servidores", () => Rede("ver-os-servidores-dns"));
        Add("limpar-o-dns", "Limpar o DNS", "Rede e internet", "limpar,dns", () => Rede("limpar-o-dns"));
        Add("renovar-o-ip", "Renovar o IP", "Rede e internet", "renovar,ip", () => Rede("renovar-o-ip"));
        Add("ver-as-redes-wi-fi-por-perto", "Ver as redes Wi-Fi por perto", "Rede e internet", "wifi,redes,perto", () => Rede("ver-as-redes-wi-fi-por-perto"));
        Add("ver-o-wi-fi-conectado", "Ver o Wi-Fi conectado", "Rede e internet", "wifi,conectado", () => Rede("ver-o-wi-fi-conectado"));
        Add("ver-as-senhas-de-wi-fi-salvas", "Ver as senhas de Wi-Fi salvas", "Rede e internet", "wifi,senha,salvas", () => Rede("ver-as-senhas-de-wi-fi-salvas"));
        Add("ver-o-uso-de-dados-da-rede", "Ver o uso de dados da rede", "Rede e internet", "dados,uso,rede", () => Rede("ver-o-uso-de-dados-da-rede"));
        Add("ver-os-compartilhamentos", "Ver os compartilhamentos", "Rede e internet", "compartilhar,rede", () => Rede("ver-os-compartilhamentos"));
        Add("ver-as-conexoes-abertas", "Ver as conexões abertas", "Rede e internet", "conexoes,abertas", () => Rede("ver-as-conexoes-abertas"));
        Add("ver-quem-esta-na-rede", "Ver quem está na rede", "Rede e internet", "rede,dispositivos", () => Rede("ver-quem-esta-na-rede"));
        Add("abrir-o-adaptador-de-rede", "Abrir o adaptador de rede", "Rede e internet", "adaptador,rede", () => Rede("abrir-o-adaptador-de-rede"));
        Add("desligar-o-wi-fi", "Desligar o Wi-Fi", "Rede e internet", "desligar,wifi", () => Rede("desligar-o-wi-fi"));
    }

    /* ------------------------------ texto ------------------------------ */
    private static string ClipboardSafe()
    {
        try { return System.Windows.Clipboard.GetText() ?? ""; }
        catch { return ""; }
    }

    private static string Rede(string id)
    {
        var dado = Args.Current;
        switch (id)
        {
            case "ver-o-meu-ip-local":
                return Dns.GetHostAddresses(Dns.GetHostName())
                    .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                    .Select(a => a.ToString()).FirstOrDefault() ?? "Sem IP de rede.";
            case "ver-o-meu-ip-publico":
                return HttpAsync("https://api.ipify.org").GetAwaiter().GetResult();
            case "testar-a-internet":
            case "fazer-ping-em-um-site":
            {
                var alvo = string.IsNullOrWhiteSpace(dado) ? "1.1.1.1" : dado;
                try
                {
                    using var ping = new Ping();
                    var volta = ping.Send(alvo, 3000);
                    return volta.Status == IPStatus.Success
                        ? $"{alvo} respondeu em {volta.RoundtripTime} ms."
                        : $"{alvo} não respondeu ({volta.Status}).";
                }
                catch (Exception error) { return error.Message; }
            }
            case "ver-a-rota-ate-um-site":
                return Rodar("tracert", "-h 8 -w 800 " + (string.IsNullOrWhiteSpace(dado) ? "1.1.1.1" : dado));
            case "testar-uma-porta":
            {
                var partes = dado.Split(':');
                if (partes.Length < 2 || !int.TryParse(partes[1], out var porta)) return "Diga site:porta.";
                try
                {
                    using var cliente = new TcpClient();
                    var tarefa = cliente.ConnectAsync(partes[0], porta);
                    var ok = tarefa.Wait(3000) && cliente.Connected;
                    return ok ? $"Porta {porta} aberta em {partes[0]}." : $"Porta {porta} fechada em {partes[0]}.";
                }
                catch (Exception error) { return error.Message; }
            }
            case "ver-os-servidores-dns":
            case "limpar-o-dns":
            case "renovar-o-ip":
                return Rodar("ipconfig", id == "limpar-o-dns" ? "/flushdns" : id == "renovar-o-ip" ? "/renew" : "/all");
            case "ver-as-redes-wi-fi-por-perto":
                return Rodar("netsh", "wlan show networks mode=bssid");
            case "ver-o-wi-fi-conectado":
                return Rodar("netsh", "wlan show interfaces");
            case "ver-as-senhas-de-wi-fi-salvas":
                return Rodar("netsh", "wlan show profiles");
            case "ver-os-compartilhamentos":
                return Rodar("net", "share");
            case "ver-as-conexoes-abertas":
                return Rodar("netstat", "-an");
            case "ver-o-uso-de-dados-da-rede":
            case "ver-quem-esta-na-rede":
                return Rodar("arp", "-a");
            case "abrir-o-adaptador-de-rede":
                return Abrir("ms-settings:network-status");
            case "desligar-o-wi-fi":
                return Rodar("netsh", "interface set interface name=\"Wi-Fi\" admin=disabled");
            default:
                return Rodar("ipconfig", "/all");
        }
    }

    private static string Rodar(string processo, string argumentos)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(processo, argumentos)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
            });
            if (p is null) return "Não consegui rodar.";
            var saida = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            p.WaitForExit(6000);
            return saida.Length > 900 ? saida[..900] + "…" : saida.Trim();
        }
        catch (Exception error)
        {
            return error.Message;
        }
    }

}
