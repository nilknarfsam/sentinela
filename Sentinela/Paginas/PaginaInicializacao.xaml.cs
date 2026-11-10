using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Sentinela.Servicos;

namespace Sentinela.Paginas;

public partial class PaginaInicializacao : UserControl
{
    public bool Carregada { get; private set; }
    private string _filtro = "programas";
    private Task? _carregamento;

    public PaginaInicializacao() => InitializeComponent();

    public Task Carregar() => _carregamento is { IsCompleted: false } ? _carregamento : _carregamento = CarregarLista();

    private async Task CarregarLista()
    {
        Carregada = true;
        BotaoAtualizar.IsEnabled = Filtros.IsEnabled = GradeInicializacao.IsEnabled = false;
        TxtMensagem.Text = "Lendo inicialização e assinaturas...";
        try
        {
            var programas = await Task.Run(() => Inicializacao.Listar(_filtro == "programas"));
            TxtResumoInicializacao.Text = $"{programas.Count(p => p.Ativo)} programa(s) abrem junto com o Windows, {programas.Count(p => !p.Ativo)} desativado(s).";
            ColunaAtivo.Visibility = _filtro == "programas" ? Visibility.Visible : Visibility.Collapsed;
            BotaoConsole.Visibility = _filtro == "programas" ? Visibility.Collapsed : Visibility.Visible;
            BotaoConsole.Content = _filtro == "tarefas" ? "Abrir o Agendador de Tarefas" : "Abrir Serviços";
            if (_filtro == "programas") { GradeInicializacao.ItemsSource = programas; TxtMensagem.Text = ""; }
            else
            {
                var lista = await Task.Run(() => _filtro == "tarefas" ? InicializacaoSistema.Tarefas() : InicializacaoSistema.Servicos());
                GradeInicializacao.ItemsSource = lista;
                TxtMensagem.Text = $"{lista.Count} item(ns) de terceiros. Somente leitura; use o console do Windows para gerenciar.";
            }
        }
        catch { TxtMensagem.Text = "Não foi possível carregar a inicialização. Tente atualizar."; }
        finally { BotaoAtualizar.IsEnabled = Filtros.IsEnabled = GradeInicializacao.IsEnabled = true; }
    }

    private async void Filtrar_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button botao || botao.Tag is not string filtro) return;
        _filtro = filtro;
        foreach (var b in Filtros.Children.OfType<Button>()) b.Style = (Style)FindResource(b == botao ? "BotaoPrincipal" : "BotaoBase");
        await Carregar();
    }

    private async void AtualizarInicializacao_Click(object sender, RoutedEventArgs e) => await Carregar();

    private async void Ativo_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox caixa || caixa.DataContext is not ProgramaInicial programa) return;
        var ligar = caixa.IsChecked == true;
        caixa.IsChecked = programa.Ativo;
        if (!ligar && programa.TodosOsUsuarios && MessageBox.Show($"Desligar {programa.Nome} para todos os usuários?",
                "Sentinela", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        GradeInicializacao.IsEnabled = Filtros.IsEnabled = BotaoAtualizar.IsEnabled = false;
        var resultado = await Task.Run(() => Inicializacao.Definir(programa, ligar));
        await Carregar();
        TxtMensagem.Text = resultado.mensagem;
    }

    private async void AbrirConsole_Click(object sender, RoutedEventArgs e)
    {
        var caminho = _filtro == "tarefas" ? CaminhosSistema.AgendadorTarefas : CaminhosSistema.Servicos;
        var ok = await Task.Run(() =>
        {
            try { Process.Start(new ProcessStartInfo(CaminhosSistema.Mmc, $"\"{caminho}\"") { UseShellExecute = true }); return true; }
            catch { return false; }
        });
        if (!ok) TxtMensagem.Text = "Não foi possível abrir o console do Windows.";
    }
}
