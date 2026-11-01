using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using Forms = System.Windows.Forms;
using Microsoft.Win32;

namespace Orbi.Services;

/// <summary>Uma ferramenta do Orbi: nome curto, grupo, palavras-chave e o que ela faz.</summary>
/// <summary>O dado que a pessoa (ou a IA) passou junto do pedido, tipo "abrir site youtube.com".</summary>
public static class Args
{
    private static readonly AsyncLocal<string> Atual = new();
    public static string Current { get => Atual.Value ?? ""; set => Atual.Value = value; }
}

public sealed record Tool(string Id, string Name, string Group, string[] Keywords, Func<string> Run,
    string? Arg = null, string? Placeholder = null)
{
    public string Label => $"{Group.Split(' ')[0]} · {Name}";

    /// <summary>A ferramenta precisa de um dado (site, texto, caminho…)?</summary>
    public bool NeedsArg => Arg is not null;
}

/// <summary>
/// Catálogo de ações reais no Windows. Cada item é uma coisa que a barra, a
/// ferramenta de busca e o Gemini podem executar pelo nome. Nada aqui pede
/// administrador e nada sai do PC.
/// </summary>
public static class Tools
{
    /* --------------------------- primitivas --------------------------- */

    private const int VK_LWIN = 0x5B;

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);

    private static void Key(Forms.Keys key) => Forms.SendKeys.SendWait(key.ToString());

    private static void Combo(params Forms.Keys[] keys)
    {
        var parts = keys.Select(k => k switch
        {
            Forms.Keys.Control => "^",
            Forms.Keys.Alt => "%",
            Forms.Keys.Shift => "+",
            _ => "{" + k.ToString().ToUpperInvariant() + "}",
        });
        Forms.SendKeys.SendWait(string.Concat(parts));
    }

    /// <summary>Tecla Windows + uma tecla (o SendKeys não manda a tecla Win).</summary>
    private static void Win(Forms.Keys key)
    {
        var vk = (byte)key;
        keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
        keybd_event(vk, 0, 0, UIntPtr.Zero);
        keybd_event(vk, 0, 2, UIntPtr.Zero);
        keybd_event(VK_LWIN, 0, 2, UIntPtr.Zero);
    }

    private static string Shell(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            return "Feito.";
        }
        catch (Exception error)
        {
            return "Não consegui abrir isso agora: " + error.Message;
        }
    }

    private static string Exec(string file, string args, bool hidden = true)
    {
        try
        {
            Process.Start(new ProcessStartInfo(file, args)
            {
                UseShellExecute = true,
                CreateNoWindow = hidden,
                WindowStyle = hidden ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal,
            });
            return "Feito.";
        }
        catch (Exception error)
        {
            return "Não deu: " + error.Message;
        }
    }

    private static string Copy(string text)
    {
        try
        {
            System.Windows.Clipboard.SetText(text);
            return text;
        }
        catch
        {
            return text;
        }
    }

    private static string Folder(Environment.SpecialFolder folder, string label)
    {
        var path = Environment.GetFolderPath(folder);
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return $"Não achei a pasta {label} neste PC.";
        Shell(path);
        return $"Abri {label}.";
    }

    private static string Panel(string uri, string label) => Shell(uri) == "Feito." ? $"Abri {label}." : $"Não consegui abrir {label}.";

    private static string Temp()
    {
        long freed = 0;
        var count = 0;
        var target = Path.GetTempPath();
        foreach (var file in Directory.EnumerateFiles(target))
        {
            try
            {
                freed += new FileInfo(file).Length;
                File.Delete(file);
                count++;
            }
            catch { }
        }
        foreach (var dir in Directory.EnumerateDirectories(target))
        {
            try
            {
                Directory.Delete(dir, true);
                count++;
            }
            catch { }
        }
        return $"Limpei {count} item(ns) temporários ({freed / 1024.0 / 1024:0.#} MB).";
    }

    /* --------------------------- catálogo --------------------------- */

    public static IReadOnlyList<Tool> All { get; } = Build();

    private static List<Tool> Build()
    {
        var tools = new List<Tool>();

        void Add(string id, string name, string group, string keywords, Func<string> run) =>
            tools.Add(new Tool(id, name, group, keywords.Split(',', StringSplitOptions.TrimEntries), run));

        void Media(string id, string name, Forms.Keys key, string keywords) =>
            Add(id, name, "Áudio e vídeo", keywords, () => { Key(key); return $"{name}: enviado ao player."; });

        /* ---------------- áudio e vídeo ---------------- */
        Add("volume-up", "Volume +", "Áudio e vídeo", "volume,som,alto,aumentar,audio", () =>
        { for (var i = 0; i < 5; i++) Key(Forms.Keys.VolumeUp); return "Volume mais alto."; });
        Add("volume-down", "Volume −", "Áudio e vídeo", "volume,som,baixo,diminuir,audio", () =>
        { for (var i = 0; i < 5; i++) Key(Forms.Keys.VolumeDown); return "Volume mais baixo."; });
        Add("mute", "Silenciar", "Áudio e vídeo", "mudo,silenciar,mutar,audio", () =>
        { Key(Forms.Keys.VolumeMute); return "Som silenciado (ou de volta)."; });
        Media("play-pause", "Play/pause", Forms.Keys.MediaPlayPause, "play,pausa,toca,musica");
        Media("next-track", "Próxima faixa", Forms.Keys.MediaNextTrack, "proxima,pular,musica");
        Media("prev-track", "Faixa anterior", Forms.Keys.MediaPreviousTrack, "anterior,voltar,musica");
        Media("stop-track", "Parar mídia", Forms.Keys.MediaStop, "parar,stop,musica");
        Add("mixer", "Mixer de volume", "Áudio e vídeo", "mixer,volume,app,sons", () => Panel("ms-settings:apps-volume", "o mixer de volume"));
        Add("sound-panel", "Dispositivos de som", "Áudio e vídeo", "som,audio,dispositivo,falante,headset", () => Shell("mmsys.cpl") == "Feito." ? "Abri os dispositivos de som." : "Não abri.");

        /* ---------------- tela ---------------- */
        Add("screenshot", "Captura de tela", "Tela", "captura,print,screenshot,foto da tela", SystemActions.Screenshot);
        Add("snip", "Ferramenta de captura", "Tela", "recorte,snip,captura,area", () => Panel("ms-screenclip:", "a ferramenta de captura"));
        Add("brightness-up", "Brilho +", "Tela", "brilho,claro,aumentar,tela", () => SystemActions.BrightnessStep(10));
        Add("brightness-down", "Brilho −", "Tela", "brilho,escuro,diminuir,tela", () => SystemActions.BrightnessStep(-10));
        Add("night-light", "Luz noturna", "Tela", "luz noturna,amarelo,descanso,olhos", () => Panel("ms-settings:nightlight", "a luz noturna"));
        Add("display-settings", "Configurações de vídeo", "Tela", "tela,monitor,resolucao,video", () => Panel("ms-settings:display", "as configurações de tela"));
        Add("magnifier", "Lupa", "Tela", "lupa,aumentar,zoom,acessibilidade", () => Shell("magnify.exe") == "Feito." ? "Lupa aberta." : "Não abri a lupa.");
        Add("project", "Projetar (segunda tela)", "Tela", "projetar,monitor,duplicar,estender", () => { Win(Forms.Keys.P); return "Escolha como projetar."; });
        Add("rotate-screen", "Girar a tela", "Tela", "girar,rotacionar,tela", () => { Combo(Forms.Keys.Control, Forms.Keys.Alt, Forms.Keys.Left); return "Tela girada."; });

        /* ---------------- energia ---------------- */
        Add("lock", "Bloquear o PC", "Energia", "bloquear,trancar,lock,seguranca", SystemActions.Lock);
        Add("sleep", "Dormir", "Energia", "dormir,suspender,sleep,economizar", SystemActions.Sleep);
        Add("hibernate", "Hibernar", "Energia", "hibernar,hibernacao", () => Exec("shutdown.exe", "/h") == "Feito." ? "Hibernando. Se a hibernação estiver desligada, nada acontece." : "Não consegui hibernar.");
        Add("shutdown-1", "Desligar em 1 min", "Energia", "desligar,encerrar,timer", () => SystemActions.Shutdown(1));
        Add("restart-1", "Reiniciar em 1 min", "Energia", "reiniciar,reboot,timer", () => SystemActions.Restart(1));
        Add("cancel-shutdown", "Cancelar desligamento", "Energia", "cancelar,abortar,desligamento", SystemActions.CancelShutdown);
        Add("power-options", "Opções de energia", "Energia", "energia,bateria,plano,powercfg", () => Shell("powercfg.cpl") == "Feito." ? "Abri as opções de energia." : "Não abri.");
        Add("battery-report", "Relatório da bateria", "Energia", "bateria,relatorio,saude", () =>
        {
            var outFile = Path.Combine(Store.Root, "bateria.html");
            Exec("cmd.exe", $"/c powercfg /batteryreport /output \"{outFile}\"", hidden: false);
            return $"Gerando o relatório em {outFile}.";
        });

        /* ---------------- sistema -------------### */
        Add("status", "Status do PC", "Sistema", "status,cpu,memoria,ram,desempenho", () =>
        {
            var s = SystemActions.Snapshot();
            return $"CPU {s.Cpu:0}% · memória {s.MemoryUsedGb:0.#}/{s.MemoryTotalGb:0.#} GB · disco {s.DiskFreeGb:0.#} GB livres" +
                   (s.Battery is { } b ? $" · bateria {b}%" : "") + (s.TopProcess is { } p ? $" · mais pesado: {p}" : "");
        });
        Add("task-manager", "Gerenciador de tarefas", "Sistema", "tarefas,processos,travar,cpu", () =>
        { Combo(Forms.Keys.Control, Forms.Keys.Shift, Forms.Keys.Escape); return "Gerenciador de tarefas aberto."; });
        Add("resource-monitor", "Monitor de recursos", "Sistema", "recursos,cpu,disco,rede", () => Shell("resmon.exe") == "Feito." ? "Monitor de recursos aberto." : "Não abri.");
        Add("event-viewer", "Visualizador de eventos", "Sistema", "eventos,logs,erros", () => Shell("eventvwr.msc") == "Feito." ? "Visualizador de eventos aberto." : "Não abri.");
        Add("services", "Serviços", "Sistema", "servicos,servico,parar", () => Shell("services.msc") == "Feito." ? "Serviços abertos." : "Não abri.");
        Add("device-manager", "Gerenciador de dispositivos", "Sistema", "dispositivo,driver,hardware", () => Shell("devmgmt.msc") == "Feito." ? "Gerenciador de dispositivos aberto." : "Não abri.");
        Add("disk-management", "Gerenciamento de disco", "Sistema", "disco,particao,volume", () => Shell("diskmgmt.msc") == "Feito." ? "Gerenciamento de disco aberto." : "Não abri.");
        Add("system-info", "Informações do sistema", "Sistema", "informacoes,hardware,modelo,versao", () => Shell("msinfo32.exe") == "Feito." ? "Informações do sistema abertas (pode levar alguns segundos)." : "Não abri.");
        Add("registry", "Editor do registro", "Sistema", "registro,registry,regedit", () => Shell("regedit.exe") == "Feito." ? "Editor do registro aberto. Cuidado por aqui." : "Não abri.");
        Add("task-scheduler", "Agendador de tarefas", "Sistema", "agendar,tarefa,agendador", () => Shell("taskschd.msc") == "Feito." ? "Agendador de tarefas aberto." : "Não abri.");
        Add("startup-apps", "Apps de inicialização", "Sistema", "inicio,inicializacao,lento,boot", () => Panel("ms-settings:startupapps", "os apps de inicialização"));
        Add("empty-recycle", "Esvaziar a lixeira", "Sistema", "lixeira,limpar,apagar", SystemActions.EmptyRecycleBin);
        Add("clean-temp", "Limpar temporários", "Sistema", "limpar,temp,temporario,espaco", Temp);
        Add("disk-cleanup", "Limpeza de disco", "Sistema", "limpeza,disco,espaco", () => Shell("cleanmgr.exe") == "Feito." ? "Limpeza de disco aberta." : "Não abri.");
        Add("system-update", "Windows Update", "Sistema", "atualizar,update,windows", () => Panel("ms-settings:windowsupdate", "o Windows Update"));
        Add("restore-point", "Ponto de restauração", "Sistema", "restaurar,backup,ponto", () => Shell("rstrui.exe") == "Feito." ? "Restauração do sistema aberta." : "Não abri.");
        Add("problem-steps", "Solução de problemas", "Sistema", "problema,ajuda,diagnostico,solucao", () => Panel("ms-settings:troubleshoot", "a solução de problemas"));
        Add("about-pc", "Sobre este PC", "Sistema", "sobre,pc,especificacoes", () => Panel("ms-settings:about", "o “Sobre este PC”"));

        /* ---------------- painéis de configuração ---------------- */
        Add("settings", "Configurações do Windows", "Painéis", "configuracoes,ajustes,windows", () => Panel("ms-settings:", "as configurações"));
        Add("bluetooth", "Bluetooth e dispositivos", "Painéis", "bluetooth,fone,mouse,teclado,dispositivo", () => Panel("ms-settings:bluetooth", "o Bluetooth"));
        Add("wifi", "Wi-Fi", "Painéis", "wifi,wi-fi,internet,rede sem fio", () => Panel("ms-settings:network-wifi", "o Wi-Fi"));
        Add("network", "Rede e internet", "Painéis", "rede,internet,ethernet", () => Panel("ms-settings:network", "a rede"));
        Add("apps-installed", "Apps instalados", "Painéis", "apps,instalados,desinstalar,programas", () => Panel("ms-settings:appsfeatures", "os apps instalados"));
        Add("printers", "Impressoras", "Painéis", "imprimir,impressora,papel", () => Shell("control.exe printers") == "Feito." ? "Impressoras abertas." : "Não abri.");
        Add("privacy", "Privacidade", "Painéis", "privacidade,permissao,camera,microfone", () => Panel("ms-settings:privacy", "a privacidade"));
        Add("accounts-settings", "Contas do Windows", "Painéis", "conta windows,usuario,senha", () => Panel("ms-settings:yourinfo", "as contas do Windows"));
        Add("time-settings", "Data e hora", "Painéis", "hora,data,fuso,relogio", () => Panel("ms-settings:dateandtime", "data e hora"));
        Add("region", "Idioma e região", "Painéis", "idioma,regiao,teclado,layout", () => Panel("ms-settings:regionlanguage", "idioma e região"));
        Add("storage-settings", "Armazenamento", "Painéis", "armazenamento,espaco,disco", () => Panel("ms-settings:storagesense", "o armazenamento"));
        Add("mouse", "Mouse", "Painéis", "mouse,ponteiro,velocidade", () => Shell("main.cpl") == "Feito." ? "Configurações do mouse abertas." : "Não abri.");
        Add("keyboard", "Teclado", "Painéis", "teclado,digitar,repeticao", () => Panel("ms-settings:keyboard", "o teclado"));
        Add("fonts", "Fontes", "Painéis", "fonte,tipografia,instalar fonte", () => Panel("ms-settings:fonts", "as fontes"));
        Add("firewall", "Firewall", "Painéis", "firewall,bloqueio,porta", () => Shell("control.exe firewall.cpl") == "Feito." ? "Firewall aberto." : "Não abri.");
        Add("control-panel", "Painel de controle", "Painéis", "painel de controle,control", () => Shell("control.exe") == "Feito." ? "Painel de controle aberto." : "Não abri.");
        Add("programs-features", "Programas e recursos", "Painéis", "desinstalar,remover,programa", () => Shell("appwiz.cpl") == "Feito." ? "Programas e recursos abertos." : "Não abri.");

        /* ---------------- apps do Windows ---------------- */
        Add("notepad", "Bloco de notas", "Apps", "notepad,bloco de notas,texto", () => Shell("notepad.exe") == "Feito." ? "Bloco de notas aberto." : "Não abri.");
        Add("calc", "Calculadora", "Apps", "calculadora,calc,conta", () => Shell("calc.exe") == "Feito." ? "Calculadora aberta." : "Não abri.");
        Add("paint", "Paint", "Apps", "paint,desenhar,imagem", () => Shell("mspaint.exe") == "Feito." ? "Paint aberto." : "Não abri.");
        Add("explorer", "Explorador de arquivos", "Apps", "explorador,arquivos,pastas", () => Shell("explorer.exe") == "Feito." ? "Explorador aberto." : "Não abri.");
        Add("terminal", "Terminal", "Apps", "terminal,cmd,console", () => Shell("wt.exe") == "Feito." ? "Terminal aberto." : Shell("powershell.exe"));
        Add("cmd", "Prompt de comando", "Apps", "cmd,prompt,comando", () => Shell("cmd.exe") == "Feito." ? "Prompt aberto." : "Não abri.");
        Add("powershell", "PowerShell", "Apps", "powershell,ps,script", () => Shell("powershell.exe") == "Feito." ? "PowerShell aberto." : "Não abri.");
        Add("store", "Microsoft Store", "Apps", "store,loja,instalar app", () => Panel("ms-windows-store:", "a Store"));
        Add("camera", "Câmera", "Apps", "camera,foto,video", () => Panel("microsoft.windows.camera:", "a câmera"));
        Add("photos", "Fotos", "Apps", "fotos,galeria,imagem", () => Panel("ms-photos:", "as fotos"));
        Add("clock", "Relógio e alarmes", "Apps", "relogio,alarme,cronometro", () => Panel("ms-clock:", "o relógio e alarmes"));
        Add("weather", "Clima", "Apps", "clima,tempo,temperatura,chuva", () => Panel("msnweather:", "o clima"));
        Add("mail", "E-mail", "Apps", "email,correio,mail", () => Panel("ms-outlook:", "o e-mail"));
        Add("xbox", "Xbox", "Apps", "xbox,jogo,game", () => Panel("ms-gamingoverlay:", "o Xbox Game Bar"));
        Add("phone-link", "Vincular celular", "Apps", "celular,telefone,phone", () => Panel("ms-phone:", "o app Vincular ao Telefone"));
        Add("media-player", "Media Player", "Apps", "player,musica,video,filme", () => Panel("mswindowsmusic:", "o Media Player"));

        /* ---------------- janelas ---------------- */
        Add("show-desktop", "Mostrar a área de trabalho", "Janelas", "area de trabalho,desktop,minimizar tudo", () => { Win(Forms.Keys.D); return "Área de trabalho à vista."; });
        Add("task-view", "Visão de tarefas", "Janelas", "visao de tarefas,alt tab,janelas", () => { Win(Forms.Keys.Tab); return "Visão de tarefas aberta."; });
        Add("new-desktop", "Nova área de trabalho", "Janelas", "nova area,desktop,virtual", () =>
        {
            keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
            keybd_event(0x11, 0, 0, UIntPtr.Zero);   // Ctrl
            keybd_event(0x44, 0, 0, UIntPtr.Zero);   // D
            keybd_event(0x44, 0, 2, UIntPtr.Zero);
            keybd_event(0x11, 0, 2, UIntPtr.Zero);
            keybd_event(VK_LWIN, 0, 2, UIntPtr.Zero);
            return "Nova área de trabalho criada.";
        });
        Add("next-desktop", "Próxima área de trabalho", "Janelas", "trocar area,desktop,virtual", () => { Win(Forms.Keys.Right); return "Troquei de área de trabalho."; });
        Add("close-window", "Fechar a janela ativa", "Janelas", "fechar janela,fechar programa,alt f4", () => { Combo(Forms.Keys.Alt, Forms.Keys.F4); return "Fechei a janela ativa."; });
        Add("switch-window", "Trocar de janela", "Janelas", "trocar,alternar,alt tab", () => { Combo(Forms.Keys.Alt, Forms.Keys.Tab); return "Troquei de janela."; });
        Add("maximize", "Maximizar a janela", "Janelas", "maximizar,tela cheia", () => { Win(Forms.Keys.Up); return "Maximizei a janela ativa."; });
        Add("minimize", "Minimizar a janela", "Janelas", "minimizar,esconder", () => { Win(Forms.Keys.Down); return "Minimizei a janela ativa."; });
        Add("snap-left", "Fixar à esquerda", "Janelas", "esquerda,lado,janela", () => { Win(Forms.Keys.Left); return "Janela fixada à esquerda."; });
        Add("snap-right", "Fixar à direita", "Janelas", "direita,lado,janela", () => { Win(Forms.Keys.Right); return "Janela fixada à direita."; });
        Add("search-windows", "Buscar no Windows", "Janelas", "buscar,pesquisar,procurar", () => { Win(Forms.Keys.S); return "Busca do Windows aberta."; });
        Add("action-center", "Central de ações", "Janelas", "notificacoes,acoes,rapido", () => { Win(Forms.Keys.A); return "Central de ações aberta."; });
        Add("emoji", "Emojis", "Janelas", "emoji,simbolo,carinha", () => { Win(Forms.Keys.OemPeriod); return "Painel de emojis aberto."; });
        Add("clipboard-history", "Histórico da área de transferência", "Janelas", "historico,copia,transferencia", () => { Win(Forms.Keys.V); return "Histórico de cópia aberto."; });

        /* ---------------- pastas ---------------- */
        Add("downloads", "Downloads", "Pastas", "downloads,baixados,baixar", FolderDownloads);
        Add("documents", "Documentos", "Pastas", "documentos,arquivos", () => Folder(Environment.SpecialFolder.MyDocuments, "Documentos"));
        Add("pictures", "Imagens", "Pastas", "imagens,fotos,pictures", () => Folder(Environment.SpecialFolder.MyPictures, "Imagens"));
        Add("music", "Músicas", "Pastas", "musicas,audio", () => Folder(Environment.SpecialFolder.MyMusic, "Músicas"));
        Add("videos", "Vídeos", "Pastas", "videos,filmes", () => Folder(Environment.SpecialFolder.MyVideos, "Vídeos"));
        Add("startup-folder", "Pasta de inicialização", "Pastas", "iniciar junto,inicializacao,pasta", () =>
        {
            var path = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            Shell(path);
            return "Abri a pasta de inicialização. Atalhos aqui abrem junto com o Windows.";
        });
        Add("orbi-data", "Pasta de dados do Orbi", "Pastas", "dados,orbi,pasta,backup", () =>
        {
            Directory.CreateDirectory(Store.Root);
            Shell(Store.Root);
            return "Abri " + Store.Root;
        });
        Add("recycle-folder", "Abrir a lixeira", "Pastas", "lixeira,abrir", () => Shell("shell:RecycleBinFolder") == "Feito." ? "Lixeira aberta." : "Não abri.");

        /* ---------------- rede ---------------- */
        Add("flush-dns", "Limpar cache de DNS", "Rede", "dns,cache,internet,site nao abre", () =>
        {
            Exec("ipconfig.exe", "/flushdns");
            return "Cache de DNS limpo.";
        });
        Add("ip-config", "Meu IP", "Rede", "ip,endereco,rede,local", () =>
        {
            var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
            var ip = host.AddressList.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            return ip is null ? "Não consegui ler o IP agora." : Copy($"IP local: {ip}");
        });
        Add("ping-test", "Testar a internet", "Rede", "ping,internet,conexao,testar", () =>
        {
            try
            {
                using var ping = new System.Net.NetworkInformation.Ping();
                var reply = ping.Send("1.1.1.1", 1500);
                return reply.Status == System.Net.NetworkInformation.IPStatus.Success
                    ? $"Internet ok: {reply.RoundtripTime} ms."
                    : "Sem resposta da internet agora.";
            }
            catch { return "Não consegui testar a conexão."; }
        });
        Add("network-adapter", "Adaptadores de rede", "Rede", "adaptador,placa de rede,reset", () => Shell("ncpa.cpl") == "Feito." ? "Adaptadores de rede abertos." : "Não abri.");

        /* ---------------- acessibilidade ---------------- */
        Add("osk", "Teclado virtual", "Acessibilidade", "teclado virtual,osk,digitar", () => Shell("osk.exe") == "Feito." ? "Teclado virtual aberto." : "Não abri.");
        Add("narrator", "Narrador", "Acessibilidade", "narrador,narrator,voz,tela", () => Shell("narrator.exe") == "Feito." ? "Narrador aberto." : "Não abri.");
        Add("ease", "Central de acessibilidade", "Acessibilidade", "acessibilidade,facil,contraste", () => Panel("ms-settings:easeofaccess", "a acessibilidade"));
        Add("color-filters", "Filtros de cor", "Acessibilidade", "cor,filtro,daltonismo", () => Panel("ms-settings:easeofaccess-colorfilter", "os filtros de cor"));

        /* ---------------- produtividade ---------------- */
        Add("copy-date", "Copiar a data", "Produtividade", "data,hoje,copiar", () => Copy(DateTime.Now.ToString("dd/MM/yyyy")));
        Add("copy-time", "Copiar a hora", "Produtividade", "hora,agora,copiar", () => Copy(DateTime.Now.ToString("HH:mm")));
        Add("copy-stamp", "Copiar data e hora", "Produtividade", "carimbo,data,hora", () => Copy(DateTime.Now.ToString("dd/MM/yyyy HH:mm")));
        Add("clip-read", "Ler a área de transferência", "Produtividade", "colar,copia,area de transferencia", () =>
        {
            var text = System.Windows.Clipboard.GetText();
            if (string.IsNullOrWhiteSpace(text)) return "A área de transferência está vazia.";
            Store.PushClipboard(text);
            return text.Length > 300 ? text[..300] + "…" : text;
        });
        Add("clip-upper", "Copiar em MAIÚSCULAS", "Produtividade", "maiuscula,caixa alta,transformar", () =>
            System.Windows.Clipboard.GetText().ToUpperInvariant());
        Add("clip-lower", "Copiar em minúsculas", "Produtividade", "minuscula,caixa baixa,transformar", () =>
            System.Windows.Clipboard.GetText().ToLowerInvariant());
        Add("clip-clear", "Limpar a área de transferência", "Produtividade", "limpar,copia,transferencia", () =>
        {
            try { System.Windows.Clipboard.Clear(); return "Área de transferência limpa."; }
            catch { return "Não consegui limpar agora."; }
        });
        Add("orbi-bar", "Abrir a barra do Orbi", "Produtividade", "barra,abrir,orbi", () =>
        {
            App.Instance.Dispatcher.BeginInvoke(new Action(() => App.Instance.ShowBar()));
            return "Abrindo a barra.";
        });
        Add("orbi-settings", "Ajustes do Orbi", "Produtividade", "ajustes,configuracoes,orbi", () =>
        {
            App.Instance.Dispatcher.BeginInvoke(new Action(() => App.Instance.ShowSettings()));
            return "Abrindo os ajustes.";
        });
        Add("orbi-profile", "Abrir o perfil", "Produtividade", "perfil,nome,usuario,orbi", () =>
        {
            App.Instance.Dispatcher.BeginInvoke(new Action(() => App.Instance.ShowAccount()));
            return "Abrindo o perfil.";
        });
        Add("sync-now", "Sincronizar com a conta", "Produtividade", "sincronizar,sync,conta,memoria", () =>
        {
            Sync.Now();
            return Firebase.SignedIn ? "Sincronizando com a sua conta." : "Entre na conta para sincronizar.";
        });
        Add("orbi-site", "Abrir o site do Orbi", "Produtividade", "site,conta,navegador", () => Shell("https://orbi-app.web.app") == "Feito." ? "Abri o site." : "Não abri o site.");
        Add("orbi-log", "Ver o log do Orbi", "Produtividade", "log,registro,problema", () =>
        {
            if (File.Exists(Log.Path)) Shell(Log.Path);
            return "Log em " + Log.Path;
        });
        Add("orbi-version", "Versão do Orbi", "Produtividade", "versao,update,atualizar", () =>
            "Orbi " + (typeof(App).Assembly.GetName().Version?.ToString(3) ?? "?"));

        // segundo bloco: mais 200 ferramentas (área de trabalho, teclado e mouse,
        // captura de tela, arquivos, rede, energia, acessibilidade, dev, texto…)
        ToolsExtra.Register(tools);

        // terceiro bloco: web, arquivos, texto, cálculo, datas, rede — com dado.

        ToolsWeb.Register(tools);

        // o controle do PC entra por último: são as ferramentas que a IA usa
        // quando precisa ver a tela, clicar, digitar ou mexer em uma janela.
        Add("control-see", "Ver a tela e o que está aberto", "Controle do PC",
            "ver tela,capturar tela,o que esta na tela,ler tela",
            () => Pc.DescribeScreen());
        Add("control-shot", "Salvar uma foto da tela", "Controle do PC",
            "foto da tela,capturar tela,screenshot,print",
            () => "Captura salva em " + Pc.Capture());
        Add("control-click", "Clicar em um ponto da tela", "Controle do PC",
            "clicar,ponto,posicao,mouse",
            () => "Me diga: “clicar em 640,400”.");
        Add("control-type", "Digitar em qualquer lugar", "Controle do PC",
            "digitar,escrever,texto,teclado",
            () => "Me diga: “digite: olá, tudo bem?”.");
        Add("control-keys", "Apertar um atalho", "Controle do PC",
            "atalho,teclas,combinacao",
            () => "Me diga: “atalho ctrl + t”.");
        Add("control-focus", "Trazer uma janela para a frente", "Controle do PC",
            "janela,focar,trazer,ativar",
            () => "Me diga o app: “traga o Chrome para a frente”.");
        Add("control-window-list", "Listar as janelas abertas", "Controle do PC",
            "janelas,abertas,listar",
            () => Pc.DescribeScreen());

        return tools;
    }

    private static string FolderDownloads()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (!Directory.Exists(path)) return "Não achei a pasta Downloads.";
        Shell(path);
        return "Abri Downloads.";
    }

    /* --------------------------- execução --------------------------- */

    public static Tool? Find(string id) =>
        All.FirstOrDefault(tool => string.Equals(tool.Id, id, StringComparison.OrdinalIgnoreCase));

    public static string Run(string id, string? arg = null)
    {
        var tool = Find(id);
        if (tool is { NeedsArg: true } && string.IsNullOrWhiteSpace(arg))
        {
            return "Preciso do dado: " + (tool.Placeholder ?? "o complemento");
        }
        Args.Current = arg?.Trim() ?? "";
        try { return RunArgh(tool); }
        finally { Args.Current = ""; }

        string RunArgh(Tool? alvo)
        {
            if (alvo is null) return "Ferramenta não encontrada: " + id;
            try
            {
                var result = alvo.Run();
                Log.Add($"ferramenta {id}: {result}");
                return result;
            }
            catch (Exception error)
            {
                Log.Error("ferramenta " + id, error);
                return "Não consegui agora: " + error.Message;
            }
        }
    }


    /// <summary>Encontra ferramentas por texto livre (nome, grupo ou palavra-chave).</summary>
    public static IReadOnlyList<Tool> Search(string query)
    {
        var text = Interpreter.Simplify(query);
        if (string.IsNullOrWhiteSpace(text)) return All;
        return All.Where(tool =>
                Interpreter.Simplify(tool.Name).Contains(text, StringComparison.Ordinal) ||
                Interpreter.Simplify(tool.Group).Contains(text, StringComparison.Ordinal) ||
                tool.Keywords.Any(key => Interpreter.Simplify(key).Contains(text, StringComparison.Ordinal)))
            .ToList();
    }

    public static IEnumerable<string> Groups => All.Select(tool => tool.Group).Distinct();

    /// <summary>
    /// O catálogo inteiro para a IA, com o dado que cada ferramenta espera.
    /// É isso que permite pedidos de vários passos com argumento.
    /// </summary>
    private static readonly string[] Ruido =
        { "de", "da", "do", "das", "dos", "a", "o", "as", "os", "um", "uma", "e", "em", "no", "na",
          "para", "por", "com", "que", "meu", "minha", "me", "meu", "favor", "por", "aqui", "isso" };

    private static IEnumerable<string> Termos(string texto) =>
        texto.ToLowerInvariant()
            .Split(new[] { ' ', ',', '.', '!', '?', '\n', '\t', ';', ':', '-', '/', '(', ')', '"' },
                StringSplitOptions.RemoveEmptyEntries)
            .Where(palavra => palavra.Length > 2 && !Ruido.Contains(palavra))
            .Distinct();

    /// <summary>
    /// Catálogo enxuto para a IA: quando o pedido é conhecido, manda só as ferramentas
    /// que casam com ele (mais as de controle sempre úteis), gastando menos contexto.
    /// </summary>
    public static string ForAi(string? pedido = null, int limitePorTermo = 4)
    {
        if (string.IsNullOrWhiteSpace(pedido)) return CatalogoCompleto();

        var termos = Termos(pedido).ToArray();
        if (termos.Length == 0) return CatalogoCompleto();

        var pontos = new Dictionary<Tool, int>();
        foreach (var tool in All)
        {
            var alvo = (tool.Id + " " + tool.Name + " " + tool.Group + " " +
                        string.Join(" ", tool.Keywords)).ToLowerInvariant();
            var acertos = termos.Count(termo => alvo.Contains(termo));
            if (acertos > 0) pontos[tool] = acertos;
        }

        var escolhidas = All.Where(pontos.ContainsKey)
            .OrderByDescending(tool => pontos[tool])
            .ThenBy(tool => tool.Id)
            .GroupBy(tool => tool.Group)
            .SelectMany(grupo => grupo.Take(limitePorTermo))
            .Take(60)
            .ToList();

        if (escolhidas.Count == 0) return CatalogoCompleto();

        var linhas = All.Where(tool => !escolhidas.Contains(tool) && Sempre(tool))
            .Select(tool => Linha(tool))
            .ToList();
        linhas.AddRange(escolhidas.Select(Linha));
        return string.Join("\n", linhas);
    }

    private static bool Sempre(Tool tool) =>
        tool.Group is "Janelas e mouse" or "Tela" or "Web e pesquisa";

    private static string Linha(Tool tool) =>
        tool.NeedsArg
            ? $"{tool.Id} — {tool.Name} (dado: {tool.Placeholder ?? "texto"})"
            : $"{tool.Id} — {tool.Name}";

    public static string CatalogoCompleto() => string.Join("\n", All.Select(Linha));
}
