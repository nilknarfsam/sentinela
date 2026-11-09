using System.Windows;
using System.Windows.Controls;
using Sentinela.Servicos;

namespace Sentinela.Paginas;

public sealed record ItemHistorico(DateTime Data, string Origem, string Tipo, string Titulo, string Detalhe)
{
    public bool Alerta => Tipo is "Ameaca" or "Configuracao" or "ameaca" or "alerta"
        || (Tipo == "Protecao" && Titulo.Contains("desligada", StringComparison.OrdinalIgnoreCase))
        || (Tipo == "Varredura" && Titulo == "Varredura falhou")
        || (Tipo == "Assinaturas" && Titulo == "Falha ao atualizar assinaturas");
}

public partial class PaginaHistorico : UserControl
{
    private List<ItemHistorico> _itens = [];
    private string _filtro = "tudo";
    private Task? _carregamento;
    public bool Carregada { get; private set; }

    public PaginaHistorico() => InitializeComponent();

    public void Invalidar()
    {
        Carregada = false;
        if (IsVisible) _ = Carregar();
    }

    public Task Carregar() => _carregamento ??= CarregarLista();

    private async Task CarregarLista()
    {
        // Garante que a tarefa seja guardada antes de qualquer continuação.
        await Task.Yield();
        try
        {
            do
            {
                Carregada = true;
                _itens = await Task.Run(() => VigiaDefender.LerRecentes(30)
                    .Select(e => new ItemHistorico(e.Data, "Defender", e.Tipo.ToString(), e.Titulo, e.Detalhe))
                    .Concat(Diario.Ler(30).Select(e => new ItemHistorico(e.Data, "Sentinela", e.Tipo, TituloDiario(e.Tipo), e.Mensagem)))
                    .OrderByDescending(e => e.Data).ToList());
                AplicarFiltro();
            } while (!Carregada && IsVisible);
        }
        catch (Exception ex)
        {
            Carregada = false;
            TxtResumo.Text = "Não consegui ler o histórico: " + ex.Message;
        }
        finally { _carregamento = null; }
    }

    private static string TituloDiario(string tipo) => tipo switch
    {
        "ameaca" => "Ameaça", "tratar" => "Tratamento de ameaças", "alerta" => "Alerta", "varredura" => "Varredura",
        "assinaturas" => "Atualização de assinaturas", "ajuste" => "Ajuste do Sentinela",
        "limpeza" => "Limpeza", "inicio" => "Sentinela iniciado", "status" => "Status do Defender",
        _ => "Registro do Sentinela",
    };

    private void Filtrar_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button botao) return;
        _filtro = (string)botao.Tag;
        foreach (Button filtro in Filtros.Children)
            filtro.Style = (Style)FindResource(filtro == botao ? "BotaoPrincipal" : "BotaoBase");
        AplicarFiltro();
    }

    private void AplicarFiltro()
    {
        var filtrados = _itens.Where(e => _filtro switch
        {
            "ameacas" => e.Tipo is "Ameaca" or "AmeacaTratada" or "ameaca" or "tratar",
            "varreduras" => e.Tipo is "Varredura" or "varredura",
            "protecao" => e.Tipo is "Protecao" or "Configuracao" or "Assinaturas" or "assinaturas" or "ajuste" or "alerta" or "status",
            "limpeza" => e.Tipo == "limpeza",
            _ => true,
        }).ToList();
        GradeHistorico.ItemsSource = filtrados;
        TxtResumo.Text = $"{filtrados.Count} eventos nos últimos 30 dias";
    }
}
