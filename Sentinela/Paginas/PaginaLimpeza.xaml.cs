using System.Windows;
using System.Windows.Controls;
using Sentinela.Servicos;
using static Sentinela.Servicos.Formatos;

namespace Sentinela.Paginas;

public partial class PaginaLimpeza : UserControl
{
    private readonly bool _admin = Sistema.EhAdmin();
    private readonly List<ItemLimpeza> _itensLimpeza = Limpeza.Itens();

    public event Action? EspacoMudou;

    public PaginaLimpeza()
    {
        InitializeComponent();
        ListaLimpeza.ItemsSource = _itensLimpeza;
    }

    private async void Analisar_Click(object sender, RoutedEventArgs e) => await Analisar();

    public async Task Analisar()
    {
        BtnAnalisar.IsEnabled = BtnLimpar.IsEnabled = false;
        TxtResultadoLimpeza.Text = "Medindo...";
        foreach (var item in _itensLimpeza) item.Bytes = -1;
        long total = 0;
        foreach (var item in _itensLimpeza)
        {
            item.Bytes = await Task.Run(() => Limpeza.Medir(item));
            total += item.Bytes;
        }
        TxtResultadoLimpeza.Text = $"Dá para liberar até {Limpeza.Formatar(total)}." +
            (_admin ? "" : " Sem administrador, parte das pastas do Windows não pode ser limpa.");
        BtnAnalisar.IsEnabled = BtnLimpar.IsEnabled = true;
    }

    private async void Limpar_Click(object sender, RoutedEventArgs e)
    {
        var marcados = _itensLimpeza.Where(i => i.Marcado).ToList();
        if (marcados.Count == 0) { TxtResultadoLimpeza.Text = "Marque pelo menos um item."; return; }
        var lista = string.Join("\n", marcados.Select(i => "  •  " + i.Nome));
        if (MessageBox.Show(Window.GetWindow(this), $"Apagar os arquivos de:\n\n{lista}\n\nOs arquivos apagados não vão para a Lixeira.",
                "Confirmar limpeza", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        BtnAnalisar.IsEnabled = BtnLimpar.IsEnabled = false;
        TxtResultadoLimpeza.Text = "Limpando...";
        long liberado = 0;
        foreach (var item in marcados)
            liberado += await Task.Run(() => Limpeza.Limpar(item));
        await Analisar();
        TxtResultadoLimpeza.Text = $"Pronto: {Limpeza.Formatar(liberado)} liberados. " + TxtResultadoLimpeza.Text;
        EspacoMudou?.Invoke();
    }
}
