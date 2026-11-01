using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Orbi;

/// <summary>
/// Console do modo --console.
///
/// Problema que deixava o terminal vazio: o Orbi é um app WinExe (sem subsistema
/// de console), então o stdout herdado é inválido — AttachConsole sozinho não
/// resolve e as escritas somem. Aqui sempre criamos um console próprio
/// (AllocConsole) e escrevemos direto no dispositivo "CONOUT$", que é o único
/// caminho confiável nesse tipo de processo.
/// </summary>
public static class ConsoleWindow
{
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareReadWrite = 0x00000003;
    private const uint OpenExisting = 3;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleOutputCP(uint codePage);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleTitle(string title);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFileW(
        string fileName, uint desiredAccess, uint shareMode, IntPtr security,
        uint creationDisposition, uint flags, IntPtr template);

    private static bool _ready;
    private static StreamWriter? _writer;

    public static bool Attach()
    {
        if (_ready) return true;

        try
        {
            // Já aberto pelo cmd/powershell: aproveita. Senão, cria um console.
            if (!AttachConsole(-1))
            {
                FreeConsole();
                AllocConsole();
            }

            SetConsoleOutputCP(65001);
            SetConsoleTitle("Orbi — log de inicialização");

            // CONOUT$ é o caminho direto para o console — funciona mesmo sem
            // os handles padrão de um app de console.
            var handle = CreateFileW("CONOUT$", GenericRead | GenericWrite,
                FileShareReadWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);

            if (handle == IntPtr.Zero || handle == new IntPtr(-1))
            {
                // Plano B: tenta os fluxos padrão.
                _writer = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false))
                {
                    AutoFlush = true,
                };
            }
            else
            {
                var stream = new FileStream(new Microsoft.Win32.SafeHandles.SafeFileHandle(handle, true),
                    FileAccess.Write, 8192, isAsync: false);
                _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
            }

            Console.SetOut(_writer);
            Console.SetError(_writer);

            // Cabeçalho imediato: se aparecer, o canal está funcionando.
            _writer.WriteLine("Orbi — log de inicialização");
            _writer.WriteLine("---------------------------------------------");
            _writer.Flush();

            _ready = true;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Escreve direto no console, sem passar por Console.Out.</summary>
    public static void Write(string line)
    {
        if (_writer is null) return;
        try
        {
            _writer.WriteLine(line);
            _writer.Flush();
        }
        catch
        {
            // console fechado pelo usuário
        }
    }
}
