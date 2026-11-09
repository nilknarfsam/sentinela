using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Sentinela.Servicos;
using static Sentinela.Servicos.Formatos;

namespace Sentinela;

public partial class MainWindow : Window
{
    private readonly bool _admin = Sistema.EhAdmin();
    private readonly DispatcherTimer _timerRapido = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly DispatcherTimer _timerDefender = new() { Interval = TimeSpan.FromSeconds(20) };
    private readonly DispatcherTimer _timerEventosDefender = new() { Interval = TimeSpan.FromSeconds(2) };
    private bool _relerStatusPendente;
    private bool _relerAmeacasPendente;
    private bool _avisouBandeja;
    private Task? _inicializacao;
    private Task? _leituraDefender;

    private App App => (App)Application.Current;

    public MainWindow()
    {
        InitializeComponent();

        FaixaAdmin.Visibility = _admin ? Visibility.Collapsed : Visibility.Visible;
        TxtAdminMenu.Text = _admin ? "Rodando como administrador" : "Rodando sem administrador";
        TxtVersaoMenu.Text = $"versão {Versao}";

        PaginaInicio.PediuVarredura += IniciarVarredura;
        PaginaInicio.PediuAtualizarStatus += AtualizarStatusAposOperacao;
        PaginaVarredura.VarreduraTerminou += AtualizarStatusAposOperacao;
        PaginaLimpeza.EspacoMudou += PaginaInicio.AtualizarDiscos;

        _timerRapido.Tick += (_, _) => PaginaInicio.AtualizarSistema();
        _timerDefender.Tick += async (_, _) => await AtualizarDefender();
        _timerEventosDefender.Tick += async (_, _) =>
        {
            try
            {
                _timerEventosDefender.Stop();
                var relerStatus = _relerStatusPendente;
                var relerAmeacas = _relerAmeacasPendente;
                _relerStatusPendente = _relerAmeacasPendente = false;
                PaginaHistorico.Invalidar();
                if (relerAmeacas)
                {
                    PaginaAmeacas.Carregada = false;
                    if (PaginaAmeacas.IsVisible) await PaginaAmeacas.Carregar();
                }
                if (relerStatus) await AtualizarDefender();
            }
            catch { /* Uma falha na releitura não pode escapar do handler assíncrono. */ }
        };

        IsVisibleChanged += async (_, _) =>
        {
            if (_inicializacao == null || App.Saindo) return;
            AjustarTimers();
            if (IsVisible)
            {
                Sistema.UsoCpu(); // descarta o intervalo em que a janela ficou oculta
                PaginaInicio.AtualizarSistema();
                await AtualizarDefender();
            }
        };
        Loaded += async (_, _) =>
        {
            await Iniciar();
            if (App.CapturaArquivo != null) await Capturar();
        };
    }

    public Task Iniciar() => _inicializacao ??= IniciarMonitoramento();

    private async Task IniciarMonitoramento()
    {
        AjustarTimers();
        if (IsVisible)
        {
            Sistema.UsoCpu();
            PaginaInicio.AtualizarSistema();
        }
        await AtualizarDefender();
    }

    private void AjustarTimers()
    {
        if (IsVisible) _timerRapido.Start();
        else _timerRapido.Stop();
        _timerDefender.Interval = TimeSpan.FromSeconds(IsVisible ? 20 : 60);
        _timerDefender.Start();
    }

    // ================================================================ Navegação

    private void Menu_Checked(object sender, RoutedEventArgs e)
    {
        if (PaginaInicio == null) return; // ainda montando a janela
        PaginaInicio.Visibility = Vis(MenuInicio.IsChecked);
        PaginaVarredura.Visibility = Vis(MenuVarredura.IsChecked);
        PaginaAmeacas.Visibility = Vis(MenuAmeacas.IsChecked);
        PaginaHistorico.Visibility = Vis(MenuHistorico.IsChecked);
        PaginaLimpeza.Visibility = Vis(MenuLimpeza.IsChecked);
        PaginaInicializacao.Visibility = Vis(MenuInicializacao.IsChecked);
        PaginaAjustes.Visibility = Vis(MenuAjustes.IsChecked);

        if (MenuAjustes.IsChecked == true && !PaginaAjustes.Carregada) _ = PaginaAjustes.Carregar();
        if (MenuAmeacas.IsChecked == true && !PaginaAmeacas.Carregada) _ = PaginaAmeacas.Carregar();
        if (MenuHistorico.IsChecked == true && !PaginaHistorico.Carregada) _ = PaginaHistorico.Carregar();
        if (MenuInicializacao.IsChecked == true && !PaginaInicializacao.Carregada) PaginaInicializacao.Carregar();
    }

    private static Visibility Vis(bool? b) => b == true ? Visibility.Visible : Visibility.Collapsed;

    private int _contadorDiscos;
    private async Task AtualizarDefender()
    {
        if (_leituraDefender != null)
        {
            await _leituraDefender;
            return;
        }
        _leituraDefender = LerDefender();
        try { await _leituraDefender; }
        finally { _leituraDefender = null; }
    }

    private async Task LerDefender()
    {
        if (_contadorDiscos++ % 3 == 0) PaginaInicio.AtualizarDiscos();

        StatusDefender? s = null;
        string? erro = null;
        try { s = await Task.Run(Defender.LerStatus); }
        catch (Exception ex) { erro = ex.Message; }
        PaginaInicio.MostrarStatus(s, erro);
        PaginaVarredura.MostrarUltimas(s);
    }

    private async void AtualizarStatusAposOperacao()
    {
        PaginaHistorico.Invalidar();
        PaginaAmeacas.Carregada = false;
        await AtualizarDefender();
    }

    public void IniciarVarredura(TipoVarredura tipo)
    {
        MenuVarredura.IsChecked = true;
        PaginaVarredura.Iniciar(tipo);
    }

    public bool IgnorarEventoDeVarredura => PaginaVarredura.EmAndamento
        || DateTime.UtcNow - PaginaVarredura.UltimoFimUtc <= TimeSpan.FromSeconds(15);

    public void ReceberEventoDefender(bool relerStatus, bool relerAmeacas)
    {
        _relerStatusPendente |= relerStatus;
        _relerAmeacasPendente |= relerAmeacas;
        if (!_timerEventosDefender.IsEnabled) _timerEventosDefender.Start();
    }

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
            if (!Configuracoes.Atual.FecharParaBandeja)
            {
                e.Cancel = true;
                Dispatcher.BeginInvoke(new Action(App.Sair));
                return;
            }
            // Fechar no X esconde: o painel continua na bandeja.
            e.Cancel = true;
            Hide();
            if (!_avisouBandeja)
            {
                _avisouBandeja = true;
                App.Avisar("Sentinela", "Continuo aqui na bandeja, perto do relógio. Para sair, clique com o botão direito no escudo.", rotina: true);
            }
            return;
        }
        _timerRapido.Stop();
        _timerDefender.Stop();
        _timerEventosDefender.Stop();
        PaginaVarredura.Cancelar();
        base.OnClosing(e);
    }

    private async Task Capturar()
    {
        switch (App.CapturaAba?.ToLowerInvariant())
        {
            case "varredura": MenuVarredura.IsChecked = true; break;
            case "ameacas": MenuAmeacas.IsChecked = true; break;
            case "historico": MenuHistorico.IsChecked = true; await PaginaHistorico.Carregar(); break;
            case "limpeza": MenuLimpeza.IsChecked = true; await PaginaLimpeza.Analisar(); break;
            case "inicializacao": MenuInicializacao.IsChecked = true; break;
            case "ajustes": MenuAjustes.IsChecked = true; break;
        }
        await Task.Delay(2500);
        PaginaInicio.AtualizarSistema();
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
}
