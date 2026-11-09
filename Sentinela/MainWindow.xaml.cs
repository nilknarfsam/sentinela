using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Sentinela.Servicos;

namespace Sentinela;

public partial class MainWindow : Window
{
    private readonly bool _admin = Sistema.EhAdmin();
    private readonly DispatcherTimer _timerRapido = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly DispatcherTimer _timerDefender = new() { Interval = TimeSpan.FromSeconds(20) };
    private readonly DispatcherTimer _timerTempo = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly List<ItemLimpeza> _itensLimpeza = Limpeza.Itens();
    private CancellationTokenSource? _ctsVarredura;
    private DateTime _inicioVarredura;
    private bool _avisouBandeja;
    private bool _ameacasCarregadas, _inicializacaoCarregada;

    private App App => (App)Application.Current;

    public MainWindow()
    {
        InitializeComponent();

        FaixaAdmin.Visibility = _admin ? Visibility.Collapsed : Visibility.Visible;
        TxtAdminMenu.Text = _admin ? "Rodando como administrador" : "Rodando sem administrador";
        ListaLimpeza.ItemsSource = _itensLimpeza;

        _timerRapido.Tick += (_, _) => AtualizarSistema();
        _timerDefender.Tick += async (_, _) => await AtualizarDefender();
        _timerTempo.Tick += (_, _) => AtualizarTempoVarredura();

        Loaded += async (_, _) =>
        {
            Sistema.UsoCpu(); // primeira leitura serve de base
            AtualizarSistema();
            _timerRapido.Start();
            _timerDefender.Start();
            await AtualizarDefender();
            if (App.CapturaArquivo != null) await Capturar();
        };
    }

    // ================================================================ Navegação

    private void Menu_Checked(object sender, RoutedEventArgs e)
    {
        if (PaginaInicio == null) return; // ainda montando a janela
        PaginaInicio.Visibility = Vis(MenuInicio.IsChecked);
        PaginaVarredura.Visibility = Vis(MenuVarredura.IsChecked);
        PaginaAmeacas.Visibility = Vis(MenuAmeacas.IsChecked);
        PaginaLimpeza.Visibility = Vis(MenuLimpeza.IsChecked);
        PaginaInicializacao.Visibility = Vis(MenuInicializacao.IsChecked);

        if (MenuAmeacas.IsChecked == true && !_ameacasCarregadas) _ = CarregarAmeacas();
        if (MenuInicializacao.IsChecked == true && !_inicializacaoCarregada) CarregarInicializacao();
    }

    private static Visibility Vis(bool? b) => b == true ? Visibility.Visible : Visibility.Collapsed;

    // ================================================================ Início

    private void AtualizarSistema()
    {
        var cpu = Sistema.UsoCpu();
        BarraCpu.Value = cpu;
        TxtCpu.Text = $"{cpu:0}%";
        var (usado, total, pct) = Sistema.Memoria();
        BarraRam.Value = pct;
        TxtRam.Text = $"{usado:0.0} de {total:0} GB";
        TxtUptime.Text = Sistema.TempoLigado();
        BarraCpu.Foreground = Cor(cpu > 85 ? "Ambar" : "Destaque");
        BarraRam.Foreground = Cor(pct > 85 ? "Ambar" : "Destaque");
    }

    private int _contadorDiscos;
    private async Task AtualizarDefender()
    {
        if (_contadorDiscos++ % 3 == 0) ListaDiscos.ItemsSource = Sistema.Discos();

        StatusDefender? s = null;
        string? erro = null;
        try { s = await Task.Run(Defender.LerStatus); }
        catch (Exception ex) { erro = ex.Message; }

        GradeProtecao.Children.Clear();
        if (s == null)
        {
            DefinirStatus("Não consegui ler o Defender", erro ?? "O Windows Defender não respondeu.", "Vermelho", "");
            App.AtualizarIconeTexto("Sentinela: Defender não respondeu");
            return;
        }

        GradeProtecao.Children.Add(CartaoProtecao("", "Proteção em tempo real", s.TempoReal));
        GradeProtecao.Children.Add(CartaoProtecao("", "Análise de comportamento", s.Comportamento));
        GradeProtecao.Children.Add(CartaoProtecao("", "Verificação de downloads", s.Downloads));
        GradeProtecao.Children.Add(CartaoProtecao("", "Proteção contra adulteração", s.ProtecaoAdulteracao));
        GradeProtecao.Children.Add(CartaoInfo("", "Assinaturas de vírus",
            s.AssinaturasAtualizadas is { } d ? $"Atualizadas {Quando(d)}" : "Sem data",
            $"Versão {s.VersaoAssinaturas}", s.AssinaturasVelhas ? "Ambar" : "Verde"));
        var ultima = new[] { s.UltimaVarreduraRapida, s.UltimaVarreduraCompleta }.Where(x => x != null).Max();
        GradeProtecao.Children.Add(CartaoInfo("", "Última varredura",
            ultima is { } u ? Quando(u) : "Nenhuma ainda",
            s.UltimaVarreduraCompleta is { } c ? $"Completa: {Quando(c)}" : "Completa: nunca",
            ultima == null || (DateTime.Now - ultima.Value).TotalDays > 7 ? "Ambar" : "Verde"));

        TxtUltimaRapida.Text = s.UltimaVarreduraRapida is { } r ? $"Última: {Quando(r)}" : "Nunca feita";
        TxtUltimaCompleta.Text = s.UltimaVarreduraCompleta is { } f ? $"Última: {Quando(f)}" : "Nunca feita";

        if (!s.TudoLigado)
        {
            DefinirStatus("Atenção: alguma proteção está desligada",
                "Abra a Segurança do Windows para religar. O Sentinela não muda configurações de segurança.", "Vermelho", "");
            App.AtualizarIconeTexto("Sentinela: proteção desligada!");
        }
        else if (s.AssinaturasVelhas)
        {
            DefinirStatus("Protegido, mas as assinaturas estão velhas", "Clique em Atualizar assinaturas.", "Ambar", "");
            App.AtualizarIconeTexto("Sentinela: atualize as assinaturas");
        }
        else
        {
            DefinirStatus("Seu PC está protegido",
                $"Windows Defender ativo  •  assinaturas {Quando(s.AssinaturasAtualizadas)}", "Verde", "");
            App.AtualizarIconeTexto("Sentinela: protegido");
        }
    }

    private void DefinirStatus(string titulo, string detalhe, string cor, string icone)
    {
        TxtStatus.Text = titulo;
        TxtStatusDetalhe.Text = detalhe;
        CirculoStatus.Background = Cor(cor);
        IconeStatus.Text = icone;
        CartaoStatus.Background = Cor(cor + "Fundo");
        var c = ((SolidColorBrush)Cor(cor)).Color;
        CartaoStatus.BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, c.R, c.G, c.B));
    }

    private Border CartaoProtecao(string icone, string titulo, bool ligado)
        => CartaoInfo(icone, titulo, ligado ? "Ligada" : "Desligada", null, ligado ? "Verde" : "Vermelho");

    private Border CartaoInfo(string icone, string titulo, string valor, string? extra, string cor)
    {
        var pilha = new StackPanel();
        var topo = new DockPanel();
        var bolinha = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = Cor(cor), VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(bolinha, Dock.Right);
        topo.Children.Add(bolinha);
        topo.Children.Add(new TextBlock { Text = icone, Style = (Style)FindResource("Icone"), Foreground = Cor("TextoFraco"), FontSize = 15, Margin = new Thickness(0, 0, 10, 0) });
        topo.Children.Add(new TextBlock { Text = titulo, Style = (Style)FindResource("Fraco"), VerticalAlignment = VerticalAlignment.Center });
        pilha.Children.Add(topo);
        pilha.Children.Add(new TextBlock { Text = valor, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 0) });
        if (extra != null) pilha.Children.Add(new TextBlock { Text = extra, Style = (Style)FindResource("Fraco"), Margin = new Thickness(0, 3, 0, 0) });
        return new Border { Style = (Style)FindResource("CartaoBorda"), Child = pilha };
    }

    // ================================================================ Varredura

    private void VarreduraRapida_Click(object sender, RoutedEventArgs e) => IniciarVarredura(TipoVarredura.Rapida);
    private void VarreduraCompleta_Click(object sender, RoutedEventArgs e) => IniciarVarredura(TipoVarredura.Completa);
    private void CancelarVarredura_Click(object sender, RoutedEventArgs e)
    {
        BtnCancelar.IsEnabled = false;
        TxtAndamento.Text = "Cancelando...";
        _ctsVarredura?.Cancel();
    }

    public async void IniciarVarredura(TipoVarredura tipo)
    {
        MenuVarredura.IsChecked = true;
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
        App.AtualizarIconeTexto($"Sentinela: {nome.ToLower()} em andamento");

        string resumo;
        bool achou = false;
        try
        {
            var (codigo, saida) = await Defender.Varrer(tipo, _ctsVarredura.Token);
            var duracao = DateTime.Now - _inicioVarredura;
            // MpCmdRun devolve 2 tanto para "achou ameaça" quanto para erro: o texto decide.
            bool falhou = saida.Contains("[Failed]", StringComparison.OrdinalIgnoreCase)
                       || saida.Contains("Failed with hr", StringComparison.OrdinalIgnoreCase);
            achou = !falhou && codigo != 0;
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
        TxtTempo.Text = "";
        BtnCancelar.IsEnabled = false;
        App.Avisar("Sentinela", resumo, achou);
        _ameacasCarregadas = false;
        await AtualizarDefender();
    }

    private void AtualizarTempoVarredura()
        => TxtTempo.Text = "Tempo: " + Duracao(DateTime.Now - _inicioVarredura);

    private async void AtualizarAssinaturas_Click(object sender, RoutedEventArgs e)
    {
        BtnAtualizar.IsEnabled = false;
        TxtStatusDetalhe.Text = "Baixando assinaturas novas...";
        try
        {
            var (codigo, _) = await Defender.AtualizarAssinaturas(CancellationToken.None);
            App.Avisar("Sentinela", codigo == 0 ? "Assinaturas atualizadas." : $"A atualização terminou com o código {codigo}.", codigo != 0);
        }
        catch (Exception ex) { App.Avisar("Sentinela", ex.Message, true); }
        BtnAtualizar.IsEnabled = true;
        await AtualizarDefender();
    }

    // ================================================================ Ameaças

    private async Task CarregarAmeacas()
    {
        _ameacasCarregadas = true;
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

    private async void AtualizarAmeacas_Click(object sender, RoutedEventArgs e) => await CarregarAmeacas();

    private async void TratarAmeacas_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, "Pedir ao Windows Defender para tratar (remover ou pôr em quarentena) as ameaças ativas?",
                "Tratar ameaças", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        BtnTratar.IsEnabled = false;
        var r = await Defender.RemoverAmeacasAtivas();
        App.Avisar("Sentinela", r);
        await CarregarAmeacas();
    }

    private void AbrirSeguranca_Click(object sender, RoutedEventArgs e) => Tentar(Defender.AbrirSegurancaDoWindows);

    // ================================================================ Otimização

    private async void Analisar_Click(object sender, RoutedEventArgs e) => await Analisar();

    private async Task Analisar()
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
        if (MessageBox.Show(this, $"Apagar os arquivos de:\n\n{lista}\n\nOs arquivos apagados não vão para a Lixeira.",
                "Confirmar limpeza", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        BtnAnalisar.IsEnabled = BtnLimpar.IsEnabled = false;
        TxtResultadoLimpeza.Text = "Limpando...";
        long liberado = 0;
        foreach (var item in marcados)
            liberado += await Task.Run(() => Limpeza.Limpar(item));
        await Analisar();
        TxtResultadoLimpeza.Text = $"Pronto: {Limpeza.Formatar(liberado)} liberados. " + TxtResultadoLimpeza.Text;
        ListaDiscos.ItemsSource = Sistema.Discos();
    }

    // ================================================================ Inicialização

    private void CarregarInicializacao()
    {
        _inicializacaoCarregada = true;
        var lista = Inicializacao.Listar();
        GradeInicializacao.ItemsSource = lista;
        TxtResumoInicializacao.Text = $"{lista.Count(p => p.Ativo)} programa(s) abrem junto com o Windows, {lista.Count(p => !p.Ativo)} desativado(s).";
    }

    private void AtualizarInicializacao_Click(object sender, RoutedEventArgs e) => CarregarInicializacao();
    private void AbrirGerenciador_Click(object sender, RoutedEventArgs e) => Tentar(Inicializacao.AbrirGerenciadorDeTarefas);

    // ================================================================ Admin, bandeja, captura

    private void AbrirComoAdmin_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--elevado") { UseShellExecute = true, Verb = "runas" });
            App.Sair();
        }
        catch (Win32Exception) { /* a pessoa clicou em Não no aviso do Windows */ }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!App.Saindo && App.CapturaArquivo == null)
        {
            // Fechar no X só esconde: o painel continua na bandeja.
            e.Cancel = true;
            Hide();
            if (!_avisouBandeja)
            {
                _avisouBandeja = true;
                App.Avisar("Sentinela", "Continuo aqui na bandeja, perto do relógio. Para sair, clique com o botão direito no escudo.");
            }
            return;
        }
        _ctsVarredura?.Cancel();
        base.OnClosing(e);
    }

    private async Task Capturar()
    {
        switch (App.CapturaAba?.ToLowerInvariant())
        {
            case "varredura": MenuVarredura.IsChecked = true; break;
            case "ameacas": MenuAmeacas.IsChecked = true; break;
            case "limpeza": MenuLimpeza.IsChecked = true; await Analisar(); break;
            case "inicializacao": MenuInicializacao.IsChecked = true; break;
        }
        await Task.Delay(2500);
        AtualizarSistema();
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);

        var conteudo = (FrameworkElement)Content;
        var dpi = VisualTreeHelper.GetDpi(this);
        var bmp = new RenderTargetBitmap((int)(conteudo.ActualWidth * dpi.DpiScaleX), (int)(conteudo.ActualHeight * dpi.DpiScaleY),
            dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        var fundo = new DrawingVisual();
        using (var dc = fundo.RenderOpen())
            dc.DrawRectangle(Cor("Fundo"), null, new Rect(0, 0, conteudo.ActualWidth, conteudo.ActualHeight));
        bmp.Render(fundo);
        bmp.Render(conteudo);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using (var f = File.Create(App.CapturaArquivo!)) enc.Save(f);
        App.Sair();
    }

    // ================================================================ Utilidades

    private Brush Cor(string chave) => (Brush)FindResource(chave);

    private static void Tentar(Action a)
    {
        try { a(); } catch (Exception ex) { MessageBox.Show(ex.Message, "Sentinela"); }
    }

    private static string Quando(DateTime? d)
    {
        if (d is not { } v) return "—";
        var dias = (DateTime.Today - v.Date).Days;
        return dias switch
        {
            0 => $"hoje às {v:HH:mm}",
            1 => $"ontem às {v:HH:mm}",
            < 7 => $"há {dias} dias",
            _ => v.ToString("dd/MM/yyyy"),
        };
    }

    private static string Duracao(TimeSpan t)
        => t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes:00}min" : t.TotalMinutes >= 1 ? $"{t.Minutes}min {t.Seconds:00}s" : $"{t.Seconds}s";
}
