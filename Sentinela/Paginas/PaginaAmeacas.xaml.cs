using System.Windows;
using System.Windows.Controls;
using Sentinela.Servicos;
using static Sentinela.Servicos.Formatos;

namespace Sentinela.Paginas;

public partial class PaginaAmeacas : UserControl
{
    private readonly bool _admin = Sistema.EhAdmin();
    private Task? _carregamento;
    public bool Carregada { get; set; }

    private App App => (App)Application.Current;

    public PaginaAmeacas()
    {
        InitializeComponent();
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
        try
        {
            var lista = await Task.Run(Defender.LerAmeacas);
            GradeAmeacas.ItemsSource = lista;
            int ativas = lista.Count(a => a.Ativa);
            TxtResumoAmeacas.Text = lista.Count == 0
                ? (_admin ? "Nenhuma ameaça no histórico do Defender." : "Nada encontrado, mas sem administrador o histórico pode vir incompleto.")
                : $"{lista.Count} no histórico, {ativas} ativa(s) agora.";
            BtnTratar.IsEnabled = ativas > 0;
        }
        catch (Exception ex)
        {
            GradeAmeacas.ItemsSource = null;
            BtnTratar.IsEnabled = false;
            TxtResumoAmeacas.Text = _admin ? "Não consegui ler: " + ex.Message : "Abra como administrador para ver o histórico de ameaças.";
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
}
