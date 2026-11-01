using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Windows;
using Forms = System.Windows.Forms;
using Microsoft.Win32;

namespace Orbi.Services;

/// <summary>Ações reais no Windows. Tudo local, nada sai do PC por aqui.</summary>
public static class SystemActions
{
    /* ----------------------------- áudio ----------------------------- */
    private static void KeyPress(Forms.Keys key)
    {
        Forms.SendKeys.SendWait(key.ToString());
    }

    public static void VolumeUp(int percent = 10)
    {
        // Via teclas de mídia: funciona com qualquer saída de áudio.
        var steps = Math.Clamp(percent / 2, 1, 50);
        for (var i = 0; i < steps; i++) KeyPress(Forms.Keys.VolumeUp);
    }

    public static void VolumeDown(int percent = 10)
    {
        var steps = Math.Clamp(percent / 2, 1, 50);
        for (var i = 0; i < steps; i++) KeyPress(Forms.Keys.VolumeDown);
    }

    public static void Mute() => KeyPress(Forms.Keys.VolumeMute);
    public static void PlayPause() => KeyPress(Forms.Keys.MediaPlayPause);

    /// <summary>Ajusta o volume para um nível absoluto (0-100) usando o estado atual.</summary>
    public static string SetVolume(int target)
    {
        target = Math.Clamp(target, 0, 100);
        // Se estiver mudo, sai do mudo; depois zera e sobe até o nível pedido.
        KeyPress(Forms.Keys.VolumeMute);
        KeyPress(Forms.Keys.VolumeMute);
        for (var i = 0; i < 50; i++) KeyPress(Forms.Keys.VolumeDown);
        var steps = (int)Math.Round(target / 2.0);
        for (var i = 0; i < steps; i++) KeyPress(Forms.Keys.VolumeUp);
        Store.Current.Volume = target;
        Store.Save();
        return $"Volume em {target}%.";
    }

    /* ----------------------------- brilho ----------------------------- */
    public static string SetBrightness(int target)
    {
        target = Math.Clamp(target, 0, 100);
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT * FROM WmiMonitorBrightnessMethods");
            foreach (System.Management.ManagementObject item in searcher.Get())
            {
                using var method = item;
                method.InvokeMethod("WmiSetBrightness", new object[] { 1, target });
            }
            return $"Brilho em {target}%.";
        }
        catch
        {
            return "Este monitor não expõe controle de brilho pelo Windows (comum em monitores externos).";
        }
    }

    public static string BrightnessStep(int delta)
    {
        var current = GetBrightness() ?? 50;
        return SetBrightness(current + delta);
    }

    public static int? GetBrightness()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT CurrentBrightness FROM WmiMonitorBrightness");
            foreach (var item in searcher.Get())
            {
                using var obj = item;
                if (obj["CurrentBrightness"] is { } value) return Convert.ToInt32(value);
            }
        }
        catch
        {
            // monitor externo
        }
        return null;
    }

    /* ----------------------------- energia ----------------------------- */
    public static string Lock() => LockScreen.Lock();

    public static string Shutdown(int minutes)
    {
        var args = minutes > 0 ? $"/s /t {minutes * 60}" : "/s /t 0";
        Run("shutdown.exe", args);
        return minutes > 0 ? $"O PC desliga em {minutes} min. Para parar, diga “cancela”." : "Desligando o PC.";
    }

    public static string Restart(int minutes)
    {
        var args = minutes > 0 ? $"/r /t {minutes * 60}" : "/r /t 0";
        Run("shutdown.exe", args);
        return minutes > 0 ? $"O PC reinicia em {minutes} min." : "Reiniciando o PC.";
    }

    public static string CancelShutdown()
    {
        Run("shutdown.exe", "/a");
        return "Desligamento cancelado.";
    }

    public static string Sleep()
    {
        Run("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0");
        return "Colocando o PC para dormir.";
    }

    /* ----------------------------- apps ----------------------------- */
    private static readonly Dictionary<string, string> KnownApps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bloco de notas"] = "notepad.exe",
        ["notepad"] = "notepad.exe",
        ["calculadora"] = "calc.exe",
        ["calc"] = "calc.exe",
        ["paint"] = "mspaint.exe",
        ["explorador"] = "explorer.exe",
        ["explorer"] = "explorer.exe",
        ["arquivos"] = "explorer.exe",
        ["configuracoes"] = "ms-settings:",
        ["configurações"] = "ms-settings:",
        ["painel de controle"] = "control.exe",
        ["edge"] = "msedge.exe",
        ["chrome"] = "chrome.exe",
        ["google chrome"] = "chrome.exe",
        ["firefox"] = "firefox.exe",
        ["word"] = "winword.exe",
        ["excel"] = "excel.exe",
        ["powerpoint"] = "powerpnt.exe",
        ["outlook"] = "outlook.exe",
        ["spotify"] = "spotify.exe",
        ["discord"] = "discord.exe",
        ["steam"] = "steam.exe",
        ["whatsapp"] = "WhatsApp.exe",
        ["zap"] = "WhatsApp.exe",
        ["zapzap"] = "WhatsApp.exe",
        ["teams"] = "ms-teams.exe",
        ["vs code"] = "Code.exe",
        ["vscode"] = "Code.exe",
        ["code"] = "Code.exe",
        ["terminal"] = "wt.exe",
        ["powershell"] = "powershell.exe",
        ["notion"] = "Notion.exe",
        ["netflix"] = "netflix:",
        ["camera"] = "microsoft.windows.camera:",
        ["fotos"] = "ms-photos:",
        ["spotify music"] = "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify",
        ["whatsapp store"] = "5319275A.WhatsAppDesktop_cv1g1gvanyjgm!WhatsAppDesktop",
        ["tiktok"] = "BytedancePte.Ltd.TikTok_6yq1qqg9905d0!App",
        ["instagram"] = "Facebook.InstagramBeta_8xx8rvfyw5cn7!App",
    };

    public static IReadOnlyCollection<string> AppNames => KnownApps.Keys;

    /// <summary>Abre um app por apelido, protocolo ou caminho. Devolve a mensagem para a tela.</summary>
    public static string Open(string rawName)
    {
        var name = rawName.Trim().Trim('"', '.', ',');
        if (string.IsNullOrWhiteSpace(name)) return "Não entendi qual app você quer abrir.";

        // 1. nome conhecido
        if (KnownApps.TryGetValue(name.ToLowerInvariant(), out var executable))
        {
            // Package Family Name (app da Store): shell:AppsFolder\AUMID
            if (executable.Contains('!'))
            {
                return TryStart("shell:AppsFolder\\" + executable)
                    ? $"Abrindo {name}."
                    : $"Não consegui abrir {name}. Ele está instalado neste PC?";
            }
            return Launch(executable, DisplayName(executable, name));
        }

        // 2. caminho direto
        if (File.Exists(name))
        {
            return Launch($"\"{name}\"", Path.GetFileNameWithoutExtension(name));
        }

        // 3. procura sozinho no Windows (Menu Iniciar, Área de trabalho, Store)
        var found = SearchStartMenu(name);
        if (found is not null)
        {
            return Launch($"\"{found}\"", Path.GetFileNameWithoutExtension(found));
        }

        // 4. app da Store / protocolo
        try
        {
            Process.Start(new ProcessStartInfo(name) { UseShellExecute = true });
            return $"Abrindo {name}.";
        }
        catch
        {
            return $"Não encontrei “{name}” instalado neste PC. Confira o nome no Menu Iniciar.";
        }
    }

    private static string DisplayName(string executable, string fallback)
    {
        var file = Path.GetFileNameWithoutExtension(executable);
        return string.IsNullOrWhiteSpace(file) ? fallback : file;
    }

    private static string Launch(string command, string display)
    {
        if (TryStart(command)) return $"Abrindo {display}.";

        // Protocolos (ms-settings:, netflix:, ms-photos:…) não têm outro caminho:
        // falhar aqui significa que o app não está instalado.
        if (command.Contains(':')) return $"{display} não está instalado neste PC.";

        // Executável fora do PATH ou app da Microsoft Store: tenta o shell.
        if (!command.Contains(':') && TryStart("shell:AppsFolder\\" + Path.GetFileNameWithoutExtension(command)))
        {
            return $"Abrindo {display}.";
        }

        var direct = ResolveExecutable(command);
        if (direct is not null && TryStart($"\"{direct}\""))
        {
            return $"Abrindo {display}.";
        }

        return $"Não consegui abrir {display}. Ele está instalado?";
    }

    private static bool TryStart(string command)
    {
        try
        {
            Process.Start(new ProcessStartInfo(command) { UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Procura o executável em Program Files, LocalAppData e afins.</summary>
    private static string? ResolveExecutable(string command)
    {
        var name = Path.GetFileName(command);
        if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name += ".exe";

        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)),
        };

        foreach (var root in roots.Where(Directory.Exists))
        {
            try
            {
                // profundidade 3 cobre Program Files\Fornecedor\App\app.exe
                foreach (var candidate in EnumerateSafe(root, name, 3))
                {
                    return candidate;
                }
            }
            catch
            {
                // pasta sem permissão: segue para a próxima
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateSafe(string root, string fileName, int depth)
    {
        var pending = new Queue<(string Path, int Level)>();
        pending.Enqueue((root, 0));

        while (pending.Count > 0)
        {
            var (folder, level) = pending.Dequeue();
            string[] files;
            try { files = Directory.GetFiles(folder, fileName); }
            catch { files = Array.Empty<string>(); }

            foreach (var file in files) yield return file;

            if (level >= depth) continue;

            string[] subfolders;
            try { subfolders = Directory.GetDirectories(folder); }
            catch { subfolders = Array.Empty<string>(); }

            foreach (var subfolder in subfolders)
            {
                if (pending.Count < 400) pending.Enqueue((subfolder, level + 1));
            }
        }
    }

    /// <summary>
    /// Acha o app sem ninguém cadastrar nada: Menu Iniciar (+ Inicializar), atalhos
    /// da Área de trabalho, executáveis soltos e apps da Microsoft Store, com
    /// comparação tolerante a acento, caixa, espaços e erros de digitação.
    /// </summary>
    private static string? SearchStartMenu(string query)
    {
        var wanted = AppKey(query);
        if (wanted.Length < 2) return null;

        string? fuzzy = null;

        foreach (var (file, key) in InstalledShortcuts())
        {
            if (key == wanted) return file;                  // exato
            if (key.Replace(" ", "") == wanted.Replace(" ", "")) return file;

            // "zap" acha "WhatsApp"; "excel" acha "Microsoft Excel"…
            if (wanted.Length >= 3 && key.Contains(wanted)) return file;
            if (fuzzy is null && wanted.Length >= 4 && key.Length >= 3 && Distance(key, wanted) <= 2)
            {
                fuzzy = file;
            }
        }

        if (fuzzy is not null) return fuzzy;

        return SearchStoreApps(wanted);
    }

    /// <summary>Atalhos instalados para este usuário, com a chave comparável.</summary>
    private static IEnumerable<(string File, string Key)> InstalledShortcuts()
    {
        var folders = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
        };

        foreach (var folder in folders.Where(Directory.Exists))
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(folder, "*.lnk", SearchOption.AllDirectories).ToList();
            }
            catch
            {
                continue; // pasta protegida: segue
            }

            foreach (var file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                yield return (file, AppKey(name));
            }
        }
    }

    /// <summary>Apps da Microsoft Store: compara com o nome de exibição.</summary>
    private static string? SearchStoreApps(string wanted)
    {
        try
        {
            // Late binding (sem interop): o Windows expõe shell:AppsFolder pelo Shell.
            dynamic? shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!);
            if (shell is null) return null;
            dynamic? folder = shell.NameSpace("shell:AppsFolder");
            if (folder is null) return null;

            foreach (dynamic entry in folder.Items())
            {
                string name;
                string path;
                try
                {
                    name = (string)(entry.Name ?? "");
                    path = (string)(entry.Path ?? "");
                }
                catch
                {
                    continue;
                }

                var key = AppKey(name);
                if (key.Length < 3) continue;
                if (key == wanted || key.Contains(wanted) || (wanted.Length >= 4 && Distance(key, wanted) <= 2))
                {
                    // O Shell devolve "pasta\AUMID"; o que abre é só o AUMID.
                    var atalho = "shell:AppsFolder\\" + Path.GetFileName(path ?? "");
                    if (!string.IsNullOrWhiteSpace(Path.GetFileName(path ?? ""))) return atalho;
                }
            }
        }
        catch
        {
            // sem shell/COM: o Menu Iniciar já cobriu os casos comuns
        }
        return null;
    }

    /// <summary>Nome comparável: sem acento, sem caixa, sem pontuação, sem "app"/"desktop".</summary>
    private static string AppKey(string name)
    {
        var lowered = (name ?? "").ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(lowered.Length);
        foreach (var ch in lowered)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(char.IsLetterOrDigit(ch) || ch == ' ' ? ch : ' ');
        }

        var words = builder.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word is not ("app" or "desktop" or "classic" or "new" or "v" or "x64" or "x86"))
            .ToArray();

        return string.Join(' ', words);
    }

    /// <summary>Distância de edição — para "whatsap" achar "WhatsApp".</summary>
    private static int Distance(string a, string b)
    {
        if (Math.Abs(a.Length - b.Length) > 3) return 99;
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) previous[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }
            (previous, current) = (current, previous);
        }
        return previous[b.Length];
    }

    public static string Close(string processName)
    {
        var cleaned = Path.GetFileNameWithoutExtension(processName);
        var count = 0;
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var title = process.MainWindowTitle;
                if (process.ProcessName.Equals(cleaned, StringComparison.OrdinalIgnoreCase) ||
                    process.MainWindowTitle.Contains(processName, StringComparison.OrdinalIgnoreCase))
                {
                    if (process.CloseMainWindow())
                    {
                        count++;
                    }
                }
            }
            catch
            {
                // processos protegidos: ignorar
            }
            finally
            {
                process.Dispose();
            }
        }
        return count > 0
            ? $"{cleaned} foi fechado."
            : $"Não achei {cleaned} aberto agora.";
    }

    /* ----------------------------- sistema ----------------------------- */
    public record SystemSnapshot(double Cpu, double MemoryUsedGb, double MemoryTotalGb,
        double DiskFreeGb, double DiskTotalGb, int? Battery, string? TopProcess);

    public static SystemSnapshot Snapshot()
    {
        double cpu = 0;
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT LoadPercentage FROM Win32_Processor");
            foreach (var item in searcher.Get())
            {
                using var obj = item;
                cpu += Convert.ToDouble(obj["LoadPercentage"] ?? 0);
            }
        }
        catch { }

        double totalGb = 0, freeGb = 0;
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            foreach (var item in searcher.Get())
            {
                using var obj = item;
                totalGb = Convert.ToDouble(obj["TotalVisibleMemorySize"] ?? 0) / 1024 / 1024;
                freeGb = Convert.ToDouble(obj["FreePhysicalMemory"] ?? 0) / 1024 / 1024;
            }
        }
        catch { }

        double diskTotal = 0, diskFree = 0;
        try
        {
            var drive = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady && d.Name.StartsWith("C"));
            if (drive is not null)
            {
                diskTotal = drive.TotalSize / 1024.0 / 1024 / 1024;
                diskFree = drive.AvailableFreeSpace / 1024.0 / 1024 / 1024;
            }
        }
        catch { }

        int? battery = null;
        try
        {
            battery = Forms.SystemInformation.PowerStatus.BatteryLifePercent is var p and >= 0 and <= 1
                ? (int)Math.Round(p * 100)
                : null;
        }
        catch { }

        string? top = null;
        try
        {
            long best = 0;
            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    if (process.WorkingSet64 > best && !string.IsNullOrWhiteSpace(process.MainWindowTitle))
                    {
                        best = process.WorkingSet64;
                        top = process.ProcessName;
                    }
                }
                catch { }
                finally { process.Dispose(); }
            }
        }
        catch { }

        return new SystemSnapshot(cpu, Math.Max(0, totalGb - freeGb), totalGb,
            diskFree, diskTotal, battery, top);
    }

    public static string Screenshot()
    {
        try
        {
            var bounds = Forms.Screen.PrimaryScreen?.Bounds ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);
            using var bitmap = new System.Drawing.Bitmap(bounds.Width, bounds.Height);
            using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(bounds.Location, System.Drawing.Point.Empty, bounds.Size);
            }
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Orbi");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"captura-{DateTime.Now:yyyyMMdd-HHmmss}.png");
            bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);

            try
            {
                System.Windows.Clipboard.SetImage(ToBitmapSource(bitmap));
            }
            catch { }

            return $"Captura salva em Imagens\\Orbi\\{Path.GetFileName(path)} e copiada para a área de transferência.";
        }
        catch (Exception error)
        {
            return $"Não consegui capturar a tela: {error.Message}";
        }
    }

    private static System.Windows.Media.Imaging.BitmapSource ToBitmapSource(System.Drawing.Bitmap bitmap)
    {
        using var memory = new MemoryStream();
        bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
        memory.Position = 0;
        var image = new System.Windows.Media.Imaging.BitmapImage();
        image.BeginInit();
        image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
        image.StreamSource = memory;
        image.EndInit();
        image.Freeze();
        return image;
    }

    public static string Wifi(bool on)
    {
        var state = on ? "enable" : "disable";
        Run("netsh.exe", $"interface set interface \"Wi-Fi\" admin={state}");
        return on ? "Wi-Fi ligado." : "Wi-Fi desligado.";
    }

    public static string EmptyRecycleBin()
    {
        try
        {
            var files = 0;
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                files += ShellNative.EmptyRecycleBin(drive.Name);
            }
            return "Lixeira esvaziada.";
        }
        catch
        {
            return "Não consegui esvaziar a lixeira.";
        }
    }

    /* ----------------------------- utilidades ----------------------------- */
    public static void Run(string file, string args)
    {
        try
        {
            Process.Start(new ProcessStartInfo(file, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            });
        }
        catch
        {
            // nunca deixamos uma falha de shell derrubar o app
        }
    }

    public static string StartWithWindows(bool enabled)
    {
        try
        {
            var exe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "Orbi.exe";
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key is null) return "Não consegui acessar o registro do Windows.";
            if (enabled) key.SetValue("Orbi", $"\"{exe}\" --tray");
            else key.DeleteValue("Orbi", throwOnMissingValue: false);
            Store.Current.StartWithWindows = enabled;
            Store.Save();
            return enabled ? "O Orbi abre junto com o Windows." : "O Orbi não abre mais sozinho.";
        }
        catch
        {
            return "Não consegui alterar a inicialização automática.";
        }
    }
}

internal static class LockScreen
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool LockWorkStation();

    public static string Lock()
    {
        return LockWorkStation()
            ? "Windows bloqueado. Só a sua senha abre."
            : "Não consegui bloquear o Windows.";
    }
}

internal static class ShellNative
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? root, uint flags);

    public static int EmptyRecycleBin(string drive)
    {
        const uint NoConfirmation = 0x1;
        const uint NoProgress = 0x2;
        const uint NoSound = 0x4;
        return SHEmptyRecycleBin(IntPtr.Zero, drive, NoConfirmation | NoProgress | NoSound);
    }
}
