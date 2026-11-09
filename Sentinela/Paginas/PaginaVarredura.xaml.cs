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
    private TimeSpan? _estimativa;
    private readonly bool _admin = Sistema.EhAdmin();
    private bool _offlineSolicitada;

    public event Action? VarreduraTerminou;
    public bool EmAndamento => _ctsVarredura != null;
    public DateTime UltimoFimUtc { get; private set; } = DateTime.MinValue;

    private App App => (App)Application.Current;

    public PaginaVarredura()
    {
        InitializeComponent();
        BtnOffline.IsEnabled = _admin;
        TxtOfflineAdmin.Visibility = _admin ? Visibility.Collapsed : Visibility.Visible;
        if (Sistema.EhAdmin())
        {
            AllowDrop = false;
            TxtPersonalizada.Text = "Escolha uma pasta ou arquivo. No Explorer, use o clique direito > Verificar com o Sentinela.";
        }
        _timerTempo.Tick += (_, _) => AtualizarTempoVarredura();
    }

    private void VarreduraRapida_Click(object sender, RoutedEventArgs e) => Iniciar(TipoVarredura.Rapida);
    private void VarreduraCompleta_Click(object sender, RoutedEventArgs e) => Iniciar(TipoVarredura.Completa);
    private async void VarreduraOffline_Click(object sender, RoutedEventArgs e)
    {
        if (!_admin || EmAndamento || _offlineSolicitada) return;
        if (MessageBox.Show(Window.GetWindow(this), "O PC vai reiniciar em instantes para verificar antes do Windows carregar. Leva uns 15 minutos. Salve seus trabalhos antes. Continuar?",
                "Varredura offline", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        if (MessageBox.Show(Window.GetWindow(this), "Salvou tudo? O PC vai reiniciar agora.",
                "Varredura offline", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        _offlineSolicitada = true;
        BtnOffline.IsEnabled = false;
        Diario.Registrar("offline", "Varredura offline solicitada. O PC vai reiniciar.");
        try
        {
            await Defender.IniciarVarreduraOffline();
            LogVarredura.Text = "Varredura offline solicitada ao Defender. Aguardando reinício.";
        }
        catch (Exception ex)
        {
            LogVarredura.Text = "Não foi possível iniciar a varredura offline: " + ex.Message;
            Diario.Registrar("offline", "Varredura offline não iniciada: " + ex.Message);
            _offlineSolicitada = false;
            BtnOffline.IsEnabled = _admin && !EmAndamento;
        }
    }
    private void CancelarVarredura_Click(object sender, RoutedEventArgs e)
    {
        BtnCancelar.IsEnabled = false;
        TxtAndamento.Text = "Cancelando...";
        _ctsVarredura?.Cancel();
    }

    private void EscolherPasta_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new Microsoft.Win32.OpenFolderDialog();
        if (dialogo.ShowDialog() == true) Iniciar(TipoVarredura.Personalizada, dialogo.FolderName);
    }

    private void EscolherArquivo_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new Microsoft.Win32.OpenFileDialog();
        if (dialogo.ShowDialog() == true) Iniciar(TipoVarredura.Personalizada, dialogo.FileName);
    }

    private void Pagina_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Pagina_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] itens) return;
        if (itens.Length > 1)
            TxtPersonalizada.Text = "Solte um item por vez.";
        else if (itens.Length == 1) Iniciar(TipoVarredura.Personalizada, itens[0]);
    }

    public void AvisarEmAndamento()
    {
        const string mensagem = "Já há uma verificação em andamento. Espere terminar.";
        TxtPersonalizada.Text = mensagem;
        App.Avisar("Sentinela", mensagem);
    }

    public async void Iniciar(TipoVarredura tipo, string? caminho = null)
    {
        if (_ctsVarredura != null) { AvisarEmAndamento(); return; }

        // Reserve a varredura antes do await para impedir duas partidas simultâneas.
        _ctsVarredura = new CancellationTokenSource();
        if (tipo == TipoVarredura.Personalizada)
        {
            bool existe;
            try
            {
                existe = caminho != null && Pedidos.CaminhoLocalValido(caminho)
                    && Path.IsPathFullyQualified(caminho)
                    && await Task.Run(() => Pedidos.UnidadePermitida(caminho)
                        && (File.Exists(caminho) || Directory.Exists(caminho)))
                        .WaitAsync(TimeSpan.FromSeconds(3));
            }
            catch { existe = false; }
            if (!existe)
            {
                _ctsVarredura.Dispose();
                _ctsVarredura = null;
                TxtPersonalizada.Text = "Escolha uma pasta ou arquivo existente em uma unidade local.";
                return;
            }
        }
        var nome = tipo == TipoVarredura.Personalizada
            ? "Verificação de " + (Path.GetFileName(Path.TrimEndingDirectorySeparator(caminho!)) is { Length: > 0 } nomeItem ? nomeItem : caminho)
            : tipo == TipoVarredura.Rapida ? "Varredura rápida" : "Varredura completa";
        _inicioVarredura = DateTime.Now;
        _estimativa = null;
        BarraVarredura.Value = 0;
        BtnOffline.IsEnabled = false;
        BtnRapida.IsEnabled = BtnCompleta.IsEnabled = BtnPasta.IsEnabled = BtnArquivo.IsEnabled = false;
        BtnCancelar.IsEnabled = true;
        CartaoAndamento.Visibility = Visibility.Visible;
        BarraVarredura.IsIndeterminate = true;
        TxtAndamento.Text = $"{nome} em andamento...";
        TxtCaminho.Text = tipo == TipoVarredura.Personalizada ? "Verificando: " + caminho : "";
        TxtCaminho.Visibility = tipo == TipoVarredura.Personalizada ? Visibility.Visible : Visibility.Collapsed;
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
            if (tipo is TipoVarredura.Rapida or TipoVarredura.Completa)
            {
                try
                {
                    _estimativa = await Task.Run(() => EstimarDuracao(VigiaDefender.LerRecentes(30), tipo), _ctsVarredura.Token)
                        .WaitAsync(TimeSpan.FromSeconds(3), _ctsVarredura.Token);
                }
                catch (TimeoutException) { /* Começa sem estimativa se a leitura do histórico demorar. */ }
                BarraVarredura.IsIndeterminate = _estimativa == null;
                AtualizarTempoVarredura();
            }
            _ctsVarredura.Token.ThrowIfCancellationRequested();
            var (codigo, saida) = await Defender.Varrer(tipo, _ctsVarredura.Token, caminho);
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
            resumo = $"{nome}: não foi possível fazer a varredura: " + ex.Message;
            LogVarredura.Text += Environment.NewLine + resumo;
        }

        _timerTempo.Stop();
        _ctsVarredura.Dispose();
        UltimoFimUtc = DateTime.UtcNow;
        _ctsVarredura = null;
        BtnOffline.IsEnabled = _admin && !_offlineSolicitada;
        BtnRapida.IsEnabled = BtnCompleta.IsEnabled = BtnPasta.IsEnabled = BtnArquivo.IsEnabled = true;
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

    internal static TimeSpan? EstimarDuracao(IEnumerable<EventoDefender> eventos, TipoVarredura tipo)
    {
        if (tipo is not (TipoVarredura.Rapida or TipoVarredura.Completa)) return null;
        var duracoes = eventos.Where(e => e.Id == 1001 && e.Tipo == TipoEvento.Varredura
                && e.Titulo == "Varredura terminada" && e.Duracao is { Ticks: > 0 }
                && (tipo == TipoVarredura.Rapida
                    ? e.Detalhe.Contains("Rápida", StringComparison.OrdinalIgnoreCase) || e.Detalhe.Contains("Quick", StringComparison.OrdinalIgnoreCase)
                    : e.Detalhe.Contains("Completa", StringComparison.OrdinalIgnoreCase) || e.Detalhe.Contains("Full", StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(e => e.Data).Take(3).Select(e => e.Duracao!.Value.Ticks).ToList();
        return duracoes.Count == 0 ? null : TimeSpan.FromTicks((long)duracoes.Average(t => (double)t));
    }

    private void AtualizarTempoVarredura()
    {
        var decorrido = DateTime.Now - _inicioVarredura;
        TxtTempo.Text = "Tempo: " + Duracao(decorrido);
        if (_estimativa is { } estimativa)
        {
            TxtTempo.Text += " de ~" + Duracao(estimativa) + " (estimado)";
            BarraVarredura.Value = Math.Clamp(decorrido.TotalSeconds / estimativa.TotalSeconds * 100, 0, 95);
        }
    }

    public void Cancelar() => _ctsVarredura?.Cancel();

    public void MostrarUltimas(StatusDefender? s)
    {
        if (s == null) return;
        TxtUltimaRapida.Text = s.UltimaVarreduraRapida is { } r ? $"Última: {Quando(r)}" : "Nunca feita";
        TxtUltimaCompleta.Text = s.UltimaVarreduraCompleta is { } f ? $"Última: {Quando(f)}" : "Nunca feita";
    }
}
