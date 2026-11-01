using System.IO;
using System.Runtime.InteropServices;

namespace Orbi;

/// <summary>
/// Cria um console ao lado do app (modo --console) para ver o log ao vivo.
/// Se o app foi aberto pelo Explorer, ele não tem console; aqui criamos um.
/// </summary>
public static class ConsoleWindow
{
    private const int AttachParentProcess = -1;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    private static bool _attached;

    public static bool Attach()
    {
        if (_attached) return true;

        try
        {
            // Aberto pelo cmd/PowerShell: usa o console do chamador.
            if (!AttachConsole(AttachParentProcess))
            {
                FreeConsole();
                AllocConsole();
            }

            Console.Title = "Orbi — log de inicialização";
            var stdout = Console.OpenStandardOutput();
            var writer = new StreamWriter(stdout) { AutoFlush = true };
            Console.SetOut(writer);
            Console.SetError(writer);
            _attached = true;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
