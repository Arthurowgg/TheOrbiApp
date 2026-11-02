using System.Windows.Media;
using System.Windows.Media.Imaging;
using Orbi.Services;

namespace Orbi;

/// <summary>O ícone do Orbi, carregado do próprio assembly (o mesmo do site).</summary>
public static class Brand
{
    private static BitmapImage? _source;

    public static ImageSource? Icon(double tamanho = 24)
    {
        try
        {
            _source ??= new BitmapImage(new Uri("pack://application:,,,/Orbi;component/Assets/orbi-512.png"));
            return _source;
        }
        catch (Exception error)
        {
            Log.Error("ícone da marca", error);
            return null;
        }
    }
}
