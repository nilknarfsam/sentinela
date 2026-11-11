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
    private RelatorioSemana? _relatorio;
    private int _versaoRelatorio;
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
                await CarregarRelatorio();
            } while (!Carregada && IsVisible);
        }
        catch (Exception ex)
        {
            Carregada = false;
            TxtResumo.Text = "Não consegui ler o histórico: " + ex.Message;
            GradeHistorico.ItemsSource = null;
            GradeHistorico.Visibility = Visibility.Collapsed;
            TxtEstadoVazio.Text = "Não foi possível carregar os eventos.";
            EstadoVazio.Visibility = Visibility.Visible;
        }
        finally { _carregamento = null; }
    }

    public Task AtualizarRelatorio() => CarregarRelatorio();

    private async Task CarregarRelatorio()
    {
        var versao = ++_versaoRelatorio;
        var passada = PeriodoRelatorio.SelectedIndex == 0;
        BtnVerRelatorio.IsEnabled = BtnExportarRelatorio.IsEnabled = false;
        try
        {
            var relatorio = await Task.Run(() => Relatorio.Ler(DateTime.Now, passada));
            if (versao != _versaoRelatorio) return;
            _relatorio = relatorio;
            TxtResumoRelatorio.Text = relatorio.Resumo;
            TxtPeriodoRelatorio.Text = Relatorio.TextoPeriodo(relatorio);
            TxtDetalheRelatorio.Text = Relatorio.Texto(relatorio);
            BtnVerRelatorio.IsEnabled = BtnExportarRelatorio.IsEnabled = true;
        }
        catch { if (versao == _versaoRelatorio) TxtResumoRelatorio.Text = "Não consegui montar o relatório agora. Abra o Histórico novamente para tentar."; }
    }

    private async void PeriodoRelatorio_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (TxtResumoRelatorio != null) await CarregarRelatorio();
    }

    private void VerRelatorio_Click(object sender, RoutedEventArgs e)
    {
        var mostrar = DetalheRelatorio.Visibility != Visibility.Visible;
        DetalheRelatorio.Visibility = mostrar ? Visibility.Visible : Visibility.Collapsed;
        BtnVerRelatorio.Content = mostrar ? "Ocultar relatório" : "Ver relatório";
    }

    private async void ExportarRelatorio_Click(object sender, RoutedEventArgs e)
    {
        if (_relatorio is not { } relatorio) return;
        var dialogo = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Exportar relatório do Sentinela", Filter = "Relatório HTML (*.html;*.htm)|*.html;*.htm",
            DefaultExt = ".html", AddExtension = true,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            FileName = $"Relatorio-Sentinela-{DateTime.Now:yyyy-MM-dd}.html"
        };
        if (dialogo.ShowDialog(Window.GetWindow(this)) != true) return;
        BtnExportarRelatorio.IsEnabled = false;
        try
        {
            var resultado = await Task.Run(() =>
            {
                var salvo = ArquivosSeguros.GravarExportacao(dialogo.FileName, Relatorio.GerarHtml(relatorio), out var motivo);
                return (Salvo: salvo, Motivo: motivo);
            });
            if (!resultado.Salvo)
            {
                MessageBox.Show(Window.GetWindow(this), resultado.Motivo, "Exportar relatório", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (MessageBox.Show(Window.GetWindow(this), "Relatório salvo. Abrir a pasta do arquivo?", "Exportar relatório", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
            {
                var processo = new System.Diagnostics.ProcessStartInfo(CaminhosSistema.Explorer) { UseShellExecute = true };
                processo.ArgumentList.Add("/select,");
                processo.ArgumentList.Add(dialogo.FileName);
                System.Diagnostics.Process.Start(processo);
            }
        }
        catch { MessageBox.Show(Window.GetWindow(this), "Não consegui abrir a pasta do relatório.", "Sentinela"); }
        finally { BtnExportarRelatorio.IsEnabled = true; }
    }

    private static string TituloDiario(string tipo) => tipo switch
    {
        "ameaca" => "Ameaça", "tratar" => "Tratamento de ameaças", "alerta" => "Alerta", "varredura" => "Varredura",
        "assinaturas" => "Atualização de assinaturas", "ajuste" => "Ajuste do Sentinela",
        "seguranca" => "Verificação de segurança", "checkup" => "Checkup de desempenho", "processo" => "Processo", "inicializacao" => "Inicialização",
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

    internal static bool SelecionaFiltro(string filtro, string tipo) => filtro switch
        {
            "ameacas" => tipo is "Ameaca" or "AmeacaTratada" or "ameaca" or "tratar",
            "varreduras" => tipo is "Varredura" or "varredura",
            "protecao" => tipo is "Protecao" or "Configuracao" or "Assinaturas" or "assinaturas" or "ajuste" or "alerta" or "status" or "seguranca",
            "desempenho" => tipo is "checkup" or "processo" or "inicializacao" or "limpeza",
            "limpeza" => tipo == "limpeza",
            _ => true,
        };

    private void AplicarFiltro()
    {
        var filtrados = _itens.Where(e => SelecionaFiltro(_filtro, e.Tipo)).ToList();
        GradeHistorico.ItemsSource = filtrados;
        TxtResumo.Text = $"{filtrados.Count} eventos nos últimos 30 dias";
        TxtEstadoVazio.Text = "Nenhum evento acessível nos últimos 30 dias.";
        GradeHistorico.Visibility = filtrados.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        EstadoVazio.Visibility = filtrados.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
