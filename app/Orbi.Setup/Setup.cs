using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;

namespace Orbi.Setup;

/// <summary>
/// Onde o Orbi é instalado e o que já existe no PC.
/// Instalação por usuário, sem administrador: %LOCALAPPDATA%\Programs\Orbi.
/// </summary>
public static class Paths
{
    public static string Target =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "Orbi");

    public static string Exe => Path.Combine(Target, "Orbi.exe");
    public static string Uninstaller => Path.Combine(Target, "Desinstalar.exe");

    public static string StartMenu =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "Orbi");

    public static string Desktop =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Orbi.lnk");

    public static string Startup =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Orbi.lnk");

    public static string Data =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Orbi");

    /// <summary>Versão instalada (lida do próprio exe), ou null se não há instalação.</summary>
    public static string? InstalledVersion
    {
        get
        {
            try
            {
                if (!File.Exists(Exe)) return null;
                var info = FileVersionInfo.GetVersionInfo(Exe);
                return info.FileVersion ?? info.ProductVersion;
            }
            catch
            {
                return null;
            }
        }
    }

    public static bool Installed => File.Exists(Exe);
}

/// <summary>
/// O app não viaja dentro do setup: o instalador tem poucos MB e baixa o pacote
/// (~200 MB) na hora de instalar. Fica em cache, então uma reinstalação é rápida.
/// </summary>
public static class Payload
{
    /// <summary>Runtime .NET Desktop 8 (o app depende dele). Só baixa se faltar.</summary>
    public const string RuntimeUrl =
        "https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/8.0.11/windowsdesktop-runtime-8.0.11-win-x64.exe";

    /// <summary>Pacote publicado junto com o instalador na mesma release.</summary>
    public const string Url =
        "https://github.com/Arthurowgg/TheOrbiApp/releases/download/v1.1.2/orbi-payload-1.1.2.zip";

    public const long ApproximateSize = 3L * 1024 * 1024;

    public static string Cache =>
        Path.Combine(Path.GetTempPath(), "orbi-payload-" + Installer.Versao + ".zip");

    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromMinutes(15) };

    public static bool Cached => File.Exists(Cache) && new FileInfo(Cache).Length > 500L * 1024;

    /// <summary>
    /// Pacote local (usado pelos testes e por quem instala sem internet):
    /// o caminho é copiado para o cache e nada é baixado.
    /// </summary>
    public static void UseLocal(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("pacote local não encontrado", path);
        if (Cache.Equals(Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase)) return;
        try { if (File.Exists(Cache)) File.Delete(Cache); } catch { }
        File.Copy(path, Cache, overwrite: true);
        Installer.Write($"Usando o pacote local ({new FileInfo(Cache).Length / 1024 / 1024} MB)");
    }

    /// <summary>Baixa o pacote com progresso (0-1). Devolve o caminho no disco.</summary>
    public static async Task<string> DownloadAsync(IProgress<double> progress, CancellationToken token)
    {
        if (Cached)
        {
            progress.Report(1);
            return Cache;
        }

        // 1. direto do endereço oficial
        try
        {
            return await DownloadFrom(Url, progress, token);
        }
        catch (Exception error)
        {
            Installer.Write("Download direto falhou: " + error.Message);
        }

        // 2. pela API da release (funciona quando o link direto é redirecionado)
        var api = "https://api.github.com/repos/Arthurowgg/TheOrbiApp/releases/tags/" + Installer.Tag;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, api);
            request.Headers.TryAddWithoutValidation("User-Agent", "OrbiSetup");
            using var response = await Client.SendAsync(request, token);
            response.EnsureSuccessStatusCode();
            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(token), cancellationToken: token);
            foreach (var asset in document.RootElement.GetProperty("assets").EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? "";
                if (!name.StartsWith("orbi-payload", StringComparison.OrdinalIgnoreCase)) continue;
                var url = asset.GetProperty("browser_download_url").GetString();
                if (url is not null) return await DownloadFrom(url, progress, token);
            }
        }
        catch (Exception error)
        {
            Installer.Write("Busca pela API falhou: " + error.Message);
        }

        throw new InvalidOperationException(
            "Não foi possível baixar o Orbi. Verifique a internet e tente de novo.");
    }

    private static async Task<string> DownloadFrom(string url, IProgress<double> progress, CancellationToken token)
    {
        using var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength ?? ApproximateSize;

        var temp = Cache + ".part";
        await using (var output = File.Create(temp))
        await using (var input = await response.Content.ReadAsStreamAsync(token))
        {
            var buffer = new byte[81920];
            long received = 0;
            int read;
            while ((read = await input.ReadAsync(buffer, token)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), token);
                received += read;
                if (total > 0) progress.Report(Math.Min(1, (double)received / total));
            }
        }

        var info = new FileInfo(temp);
        if (info.Length < 500L * 1024)
        {
            File.Delete(temp);
            throw new InvalidOperationException($"o pacote veio incompleto ({info.Length} bytes)");
        }

        if (File.Exists(Cache)) File.Delete(Cache);
        File.Move(temp, Cache);
        return Cache;
    }

    /// <summary>Lê o zip do cache sem carregar tudo na memória.</summary>
    public static ZipArchive OpenDownloaded(string path) =>
        ZipFile.OpenRead(path);
}

/// <summary>
/// Instalação: encerra o Orbi, apaga a versão anterior inteira, extrai o pacote,
/// cria atalhos e registra o desinstalador. Tudo com log do que aconteceu.
/// </summary>
public static class Installer
{
    public static readonly List<string> Log = new();

    /// <summary>Quem escuta o log: a janela ou o modo silencioso.</summary>
    public static Action<string>? Sink;

    internal static void Write(string line)
    {
        Log.Add(line);
        try { Sink?.Invoke(line); } catch { /* log nunca derruba a instalação */ }
    }

    /// <summary>Abre o Orbi depois de instalar.</summary>
    public static void Launch()
    {
        try
        {
            Process.Start(new ProcessStartInfo(Paths.Exe) { UseShellExecute = true });
        }
        catch { }
    }

    public static async Task RunAsync(IProgress<double> progress, CancellationToken token)
    {
        await Task.Run(() =>
        {
            progress.Report(0.04);
            CloseRunning();

            // 1. apaga a instalação anterior (arquivos, não dados do usuário)
            if (Directory.Exists(Paths.Target))
            {
                Write($"Removendo a versão anterior em {Paths.Target}");
                progress.Report(0.10);
                DeleteDirectory(Paths.Target);
            }

            // 2. atalhos antigos
            foreach (var atalho in new[] { Paths.Desktop, Paths.Startup })
            {
                Try(() => { if (File.Exists(atalho)) File.Delete(atalho); }, $"atalho {atalho}");
            }
            Try(() => { if (Directory.Exists(Paths.StartMenu)) Directory.Delete(Paths.StartMenu, true); },
                "pasta do Menu Iniciar");

            token.ThrowIfCancellationRequested();

            // 3. runtime do .NET (só se faltar) e o pacote do app
            EnsureRuntime();

            Write("Baixando o Orbi…");
            var zipPath = Payload
                .DownloadAsync(new Progress<double>(value => progress.Report(0.16 + 0.60 * value)), token)
                .GetAwaiter().GetResult();
            Write($"Pacote pronto ({new FileInfo(zipPath).Length / 1024 / 1024} MB)");
            progress.Report(0.78);

            Write("Instalando os arquivos…");
            Directory.CreateDirectory(Paths.Target);

            using (var zip = ZipFile.OpenRead(zipPath))
            {
                var total = zip.Entries.Count(entry => !string.IsNullOrEmpty(entry.Name));
                var done = 0;
                foreach (var entry in zip.Entries)
                {
                    token.ThrowIfCancellationRequested();
                    if (string.IsNullOrEmpty(entry.Name)) continue;

                    var destino = Path.GetFullPath(Path.Combine(Paths.Target, entry.FullName));
                    if (!destino.StartsWith(Paths.Target, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException($"entrada suspeita no pacote: {entry.FullName}");
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
                    entry.ExtractToFile(destino, overwrite: true);
                    done++;
                    progress.Report(0.78 + 0.14 * done / Math.Max(1, total));
                }
                Write($"{done} arquivos instalados");
            }

            // 4. desinstalador (o próprio setup, copiado)
            Write("Criando o desinstalador…");
            progress.Report(0.84);
            var atual = Environment.ProcessPath;
            if (atual is not null && File.Exists(atual))
            {
                File.Copy(atual, Paths.Uninstaller, overwrite: true);
            }
            WriteUninstallEntry();

            // 5. atalhos
            Write("Criando os atalhos…");
            progress.Report(0.92);
            Directory.CreateDirectory(Paths.StartMenu);
            Shortcut.Create(Path.Combine(Paths.StartMenu, "Orbi.lnk"), Paths.Exe, "", Paths.Target,
                "Seu PC em português");
            Shortcut.Create(Path.Combine(Paths.StartMenu, "Orbi (ajustes e ferramentas).lnk"), Paths.Exe,
                "--settings", Paths.Target, "Abre direto o painel de ferramentas");
            Shortcut.Create(Path.Combine(Paths.StartMenu, "Desinstalar o Orbi.lnk"), Paths.Uninstaller,
                "--uninstall", Paths.Target, "Remove o Orbi deste PC");
            Shortcut.Create(Paths.Desktop, Paths.Exe, "", Paths.Target, "Seu PC em português");

            progress.Report(1.0);
            Write("Instalação concluída.");
        }, token);
    }

    /// <summary>
    /// O app é publicado "framework-dependent" (é o que faz ele abrir sem extrair
    /// nada). Se este PC ainda não tem o .NET Desktop 8, instala agora — é a única
    /// dependência, e é por isso que o setup em si tem poucos MB.
    /// </summary>
    private static void EnsureRuntime()
    {
        if (RuntimeInstalled())
        {
            Write("Runtime .NET já instalado");
            return;
        }

        Write("Instalando o .NET Desktop 8 (uma vez, só neste PC)…");
        var installer = Path.Combine(Path.GetTempPath(), "orbi-dotnet-desktop.exe");
        if (!File.Exists(installer))
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            using var response = client.GetAsync(Payload.RuntimeUrl).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
            using var file = File.Create(installer);
            response.Content.ReadAsStream().CopyTo(file);
            Write($".NET baixado ({new FileInfo(installer).Length / 1024 / 1024} MB)");
        }

        var processo = Process.Start(new ProcessStartInfo(installer, "/install /quiet /norestart")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        });
        processo?.WaitForExit(10 * 60 * 1000);

        if (!RuntimeInstalled())
        {
            throw new InvalidOperationException(
                "Não consegui instalar o .NET Desktop 8. Baixe em dotnet.microsoft.com/download/dotnet/8.0 e rode o setup de novo.");
        }
        Write("Runtime .NET pronto");
    }

    private static bool RuntimeInstalled()
    {
        foreach (var baseKey in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            try
            {
                using var key = baseKey.OpenSubKey(
                    @"SOFTWARE\WOW6432Node\dotnet\WindowsDesktop\v8.0");
                if (key is not null) return true;
            }
            catch { }
        }
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\dotnet\WindowsDesktop\v8.0");
            if (key is not null) return true;
        }
        catch { }
        return false;
    }

    private static void CloseRunning()
    {
        foreach (var processo in Process.GetProcessesByName("Orbi"))
        {
            Try(() =>
            {
                Write($"Encerrando o Orbi em execução (PID {processo.Id})");
                processo.Kill(entireProcessTree: true);
                processo.WaitForExit(4000);
            }, "encerrar o Orbi");
        }
    }

    private static void DeleteDirectory(string pasta)
    {
        for (var tentativa = 0; tentativa < 3; tentativa++)
        {
            try
            {
                Directory.Delete(pasta, recursive: true);
                return;
            }
            catch (Exception error)
            {
                Write($"não consegui apagar a pasta (tentativa {tentativa + 1}): {error.Message}");
                Thread.Sleep(600);
            }
        }
    }

    private static void Try(Action action, string oque)
    {
        try
        {
            action();
        }
        catch (Exception error)
        {
            Write($"aviso: falhou {oque} — {error.Message}");
        }
    }

    /// <summary>Aparece em Configurações ▸ Aplicativos do Windows.</summary>
    private static void WriteUninstallEntry()
    {
        Try(() =>
        {
            using var chave = Registry.CurrentUser.CreateSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Orbi");
            if (chave is null) return;
            chave.SetValue("DisplayName", "Orbi");
            chave.SetValue("DisplayVersion", Versao);
            chave.SetValue("DisplayIcon", Paths.Exe);
            chave.SetValue("InstallLocation", Paths.Target);
            chave.SetValue("Publisher", "Orbi");
            chave.SetValue("NoModify", 1, RegistryValueKind.DWord);
            chave.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            chave.SetValue("UninstallString", $"\"{Paths.Uninstaller}\" --uninstall");
            chave.SetValue("QuietUninstallString", $"\"{Paths.Uninstaller}\" --uninstall --silent");
            chave.SetValue("EstimatedSize", Directory.Exists(Paths.Target)
                ? (int)(new DirectoryInfo(Paths.Target).EnumerateFiles("*", SearchOption.AllDirectories)
                    .Sum(file => file.Length) / 1024)
                : 0, RegistryValueKind.DWord);
        }, "registrar o desinstalador");
    }

    public static string Versao =>
        System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    /// <summary>Tag da release que combina com esta versão (v1.1.2).</summary>
    public static string Tag => "v" + Versao;

    /// <summary>Desinstalação: encerra o app, apaga atalhos, registro e arquivos.</summary>
    public static void Uninstall()
    {
        foreach (var processo in Process.GetProcessesByName("Orbi"))
        {
            try { processo.Kill(entireProcessTree: true); processo.WaitForExit(4000); } catch { }
        }

        foreach (var atalho in new[] { Paths.Desktop, Paths.Startup })
        {
            try { if (File.Exists(atalho)) File.Delete(atalho); } catch { }
        }
        try { if (Directory.Exists(Paths.StartMenu)) Directory.Delete(Paths.StartMenu, true); } catch { }
        try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Orbi", false); }
        catch { }

        // Apaga agora tudo que não está em uso; o próprio Desinstalar.exe (em uso)
        // fica para o cmd remover.
        try
        {
            foreach (var arquivo in Directory.GetFiles(Paths.Target))
            {
                if (string.Equals(arquivo, Paths.Uninstaller, StringComparison.OrdinalIgnoreCase)) continue;
                try { File.Delete(arquivo); } catch { }
            }
            foreach (var pasta in Directory.GetDirectories(Paths.Target))
            {
                try { Directory.Delete(pasta, true); } catch { }
            }
        }
        catch { }

        // O executável em uso não pode ser apagado: encerra e apaga pelo cmd.
        var alvo = Paths.Target;
        var comando = $"/c ping 127.0.0.1 -n 3 > nul & rmdir /s /q \"{alvo}\"";
        try
        {
            Process.Start(new ProcessStartInfo("cmd.exe", comando) { CreateNoWindow = true, UseShellExecute = false });
        }
        catch { }
    }
}
