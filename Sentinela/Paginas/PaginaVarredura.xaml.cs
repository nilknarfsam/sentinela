using System.Windows;
using System.Windows.Controls;
using Sentinela.Servicos;
using static Sentinela.Servicos.Formatos;
using System.Windows.Threading;

namespace Sentinela.Paginas;

public partial class PaginaVarredura : UserControl
{
    private readonly DispatcherTimer _timerTempo = new() { Interval = TimeSpan.FromSeconds(1) };
    private CancellationTokenSource? _ctsVarredura;
    private DateTime _inicioVarredura;

    public event Action? VarreduraTerminou;
    public bool EmAndamento => _ctsVarredura != null;

    private App App => (App)Application.Current;

    public PaginaVarredura()
    {
        InitializeComponent();
        _timerTempo.Tick += (_, _) => AtualizarTempoVarredura();
    }

    private void VarreduraRapida_Click(object sender, RoutedEventArgs e) => Iniciar(TipoVarredura.Rapida);
    private void VarreduraCompleta_Click(object sender, RoutedEventArgs e) => Iniciar(TipoVarredura.Completa);
    private void CancelarVarredura_Click(object sender, RoutedEventArgs e)
    {
        BtnCancelar.IsEnabled = false;
        TxtAndamento.Text = "Cancelando...";
        _ctsVarredura?.Cancel();
    }

    public async void Iniciar(TipoVarredura tipo)
    {
        if (_ctsVarredura != null) return; // já tem uma rodando

        var nome = tipo == TipoVarredura.Rapida ? "Varredura rápida" : "Varredura completa";
        _ctsVarredura = new CancellationTokenSource();
        _inicioVarredura = DateTime.Now;
        BtnRapida.IsEnabled = BtnCompleta.IsEnabled = false;
        BtnCancelar.IsEnabled = true;
        CartaoAndamento.Visibility = Visibility.Visible;
        BarraVarredura.IsIndeterminate = true;
        TxtAndamento.Text = $"{nome} em andamento...";
        LogVarredura.Text = $"[{DateTime.Now:HH:mm:ss}] {nome} iniciada.";
        AtualizarTempoVarredura();
        _timerTempo.Start();
        App.AtualizarAndamentoVarredura($"Sentinela: {nome.ToLower()} em andamento");

        string resumo;
        bool achou = false;
        bool encontrouAmeaca = false;
        bool limpa = false;
        try
        {
            var (codigo, saida) = await Defender.Varrer(tipo, _ctsVarredura.Token);
            var duracao = DateTime.Now - _inicioVarredura;
            // MpCmdRun devolve 2 tanto para "achou ameaça" quanto para erro: o texto decide.
            bool falhou = saida.Contains("[Failed]", StringComparison.OrdinalIgnoreCase)
                       || saida.Contains("Failed with hr", StringComparison.OrdinalIgnoreCase);
            achou = !falhou && codigo != 0;
            limpa = !falhou && codigo == 0;
            encontrouAmeaca = achou;
            resumo = falhou
                ? $"{nome} não rodou: o Defender recusou o pedido. Tente abrir o Sentinela como administrador."
                : codigo == 0
                    ? $"{nome} terminada em {Duracao(duracao)}. Nenhuma ameaça encontrada."
                    : $"{nome} terminada em {Duracao(duracao)}. O Defender ENCONTROU ameaças. Veja em Ameaças e quarentena.";
            if (falhou) achou = true; // mostra o aviso da bandeja como alerta
            LogVarredura.Text += Environment.NewLine + Environment.NewLine + saida.Trim() + Environment.NewLine + Environment.NewLine + resumo;
        }
        catch (OperationCanceledException)
        {
            resumo = $"{nome} cancelada.";
            LogVarredura.Text += Environment.NewLine + resumo;
        }
        catch (Exception ex)
        {
            resumo = "Não foi possível fazer a varredura: " + ex.Message;
            LogVarredura.Text += Environment.NewLine + resumo;
        }

        _timerTempo.Stop();
        _ctsVarredura.Dispose();
        _ctsVarredura = null;
        BtnRapida.IsEnabled = BtnCompleta.IsEnabled = true;
        BarraVarredura.IsIndeterminate = false;
        BarraVarredura.Value = 100;
        TxtAndamento.Text = resumo;
        Diario.Registrar("varredura", resumo);
        if (encontrouAmeaca) Diario.Registrar("ameaca", resumo);
        TxtTempo.Text = "";
        BtnCancelar.IsEnabled = false;
        App.Avisar("Sentinela", resumo, achou, rotina: limpa);
        VarreduraTerminou?.Invoke();
    }

    private void AtualizarTempoVarredura()
        => TxtTempo.Text = "Tempo: " + Duracao(DateTime.Now - _inicioVarredura);

    public void Cancelar() => _ctsVarredura?.Cancel();

    public void MostrarUltimas(StatusDefender? s)
    {
        if (s == null) return;
        TxtUltimaRapida.Text = s.UltimaVarreduraRapida is { } r ? $"Última: {Quando(r)}" : "Nunca feita";
        TxtUltimaCompleta.Text = s.UltimaVarreduraCompleta is { } f ? $"Última: {Quando(f)}" : "Nunca feita";
    }
}
