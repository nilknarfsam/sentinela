using System.Windows;
using System.Windows.Controls;
using Sentinela.Servicos;
using static Sentinela.Servicos.Formatos;

namespace Sentinela.Paginas;

public partial class PaginaAmeacas : UserControl
{
    private readonly bool _admin = Sistema.EhAdmin();
    private Task? _carregamento;
    private bool _restaurando;
    public bool Carregada { get; set; }

    private App App => (App)Application.Current;

    public PaginaAmeacas()
    {
        InitializeComponent();
        GradeQuarentena.IsEnabled = BtnExcluir.IsEnabled = TextoDefender.IsEnabled = _admin;
        if (!_admin) TxtResultadoQuarentena.Text = "Abra como administrador para ver a quarentena.";
    }

    public Task Carregar() => _carregamento ??= CarregarLista();

    private async Task CarregarLista()
    {
        await Task.Yield();
        try
        {
            do
            {
                await LerLista();
            } while (!Carregada && IsVisible);
        }
        finally { _carregamento = null; }
    }

    private async Task LerLista()
    {
        Carregada = true;
        TxtResumoAmeacas.Text = "Carregando...";
        TxtQuarentena.Text = "Carregando...";
        GradeQuarentena.ItemsSource = null;
        try
        {
            var lista = await Task.Run(Defender.LerAmeacas);
            GradeAmeacas.ItemsSource = lista;
            if (_admin)
                GradeQuarentena.ItemsSource = lista.Where(a => a.Situacao == "Em quarentena")
                    .SelectMany(a => a.Local.Split("  |  ", StringSplitOptions.None).Select(local => a with { Local = local })).ToList();
            int ativas = lista.Count(a => a.Ativa);
            TxtResumoAmeacas.Text = lista.Count == 0
                ? (_admin ? "Nenhuma ameaça encontrada no histórico acessível do Defender." : "Lista vazia; o histórico pode estar incompleto sem administrador.")
                : $"{lista.Count} no histórico, {ativas} ativa(s) agora.";
            AvisoConsulta.Visibility = _admin ? Visibility.Collapsed : Visibility.Visible;
            if (!_admin) TxtAvisoConsulta.Text = "Não foi possível confirmar a ausência de ameaças: abra como administrador para consultar o histórico e a quarentena completos.";
            BtnTratar.IsEnabled = ativas > 0;
        }
        catch (Exception ex)
        {
            GradeAmeacas.ItemsSource = null;
            BtnTratar.IsEnabled = false;
            TxtResumoAmeacas.Text = _admin ? "Não consegui ler: " + ex.Message : "Abra como administrador para ver o histórico de ameaças.";
            AvisoConsulta.Visibility = Visibility.Visible;
            TxtAvisoConsulta.Text = _admin
                ? "Não foi possível carregar o histórico de ameaças. A tabela vazia não confirma ausência de ameaças. Tente Atualizar."
                : "A consulta de ameaças exige acesso de administrador. A lista vazia não confirma ausência de ameaças.";
        }

        try
        {
            TxtQuarentena.Text = _admin ? await Defender.ListarQuarentena() : "Abra como administrador para ver a quarentena.";
        }
        catch (Exception ex) { TxtQuarentena.Text = "Não consegui ler a quarentena: " + ex.Message; }
    }

    private async void AtualizarAmeacas_Click(object sender, RoutedEventArgs e) => await Carregar();

    private async void TratarAmeacas_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(Window.GetWindow(this), "Pedir ao Windows Defender para tratar (remover ou pôr em quarentena) as ameaças ativas?",
                "Tratar ameaças", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        BtnTratar.IsEnabled = false;
        var r = await Defender.RemoverAmeacasAtivas();
        Diario.Registrar("tratar", r);
        App.Avisar("Sentinela", r, rotina: r == "As ameaças ativas foram tratadas pelo Defender.");
        await Carregar();
    }

    private void AbrirSeguranca_Click(object sender, RoutedEventArgs e) => Tentar(Defender.AbrirSegurancaDoWindows);

    private void Quarentena_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => AtualizarRestaurar();

    private void AtualizarRestaurar()
    {
        var selecionada = GradeQuarentena.SelectedItem as Ameaca;
        var restauravel = selecionada != null && Pedidos.CaminhoLocalValido(selecionada.Local);
        BtnRestaurar.IsEnabled = _admin && !_restaurando && GradeQuarentena.SelectedItems.Count == 1 && restauravel;
        TxtSemArquivoRestauravel.Visibility = selecionada != null && !restauravel ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void Restaurar_Click(object sender, RoutedEventArgs e)
    {
        if (!_admin || _restaurando || GradeQuarentena.SelectedItem is not Ameaca ameaca
            || !Pedidos.CaminhoLocalValido(ameaca.Local)) return;
        if (MessageBox.Show(Window.GetWindow(this), "Restaurar devolve o arquivo ao lugar original. Só faça isso se tiver CERTEZA de que o arquivo é seguro (por exemplo, um falso alarme num programa seu). Continuar?",
                "Restaurar", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        if (MessageBox.Show(Window.GetWindow(this), $"Última confirmação: restaurar {ameaca.Nome} em {ameaca.Local}?",
                "Restaurar", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        _restaurando = true;
        BtnRestaurar.IsEnabled = false;
        try
        {
            string resultado;
            try
            {
                var (codigo, saida) = await Defender.Restaurar(ameaca.Nome, ameaca.Local);
                resultado = (codigo == 0 ? "" : "Falha ao restaurar: ") + $"Restauração de {ameaca.Nome} em {ameaca.Local}: código {codigo}. {saida.Trim()}";
            }
            catch (Exception ex) { resultado = "Não foi possível restaurar: " + ex.Message; }
            TxtResultadoQuarentena.Text = resultado;
            Diario.Registrar("restaurar", resultado);
            await Carregar();
        }
        finally
        {
            _restaurando = false;
            AtualizarRestaurar();
        }
    }
}
