using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
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



        // dado vazio = usa o que está copiado
        string Texto(string valor) => string.IsNullOrWhiteSpace(valor) ? ClipboardSafe() : valor;

        string Copia(string texto)
        {
            var curto = texto.Length > 140 ? texto[..140] + "…" : texto;
            try { System.Windows.Clipboard.SetText(texto); return "Copiado: " + curto; }
            catch { return curto; }
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

        Add("pesquisar-na-web", "Pesquisar na web", "Web e pesquisa", "pesquisar,web,busca", () => Abrir($"https://duckduckgo.com/?q={Uri.EscapeDataString(Args.Current)}"), "o que pesquisar");
        Add("pesquisar-no-google", "Pesquisar no Google", "Web e pesquisa", "google,pesquisar", () => Abrir($"https://www.google.com/search?q={Uri.EscapeDataString(Args.Current)}"), "o que pesquisar");
        Add("pesquisar-no-youtube", "Pesquisar no YouTube", "Web e pesquisa", "youtube,video", () => Abrir($"https://www.youtube.com/results?search_query={Uri.EscapeDataString(Args.Current)}"), "o que assistir");
        Add("pesquisar-imagens", "Pesquisar imagens", "Web e pesquisa", "imagens,fotos", () => Abrir($"https://www.google.com/search?tbm=isch&q={Uri.EscapeDataString(Args.Current)}"), "o que procurar");
        Add("pesquisar-no-github", "Pesquisar no GitHub", "Web e pesquisa", "github,codigo", () => Abrir($"https://github.com/search?q={Uri.EscapeDataString(Args.Current)}"), "o que procurar");
        Add("pesquisar-no-stack-overflow", "Pesquisar no Stack Overflow", "Web e pesquisa", "stackoverflow,erro,codigo", () => Abrir($"https://stackoverflow.com/search?q={Uri.EscapeDataString(Args.Current)}"), "o erro ou assunto");
        Add("pesquisar-na-wikipedia", "Pesquisar na Wikipedia", "Web e pesquisa", "wikipedia,enciclopedia", () => Abrir($"https://pt.wikipedia.org/w/index.php?search={Uri.EscapeDataString(Args.Current)}"), "o assunto");
        Add("pesquisar-no-mercado-livre", "Pesquisar no Mercado Livre", "Web e pesquisa", "comprar,preco,produto", () => Abrir($"https://lista.mercadolivre.com.br/{Uri.EscapeDataString(Args.Current)}"), "o produto");
        Add("pesquisar-videos", "Pesquisar vídeos", "Web e pesquisa", "video,assistir", () => Abrir($"https://www.youtube.com/results?search_query={Uri.EscapeDataString(Args.Current)}"), "o assunto");
        Add("abrir-um-site", "Abrir um site", "Web e pesquisa", "site,url,navegador", () => Abrir(Args.Current.Contains("://") ? Args.Current : "https://" + Args.Current), "o endereço");
        Add("abrir-um-link-do-youtube", "Abrir um link do YouTube", "Web e pesquisa", "youtube,video", () => Abrir(Args.Current.Contains("://") ? Args.Current : $"https://www.youtube.com/results?search_query={Uri.EscapeDataString(Args.Current)}"), "o endereço ou o nome");
        Add("ver-um-lugar-no-mapa", "Ver um lugar no mapa", "Web e pesquisa", "mapa,lugar,endereco", () => Abrir($"https://www.google.com/maps/search/{Uri.EscapeDataString(Args.Current)}"), "o lugar");
        Add("tracar-uma-rota", "Traçar uma rota", "Web e pesquisa", "rota,caminho,ir", () => Abrir($"https://www.google.com/maps/dir/{Uri.EscapeDataString(Args.Current)}"), "de onde para onde");
        Add("traduzir-um-texto", "Traduzir um texto", "Web e pesquisa", "traduzir,idioma", () => Abrir($"https://translate.google.com/?sl=auto&tl=pt&text={Uri.EscapeDataString(Args.Current)}&op=translate"), "o texto");
        Add("ver-a-previsao-do-tempo", "Ver a previsão do tempo", "Web e pesquisa", "tempo,clima,chuva", () => Abrir($"https://www.google.com/search?q=previs%C3%A3o+do+tempo+{Uri.EscapeDataString(Args.Current)}"), "a cidade");
        Add("ver-a-cotacao-do-dolar", "Ver a cotação do dólar", "Web e pesquisa", "dolar,cotacao,moeda", () => Abrir("https://www.google.com/search?q=cota%C3%A7%C3%A3o+hoje"), "a moeda ou deixe vazio");
        Add("ler-as-noticias", "Ler as notícias", "Web e pesquisa", "noticias,jornal", () => Abrir(Args.Current.Length > 0 ? $"https://news.google.com/search?q={Uri.EscapeDataString(Args.Current)}" : "https://news.google.com"), "o assunto");
        Add("pesquisar-no-reddit", "Pesquisar no Reddit", "Web e pesquisa", "reddit,forum", () => Abrir($"https://www.reddit.com/search/?q={Uri.EscapeDataString(Args.Current)}"), "o assunto");
        Add("pesquisar-no-x", "Pesquisar no X", "Web e pesquisa", "x,twitter,post", () => Abrir($"https://x.com/search?q={Uri.EscapeDataString(Args.Current)}"), "o assunto");
        Add("pesquisar-um-filme", "Pesquisar um filme", "Web e pesquisa", "filme,serie,onde", () => Abrir($"https://www.google.com/search?q={Uri.EscapeDataString(Args.Current)}+onde+assistir"), "o filme");
        Add("pesquisar-uma-musica", "Pesquisar uma música", "Web e pesquisa", "musica,cancao,ouvir", () => Abrir($"https://open.spotify.com/search/{Uri.EscapeDataString(Args.Current)}"), "a música");
        Add("pesquisar-uma-receita", "Pesquisar uma receita", "Web e pesquisa", "receita,comida,cozinhar", () => Abrir($"https://www.google.com/search?q=receita+{Uri.EscapeDataString(Args.Current)}"), "o prato");
        Add("pesquisar-uma-vaga", "Pesquisar uma vaga", "Web e pesquisa", "emprego,vaga,linkedin", () => Abrir($"https://www.linkedin.com/jobs/search/?keywords={Uri.EscapeDataString(Args.Current)}"), "o cargo");
        Add("abrir-o-e-mail", "Abrir o e-mail", "Web e pesquisa", "email,gmail,caixa", () => Abrir("https://mail.google.com"));
        Add("escrever-um-e-mail", "Escrever um e-mail", "Web e pesquisa", "email,escrever", () => Abrir($"https://mail.google.com/mail/?view=cm&tf=1&su={Uri.EscapeDataString(Args.Current)}"), "para quem e sobre o quê");
        Add("pesquisar-no-e-mail", "Pesquisar no e-mail", "Web e pesquisa", "email,buscar", () => Abrir($"https://mail.google.com/mail/u/0/#search/{Uri.EscapeDataString(Args.Current)}"), "o que procurar");
        Add("abrir-a-agenda", "Abrir a agenda", "Web e pesquisa", "agenda,calendario,compromisso", () => Abrir("https://calendar.google.com"));
        Add("criar-um-evento", "Criar um evento", "Web e pesquisa", "evento,agenda,marcar", () => Abrir($"https://calendar.google.com/calendar/render?action=TEMPLATE&text={Uri.EscapeDataString(Args.Current)}"), "o evento");
        Add("abrir-os-documentos", "Abrir os documentos", "Web e pesquisa", "docs,documento", () => Abrir("https://docs.google.com/document/u/0/"));
        Add("abrir-as-planilhas", "Abrir as planilhas", "Web e pesquisa", "planilha,sheets", () => Abrir("https://docs.google.com/spreadsheets/u/0/"));
        Add("abrir-as-apresentacoes", "Abrir as apresentações", "Web e pesquisa", "slides,apresentacao", () => Abrir("https://docs.google.com/presentation/u/0/"));
        Add("abrir-o-drive", "Abrir o Drive", "Web e pesquisa", "drive,arquivos,nuvem", () => Abrir("https://drive.google.com"));
        Add("abrir-o-whatsapp", "Abrir o WhatsApp", "Web e pesquisa", "whatsapp,mensagem", () => Abrir("https://web.whatsapp.com"));
        Add("mandar-mensagem-no-whatsapp", "Mandar mensagem no WhatsApp", "Web e pesquisa", "whatsapp,mensagem,enviar", () => Abrir($"https://wa.me/{new string(Args.Current.Where(char.IsDigit).ToArray())}"), "o número");
        Add("abrir-o-telegram", "Abrir o Telegram", "Web e pesquisa", "telegram,mensagem", () => Abrir("https://web.telegram.org"));
        Add("abrir-o-instagram", "Abrir o Instagram", "Web e pesquisa", "instagram,social", () => Abrir("https://www.instagram.com"));
        Add("abrir-o-linkedin", "Abrir o LinkedIn", "Web e pesquisa", "linkedin,emprego", () => Abrir("https://www.linkedin.com"));
        Add("abrir-o-spotify", "Abrir o Spotify", "Web e pesquisa", "spotify,musica", () => Abrir("https://open.spotify.com"));
        Add("abrir-a-netflix", "Abrir a Netflix", "Web e pesquisa", "netflix,filme", () => Abrir("https://www.netflix.com"));
        Add("abrir-o-notion", "Abrir o Notion", "Web e pesquisa", "notion,anotacao", () => Abrir("https://www.notion.so"));
        Add("abrir-o-figma", "Abrir o Figma", "Web e pesquisa", "figma,design", () => Abrir("https://www.figma.com"));
        Add("abrir-o-canva", "Abrir o Canva", "Web e pesquisa", "canva,design,arte", () => Abrir("https://www.canva.com"));
        Add("abrir-um-pdf", "Abrir um PDF", "Web e pesquisa", "pdf,documento", () => Abrir(Args.Current), "o caminho ou o link");
        Add("baixar-um-arquivo", "Baixar um arquivo", "Web e pesquisa", "baixar,download", () => Abrir(Args.Current), "o link");
        Add("procurar-um-arquivo", "Procurar um arquivo", "Arquivos e pastas", "procurar,arquivo,nome", () => Busca(Args.Current), "o nome");
        Add("procurar-uma-pasta", "Procurar uma pasta", "Arquivos e pastas", "procurar,pasta", () => BuscaPasta(Args.Current), "o nome");
        Add("abrir-a-pasta-de-um-arquivo", "Abrir a pasta de um arquivo", "Arquivos e pastas", "abrir,pasta,arquivo", () => AbrirPastaDe(Args.Current), "o nome");
        Add("ver-o-tamanho-de-uma-pasta", "Ver o tamanho de uma pasta", "Arquivos e pastas", "tamanho,pasta", () => Tamanho(Args.Current), "o caminho ou o nome");
        Add("contar-os-arquivos-de-uma-pasta", "Contar os arquivos de uma pasta", "Arquivos e pastas", "contar,arquivos", () => Contar(Args.Current), "o caminho");
        Add("listar-o-que-tem-em-uma-pasta", "Listar o que tem em uma pasta", "Arquivos e pastas", "listar,pasta", () => Listar(Args.Current), "o caminho");
        Add("abrir-um-caminho", "Abrir um caminho", "Arquivos e pastas", "abrir,caminho", () => Abrir(Args.Current), "o caminho");
        Add("criar-uma-pasta", "Criar uma pasta", "Arquivos e pastas", "criar,pasta", () => CriarPasta(Args.Current), "o caminho");
        Add("criar-um-arquivo-de-texto", "Criar um arquivo de texto", "Arquivos e pastas", "criar,arquivo,texto", () => CriarArquivo(Args.Current), "o caminho");
        Add("renomear-um-arquivo", "Renomear um arquivo", "Arquivos e pastas", "renomear,arquivo", () => Renomear(Args.Current), "de → para");
        Add("copiar-um-arquivo", "Copiar um arquivo", "Arquivos e pastas", "copiar,arquivo", () => Copiar(Args.Current), "origem → destino");
        Add("mover-um-arquivo", "Mover um arquivo", "Arquivos e pastas", "mover,arquivo", () => Mover(Args.Current), "origem → destino");
        Add("apagar-um-arquivo", "Apagar um arquivo", "Arquivos e pastas", "apagar,arquivo,excluir", () => Apagar(Args.Current), "o caminho");
        Add("ler-um-arquivo-de-texto", "Ler um arquivo de texto", "Arquivos e pastas", "ler,arquivo,texto", () => Ler(Args.Current), "o caminho");
        Add("contar-as-palavras", "Contar as palavras", "Texto", "contar,palavras", () => Texto(Args.Current).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length + " palavras.", "o texto");
        Add("contar-as-letras", "Contar as letras", "Texto", "contar,letras,caracteres", () => Texto(Args.Current).Length.ToString("N0") + " letras.", "o texto");
        Add("deixar-em-maiusculas", "Deixar em MAIÚSCULAS", "Texto", "maiusculas,texto", () => Copia(Texto(Args.Current).ToUpperInvariant()), "o texto");
        Add("deixar-em-minusculas", "Deixar em minúsculas", "Texto", "minusculas,texto", () => Copia(Texto(Args.Current).ToLowerInvariant()), "o texto");
        Add("deixar-em-titulo", "Deixar em Título", "Texto", "titulo,capitalizar", () => Copia(Cultura.TextInfo.ToTitleCase(Texto(Args.Current).ToLowerInvariant())), "o texto");
        Add("tirar-os-acentos", "Tirar os acentos", "Texto", "acentos,texto", () => Copia(SemAcento(Texto(Args.Current))), "o texto");
        Add("inverter-o-texto", "Inverter o texto", "Texto", "inverter,texto", () => Copia(new string(Texto(Args.Current).Reverse().ToArray())), "o texto");
        Add("ordenar-as-linhas", "Ordenar as linhas", "Texto", "ordenar,linhas", () => Copia(string.Join("\n", Texto(Args.Current).Split('\n').Select(l => l.Trim()).OrderBy(l => l, StringComparer.CurrentCulture))), "o texto");
        Add("remover-linhas-repetidas", "Remover linhas repetidas", "Texto", "repetidas,linhas", () => Copia(string.Join("\n", Texto(Args.Current).Split('\n').Select(l => l.Trim()).Distinct())), "o texto");
        Add("remover-linhas-vazias", "Remover linhas vazias", "Texto", "vazias,linhas", () => Copia(string.Join("\n", Texto(Args.Current).Split('\n').Where(l => l.Trim().Length > 0))), "o texto");
        Add("juntar-as-linhas", "Juntar as linhas", "Texto", "juntar,linhas,paragrafo", () => Copia(string.Join(" ", Texto(Args.Current).Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0))), "o texto");
        Add("numerar-as-linhas", "Numerar as linhas", "Texto", "numerar,linhas", () => Copia(string.Join("\n", Texto(Args.Current).Split('\n').Select((l, i) => $"{i + 1}. {l.Trim()}"))), "o texto");
        Add("colocar-em-lista", "Colocar em lista", "Texto", "lista,marcadores", () => Copia(string.Join("\n", Texto(Args.Current).Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).Select(l => "• " + l))), "o texto");
        Add("tirar-os-espacos-duplos", "Tirar os espaços duplos", "Texto", "espacos,texto", () => Copia(System.Text.RegularExpressions.Regex.Replace(Texto(Args.Current), @"[ \t]{2,}", " ")), "o texto");
        Add("copiar-em-base64", "Copiar em Base64", "Texto", "base64,codificar", () => Copia(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Texto(Args.Current)))), "o texto");
        Add("embaralhar-as-linhas", "Embaralhar as linhas", "Texto", "embaralhar,linhas", () => Copia(string.Join("\n", Texto(Args.Current).Split('\n').OrderBy(_ => Guid.NewGuid()))), "o texto");
        Add("sortear-uma-linha", "Sortear uma linha", "Texto", "sortear,escolher,linha", () => Sortear(Texto(Args.Current)), "o texto");
        Add("trocar-uma-palavra", "Trocar uma palavra", "Texto", "trocar,substituir,palavra", () => Trocar(Args.Current), "antiga → nova");
        Add("focar-uma-janela", "Focar uma janela", "Janelas e mouse", "focar,janela,trazer", () => Pc.Focus(Args.Current) ? $"Trouxe {Args.Current} para a frente." : $"Não achei uma janela de {Args.Current}.", "o nome da janela");
        Add("maximizar-uma-janela", "Maximizar uma janela", "Janelas e mouse", "maximizar,janela", () => Pc.Maximize(Args.Current) ? $"Maximizei {Args.Current}." : $"Não achei {Args.Current}.", "o nome da janela");
        Add("minimizar-uma-janela", "Minimizar uma janela", "Janelas e mouse", "minimizar,janela", () => Pc.Minimize(Args.Current) ? $"Minimizei {Args.Current}." : $"Não achei {Args.Current}.", "o nome da janela");
        Add("fechar-uma-janela", "Fechar uma janela", "Janelas e mouse", "fechar,janela", () => Pc.Close(Args.Current) ? $"Fechei {Args.Current}." : $"Não achei {Args.Current}.", "o nome da janela");
        Add("mover-o-mouse", "Mover o mouse", "Janelas e mouse", "mover,mouse,cursor", () => { if (Pc.ParsePoint(Args.Current) is not { } p) return "Diga a posição como 640,400."; Pc.Move(p.X, p.Y); return $"Mouse em {p.X},{p.Y}."; }, "x,y");
        Add("clicar-em-uma-posicao", "Clicar em uma posição", "Janelas e mouse", "clicar,posicao,mouse", () => { if (Pc.ParsePoint(Args.Current) is not { } p) return "Diga a posição como 640,400."; Pc.Click(p.X, p.Y); return $"Cliquei em {p.X},{p.Y}."; }, "x,y");
        Add("clicar-com-o-botao-direito", "Clicar com o botão direito", "Janelas e mouse", "clique,direito", () => { if (Pc.ParsePoint(Args.Current) is not { } p) return "Diga a posição como 640,400."; Pc.Click(p.X, p.Y, "right"); return $"Clique direito em {p.X},{p.Y}."; }, "x,y");
        Add("clique-duplo", "Clique duplo", "Janelas e mouse", "clique,duplo", () => { if (Pc.ParsePoint(Args.Current) is not { } p) return "Diga a posição como 640,400."; Pc.DoubleClick(p.X, p.Y); return $"Clique duplo em {p.X},{p.Y}."; }, "x,y");
        Add("digitar-um-texto", "Digitar um texto", "Janelas e mouse", "digitar,escrever", () => { Pc.Type(Args.Current); return "Digitei."; }, "o texto");
        Add("apertar-um-atalho", "Apertar um atalho", "Janelas e mouse", "atalho,tecla,aperte", () => { Pc.Hotkey(Args.Current); return $"Atalho {Args.Current} enviado."; }, "ctrl+t");
        Add("rolar-a-tela", "Rolar a tela", "Janelas e mouse", "rolar,scroll", () => { var n = int.TryParse(Args.Current, out var valor) ? valor : 3; Pc.Scroll(n); return $"Rolei {n}."; }, "quantos cliques");
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
        Add("somar", "Somar", "Cálculos", "somar,mais,calculadora", () => Calculo("somar", Args.Current), "2+2 ou uma lista de números");
        Add("subtrair", "Subtrair", "Cálculos", "subtrair,menos", () => Calculo("subtrair", Args.Current), "10-3");
        Add("multiplicar", "Multiplicar", "Cálculos", "multiplicar,vezes", () => Calculo("multiplicar", Args.Current), "6*7");
        Add("dividir", "Dividir", "Cálculos", "dividir,por", () => Calculo("dividir", Args.Current), "10/2");
        Add("calcular-uma-porcentagem", "Calcular uma porcentagem", "Cálculos", "porcentagem,desconto", () => Calculo("porcentagem", Args.Current), "20% de 250");
        Add("calcular-um-aumento", "Calcular um aumento", "Cálculos", "aumento,acrescimo", () => Calculo("aumento", Args.Current), "250 + 15%");
        Add("calcular-um-desconto", "Calcular um desconto", "Cálculos", "desconto,off", () => Calculo("desconto", Args.Current), "250 - 15%");
        Add("dividir-uma-conta", "Dividir uma conta", "Cálculos", "dividir,conta,rachar", () => Calculo("rachar", Args.Current), "120/4");
        Add("calcular-a-media", "Calcular a média", "Cálculos", "media,media de", () => Calculo("media", Args.Current), "10, 8, 6");
        Add("calcular-o-imc", "Calcular o IMC", "Cálculos", "imc,peso,altura", () => Conversao("calcular-o-imc", Args.Current), "80 1.80");
        Add("converter-celsius-para-fahrenheit", "Converter Celsius para Fahrenheit", "Cálculos", "celsius,fahrenheit", () => Conversao("converter-celsius-para-fahrenheit", Args.Current), "30");
        Add("converter-fahrenheit-para-celsius", "Converter Fahrenheit para Celsius", "Cálculos", "fahrenheit,celsius", () => Conversao("converter-fahrenheit-para-celsius", Args.Current), "86");
        Add("converter-quilometros-para-milhas", "Converter quilômetros para milhas", "Cálculos", "km,milhas", () => Conversao("converter-quilometros-para-milhas", Args.Current), "42");
        Add("converter-metros-para-pes", "Converter metros para pés", "Cálculos", "metros,pés", () => Conversao("converter-metros-para-pes", Args.Current), "1.80");
        Add("converter-quilos-para-libras", "Converter quilos para libras", "Cálculos", "quilos,libras", () => Conversao("converter-quilos-para-libras", Args.Current), "75");
        Add("converter-litros-para-galoes", "Converter litros para galões", "Cálculos", "litros,galao", () => Conversao("converter-litros-para-galoes", Args.Current), "10");
        Add("converter-reais-para-dolares", "Converter reais para dólares", "Cálculos", "reais,dolar,cambio", () => Conversao("converter-reais-para-dolares", Args.Current), "100");
        Add("calcular-a-regra-de-tres", "Calcular a regra de três", "Cálculos", "regra de tres,proporcao", () => Conversao("calcular-a-regra-de-tres", Args.Current), "a b c");
        Add("calcular-juros-simples", "Calcular juros simples", "Cálculos", "juros,simples", () => Conversao("calcular-juros-simples", Args.Current), "1000 2 12");
        Add("calcular-o-troco", "Calcular o troco", "Cálculos", "troco,resto", () => Conversao("calcular-o-troco", Args.Current), "50-37.20");
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

    /// <summary>Abre um endereço, arquivo ou pasta (usado fora do Register também).</summary>
    private static string Abrir(string alvo)
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

    /* ------------------------------ arquivos ------------------------------ */
    private static string Busca(string nome)
    {
        foreach (var raiz in Raizes())
        {
            try
            {
                var achado = Directory.EnumerateFiles(raiz, "*" + nome + "*", SearchOption.AllDirectories)
                    .Take(5).ToList();
                if (achado.Count > 0) return string.Join("\n", achado);
            }
            catch { }
        }
        return "Não achei nenhum arquivo com esse nome.";
    }

    private static string BuscaPasta(string nome)
    {
        foreach (var raiz in Raizes())
        {
            try
            {
                var achado = Directory.EnumerateDirectories(raiz, "*" + nome + "*", SearchOption.AllDirectories)
                    .Take(5).ToList();
                if (achado.Count > 0) return string.Join("\n", achado);
            }
            catch { }
        }
        return "Não achei nenhuma pasta com esse nome.";
    }

    private static IEnumerable<string> Raizes()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        yield return @"C:\ProgramData";
    }

    private static string AbrirPastaDe(string nome)
    {
        var lista = Busca(nome).Split('\n');
        var caminho = lista.FirstOrDefault();
        if (caminho is null || !File.Exists(caminho)) return "Não achei esse arquivo.";
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + caminho + "\"") { UseShellExecute = true });
            return "Pasta aberta em " + Path.GetDirectoryName(caminho);
        }
        catch (Exception error)
        {
            return error.Message;
        }
    }

    private static string Tamanho(string caminho)
    {
        try
        {
            if (File.Exists(caminho)) return $"{new FileInfo(caminho).Length / 1024.0 / 1024.0:0.#} MB.";
            if (!Directory.Exists(caminho)) return "Não achei esse caminho.";
            var total = new DirectoryInfo(caminho).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
            return $"{total / 1024.0 / 1024.0:0.#} MB em {caminho}.";
        }
        catch (Exception error)
        {
            return error.Message;
        }
    }

    private static string Contar(string caminho)
    {
        try
        {
            if (!Directory.Exists(caminho)) return "Não achei essa pasta.";
            var arquivos = Directory.EnumerateFiles(caminho).Count();
            var pastas = Directory.EnumerateDirectories(caminho).Count();
            return $"{arquivos} arquivos e {pastas} pastas.";
        }
        catch (Exception error)
        {
            return error.Message;
        }
    }

    private static string Listar(string caminho)
    {
        try
        {
            if (!Directory.Exists(caminho)) return "Não achei essa pasta.";
            var itens = new DirectoryInfo(caminho).EnumerateFileSystemInfos()
                .OrderByDescending(i => i.LastWriteTimeUtc).Take(20)
                .Select(i => i.Name);
            return string.Join("\n", itens);
        }
        catch (Exception error)
        {
            return error.Message;
        }
    }

    private static string Pasta(string caminho) => Directory.Exists(caminho)
        ? "Já existe."
        : Criar(caminho);

    private static string Criar(string caminho)
    {
        try
        {
            Directory.CreateDirectory(caminho);
            return "Criado em " + caminho;
        }
        catch (Exception error)
        {
            return error.Message;
        }
    }

    private static string CriarPasta(string caminho) => Criar(caminho);

    private static string CriarArquivo(string caminho)
    {
        try
        {
            File.WriteAllText(caminho, "");
            return "Arquivo criado em " + caminho;
        }
        catch (Exception error)
        {
            return error.Message;
        }
    }

    private static (string A, string B) Dois(string dado)
    {
        var partes = dado.Split(new[] { "->", "→", " para ", "|" }, StringSplitOptions.TrimEntries);
        return partes.Length >= 2 ? (partes[0], partes[1]) : (dado, dado);
    }

    private static string Renomear(string dado)
    {
        var (origem, destino) = Dois(dado);
        try
        {
            File.Move(origem, destino);
            return "Renomeado para " + destino;
        }
        catch (Exception error)
        {
            return error.Message;
        }
    }

    private static string Copiar(string dado)
    {
        var (origem, destino) = Dois(dado);
        try
        {
            File.Copy(origem, destino, true);
            return "Copiado para " + destino;
        }
        catch (Exception error)
        {
            return error.Message;
        }
    }

    private static string Mover(string dado)
    {
        var (origem, destino) = Dois(dado);
        try
        {
            File.Move(origem, destino, true);
            return "Movido para " + destino;
        }
        catch (Exception error)
        {
            return error.Message;
        }
    }

    private static string Apagar(string caminho)
    {
        try
        {
            if (File.Exists(caminho)) { File.Delete(caminho); return "Apagado."; }
            if (Directory.Exists(caminho)) { Directory.Delete(caminho, true); return "Pasta apagada."; }
            return "Não achei esse caminho.";
        }
        catch (Exception error)
        {
            return error.Message;
        }
    }

    private static string Ler(string caminho)
    {
        try
        {
            var texto = File.ReadAllText(caminho);
            return texto.Length > 600 ? texto[..600] + "…" : texto;
        }
        catch (Exception error)
        {
            return error.Message;
        }
    }

    /* ------------------------------ texto ------------------------------ */
    private static string SemAcento(string texto)
    {
        var normalizado = texto.Normalize(NormalizationForm.FormD);
        var saida = new StringBuilder();
        foreach (var c in normalizado)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) saida.Append(c);
        }
        return saida.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string Sortear(string texto)
    {
        var linhas = texto.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        return linhas.Count == 0 ? "Nada para sortear." : "Sorteado: " + linhas[Random.Shared.Next(linhas.Count)];
    }

    private static string Trocar(string dado)
    {
        var (de, para) = Dois(dado);
        var alvo = ClipboardSafe();
        return alvo.Length == 0 ? "Copie um texto primeiro." : "Trocado: " + Curto(alvo.Replace(de, para));
    }

    private static string Curto(string texto) => texto.Length > 140 ? texto[..140] + "…" : texto;

    private static string ClipboardSafe()
    {
        try { return System.Windows.Clipboard.GetText() ?? ""; }
        catch { return ""; }
    }

    /* ------------------------------ conversões ------------------------------ */
    private static string Conversao(string id, string dado)
    {
        var numeros = Regex.Matches(dado.Replace(",", " "), @"-?\d+([.,]\d+)?")
            .Select(m => double.TryParse(m.Value.Replace(',', '.'), NumberStyles.Any, Cultura, out var n) ? n : 0)
            .ToList();
        if (numeros.Count == 0) return "Preciso dos números.";
        double Um(Func<double, double> conta) => conta(numeros[0]);
        var imc = numeros.Count >= 2 ? numeros[0] / Math.Pow(numeros[1], 2) : 0;

        return id switch
        {
            "calcular-o-imc" => imc.ToString("0.0", Cultura) + " — " + (imc switch
            {
                < 18.5 => "abaixo do ideal",
                < 25 => "dentro do ideal",
                < 30 => "acima do ideal",
                _ => "alto",
            }),
            "converter-celsius-para-fahrenheit" => Um(c => c * 9 / 5 + 32).ToString("0.#", Cultura) + " °F",
            "converter-fahrenheit-para-celsius" => Um(f => (f - 32) * 5 / 9).ToString("0.#", Cultura) + " °C",
            "converter-quilometros-para-milhas" => Um(km => km * 0.621371).ToString("0.##", Cultura) + " mi",
            "converter-metros-para-pes" => Um(m => m * 3.28084).ToString("0.##", Cultura) + " ft",
            "converter-quilos-para-libras" => Um(kg => kg * 2.20462).ToString("0.#", Cultura) + " lb",
            "converter-litros-para-galoes" => Um(l => l * 0.264172).ToString("0.##", Cultura) + " gal",
            "converter-reais-para-dolares" => Um(r => r / 5.4).ToString("0.00", Cultura) + " USD (câmbio ~5,40)",
            "calcular-a-regra-de-tres" when numeros.Count >= 3 => (numeros[1] * numeros[2] / numeros[0]).ToString("0.###", Cultura),
            "calcular-juros-simples" when numeros.Count >= 3 => (numeros[0] * numeros[1] / 100 * numeros[2]).ToString("0.00", Cultura) + " de juros",
            "calcular-o-troco" when numeros.Count >= 2 => (numeros[0] - numeros[1]).ToString("0.00", Cultura),
            _ => Calculo("somar", dado),
        };
    }

    /* ------------------------------ cálculo ------------------------------ */
    private static string Calculo(string tipo, string dado)
    {
        var numeros = Regex.Matches(dado.Replace(",", " "), @"-?\d+([.,]\d+)?")
            .Select(m => double.TryParse(m.Value.Replace(',', '.'), NumberStyles.Any, Cultura, out var n) ? n : 0)
            .Where(n => true).ToList();
        if (numeros.Count == 0) return "Preciso dos números.";

        double resultado;
        try
        {
            resultado = tipo switch
            {
                "somar" => numeros.Sum(),
                "subtrair" => numeros.Aggregate((a, b) => a - b),
                "multiplicar" => numeros.Aggregate((a, b) => a * b),
                "dividir" => numeros.Skip(1).Aggregate(numeros[0], (a, b) => b == 0 ? a : a / b),
                "porcentagem" => numeros.Count >= 2 ? numeros[0] / 100 * numeros[1] : numeros[0] / 100,
                "aumento" => numeros.Count >= 2 ? numeros[0] * (1 + numeros[1] / 100) : numeros[0],
                "desconto" => numeros.Count >= 2 ? numeros[0] * (1 - numeros[1] / 100) : numeros[0],
                "media" => numeros.Average(),
                "rachar" => numeros.Count >= 2 ? numeros[0] / numeros[1] : numeros[0],
                _ => numeros.Sum(),
            };
        }
        catch (Exception error)
        {
            return "Não consegui calcular: " + error.Message;
        }
        return resultado.ToString("0.###", Cultura);
    }

    /* ------------------------------ datas ------------------------------ */
    private static string Data(string id)
    {
        var hoje = DateTime.Now;
        var dado = Args.Current;
        return id switch
        {
            "ver-a-data-de-hoje" => hoje.ToString("dddd, dd 'de' MMMM 'de' yyyy", Cultura),
            "ver-a-hora-agora" => hoje.ToString("HH:mm"),
            "ver-o-dia-da-semana" => hoje.ToString("dddd", Cultura),
            "ver-a-semana-do-ano" => "Semana " + ISOWeek.GetWeekOfYear(hoje) + " de " + hoje.Year,
            "ver-o-mes-por-extenso" => hoje.ToString("MMMM 'de' yyyy", Cultura),
            "ver-quanto-falta-para-o-fim-do-ano" => $"Faltam {(new DateTime(hoje.Year, 12, 31) - hoje).Days} dias.",
            "ver-a-data-da-pascoa-deste-ano" => "Páscoa de " + hoje.Year + ": " + Pascoa(hoje.Year).ToString("dd/MM/yyyy"),
            "ver-o-proximo-feriado" => ProximoFeriado(hoje),
            _ => ComData(id, hoje, dado),
        };
    }

    private static string ComData(string id, DateTime hoje, string dado)
    {
        switch (id)
        {
            case "ver-quantos-dias-faltam-para-uma-data":
                return DateTime.TryParse(dado, Cultura, DateTimeStyles.None, out var alvo)
                    ? $"{(alvo.Date - hoje.Date).Days} dias."
                    : "Diga a data como 25/12.";
            case "ver-quantos-dias-passaram-de-uma-data":
                return DateTime.TryParse(dado, Cultura, DateTimeStyles.None, out var inicio)
                    ? $"{(hoje.Date - inicio.Date).Days} dias."
                    : "Diga a data como 01/01/2024.";
            case "calcular-a-idade":
                if (!DateTime.TryParse(dado, Cultura, DateTimeStyles.None, out var nascimento)) return "Diga a data de nascimento.";
                var idade = hoje.Year - nascimento.Year;
                if (hoje.DayOfYear < nascimento.DayOfYear) idade--;
                return idade + " anos.";
            case "somar-dias-a-hoje":
                return int.TryParse(dado, out var dias) ? hoje.AddDays(dias).ToString("dd/MM/yyyy") : "Diga quantos dias.";
            case "ver-a-diferenca-entre-duas-datas":
            {
                var partes = dado.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (partes.Length < 2) return "Diga as duas datas.";
                if (!DateTime.TryParse(partes[0], Cultura, DateTimeStyles.None, out var a) ||
                    !DateTime.TryParse(partes[1], Cultura, DateTimeStyles.None, out var b)) return "Não entendi as datas.";
                return $"{(b.Date - a.Date).Days} dias.";
            }
            case "formatar-uma-data":
                return DateTime.TryParse(dado, Cultura, DateTimeStyles.None, out var quando)
                    ? quando.ToString("dddd, dd 'de' MMMM 'de' yyyy", Cultura)
                    : "Diga a data.";
            default:
                return hoje.ToString("dddd, dd/MM/yyyy HH:mm", Cultura);
        }
    }

    private static DateTime Pascoa(int ano)
    {
        // algoritmo de Meeus/Jones/Butcher
        var a = ano % 19;
        var b = ano / 100;
        var c = ano % 100;
        var e = b / 4;
        var f = b % 4;
        var g = (b + 8) / 25;
        var h = (b - g + 1) / 3;
        var i = (19 * a + b - e - h + 15) % 30;
        var k = c / 4;
        var l = c % 4;
        var m = (32 + 2 * e + 2 * k - i - l) % 7;
        var n = (a + 11 * i + 22 * m) / 451;
        var mes = (i + m - 7 * n + 114) / 31;
        var dia = (i + m - 7 * n + 114) % 31 + 1;
        return new DateTime(ano, mes, dia);
    }

    private static readonly (int Mes, int Dia, string Nome)[] Fixos =
    {
        (1, 1, "Confraternização Universal"),
        (4, 21, "Tiradentes"),
        (5, 1, "Dia do Trabalho"),
        (9, 7, "Independência"),
        (10, 12, "Nossa Senhora Aparecida"),
        (11, 2, "Finados"),
        (11, 15, "Proclamação da República"),
        (11, 20, "Consciência Negra"),
        (12, 25, "Natal"),
    };

    private static string ProximoFeriado(DateTime hoje)
    {
        foreach (var (mes, dia, nome) in Fixos)
        {
            var candidato = new DateTime(hoje.Year, mes, dia);
            if (candidato.Date > hoje.Date) return $"{nome} em {candidato:dd/MM}.";
        }
        var pascoa = Pascoa(hoje.Year + 1);
        return $"Carnaval em {pascoa.AddDays(-48):dd/MM/(yyyy)} e Natal em 25/12/{hoje.Year + 1}.";
    }

    /* ------------------------------ área de transferência ------------------------------ */
    private static string Clip(string id)
    {
        var texto = ClipboardSafe();
        var dado = Args.Current;
        switch (id)
        {
            case "copiar-para-a-area-de-transferencia":
                try { System.Windows.Clipboard.SetText(dado); return "Copiado."; }
                catch (Exception error) { return error.Message; }
            case "colar-da-area-de-transferencia":
            case "ver-o-que-esta-copiado":
                return texto.Length == 0 ? "Nada copiado." : Curto(texto);
            case "limpar-a-area-de-transferencia":
                try { System.Windows.Clipboard.Clear(); return "Limpo."; }
                catch (Exception error) { return error.Message; }
            case "contar-as-palavras-do-texto-copiado":
                return texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length + " palavras.";
            case "guardar-o-texto-copiado":
                if (texto.Length == 0) return "Nada copiado.";
                Memory.Remember(texto, "guardado");
                return "Guardado na memória.";
            case "traduzir-o-texto-copiado":
                return Abrir("https://translate.google.com/?sl=auto&tl=pt&text=" + Uri.EscapeDataString(texto));
            case "pesquisar-o-texto-copiado":
                return Abrir("https://duckduckgo.com/?q=" + Uri.EscapeDataString(texto));
            default:
                return texto.Length == 0 ? "Nada copiado." : Curto(texto);
        }
    }

    /* ------------------------------ rede ------------------------------ */
    private static async Task<string> HttpAsync(string url)
    {
        try
        {
            using var cliente = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            return await cliente.GetStringAsync(url);
        }
        catch (Exception error)
        {
            return "Erro: " + error.Message;
        }
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
