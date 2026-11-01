using System.Diagnostics;
using System.IO;
using System.Management;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace Orbi.Services;

/// <summary>
/// O segundo bloco do catálogo: mais 200 ações reais do Windows, agrupadas por
/// assunto. Todas rodam sem administrador e sem sair do PC.
/// </summary>
public static class ToolsExtra
{
    public static void Register(List<Tool> tools)
    {
        void Add(string id, string name, string group, string keywords, Func<string> run) =>
            tools.Add(new Tool(id, name, group, keywords.Split(',', StringSplitOptions.TrimEntries), run));

        string Say(string text)
        {
            Memory.Remember(text, "ferramenta");
            return text;
        }

        string Panel(string uri) => Open(uri);

        string Path_(Environment.SpecialFolder folder, string label)
        {
            var path = Environment.GetFolderPath(folder);
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return $"Não achei {label} neste PC.";
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return $"Abri {label}.";
        }

        void Key(Forms.Keys key) => Forms.SendKeys.SendWait("{" + key.ToString().ToUpperInvariant() + "}");

        /* ----------------------------- área de trabalho ----------------------------- */

        var desktop = "Área de trabalho";
        Add("dt-show-icons", "Mostrar ícones da área de trabalho", desktop, "icones,area de trabalho,mostrar",
            () => ToggleDesktopIcons(true));
        Add("dt-hide-icons", "Esconder ícones da área de trabalho", desktop, "icones,area de trabalho,esconder",
            () => ToggleDesktopIcons(false));
        Add("dt-refresh", "Atualizar a área de trabalho", desktop, "atualizar,refresh,f5,area de trabalho",
            () => { Key(Forms.Keys.F5); return "Área de trabalho atualizada."; });
        Add("dt-wallpaper", "Trocar o papel de parede", desktop, "papel de parede,wallpaper,fundo",
            () => Panel("ms-settings:personalization-background"));
        Add("dt-theme", "Temas", desktop, "tema,cores,aparencia", () => Panel("ms-settings:themes"));
        Add("dt-colors", "Cor de destaque", desktop, "cor,destaque,barra", () => Panel("ms-settings:colors"));
        Add("dt-lockscreen", "Tela de bloqueio", desktop, "bloqueio,tela,imagem", () => Panel("ms-settings:lockscreen"));
        Add("dt-taskbar", "Barra de tarefas", desktop, "barra de tarefas,taskbar", () => Panel("ms-settings:taskbar"));
        Add("dt-desktop-icons-settings", "Ícones da área de trabalho", desktop, "icones,configurar,desktop",
            () => { Shell("control.exe", "desk.cpl,,0"); return "Ícones da área de trabalho abertos."; });
        Add("dt-new-folder", "Criar uma pasta na área de trabalho", desktop, "pasta,nova,area de trabalho", () =>
        {
            var alvo = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Nova pasta");
            var nome = alvo;
            var i = 2;
            while (Directory.Exists(nome)) nome = alvo + " " + i++;
            Directory.CreateDirectory(nome);
            return "Criei " + nome;
        });

        /* ----------------------------- janelas ----------------------------- */

        var janelas = "Janelas";
        Add("win-list", "Listar janelas abertas", janelas, "listar,janelas,abertas", () =>
        {
            var lista = Pc.Windows().Take(15).Select(w => w.Process + " — " + w.Title);
            return string.Join("\n", lista);
        });
        Add("win-active", "Janela em foco", janelas, "foco,ativa,janela", () => Pc.ActiveWindowTitle() is { Length: > 0 } t ? t : "Nenhuma janela em foco.");
        Add("win-close-all", "Fechar todas as janelas do app em foco", janelas, "fechar,todas,janelas",
            () => "Use o atalho Alt+F4 na janela ativa ou me diga o nome do app.");
        Add("win-cycle", "Alternar entre janelas", janelas, "alternar,cycle,alt tab",
            () => { Pc.Hotkey("alt+tab"); return "Próxima janela."; });
        Add("win-cycle-back", "Voltar de janela", janelas, "voltar,anterior,alt tab",
            () => { Pc.Hotkey("alt+shift+tab"); return "Janela anterior."; });
        Add("win-snap-tl", "Janela no canto superior esquerdo", janelas, "canto,esquerda,cima,janela",
            () => { Pc.WinKey(0x25); Pc.WinKey(0x26); return "Janela encaixada no canto."; });
        Add("win-snap-tr", "Janela no canto superior direito", janelas, "canto,direita,cima,janela",
            () => { Pc.WinKey(0x27); Pc.WinKey(0x26); return "Janela encaixada no canto."; });
        Add("win-snap-bl", "Janela no canto inferior esquerdo", janelas, "canto,esquerda,baixo,janela",
            () => { Pc.WinKey(0x25); Pc.WinKey(0x28); return "Janela encaixada no canto."; });
        Add("win-snap-br", "Janela no canto inferior direito", janelas, "canto,direita,baixo,janela",
            () => { Pc.WinKey(0x27); Pc.WinKey(0x28); return "Janela encaixada no canto."; });
        Add("win-restore", "Restaurar a janela", janelas, "restaurar,janela,tamanho",
            () => { Pc.Hotkey("win+down"); return "Janela restaurada."; });
        Add("win-center", "Centralizar a janela", janelas, "centralizar,janela,meio",
            () => "Arraste a janela ou use Win+setas para encaixar.");
        Add("win-desktop-left", "Mover para a área de trabalho à esquerda", janelas, "area de trabalho,esquerda,mover",
            () => { Pc.Hotkey("win+ctrl+left"); return "Área de trabalho à esquerda."; });
        Add("win-desktop-right", "Mover para a área de trabalho à direita", janelas, "area de trabalho,direita,mover",
            () => { Pc.Hotkey("win+ctrl+right"); return "Área de trabalho à direita."; });
        Add("win-close-desktop", "Fechar a área de trabalho atual", janelas, "fechar,area de trabalho,virtual",
            () => { Pc.Hotkey("win+ctrl+f4"); return "Área de trabalho atual fechada."; });
        Add("win-always-on-top", "Ver as janelas em visão de tarefas", janelas, "visao,tarefas,janelas",
            () => { Pc.Hotkey("win+tab"); return "Visão de tarefas aberta."; });
        Add("win-menu", "Menu do sistema da janela", janelas, "menu,alt,espaco,janela",
            () => { Pc.Hotkey("alt+space"); return "Menu da janela aberto."; });
        Add("win-fullscreen", "Tela cheia", janelas, "tela cheia,fullscreen,f11",
            () => { Pc.Press(0x7A); return "F11 enviado."; });
        Add("win-zoom-in", "Aumentar o zoom do app", janelas, "zoom,aumentar,ctrl mais",
            () => { Pc.Hotkey("ctrl+plus"); return "Zoom aumentado."; });
        Add("win-zoom-out", "Diminuir o zoom do app", janelas, "zoom,diminuir,ctrl menos",
            () => { Pc.Hotkey("ctrl+minus"); return "Zoom diminuído."; });
        Add("win-zoom-reset", "Voltar o zoom ao normal", janelas, "zoom,normal,ctrl zero",
            () => { Pc.Hotkey("ctrl+0"); return "Zoom normal."; });

        /* ----------------------------- teclado e escrita ----------------------------- */

        var entrada = "Teclado e mouse";
        Add("type-text", "Digitar um texto", entrada, "digitar,escrever,texto,teclado",
            () => "Me diga o texto: “digite: bom dia”.");
        Add("key-enter", "Apertar Enter", entrada, "enter,confirmar", () => { Pc.Press(0x0D); return "Enter."; });
        Add("key-tab", "Apertar Tab", entrada, "tab,proximo campo", () => { Pc.Press(0x09); return "Tab."; });
        Add("key-esc", "Apertar Esc", entrada, "esc,fechar,cancelar", () => { Pc.Press(0x1B); return "Esc."; });
        Add("key-space", "Apertar espaço", entrada, "espaco,barra", () => { Pc.Press(0x20); return "Espaço."; });
        Add("key-backspace", "Apagar o último caractere", entrada, "apagar,backspace,corrigir",
            () => { Pc.Press(0x08); return "Apaguei um caractere."; });
        Add("key-delete", "Apagar à frente", entrada, "delete,apagar", () => { Pc.Press(0x2E); return "Delete."; });
        Add("key-select-all", "Selecionar tudo", entrada, "selecionar tudo,ctrl a",
            () => { Pc.Hotkey("ctrl+a"); return "Tudo selecionado."; });
        Add("key-undo", "Desfazer", entrada, "desfazer,ctrl z,errei",
            () => { Pc.Hotkey("ctrl+z"); return "Desfiz."; });
        Add("key-redo", "Refazer", entrada, "refazer,ctrl y", () => { Pc.Hotkey("ctrl+y"); return "Refiz."; });
        Add("key-copy", "Copiar", entrada, "copiar,ctrl c", () => { Pc.Hotkey("ctrl+c"); return "Copiado."; });
        Add("key-cut", "Recortar", entrada, "recortar,ctrl x", () => { Pc.Hotkey("ctrl+x"); return "Recortado."; });
        Add("key-paste", "Colar", entrada, "colar,ctrl v", () => { Pc.Hotkey("ctrl+v"); return "Colei."; });
        Add("key-save", "Salvar", entrada, "salvar,ctrl s", () => { Pc.Hotkey("ctrl+s"); return "Salvei."; });
        Add("key-find", "Procurar no app", entrada, "procurar,buscar,ctrl f",
            () => { Pc.Hotkey("ctrl+f"); return "Busca aberta."; });
        Add("key-print", "Imprimir", entrada, "imprimir,ctrl p", () => { Pc.Hotkey("ctrl+p"); return "Impressão aberta."; });
        Add("key-new-tab", "Nova aba", entrada, "aba,nova,ctrl t", () => { Pc.Hotkey("ctrl+t"); return "Nova aba."; });
        Add("key-close-tab", "Fechar a aba", entrada, "aba,fechar,ctrl w", () => { Pc.Hotkey("ctrl+w"); return "Aba fechada."; });
        Add("key-reopen-tab", "Reabrir a aba fechada", entrada, "aba,reabrir,ctrl shift t",
            () => { Pc.Hotkey("ctrl+shift+t"); return "Aba reaberta."; });
        Add("key-next-tab", "Próxima aba", entrada, "aba,proxima,ctrl tab",
            () => { Pc.Hotkey("ctrl+tab"); return "Próxima aba."; });
        Add("key-prev-tab", "Aba anterior", entrada, "aba,anterior,ctrl shift tab",
            () => { Pc.Hotkey("ctrl+shift+tab"); return "Aba anterior."; });
        Add("key-home", "Início da linha", entrada, "home,inicio", () => { Pc.Press(0x24); return "Início."; });
        Add("key-end", "Fim da linha", entrada, "end,fim", () => { Pc.Press(0x23); return "Fim."; });
        Add("key-page-up", "Subir uma página", entrada, "page up,subir", () => { Pc.Press(0x21); return "Subi uma página."; });
        Add("key-page-down", "Descer uma página", entrada, "page down,descer", () => { Pc.Press(0x22); return "Desci uma página."; });
        Add("key-f5", "Atualizar (F5)", entrada, "atualizar,f5,refresh", () => { Pc.Press(0x74); return "Atualizei."; });
        Add("key-f2", "Renomear (F2)", entrada, "renomear,f2,nome", () => { Pc.Press(0x73); return "F2 enviado."; });
        Add("key-f11", "Tela cheia (F11)", entrada, "tela cheia,f11", () => { Pc.Press(0x7A); return "F11 enviado."; });
        Add("mouse-left", "Clicar com o botão esquerdo", entrada, "clicar,esquerdo,mouse",
            () => { Forms.SendKeys.SendWait("{ENTER}"); return "Clique enviado ao elemento em foco."; });
        Add("mouse-scroll-down", "Rolar para baixo", entrada, "rolar,scroll,descer,baixo",
            () => { Pc.Scroll(-3); return "Rolei para baixo."; });
        Add("mouse-scroll-up", "Rolar para cima", entrada, "rolar,scroll,subir,cima",
            () => { Pc.Scroll(3); return "Rolei para cima."; });
        Add("mouse-scroll-top", "Ir para o topo", entrada, "topo,inicio,rolar",
            () => { Pc.Hotkey("ctrl+home"); return "Fui ao topo."; });
        Add("mouse-scroll-bottom", "Ir para o fim", entrada, "fim,rolar,baixo",
            () => { Pc.Hotkey("ctrl+end"); return "Fui ao fim."; });
        Add("mouse-where", "Onde está o mouse", entrada, "mouse,posicao,onde",
            () => { var (x, y) = Pc.CursorPosition(); return $"Mouse em {x},{y}."; });
        Add("mouse-move-center", "Levar o mouse ao centro da tela", entrada, "mouse,centro,mover",
            () => { var (w, h) = Pc.ScreenSize(); Pc.Move(w / 2, h / 2); return "Mouse no centro."; });
        Add("mouse-move-corner", "Levar o mouse ao canto", entrada, "mouse,canto,mover",
            () => { Pc.Move(4, 4); return "Mouse no canto superior esquerdo."; });
        Add("mouse-double-click", "Clique duplo no centro", entrada, "duplo,clicar,abrir",
            () => { var (w, h) = Pc.ScreenSize(); Pc.DoubleClick(w / 2, h / 2); return "Clique duplo no centro."; });

        /* ----------------------------- tela e captura ----------------------------- */

        var tela = "Tela e captura";
        Add("screen-see", "Ver a tela agora", tela, "ver,tela,descrever,o que esta na tela",
            () => Pc.DescribeScreen());
        Add("screen-shot-pictures", "Salvar captura em Imagens", tela, "captura,salvar,imagens,screenshot",
            () => "Captura salva em " + Pc.Capture());
        Add("screen-shot-desktop", "Salvar captura na área de trabalho", tela, "captura,salvar,area de trabalho",
            () => "Captura salva em " + Pc.Capture(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                $"orbi-tela-{DateTime.Now:HH:mm:ss}".Replace(":", "-") + ".png")));
        Add("screen-copy-shot", "Capturar e copiar", tela, "capturar,copiar,print",
            () => { Pc.Hotkey("win+shift+s"); return "Escolha a área na tela."; });
        Add("screen-record", "Gravar a tela", tela, "gravar,video,tela,game bar",
            () => { Pc.Hotkey("win+alt+r"); return "Gravação do Xbox Game Bar alternada."; });
        Add("screen-freeze", "Congelar a tela", tela, "congelar,pausar,tela",
            () => { Pc.WinKey(0x13); return "Tela congelada."; });
        Add("screen-project-pc", "Projetar só no PC", tela, "projetar,monitor,pc",
            () => { Pc.Hotkey("win+p"); return "Escolha “Somente tela do PC”."; });
        Add("screen-duplicate", "Duplicar a tela", tela, "duplicar,monitor,espelhar",
            () => { Pc.Hotkey("win+p"); return "Escolha “Duplicar”."; });
        Add("screen-extend", "Estender a tela", tela, "estender,monitor,segunda tela",
            () => { Pc.Hotkey("win+p"); return "Escolha “Estender”."; });
        Add("screen-scale", "Escala da tela", tela, "escala,dpi,tamanho,texto",
            () => Panel("ms-settings:display-advanced"));
        Add("screen-rotate-settings", "Rotação da tela", tela, "rotacao,girar,orientacao",
            () => Panel("ms-settings:display"));
        Add("screen-hdr", "HDR", tela, "hdr,cores,video", () => Panel("ms-settings:display-advanced"));
        Add("screen-nighty", "Luz noturna agora", tela, "luz noturna,descanso,olhos",
            () => { Pc.Hotkey("win+a"); return "Abra a luz noturna na central de ações."; });
        Add("screen-color-profile", "Perfil de cor", tela, "cor,perfil,calibracao",
            () => { Shell("control.exe", "color"); return "Perfis de cor abertos."; });
        Add("screen-dpi", "Ajustar tamanho do texto", tela, "texto,tamanho,dpi,escala",
            () => Panel("ms-settings:easeofaccess-visualeffects"));

        /* ----------------------------- som e mídia ----------------------------- */

        var som = "Som e mídia";
        Add("sound-volume-settings", "Volume do sistema", som, "volume,som,mixer",
            () => Panel("ms-settings:apps-volume"));
        Add("sound-output", "Escolher a saída de som", som, "saida,falante,headset,audio",
            () => { Shell("control.exe", "mmsys.cpl,,0"); return "Saídas de som abertas."; });
        Add("sound-input", "Escolher o microfone", som, "microfone,entrada,gravar",
            () => { Shell("control.exe", "mmsys.cpl,,1"); return "Entradas de áudio abertas."; });
        Add("sound-sounds", "Sons do Windows", som, "som,esquema,notificacao",
            () => { Shell("control.exe", "mmsys.cpl,,2"); return "Sons do Windows abertos."; });
        Add("sound-levels", "Níveis de volume dos apps", som, "niveis,app,volume",
            () => Panel("ms-settings:apps-volume"));
        Add("sound-mute-app", "Silenciar o app em foco", som, "silenciar,app,mudo",
            () => { Pc.Hotkey("win+alt+m"); return "Mudo alternado no app em foco."; });
        Add("sound-spatial", "Som espacial", som, "espacial,3d,audio",
            () => Panel("ms-settings:apps-volume"));
        Add("sound-spotify", "Abrir o Spotify", som, "spotify,musica,streaming",
            () => Open("spotify:"));
        Add("sound-youtube-music", "Abrir o YouTube Music", som, "youtube,musica,streaming",
            () => Open("https://music.youtube.com"));
        Add("sound-media-settings", "Configurações de mídia", som, "midia,player,video",
            () => Panel("ms-settings:appsfeatures"));

        /* ----------------------------- arquivos e pastas ----------------------------- */

        var arquivos = "Arquivos e pastas";
        Add("file-explorer", "Abrir o Explorador", arquivos, "explorador,arquivos,pastas",
            () => { Process.Start("explorer.exe"); return "Explorador aberto."; });
        Add("file-desktop-folder", "Abrir a área de trabalho", arquivos, "area de trabalho,pasta",
            () => Path_(Environment.SpecialFolder.DesktopDirectory, "a área de trabalho"));
        Add("file-user", "Abrir a pasta do usuário", arquivos, "usuario,casa,home",
            () => Path_(Environment.SpecialFolder.UserProfile, "a sua pasta"));
        Add("file-appdata", "Abrir %APPDATA%", arquivos, "appdata,configuracao,pasta",
            () => Path_(Environment.SpecialFolder.ApplicationData, "o AppData"));
        Add("file-localappdata", "Abrir %LOCALAPPDATA%", arquivos, "localappdata,programas",
            () => Path_(Environment.SpecialFolder.LocalApplicationData, "o LocalAppData"));
        Add("file-program-files", "Abrir Arquivos de Programas", arquivos, "programas,instalados",
            () => Path_(Environment.SpecialFolder.ProgramFiles, "Arquivos de Programas"));
        Add("file-fonts", "Abrir a pasta de fontes", arquivos, "fontes,tipografia",
            () => { Shell("control.exe", "fonts"); return "Fontes abertas."; });
        Add("file-c-drive", "Abrir o disco C:", arquivos, "disco,c,unidade",
            () => { Process.Start("explorer.exe", "C:\\"); return "Disco C: aberto."; });
        Add("file-new-folder", "Criar uma pasta em Documentos", arquivos, "criar,pasta,documentos",
            () => CreateFolder(Environment.SpecialFolder.MyDocuments));
        Add("file-new-folder-downloads", "Criar uma pasta em Downloads", arquivos, "criar,pasta,downloads",
            () => CreateFolder(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")));
        Add("file-open-downloads-latest", "Abrir o último download", arquivos, "ultimo,download,baixado", () =>
        {
            var pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            var ultimo = Directory.Exists(pasta)
                ? new DirectoryInfo(pasta).EnumerateFiles().OrderByDescending(f => f.LastWriteTime).FirstOrDefault()
                : null;
            if (ultimo is null) return "Não achei nada em Downloads.";
            Process.Start(new ProcessStartInfo(ultimo.FullName) { UseShellExecute = true });
            return "Abri " + ultimo.Name;
        });
        Add("file-sort-by-size", "Listar os maiores arquivos dos downloads", arquivos, "maiores,pesados,espaco", () =>
        {
            var pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (!Directory.Exists(pasta)) return "Não achei Downloads.";
            var lista = new DirectoryInfo(pasta).EnumerateFiles()
                .OrderByDescending(f => f.Length).Take(5)
                .Select(f => $"{f.Name} — {f.Length / 1024.0 / 1024.0:0.#} MB");
            return string.Join("\n", lista);
        });
        Add("file-free-space", "Quanto espaço livre tem", arquivos, "espaco,disco,livre", () =>
        {
            var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\");
            return $"Livre em {drive.Name}: {drive.AvailableFreeSpace / 1024.0 / 1024 / 1024:0.#} GB de {drive.TotalSize / 1024.0 / 1024 / 1024:0.#} GB.";
        });
        Add("file-properties", "Propriedades do sistema", arquivos, "propriedades,pc,especificacoes",
            () => { Shell("control.exe", "system"); return "Propriedades do sistema abertas."; });
        Add("file-recent", "Arquivos recentes", arquivos, "recentes,ultimos,arquivos",
            () => { Shell("shell:recent"); return "Arquivos recentes abertos."; });
        Add("file-network", "Rede", arquivos, "rede,compartilhamento,pc",
            () => { Shell("shell:NetworkPlacesFolder"); return "Rede aberta."; });
        Add("file-trash-size", "Tamanho da lixeira", arquivos, "lixeira,tamanho,espaco", () =>
        {
            var lixeira = Path.Combine("C:\\", "$Recycle.Bin");
            if (!Directory.Exists(lixeira)) return "Não consegui ler a lixeira.";
            long total = 0;
            try
            {
                total = new DirectoryInfo(lixeira).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
            }
            catch { }
            return $"A lixeira tem cerca de {total / 1024.0 / 1024:0.#} MB.";
        });

        /* ----------------------------- rede e internet ----------------------------- */

        var rede = "Rede e internet";
        Add("net-speed", "Testar a velocidade da internet", rede, "velocidade,internet,teste",
            () => Open("https://fast.com"));
        Add("net-wifi-list", "Redes Wi-Fi por perto", rede, "wifi,redes,procurar",
            () => { Shell("explorer.exe", "ms-availablenetworks:"); return "Redes disponíveis abertas."; });
        Add("net-wifi-settings", "Configurações de Wi-Fi", rede, "wifi,senha,configurar",
            () => Panel("ms-settings:network-wifi"));
        Add("net-airplane", "Modo avião", rede, "aviao,offline,rede",
            () => Panel("ms-settings:network-airplanemode"));
        Add("net-proxy", "Proxy", rede, "proxy,servidor,rede",
            () => Panel("ms-settings:network-proxy"));
        Add("net-vpn", "VPN", rede, "vpn,rede,privada", () => Panel("ms-settings:network-vpn"));
        Add("net-dns-settings", "Servidor DNS", rede, "dns,servidor,rapido",
            () => Panel("ms-settings:network"));
        Add("net-hostname", "Nome do computador", rede, "nome,pc,hostname", () => Say(Environment.MachineName));
        Add("net-user", "Usuário do Windows", rede, "usuario,conta,login", () => Say(Environment.UserName));
        Add("net-domain", "Domínio do usuário", rede, "dominio,usuario", () =>
            Say(Environment.UserDomainName));
        Add("net-public-ip", "Meu IP público", rede, "ip publico,externo,internet", () =>
        {
            try
            {
                using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(6) };
                var ip = client.GetStringAsync("https://api.ipify.org").GetAwaiter().GetResult();
                System.Windows.Clipboard.SetText(ip);
                return "IP público: " + ip + " (copiado).";
            }
            catch { return "Não consegui consultar o IP público agora."; }
        });
        Add("net-ping-google", "Testar a conexão com o Google", rede, "ping,google,teste", () =>
        {
            try
            {
                using var ping = new System.Net.NetworkInformation.Ping();
                var resposta = ping.Send("8.8.8.8", 1500);
                return resposta.Status == System.Net.NetworkInformation.IPStatus.Success
                    ? $"Google respondeu em {resposta.RoundtripTime} ms."
                    : "O Google não respondeu.";
            }
            catch { return "Não consegui testar agora."; }
        });
        Add("net-release-ip", "Renovar o IP", rede, "renovar,ip,dhcp",
            () => Run("ipconfig.exe", "/release"));
        Add("net-renew-ip", "Pedir um IP novo", rede, "renovar,ip,novo",
            () => Run("ipconfig.exe", "/renew"));
        Add("net-reset-winsock", "Reparar a rede", rede, "reparar,reset,rede,internet",
            () => Run("netsh.exe", "winsock reset"));
        Add("net-tracert", "Rastrear a rota até um site", rede, "rota,tracert,caminho",
            () => "Me diga o site: “rastrear rota para google.com”.");
        Add("net-firewall", "Firewall do Windows", rede, "firewall,protecao",
            () => { Shell("control.exe", "firewall.cpl"); return "Firewall aberto."; });
        Add("net-share", "Compartilhamento avançado", rede, "compartilhar,pasta,rede",
            () => { Shell("control.exe", "desk.cpl,,4"); return "Compartilhamento aberto."; });
        Add("net-hotspot", "Ponto de acesso móvel", rede, "hotspot,compartilhar,internet",
            () => Panel("ms-settings:network-mobilehotspot"));
        Add("net-netstat", "Conexões abertas", rede, "conexoes,portas,netstat", () =>
            Run("netstat.exe", "-an | more"));
        Add("net-hostname-check", "Consultar um endereço", rede, "dns,consultar,endereco",
            () => "Me diga o endereço: “consultar google.com”.");

        /* ----------------------------- energia e bateria ----------------------------- */

        var energia = "Energia e bateria";
        Add("pwr-plans", "Planos de energia", energia, "plano,energia,desempenho",
            () => { Shell("control.exe", "powercfg.cpl"); return "Planos de energia abertos."; });
        Add("pwr-saver", "Modo economia de bateria", energia, "economia,bateria,poupanca",
            () => Panel("ms-settings:batterysaver"));
        Add("pwr-battery-settings", "Bateria e energia", energia, "bateria,energia,configurar",
            () => Panel("ms-settings:battery"));
        Add("pwr-battery-level", "Nível da bateria", energia, "bateria,nivel,quanto",
            () => SystemActions.Snapshot().Battery is { } nivel ? $"Bateria em {nivel}%." : "Este PC não tem bateria.");
        Add("pwr-sleep-settings", "Suspender configurações", energia, "suspender,dormir,tempo",
            () => Panel("ms-settings:powersleep"));
        Add("pwr-screen-off", "Desligar a tela", energia, "tela,desligar,economizar", () =>
        {
            Pc.Press(0x2C); // PrintScreen não desliga; usa o monitor off
            SendMessage(0x0112, 0xF170, 2);
            return "Tela desligada. Qualquer tecla volta.";
        });
        Add("pwr-uptime", "Há quanto tempo o PC está ligado", energia, "ligado,uptime,tempo",
            () => $"Ligado há {(DateTime.Now - Boot).Days} dia(s) e {(DateTime.Now - Boot).Hours} hora(s).");
        Add("pwr-fast-startup", "Inicialização rápida", energia, "inicio,rapido,boot",
            () => { Shell("control.exe", "powercfg.cpl,,1"); return "Opções de energia abertas."; });
        Add("pwr-shutdown-5", "Desligar em 5 minutos", energia, "desligar,5 minutos,agendar",
            () => SystemActions.Shutdown(5));
        Add("pwr-restart-5", "Reiniciar em 5 minutos", energia, "reiniciar,5 minutos,agendar",
            () => SystemActions.Restart(5));
        Add("pwr-shutdown-now", "Desligar agora", energia, "desligar,agora,já",
            () => SystemActions.Shutdown(1));
        Add("pwr-lock-now", "Bloquear agora", energia, "bloquear,agora,senha", SystemActions.Lock);
        Add("pwr-signout", "Sair da conta do Windows", energia, "sair,logoff,logout",
            () => Run("shutdown.exe", "/l"));
        Add("pwr-display-off", "Apagar a tela", energia, "apagar tela,monitor,off",
            () => { SendMessage(0x0112, 0xF170, 2); return "Tela apagada."; });

        /* ----------------------------- programa e instalação ----------------------------- */

        var apps = "Apps";
        Add("app-notepad", "Abrir o Bloco de notas", apps, "notepad,bloco de notas,texto",
            () => Start("notepad.exe", "Bloco de notas aberto."));
        Add("app-calculator", "Abrir a Calculadora", apps, "calculadora,conta,matematica",
            () => Open("calculator:"));
        Add("app-paint", "Abrir o Paint", apps, "paint,desenhar,imagem", () => Start("mspaint.exe", "Paint aberto."));
        Add("app-snipping", "Ferramenta de captura", apps, "captura,recorte,snip", () => Open("ms-screenclip:"));
        Add("app-wordpad", "Abrir o WordPad", apps, "wordpad,texto,escrever",
            () => Start("write.exe", "WordPad aberto."));
        Add("app-explorer", "Explorador de arquivos", apps, "explorador,arquivos", () => Start("explorer.exe", "Explorador aberto."));
        Add("app-edge", "Abrir o Edge", apps, "edge,navegador,internet", () => Open("microsoft-edge:"));
        Add("app-spotify-search", "Pesquisar uma música", apps, "musica,pesquisar,spotify",
            () => Open("https://open.spotify.com"));
        Add("app-whatsapp", "Abrir o WhatsApp Web", apps, "whatsapp,mensagem,chat", () => Open("https://web.whatsapp.com"));
        Add("app-teams", "Abrir o Teams", apps, "teams,reuniao,time", () => Open("msteams:"));
        Add("app-onedrive", "Abrir o OneDrive", apps, "onedrive,nuvem,arquivos", () => Open("https://onedrive.live.com"));
        Add("app-excel", "Abrir o Excel Online", apps, "excel,planilha,tabela", () => Open("https://office.com"));
        Add("app-notion", "Abrir o Notion", apps, "notion,notas,documentos", () => Open("https://notion.so"));
        Add("app-github", "Abrir o GitHub", apps, "github,codigo,repositorio", () => Open("https://github.com"));
        Add("app-uninstall", "Desinstalar um programa", apps, "desinstalar,remover,programa",
            () => Panel("ms-settings:appsfeatures"));
        Add("app-default-apps", "Apps padrão", apps, "padrao,abrir com,associacao",
            () => Panel("ms-settings:defaultapps"));
        Add("app-startup", "Apps que abrem com o Windows", apps, "inicio,abrir junto,lento",
            () => Panel("ms-settings:startupapps"));
        Add("app-optional", "Recursos opcionais", apps, "recursos,opcionais,.net,idioma",
            () => Panel("ms-settings:optionalfeatures"));
        Add("app-maps", "Abrir o Maps", apps, "mapas,maps,endereco", () => Open("bingmaps:"));
        Add("app-photos-open", "Abrir a pasta de imagens", apps, "fotos,imagens,pasta",
            () => Path_(Environment.SpecialFolder.MyPictures, "as imagens"));

        /* ----------------------------- privacidade e segurança ----------------------------- */

        var seguranca = "Privacidade e segurança";
        Add("sec-defender", "Windows Security", seguranca, "defender,antivirus,seguranca",
            () => Open("windowsdefender:"));
        Add("sec-scan", "Iniciar uma verificação de vírus", seguranca, "verificar,virus,scan",
            () => Open("windowsdefender://threat"));
        Add("sec-privacy", "Privacidade", seguranca, "privacidade,permissao", () => Panel("ms-settings:privacy"));
        Add("sec-camera-permission", "Permissão da câmera", seguranca, "camera,permissao",
            () => Panel("ms-settings:privacy-webcam"));
        Add("sec-mic-permission", "Permissão do microfone", seguranca, "microfone,permissao",
            () => Panel("ms-settings:privacy-microphone"));
        Add("sec-location", "Localização", seguranca, "localizacao,privacidade",
            () => Panel("ms-settings:privacy-location"));
        Add("sec-signin", "Opções de entrada", seguranca, "entrada,senha,pin,login",
            () => Panel("ms-settings:signinoptions"));
        Add("sec-bitlocker", "BitLocker", seguranca, "bitlocker,criptografia,disco",
            () => { Shell("control.exe", "bitlocker"); return "BitLocker aberto."; });
        Add("sec-uac", "Controle de conta de usuário", seguranca, "uac,admin,permissao",
            () => { Shell("control.exe", "useraccountcontrolsettings"); return "UAC aberto."; });
        Add("sec-credential", "Gerenciador de credenciais", seguranca, "senhas,credenciais,login",
            () => { Shell("control.exe", "credentialmanager"); return "Credenciais abertas."; });
        Add("sec-certificates", "Certificados", seguranca, "certificado,seguranca",
            () => { Shell("certmgr.msc"); return "Certificados abertos."; });
        Add("sec-users", "Contas de usuário", seguranca, "usuarios,contas,pc",
            () => { Shell("control.exe", "userpasswords2"); return "Contas abertas."; });
        Add("sec-history", "Histórico de atividade", seguranca, "historico,atividade,privacidade",
            () => Panel("ms-settings:privacy-activityhistory"));
        Add("sec-lock-screen-timeout", "Bloqueio automático", seguranca, "bloqueio,tempo,automatico",
            () => Panel("ms-settings:signinoptions"));
        Add("sec-firewall-rule", "Regras de firewall", seguranca, "firewall,regra,porta",
            () => { Shell("control.exe", "firewall.cpl"); return "Firewall aberto."; });

        /* ----------------------------- acessibilidade ----------------------------- */

        var acesso = "Acessibilidade";
        Add("acc-magnifier-settings", "Configurar a lupa", acesso, "lupa,zoom,configurar",
            () => Panel("ms-settings:easeofaccess-magnifier"));
        Add("acc-text-size", "Tamanho do texto", acesso, "texto,tamanho,ler",
            () => Panel("ms-settings:easeofaccess-visualeffects"));
        Add("acc-high-contrast", "Alto contraste", acesso, "contraste,cores,visao",
            () => Panel("ms-settings:easeofaccess-contrast"));
        Add("acc-cursor", "Tamanho do cursor", acesso, "cursor,mouse,tamanho",
            () => Panel("ms-settings:easeofaccess-cursor"));
        Add("acc-caption", "Legendas", acesso, "legenda,audio,surdo",
            () => Panel("ms-settings:easeofaccess-closedcaptioning"));
        Add("acc-voice", "Voz do Windows", acesso, "voz,falar,narrar",
            () => Panel("ms-settings:speech"));
        Add("acc-speech-recognition", "Ditado", acesso, "ditar,voz,escrever",
            () => { Pc.Hotkey("win+h"); return "Ditado aberto."; });
        Add("acc-sticky-keys", "Teclas de aderência", acesso, "teclas,aderencia,shift",
            () => Panel("ms-settings:easeofaccess-keyboard"));
        Add("acc-filter-keys", "Teclas de filtragem", acesso, "teclas,filtro,repeticao",
            () => Panel("ms-settings:easeofaccess-keyboard"));
        Add("acc-narrator-start", "Abrir o Narrador", acesso, "narrador,voz,ler tela",
            () => Start("narrator.exe", "Narrador aberto."));
        Add("acc-on-screen-keyboard", "Teclado virtual", acesso, "teclado,virtual,digitar",
            () => Start("osk.exe", "Teclado virtual aberto."));

        /* ----------------------------- data, hora e sistema ----------------------------- */

        var sistema = "Sistema";
        Add("sys-date", "Que dia é hoje", sistema, "data,hoje,dia", () => Say(DateTime.Now.ToString("dddd, dd 'de' MMMM 'de' yyyy")));
        Add("sys-time", "Que horas são", sistema, "hora,agora,relogio", () => Say(DateTime.Now.ToString("HH:mm")));
        Add("sys-timezone", "Fuso horário", sistema, "fuso,horario,regiao",
            () => Panel("ms-settings:dateandtime"));
        Add("sys-language", "Idioma do Windows", sistema, "idioma,traducao,teclado",
            () => Panel("ms-settings:regionlanguage"));
        Add("sys-about", "Sobre este PC", sistema, "sobre,versao,hardware",
            () => Panel("ms-settings:about"));
        Add("sys-windows-version", "Versão do Windows", sistema, "versao,windows,build", () =>
            Say($"{Environment.OSVersion.VersionString} · {RuntimeInformationDescription()}"));
        Add("sys-cpu-name", "Qual é o processador", sistema, "processador,cpu,modelo",
            () => Query("select Name from Win32_Processor"));
        Add("sys-gpu-name", "Qual é a placa de vídeo", sistema, "gpu,video,placa",
            () => Query("select Name from Win32_VideoController"));
        Add("sys-ram-total", "Quanta memória tem o PC", sistema, "memoria,ram,total",
            () => $"{SystemActions.Snapshot().MemoryTotalGb:0.#} GB.");
        Add("sys-ram-free", "Quanta memória está livre", sistema, "memoria,ram,livre",
            () => $"{SystemActions.Snapshot().MemoryTotalGb - SystemActions.Snapshot().MemoryUsedGb:0.#} GB livres.");
        Add("sys-disk-list", "Listar as unidades de disco", sistema, "discos,unidades,c,d", () =>
        {
            var lista = DriveInfo.GetDrives().Where(d => d.IsReady)
                .Select(d => $"{d.Name} · {d.AvailableFreeSpace / 1024.0 / 1024 / 1024:0.#} GB livres de {d.TotalSize / 1024.0 / 1024 / 1024:0.#} GB");
            return string.Join("\n", lista);
        });
        Add("sys-processes", "Quais programas estão abertos", sistema, "processos,programas,abertos", () =>
        {
            var lista = System.Diagnostics.Process.GetProcesses()
                .Select(p => p.ProcessName).Distinct().OrderBy(n => n).Take(20);
            return string.Join(", ", lista);
        });
        Add("sys-heavy-process", "O que mais pesa agora", sistema, "pesado,cpu,ram,gargalo",
            () => SystemActions.Snapshot().TopProcess is { } p ? $"O mais pesado agora é {p}." : "Não achei nada pesado.");
        Add("sys-env-var", "Ver uma variável de ambiente", sistema, "variavel,ambiente,path",
            () => "Me diga o nome: “variável de ambiente PATH”.");
        Add("sys-registry-open", "Abrir o registro do usuário", sistema, "registro,reg,usuario",
            () => { Shell("regedit.exe"); return "Editor do registro aberto. Cuidado: é a raiz do Windows."; });
        Add("sys-cmd-open", "Abrir um terminal aqui", sistema, "terminal,cmd,powershell",
            () => Start("cmd.exe", "Terminal aberto."));
        Add("sys-admin-terminal", "Abrir terminal de administrador", sistema, "admin,elevado,powershell",
            () => { Shell("powershell.exe"); return "Abri o PowerShell. Para elevar, use Windows + X."; });
        Add("sys-uptime-short", "Tempo ligado em horas", sistema, "ligado,horas,uptime",
            () => $"{(int)(DateTime.Now - Boot).TotalHours} horas.");
        Add("sys-events", "Ver erros recentes", sistema, "erros,eventos,problema",
            () => { Shell("eventvwr.msc"); return "Visualizador de eventos aberto."; });
        Add("sys-device-manager", "Gerenciador de dispositivos", sistema, "driver,dispositivo",
            () => { Shell("devmgmt.msc"); return "Gerenciador de dispositivos aberto."; });
        Add("sys-dxdiag", "Diagnóstico do DirectX", sistema, "directx,dxdiag,jogo", () => Start("dxdiag.exe", "DirectX diagnósticos abertos."));
        Add("sys-regedit-user", "Ferramenta de serviços do sistema", sistema, "servicos,system",
            () => { Shell("services.msc"); return "Serviços abertos."; });
        Add("sys-winsat", "Avaliação do desempenho", sistema, "desempenho,nota,avaliacao",
            () => { Shell("control.exe", "sysdm.cpl"); return "Propriedades do sistema abertas."; });

        /* ----------------------------- desenvolvimento ----------------------------- */

        var dev = "Desenvolvimento";
        Add("dev-what-dotnet", "Qual .NET está instalado", dev, ".net,dotnet,versao", () =>
            Run("dotnet.exe", "--list-runtimes"));
        Add("dev-git-version", "Versão do Git", dev, "git,versao", () => Run("git.exe", "--version"));
        Add("dev-node-version", "Versão do Node", dev, "node,javascript,versao", () => Run("node.exe", "--version"));
        Add("dev-python-version", "Versão do Python", dev, "python,versao", () => Run("python.exe", "--version"));
        Add("dev-code", "Abrir o VS Code", dev, "vscode,code,editor", () => Start("code.cmd", "VS Code aberto."));
        Add("dev-hosts-file", "Abrir o arquivo hosts", dev, "hosts,dns,bloqueio",
            () => { Shell(Path.Combine(Environment.SystemDirectory, "drivers", "etc", "hosts")); return "Arquivo hosts aberto."; });
        Add("dev-path", "Ver o PATH", dev, "path,variavel,ambiente",
            () => { var p = Environment.GetEnvironmentVariable("PATH") ?? ""; System.Windows.Clipboard.SetText(p); return "PATH copiado (" + p.Length + " caracteres)."; });
        Add("dev-ports", "Portas em uso", dev, "portas,listening,servidor", () => Run("netstat.exe", "-an"));
        Add("dev-process-kill", "Fechar um programa travado", dev, "fechar,travar,finalizar",
            () => { Pc.Hotkey("ctrl+shift+esc"); return "Gerenciador de tarefas aberto."; });
        Add("dev-temp-folder", "Abrir a pasta de temporários", dev, "temp,temporarios,limpar",
            () => { Shell(Path.GetTempPath()); return "Temporários abertos."; });
        Add("dev-env-list", "Listar variáveis de ambiente", dev, "ambiente,variaveis", () =>
        {
            var lista = Environment.GetEnvironmentVariables().Keys.Cast<string>().OrderBy(k => k).Take(20);
            return string.Join(", ", lista);
        });
        Add("dev-guid", "Gerar um identificador", dev, "guid,uuid,id", () =>
            { var g = Guid.NewGuid().ToString(); System.Windows.Clipboard.SetText(g); return g + " (copiado)"; });
        Add("dev-format-json", "Organizar um JSON", dev, "json,formatar,indentar", () =>
        {
            try
            {
                var doc = System.Text.Json.JsonDocument.Parse(System.Windows.Clipboard.GetText());
                var bonito = System.Text.Json.JsonSerializer.Serialize(doc,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                System.Windows.Clipboard.SetText(bonito);
                return "JSON organizado e copiado.";
            }
            catch { return "O texto copiado não é um JSON válido."; }
        });
        Add("dev-json-minify", "Compactar um JSON", dev, "json,minificar,compactar", () =>
        {
            try
            {
                var doc = System.Text.Json.JsonDocument.Parse(System.Windows.Clipboard.GetText());
                System.Windows.Clipboard.SetText(System.Text.Json.JsonSerializer.Serialize(doc));
                return "JSON compactado e copiado.";
            }
            catch { return "O texto copiado não é um JSON válido."; }
        });
        Add("dev-cpf-validate", "Conferir um CPF", dev, "cpf,validar,documento", () =>
        {
            var digitos = new string(System.Windows.Clipboard.GetText().Where(char.IsDigit).ToArray());
            if (digitos.Length != 11) return "Copie um CPF de 11 dígitos antes.";
            var valido = true;
            for (var posicao = 9; posicao < 11 && valido; posicao++)
            {
                var soma = 0;
                for (var i = 0; i < posicao; i++) soma += (digitos[i] - '0') * (posicao + 1 - i);
                var resto = soma * 10 % 11 % 10;
                valido = resto == digitos[posicao] - '0';
            }
            return valido ? "CPF válido." : "CPF inválido.";
        });
        Add("dev-cnpj-validate", "Conferir um CNPJ", dev, "cnpj,validar,empresa", () =>
        {
            var digitos = new string(System.Windows.Clipboard.GetText().Where(char.IsDigit).ToArray());
            if (digitos.Length != 14) return "Copie um CNPJ de 14 dígitos antes.";
            int[] pesos1 = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
            int[] pesos2 = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
            int Digito(int[] pesos)
            {
                var soma = 0;
                for (var i = 0; i < pesos.Length; i++) soma += (digitos[i] - '0') * pesos[i];
                var resto = soma % 11;
                return resto < 2 ? 0 : 11 - resto;
            }
            var ok = Digito(pesos1) == digitos[12] - '0' && Digito(pesos2) == digitos[13] - '0';
            return ok ? "CNPJ válido." : "CNPJ inválido.";
        });
        Add("dev-random-password", "Gerar uma senha forte", dev, "senha,password,aleatoria", () =>
        {
            const string chars = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789!@#$%";
            var random = new Random();
            var senha = new string(Enumerable.Range(0, 16).Select(_ => chars[random.Next(chars.Length)]).ToArray());
            System.Windows.Clipboard.SetText(senha);
            return senha + " (copiada)";
        });
        Add("dev-zip-folder", "Compactar uma pasta", dev, "zip,compactar,pasta",
            () => "Me diga a pasta e o destino: “compactar Documentos”.");

        /* ----------------------------- texto e conversão ----------------------------- */

        var texto = "Texto e números";
        Add("txt-count-words", "Contar palavras do texto copiado", texto, "contar,palavras,texto", () =>
        {
            var t = System.Windows.Clipboard.GetText();
            return string.IsNullOrWhiteSpace(t) ? "A área de transferência está vazia." : $"{t.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length} palavras, {t.Length} caracteres.";
        });
        Add("txt-upper-copy", "Copiar o texto em maiúsculas", texto, "maiusculas,texto,transformar", () =>
            System.Windows.Clipboard.GetText().ToUpperInvariant());
        Add("txt-lower-copy", "Copiar o texto em minúsculas", texto, "minusculas,texto,transformar", () =>
            System.Windows.Clipboard.GetText().ToLowerInvariant());
        Add("txt-title-copy", "Copiar em formato de título", texto, "titulo,capitalizar,texto", () =>
            System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(System.Windows.Clipboard.GetText().ToLowerInvariant()));
        Add("txt-trim-copy", "Copiar sem espaços sobrando", texto, "trim,espacos,limpar",
            () => System.Windows.Clipboard.GetText().Trim());
        Add("txt-replace-newlines", "Transformar quebras em espaço", texto, "quebras,linhas,espaco",
            () => System.Windows.Clipboard.GetText().Replace("\r\n", " ").Replace("\n", " "));
        Add("txt-base64", "Codificar em Base64", texto, "base64,codificar,texto",
            () => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(System.Windows.Clipboard.GetText())));
        Add("txt-decode-base64", "Decodificar Base64", texto, "base64,decodificar,texto", () =>
        {
            try { return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(System.Windows.Clipboard.GetText().Trim())); }
            catch { return "O texto da área de transferência não é Base64."; }
        });
        Add("txt-url-encode", "Codificar para endereço web", texto, "url,encode,endereco",
            () => Uri.EscapeDataString(System.Windows.Clipboard.GetText()));
        Add("txt-json-format", "Organizar um JSON", texto, "json,formatar,indentar", () =>
        {
            try
            {
                var doc = System.Text.Json.JsonDocument.Parse(System.Windows.Clipboard.GetText());
                var bonito = System.Text.Json.JsonSerializer.Serialize(doc, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                System.Windows.Clipboard.SetText(bonito);
                return "JSON organizado e copiado.";
            }
            catch { return "O texto copiado não é um JSON válido."; }
        });
        Add("txt-sort-lines", "Ordenar as linhas copiadas", texto, "ordenar,linhas,alfabetica", () =>
        {
            var linhas = System.Windows.Clipboard.GetText().Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).OrderBy(l => l);
            return string.Join("\n", linhas);
        });
        Add("txt-unique-lines", "Remover linhas repetidas", texto, "repetidas,unicas,linhas", () =>
        {
            var linhas = System.Windows.Clipboard.GetText().Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).Distinct();
            return string.Join("\n", linhas);
        });
        Add("txt-reverse", "Inverter o texto copiado", texto, "inverter,reverso,texto", () =>
            new string(System.Windows.Clipboard.GetText().Reverse().ToArray()));
        Add("txt-morse", "Texto para código Morse", texto, "morse,codigo,pontos", () =>
            string.Join(" ", System.Windows.Clipboard.GetText().ToLowerInvariant().Select(c => c switch
            {
                'a' => ".-", 'b' => "-...", 'c' => "-.-.", 'd' => "-..", 'e' => ".", 'f' => "..-.",
                'g' => "--.", 'h' => "....", 'i' => "..", 'j' => ".---", 'k' => "-.-", 'l' => ".-..",
                'm' => "--", 'n' => "-.", 'o' => "---", 'p' => ".--.", 'q' => "--.-", 'r' => ".-.",
                's' => "...", 't' => "-", 'u' => "..-", 'v' => "...-", 'w' => ".--", 'x' => "-..-",
                'y' => "-.--", 'z' => "--..", ' ' => "/", _ => ""
            }).Where(s => s.Length > 0)));

        /* ----------------------------- cálculos rápidos ----------------------------- */

        var calc = "Cálculos";
        Add("calc-open", "Abrir a Calculadora", calc, "calculadora,conta", () => Open("calculator:"));
        Add("calc-percent", "Calcular uma porcentagem", calc, "porcentagem,desconto,percentual",
            () => "Me diga: “20% de 340”.");
        Add("calc-tip", "Calcular a gorjeta", calc, "gorjeta,10%,conta restaurante",
            () => "Me diga o valor: “gorjeta de 120 reais”.");
        Add("calc-rule-of-three", "Regra de três", calc, "regra de tres,proporcao", () =>
            "Me diga os três valores: “regra de três: 2, 4, 10”.");
        Add("calc-bmi", "Calcular o IMC", calc, "imc,peso,altura", () => "Me diga: “imc 1,75 80”.");
        Add("calc-random", "Sortear um número", calc, "sorteio,aleatorio,numero",
            () => Say(new Random().Next(1, 101).ToString()));
        Add("calc-coin", "Jogar uma moeda", calc, "moeda,cara,coroa,sorte",
            () => Say(new Random().Next(2) == 0 ? "Cara." : "Coroa."));
        Add("calc-dice", "Jogar um dado", calc, "dado,sorte,numero",
            () => Say("Saiu " + new Random().Next(1, 7) + "."));
        Add("calc-unit", "Converter unidades", calc, "converter,km,metros,temperatura", () =>
            "Me diga a conversão: “30 graus para fahrenheit”.");

        /* ----------------------------- Orbi ----------------------------- */

        var orbi = "Orbi";
        Add("orbi-history", "O que eu fiz hoje", orbi, "historico,fiz,hoje,memoria",
            () => Memory.TodaySummary() is { Length: > 0 } resumo ? resumo : "Nada registrado hoje.");
        Add("orbi-forget", "Esquecer o dia de hoje", orbi, "esquecer,memoria,limpar",
            () => "Vá em Ajustes ▸ Memória e use “Esquecer tudo”.");
        Add("orbi-tools", "Quantas ferramentas o Orbi tem", orbi, "ferramentas,quantas,catalogo",
            () => $"O Orbi tem {Tools.All.Count} ferramentas em {Tools.Groups.Count()} grupos.");
        Add("orbi-update", "Procurar atualização", orbi, "atualizar,versao,nova",
            () => Open("https://orbi-app.web.app/baixar"));
        Add("orbi-about", "Sobre o Orbi", orbi, "sobre,orbi,versao",
            () => "Orbi " + (typeof(App).Assembly.GetName().Version?.ToString(3) ?? "?") + " · Windows 11 · feito para português.");
        Add("orbi-bar-reset", "Voltar a barra ao centro", orbi, "barra,centro,resetar,posicao", () =>
        {
            Store.Current.BarLeft = null;
            Store.Current.BarTop = null;
            Store.Save();
            return "A barra volta ao centro na próxima vez.";
        });
        Add("orbi-settings-ia", "Ajustar a IA", orbi, "ia,chave,gemini,ajustar",
            () => { App.Instance.Dispatcher.BeginInvoke(new Action(() => App.Instance.ShowSettings("ia"))); return "Abrindo a IA."; });
        Add("orbi-settings-memory", "Ajustar a memória", orbi, "memoria,ajuste", () =>
        {
            App.Instance.Dispatcher.BeginInvoke(new Action(() => App.Instance.ShowSettings("memoria")));
            return "Abrindo a memória.";
        });
        Add("orbi-account-site", "Abrir a minha conta no site", orbi, "conta,site,navegador",
            () => Open("https://orbi-app.web.app/conta"));
        Add("orbi-quiet", "Esconder a barra", orbi, "esconder,fechar,barra",
            () => { App.Instance.Dispatcher.BeginInvoke(new Action(() => App.Instance.HideBar())); return "Barra escondida."; });
    }

    /* ------------------------------ utilidades ------------------------------ */

    private static string Open(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            return "Feito.";
        }
        catch (Exception error)
        {
            return "Não consegui abrir: " + error.Message;
        }
    }

    private static string Run(string file, string args)
    {
        try
        {
            var saida = Process.Start(new ProcessStartInfo(file, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (saida is null) return "Não deu para rodar " + file + ".";
            var texto = saida.StandardOutput.ReadToEnd() + saida.StandardError.ReadToEnd();
            saida.WaitForExit(6000);
            texto = texto.Trim();
            if (texto.Length == 0) return "Comando executado.";
            return texto.Length > 600 ? texto[..600] + "…" : texto;
        }
        catch (Exception error)
        {
            return "Não deu para rodar " + file + ": " + error.Message;
        }
    }

    private static string Start(string file, string message)
    {
        try
        {
            Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
            return message;
        }
        catch (Exception error)
        {
            return "Não consegui abrir: " + error.Message;
        }
    }

    private static string CreateFolder(Environment.SpecialFolder folder) => CreateFolder(Environment.GetFolderPath(folder));

    private static string CreateFolder(string pai)
    {
        try
        {
            Directory.CreateDirectory(pai);
            var alvo = Path.Combine(pai, "Nova pasta");
            var nome = alvo;
            var i = 2;
            while (Directory.Exists(nome)) nome = alvo + " " + i++;
            Directory.CreateDirectory(nome);
            return "Criei " + nome;
        }
        catch (Exception error)
        {
            return "Não consegui criar a pasta: " + error.Message;
        }
    }

    private static string ToggleDesktopIcons(bool mostrar)
    {
        try
        {
            var handle = Pc.FindWindowW("Progman", null);
            if (handle == IntPtr.Zero) return "Não achei a área de trabalho.";
            var shell = Pc.FindWindowW("SHELLDLL_DefView", null);
            var alvo = shell != IntPtr.Zero ? shell : handle;
            Pc.SendMessageToWindow(alvo, 0x111, (IntPtr)(mostrar ? 0x7402 : 0x7401), IntPtr.Zero);
            return mostrar ? "Ícones visíveis." : "Ícones escondidos.";
        }
        catch (Exception error)
        {
            return "Não consegui mexer nos ícones: " + error.Message;
        }
    }

    private static void SendMessage(uint msg, int wParam, int lParam) =>
        Pc.SendMessageToWindow(IntPtr.Zero, msg, (IntPtr)wParam, (IntPtr)lParam);

    private static readonly DateTime Boot = DateTime.Now - TimeSpan.FromMilliseconds(Environment.TickCount64);

    private static string Query(string wmi)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(wmi);
            var nomes = searcher.Get().Cast<ManagementBaseObject>().Select(o => o["Name"]?.ToString()).Where(n => n is not null);
            var texto = string.Join("\n", nomes!);
            return string.IsNullOrWhiteSpace(texto) ? "Não consegui ler agora." : texto!;
        }
        catch
        {
            return "Não consegui ler agora.";
        }
    }

    private static string RuntimeInformationDescription() =>
        System.Runtime.InteropServices.RuntimeInformation.OSDescription;

    private static string Shell(string file, string args = "")
    {
        try
        {
            Process.Start(new ProcessStartInfo(file, args) { UseShellExecute = true });
            return "Feito.";
        }
        catch
        {
            return "Não abri.";
        }
    }
}
