using System.Diagnostics;

namespace Orbi.Services;

/// <summary>Abrir um endereço no navegador, sem derrubar nada se não houver um.</summary>
public static class Browser
{
    public static string Open(string url)
    {
        try
        {
            var endereco = url.Contains("://") ? url : "https://" + url;
            Process.Start(new ProcessStartInfo(endereco) { UseShellExecute = true });
            return endereco;
        }
        catch (Exception error)
        {
            Log.Error("abrir no navegador", error);
            return "não abri (" + error.Message + ")";
        }
    }
}
