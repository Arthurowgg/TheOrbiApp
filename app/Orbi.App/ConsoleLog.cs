using System.IO;
using System.Text;

namespace Orbi;

/// <summary>
/// Registro de inicialização que aparece no terminal (modo --console), no
/// arquivo e na janela de diagnóstico.
///
/// Cada passo ("tema carregado", "bandeja criada", "janela exibida") entra aqui
/// com tempo decorrido. Se o app morrer no meio, a última linha do log diz onde
/// parou — sem precisar de depurador.
/// </summary>
public static class ConsoleLog
{
    private static readonly StringBuilder Buffer = new();
    private static readonly DateTime Started = DateTime.Now;
    private static readonly object Gate = new();
    private static bool _echo = true;

    public static string Path => System.IO.Path.Combine(Store.Root, "startup.log");

    public static string Transcript
    {
        get { lock (Gate) return Buffer.ToString(); }
    }

    public static void EnableConsole(bool enable) => _echo = enable;

    public static void Step(string message, string? detail = null)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff} +{(DateTime.Now - Started).TotalMilliseconds:0}ms] {message}" +
                   (string.IsNullOrWhiteSpace(detail) ? "" : $"  · {detail}");

        lock (Gate)
        {
            Buffer.AppendLine(line);
            try
            {
                Directory.CreateDirectory(Store.Root);
                File.AppendAllText(Path, line + Environment.NewLine);
            }
            catch
            {
                // sem arquivo: o terminal e o diagnóstico ainda têm a linha
            }
        }

        if (_echo)
        {
            try
            {
                Console.WriteLine(line);
                Console.Out.Flush();
            }
            catch
            {
                // app sem console anexado
            }
        }
    }

    public static void Fail(string step, Exception error) =>
        Step($"FALHOU: {step}", $"{error.GetType().Name}: {error.Message}");

    /// <summary>Limpa o log de inicialização (o app faz isso a cada execução).</summary>
    public static void Reset()
    {
        lock (Gate)
        {
            Buffer.Clear();
            try
            {
                Directory.CreateDirectory(Store.Root);
                File.WriteAllText(Path, "");
            }
            catch
            {
                // ignorar
            }
        }
    }
}
