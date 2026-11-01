using System.Diagnostics;

namespace Orbi.Services;

/// <summary>Abrir um endereço no navegador, sem derrubar nada se não houver um.</summary>
public static class Browser
{
    public static void Open(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { }
    }
}
