using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Sentinela.Servicos;
using Sentinela.Apresentacao;
using Sentinela.Servicos.Checkup;
using static Sentinela.Servicos.Formatos;

namespace Sentinela.Paginas;

public partial class PaginaDesempenho : UserControl
{
    private ResultadoCheckup? _ultimo;
    private Task? _execucao;
    private Task? _leituraDiscos;

    private readonly HistoricoDesempenho _historico;
    private IReadOnlyList<InfoProcesso> _processos = Array.Empty<InfoProcesso>();
    private Task? _amostra;
    private CancellationTokenSource? _cancelamentoAmostra;
    private bool _porMemoria;

    public PaginaDesempenho(HistoricoDesempenho historico)
    {
        _historico = historico;
        InitializeComponent();
    }

    public void CancelarAmostra() => _cancelamentoAmostra?.Cancel();

    public void AtualizarGrafico()
    {
        var pontos = _historico.Pontos;
        if (pontos.LastOrDefault() is { } atual)
        {
            TxtCpuAtual.Text = $"{atual.CpuPercent:0}%";
            TxtMemoriaAtual.Text = $"{atual.MemoriaPercent:0}%";
            BarraCpuAtual.Value = atual.CpuPercent;
            BarraMemoriaAtual.Value = atual.MemoriaPercent;
        }
        else
        {
            TxtCpuAtual.Text = TxtMemoriaAtual.Text = "Aguardando leitura";
            BarraCpuAtual.Value = BarraMemoriaAtual.Value = 0;
        }
        var agora = DateTime.Now;
        LinhaCpu.Data = CriarLinha(pontos, agora, p => p.CpuPercent);
        LinhaMemoria.Data = CriarLinha(pontos, agora, p => p.MemoriaPercent);
        TxtHistorico.Text = $"{pontos.Count} amostras · a cada 5 s";
    }

    private StreamGeometry CriarLinha(IReadOnlyList<PontoDesempenho> pontos, DateTime agora, Func<PontoDesempenho, double> valor)
    {
        var geometria = new StreamGeometry();
        using (var contexto = geometria.Open())
        {
            for (var i = 0; i < pontos.Count; i++)
            {
                var ponto = pontos[i];
                var x = Grafico.ActualWidth * Math.Clamp(1 - (agora - ponto.Instante).TotalHours, 0, 1);
                var posicao = new Point(x, Grafico.ActualHeight * (1 - valor(ponto) / 100));
                if (i == 0 || ponto.IniciaSegmento) contexto.BeginFigure(posicao, false, false);
                else contexto.LineTo(posicao, true, false);
            }
        }
        geometria.Freeze();
        return geometria;
    }

    private void Grafico_SizeChanged(object sender, SizeChangedEventArgs e) => AtualizarGrafico();
    public Task Amostrar()
    {
        if (_amostra == null) _amostra = LerProcessos();
        return _amostra;
    }

    private async Task LerProcessos()
    {
        await Task.Yield();
        using var cancelamento = new CancellationTokenSource();
        _cancelamentoAmostra = cancelamento;
        BtnAmostrar.IsEnabled = false;
        ProgressoAmostra.Visibility = Visibility.Visible;
        TxtProcessos.Text = "Amostrando por 2 segundos…";
        try
        {
            var processos = await Processos.Amostrar(TimeSpan.FromSeconds(2), cancelamento.Token);
            if (cancelamento.IsCancellationRequested) { TxtProcessos.Text = "Amostra cancelada com a janela oculta."; return; }
            _processos = processos;
            MostrarProcessos();
            TxtProcessos.Text = processos.Count == 0 ? "Não consegui ler os processos. Tente novamente." : $"Top 10 · {DateTime.Now:HH:mm:ss} · memória privada quando disponível";
        }
        catch (Exception ex) { TxtProcessos.Text = $"Não consegui amostrar: {ex.Message}"; }
        finally
        {
            ProgressoAmostra.Visibility = Visibility.Collapsed;
            BtnAmostrar.IsEnabled = true;
            _cancelamentoAmostra = null;
            _amostra = null;
        }
    }

    private void MostrarProcessos()
    {
        var grupos = Processos.Agrupar(_processos);
        ListaProcessos.ItemsSource = (_porMemoria ? grupos.OrderByDescending(g => g.MemoriaBytes).ThenByDescending(g => g.CpuPercent)
            : grupos.OrderByDescending(g => g.CpuPercent).ThenByDescending(g => g.MemoriaBytes)).Take(10).ToArray();
        BtnCpu.Content = _porMemoria ? "CPU" : "CPU ↓";
        BtnMemoria.Content = _porMemoria ? "Memória ↓" : "Memória";
    }

    private async void Amostrar_Click(object sender, RoutedEventArgs e) => await Amostrar();
    private void OrdenarCpu_Click(object sender, RoutedEventArgs e) { _porMemoria = false; MostrarProcessos(); }
    private void OrdenarMemoria_Click(object sender, RoutedEventArgs e) { _porMemoria = true; MostrarProcessos(); }

    private async void Finalizar_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: GrupoProcessos grupo } || !grupo.PodeFinalizar) return;
        if (MessageBox.Show(Window.GetWindow(this), $"Finalizar {grupo.Nome}? Trabalho não salvo nesse programa será perdido.",
            "Sentinela", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        grupo.Finalizando = true;
        try
        {
            var resultados = await Task.Run(() => grupo.Processos.Select(p => Processos.Finalizar(p.Pid, p.Nome, p.Inicio)).ToArray());
            // Aguarda o descarte da leitura anterior antes de iniciar uma posterior ao Kill.
            var anterior = _amostra;
            CancelarAmostra();
            if (anterior != null) await anterior;
            await Amostrar();
            TxtProcessos.Text = string.Join(" ", resultados.Select(r => r.mensagem).Distinct());
        }
        catch (Exception ex) { TxtProcessos.Text = $"Não consegui finalizar: {ex.Message}"; }
        finally { grupo.Finalizando = false; }
    }

    // A página é mantida pela janela; navegar nunca dispara uma nova leitura.
    public void Carregar()
    {
        _ultimo ??= Checkup.Ultimo;
        if (_ultimo != null) Mostrar(_ultimo);
        AtualizarGrafico();
        _leituraDiscos ??= CarregarDiscos();
    }

    private async Task CarregarDiscos()
    {
        try
        {
            TxtDiscos.Text = "Lendo discos físicos...";
            var discos = await SaudeDisco.Ler();
            ListaDiscos.ItemsSource = discos.Select(d => new CartaoDisco(d)).ToArray();
            TxtDiscos.Text = discos.Count == 0 ? "Não consegui ler os discos agora." :
                Sistema.EhAdmin() && discos.Any(d => !d.DadosAvancados) ? "Alguns discos não disponibilizaram os contadores avançados." : "";
            AvisoAdminDiscos.Visibility = Sistema.EhAdmin() ? Visibility.Collapsed : Visibility.Visible;
        }
        catch (Exception ex) { TxtDiscos.Text = $"Não consegui ler os discos: {ex.Message}"; }
    }

    private void AdminDiscos_Click(object sender, RoutedEventArgs e) => (Window.GetWindow(this) as MainWindow)?.AbrirComoAdmin();

    public Task Verificar() => _execucao ??= Executar();

    private async Task Executar()
    {
        BtnVerificar.IsEnabled = false;
        BtnVerificar.Content = "Verificando...";
        try
        {
            _ultimo = await Checkup.Executar();
            Mostrar(_ultimo);
            if (_leituraDiscos != null) await _leituraDiscos;
            _leituraDiscos = CarregarDiscos();
            await _leituraDiscos;
        }
        finally
        {
            BtnVerificar.IsEnabled = true;
            BtnVerificar.Content = "Verificar o PC";
            _execucao = null;
        }
    }

    private void Mostrar(ResultadoCheckup resultado)
    {
        Convite.Visibility = Visibility.Collapsed;
        CartaoNota.Visibility = Visibility.Visible;
        var cor = resultado.Nota >= 85 ? "Verde" : resultado.Nota >= 60 ? "Ambar" : "Vermelho";
        TxtNota.Text = resultado.Nota.ToString();
        var corApresentacao = EstadoApresentacao.Cor(resultado, cor);
        TxtNota.Foreground = Cor(corApresentacao);
        CartaoNota.Background = Cor(corApresentacao + "Fundo");
        var tituloCompleto = resultado.Nota >= 85 ? "Seu PC está em ótima forma" : resultado.Nota >= 60 ? "Dá para melhorar algumas coisas" : "Seu PC precisa de atenção";
        TxtFaixa.Text = EstadoApresentacao.Titulo(resultado, tituloCompleto);
        TxtConfianca.Text = EstadoApresentacao.Mensagem(resultado, "o bom estado geral do PC");
        TxtQuando.Text = $"Verificado às {resultado.Data:HH:mm} em {resultado.Duracao.TotalSeconds:0.0} s";
        ListaVerificacoes.ItemsSource = resultado.Itens.OrderByDescending(i => i.Nivel).Select(i => new CartaoVerificacao(i)).ToArray();
    }

    private async void Verificar_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            TxtErro.Text = "";
            await Verificar();
        }
        catch (Exception ex) { MostrarErro(ex); }
    }

    private async void Resolver_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            TxtErro.Text = "";
            if (sender is not Button { Tag: AcaoResolver acao }) return;
            if (acao.Tipo == "pagina" && acao.Alvo != null)
                (Window.GetWindow(this) as MainWindow)?.IrPara(acao.Alvo);
            else if (acao.Tipo == "rolar" && acao.Alvo == "agora")
            {
                SecaoAgora.BringIntoView();
                await Amostrar();
            }
            else if (acao.Tipo == "rolar" && acao.Alvo == "discos") SecaoDiscos.BringIntoView();
            else if (acao.Tipo == "abrir" && AlvoPermitido(acao.Alvo))
            {
                // Abrir somente atalhos fixos de configuração; nunca interpretar comandos recebidos.
                await Task.Run(() =>
                {
                    var inicio = acao.Alvo switch
                    {
                        "devmgmt.msc" => new ProcessStartInfo(CaminhosSistema.Mmc, $"\"{CaminhosSistema.GerenciadorDispositivos}\""),
                        "powercfg.cpl" => new ProcessStartInfo(CaminhosSistema.Control, $"\"{CaminhosSistema.OpcoesEnergia}\""),
                        _ => new ProcessStartInfo("ms-settings:windowsupdate")
                    };
                    inicio.UseShellExecute = true;
                    Process.Start(inicio);
                });
            }
        }
        catch (Exception ex) { MostrarErro(ex); }
    }

    private void MostrarErro(Exception ex)
    {
        Diario.Registrar("erro", $"Checkup: {ex.Message}");
        TxtErro.Text = $"Não consegui concluir a verificação: {ex.Message}";
    }

    internal static bool AlvoPermitido(string? alvo) => alvo is "ms-settings:windowsupdate" or "devmgmt.msc" or "powercfg.cpl";

    public sealed class CartaoVerificacao(ResultadoVerificacao item)
    {
        public ResultadoVerificacao Item { get; } = item;
        public Brush CorNivel => Cor(Item.Nivel == NivelVerificacao.Problema ? "Vermelho" : Item.Nivel == NivelVerificacao.Atencao ? "Ambar" : "Verde");
        public Visibility VisibilidadeAcao => Item.Acao == null ? Visibility.Collapsed : Visibility.Visible;
    }

    public sealed class CartaoDisco(DiscoFisico disco)
    {
        public DiscoFisico Disco { get; } = disco;
        public Brush CorNivel => Cor(SaudeDisco.Avaliar(Disco).nivel switch { NivelVerificacao.Problema => "Vermelho", NivelVerificacao.Atencao => "Ambar", _ => "Verde" });
        public string Resumo => $"{Disco.Tipo} · {Limpeza.Formatar(Disco.TamanhoBytes)} · {SaudeDisco.EstadoTexto(Disco)}";
        public string DetalheOperacional => Disco.Operacional == "Funcionando" ? "" : $"({Disco.Operacional})";
        public Visibility Avancados => Disco.DadosAvancados ? Visibility.Visible : Visibility.Collapsed;
        public string DesgasteTexto => $"Desgaste: {Disco.DesgastePercent?.ToString() ?? "indisponível"}" + (Disco.DesgastePercent.HasValue ? "%" : "");
        public string TemperaturaTexto => $"Temperatura: {Disco.Temperatura?.ToString() ?? "—"} °C · máxima: {Disco.TemperaturaMax?.ToString() ?? "—"} °C";
        public string ContadoresTexto => $"Ligado: {(Disco.HorasLigado.HasValue ? $"{Disco.HorasLigado.Value / 24.0:0.#} dias" : "—")} · erros de leitura: {Disco.ErrosLeitura?.ToString() ?? "—"} · escrita: {Disco.ErrosEscrita?.ToString() ?? "—"}";
    }
}
