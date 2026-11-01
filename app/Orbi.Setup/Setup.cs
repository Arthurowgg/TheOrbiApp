using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
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

/// <summary>O pacote do app vai embutido no setup (assets\payload.zip).</summary>
public static class Payload
{
    public static Stream? Open()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var nome = assembly.GetManifestResourceNames()
            .FirstOrDefault(item => item.EndsWith("payload.zip", StringComparison.OrdinalIgnoreCase));
        return nome is null ? null : assembly.GetManifestResourceStream(nome);
    }

    public static bool Exists => Open() is not null;

    /// <summary>Quantos arquivos tem o pacote (mostrado antes de instalar).</summary>
    public static int FileCount()
    {
        try
        {
            using var stream = Open();
            if (stream is null) return 0;
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
            return zip.Entries.Count(entry => !string.IsNullOrEmpty(entry.Name));
        }
        catch
        {
            return 0;
        }
    }
}

/// <summary>
/// Instalação: encerra o Orbi, apaga a versão anterior inteira, extrai o pacote,
/// cria atalhos e registra o desinstalador. Tudo com log do que aconteceu.
/// </summary>
public static class Installer
{
    public static readonly List<string> Log = new();

    private static void Write(string line)
    {
        Log.Add(line);
        App.Current.Dispatcher.BeginInvoke(() => (App.Current as App)?.AppendLog(line));
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

            // 3. extrai o app
            Write("Copiando o Orbi…");
            progress.Report(0.16);
            Directory.CreateDirectory(Paths.Target);

            using (var stream = Payload.Open() ?? throw new InvalidOperationException("pacote do app ausente"))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
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
                    progress.Report(0.16 + 0.66 * done / Math.Max(1, total));
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
            Shortcut.Create(Path.Combine(Paths.StartMenu, "Orbi (abrir com terminal).lnk"), Paths.Exe,
                "--console-keep", Paths.Target, "Abre o Orbi com o log ao vivo");
            Shortcut.Create(Path.Combine(Paths.StartMenu, "Orbi (diagnóstico).lnk"), Paths.Exe,
                "--diag", Paths.Target, "Mostra por que a barra não apareceu");
            Shortcut.Create(Path.Combine(Paths.StartMenu, "Desinstalar o Orbi.lnk"), Paths.Uninstaller,
                "--uninstall", Paths.Target, "Remove o Orbi deste PC");
            Shortcut.Create(Paths.Desktop, Paths.Exe, "", Paths.Target, "Seu PC em português");

            progress.Report(1.0);
            Write("Instalação concluída.");
        }, token);
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
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

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
