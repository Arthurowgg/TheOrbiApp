using System.IO;
using System.Text;

namespace Orbi.Services;

/// <summary>
/// Log de uma linha por etapa, em %APPDATA%\Orbi\orbi.log. É o único arquivo que
/// o app grava para se explicar — sem sondas, sem relatórios, sem janelas de
/// diagnóstico. Roda em modo simples: se não der para escrever, segue a vida.
/// </summary>
public static class Log
{
    private static readonly object Lock = new();

    public static string Path => System.IO.Path.Combine(Store.Root, "orbi.log");

    public static void Add(string message, string? detail = null)
    {
        try
        {
            lock (Lock)
            {
                Directory.CreateDirectory(Store.Root);
                var line = $"{DateTime.Now:HH:mm:ss}  {message}";
                if (!string.IsNullOrWhiteSpace(detail)) line += " · " + detail;
                File.AppendAllText(Path, line + Environment.NewLine, Encoding.UTF8);

                // Não deixa o arquivo crescer sem fim.
                var info = new FileInfo(Path);
                if (info.Exists && info.Length > 512 * 1024)
                {
                    var tail = File.ReadAllLines(Path);
                    File.WriteAllLines(Path, tail.Skip(tail.Length - 500), Encoding.UTF8);
                }
            }
        }
        catch
        {
            // log nunca pode derrubar o app
        }
    }

    public static void Error(string what, Exception error) => Add("erro em " + what, error.GetType().Name + ": " + error.Message);
}
