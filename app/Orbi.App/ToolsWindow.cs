using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Orbi.Services;

namespace Orbi;

/// <summary>
/// Painel de ferramentas: busca, grupos e a lista do catálogo. Um duplo clique
/// (ou Enter) executa — sem abrir os ajustes do Windows.
/// </summary>
public sealed class ToolsWindow : Window
{
    private readonly TextBox _busca = Panel.Field();
    private readonly ListBox _lista = new()
    {
        BorderThickness = new Thickness(0),
        Background = Brushes.Transparent,
    };
    private readonly TextBlock _status = Panel.Body("");
    private readonly TextBlock _contagem = Panel.Body("");

    public ToolsWindow()
    {
        var janela = Panel.Shell("Orbi — Ferramentas", 640, 620);
        Title = janela.Title;
        Width = janela.Width;
        Height = janela.Height;
        MinWidth = 460;
        MinHeight = 380;
        Background = janela.Background;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        UseLayoutRounding = true;
        FontFamily = janela.FontFamily;

        var raiz = new Grid();
        raiz.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        raiz.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        raiz.Children.Add(Panel.Header("Ferramentas", $"{Tools.All.Count} ações", () => App.Instance.ReturnBar(), this));

        var corpo = new Grid { Margin = new Thickness(20, 0, 20, 20) };
        corpo.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        corpo.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        corpo.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _busca.Margin = new Thickness(0, 0, 0, 10);
        _busca.TextChanged += (_, _) => Filtrar(_busca.Text);
        corpo.Children.Add(_busca);

        _lista.ItemTemplate = Linha();
        _lista.ItemContainerStyle = ItemStyle();
        _lista.MouseDoubleClick += (_, _) => ExecutarSelecionada();
        _lista.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) ExecutarSelecionada();
        };

        var rolagem = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _lista,
        };
        Grid.SetRow(rolagem, 1);
        corpo.Children.Add(rolagem);

        var rodape = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        rodape.Children.Add(_contagem);
        rodape.Children.Add(_status);
        Grid.SetRow(rodape, 2);
        corpo.Children.Add(rodape);

        Grid.SetRow(corpo, 1);
        raiz.Children.Add(corpo);
        Content = raiz;

        Filtrar("");
        Loaded += (_, _) => _busca.Focus();
    }

    private static DataTemplate Linha()
    {
        var template = new DataTemplate(typeof(Tool));
        var grade = new FrameworkElementFactory(typeof(Grid));
        var colunaGrupo = new FrameworkElementFactory(typeof(TextBlock));
        colunaGrupo.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(Tool.Group)));
        colunaGrupo.SetValue(TextBlock.WidthProperty, 132.0);
        colunaGrupo.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Cascadia Mono, Consolas"));
        colunaGrupo.SetValue(TextBlock.FontSizeProperty, 10.0);
        colunaGrupo.SetValue(TextBlock.ForegroundProperty, Panel.Warm);
        colunaGrupo.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        grade.AppendChild(colunaGrupo);

        var colunaNome = new FrameworkElementFactory(typeof(TextBlock));
        colunaNome.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(Tool.Name)));
        colunaNome.SetValue(TextBlock.FontSizeProperty, 13.0);
        colunaNome.SetValue(TextBlock.ForegroundProperty, Panel.Ink);
        colunaNome.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        colunaNome.SetValue(Grid.ColumnProperty, 1);
        grade.AppendChild(colunaNome);

        var colunas = new FrameworkElementFactory(typeof(ColumnDefinitions));
        template.VisualTree = grade;
        return template;
    }

    private static Style ItemStyle()
    {
        var style = new Style(typeof(ListBoxItem));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6, 7, 6, 7)));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, Panel.Rule));
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        return style;
    }

    private void Filtrar(string texto)
    {
        var encontradas = string.IsNullOrWhiteSpace(texto) ? Tools.All : Tools.Search(texto);
        _lista.ItemsSource = encontradas;
        _contagem.Text = $"{encontradas.Count} de {Tools.All.Count}";
    }

    private void ExecutarSelecionada()
    {
        if (_lista.SelectedItem is not Tool ferramenta) return;
        var resultado = Tools.Run(ferramenta.Id);
        _status.Text = $"{ferramenta.Name}: {resultado}";
    }
}
