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
    public DateTime UltimoFimUtc { get; private set; } = DateTime.MinValue;

    private App App => (App)Application.Current;

    public PaginaVarredura()
    {
        InitializeComponent();
        if (Sistema.EhAdmin())
        {
            AllowDrop = false;
            TxtPersonalizada.Text = "Escolha uma pasta ou arquivo. No Explorer, use o clique direito > Verificar com o Sentinela.";
        }
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
