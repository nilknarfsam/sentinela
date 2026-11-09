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
    private bool _avisouBandeja;

    private App App => (App)Application.Current;

    public MainWindow()
    {
        InitializeComponent();

        FaixaAdmin.Visibility = _admin ? Visibility.Collapsed : Visibility.Visible;
        TxtAdminMenu.Text = _admin ? "Rodando como administrador" : "Rodando sem administrador";

        PaginaInicio.PediuVarredura += IniciarVarredura;
        PaginaInicio.PediuAtualizarStatus += AtualizarStatusAposOperacao;
        PaginaVarredura.VarreduraTerminou += AtualizarStatusAposOperacao;
        PaginaLimpeza.EspacoMudou += PaginaInicio.AtualizarDiscos;

        _timerRapido.Tick += (_, _) => PaginaInicio.AtualizarSistema();
        _timerDefender.Tick += async (_, _) => await AtualizarDefender();

        Loaded += async (_, _) =>
        {
            Sistema.UsoCpu(); // primeira leitura serve de base
            PaginaInicio.AtualizarSistema();
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

        if (MenuAmeacas.IsChecked == true && !PaginaAmeacas.Carregada) _ = PaginaAmeacas.Carregar();
        if (MenuInicializacao.IsChecked == true && !PaginaInicializacao.Carregada) PaginaInicializacao.Carregar();
    }

    private static Visibility Vis(bool? b) => b == true ? Visibility.Visible : Visibility.Collapsed;

    private int _contadorDiscos;
    private async Task AtualizarDefender()
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
        PaginaAmeacas.Carregada = false;
        await AtualizarDefender();
    }

    public void IniciarVarredura(TipoVarredura tipo)
    {
        MenuVarredura.IsChecked = true;
        PaginaVarredura.Iniciar(tipo);
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
        PaginaVarredura.Cancelar();
        base.OnClosing(e);
    }

    private async Task Capturar()
    {
        switch (App.CapturaAba?.ToLowerInvariant())
        {
            case "varredura": MenuVarredura.IsChecked = true; break;
            case "ameacas": MenuAmeacas.IsChecked = true; break;
            case "limpeza": MenuLimpeza.IsChecked = true; await PaginaLimpeza.Analisar(); break;
            case "inicializacao": MenuInicializacao.IsChecked = true; break;
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
