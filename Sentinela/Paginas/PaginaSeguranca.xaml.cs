using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Sentinela.Servicos;
using Sentinela.Servicos.Checkup;
using Sentinela.Servicos.Seguranca;
using static Sentinela.Servicos.Formatos;
using ServicoSeguranca = Sentinela.Servicos.Seguranca.Seguranca;

namespace Sentinela.Paginas;

public partial class PaginaSeguranca : UserControl
{
    private Task? _execucao;
    public PaginaSeguranca() => InitializeComponent();
    public void Carregar()
    {
        if (ServicoSeguranca.Ultimo is { } ultimo) Mostrar(ultimo);
        else _ = Verificar();
    }
    public Task Verificar() => _execucao ??= Executar();
    private async Task Executar()
    {
        await Task.Yield();
        BtnVerificar.IsEnabled = false;
        BtnVerificar.Content = "Verificando...";
        TxtErro.Text = "";
        try { Mostrar(await ServicoSeguranca.Executar()); }
        catch (Exception ex) { TxtErro.Text = $"Não consegui verificar agora: {ex.Message}"; }
        finally
        {
            BtnVerificar.IsEnabled = true;
            BtnVerificar.Content = "Verificar agora";
            _execucao = null;
        }
    }
    private void Mostrar(ResultadoCheckup resultado)
    {
        Convite.Visibility = Visibility.Collapsed;
        CartaoNota.Visibility = Visibility.Visible;
        var cor = resultado.Nota >= 85 ? "Verde" : resultado.Nota >= 60 ? "Ambar" : "Vermelho";
        TxtNota.Text = resultado.Nota.ToString();
        TxtNota.Foreground = Cor(cor);
        CartaoNota.Background = Cor(cor + "Fundo");
        TxtFaixa.Text = resultado.Nota >= 85 ? "Segurança bem configurada" : resultado.Nota >= 60 ? "Dá para melhorar algumas coisas" : "Sua segurança precisa de atenção";
        TxtQuando.Text = $"Verificado em {resultado.Data:dd/MM/yyyy} às {resultado.Data:HH:mm} em {resultado.Duracao.TotalSeconds:0.0} s";
        ListaVerificacoes.ItemsSource = resultado.Itens.Select(i => new CartaoVerificacao(i)).ToArray();
    }
    private async void Verificar_Click(object sender, RoutedEventArgs e) => await Verificar();
    private async void Resolver_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: AcaoResolver acao }) return;
        try
        {
            if (acao.Tipo == "abrir" && AlvoPermitido(acao.Alvo))
            {
                await Task.Run(() =>
                {
                    using var processo = Process.Start(new ProcessStartInfo(acao.Alvo == "uac" ? CaminhosSistema.ConfiguracoesUac : acao.Alvo!) { UseShellExecute = true });
                });
            }
            else if (acao.Tipo == "aceitar-exclusoes" && sender is Button { DataContext: CartaoVerificacao cartao }
                && VerificacaoExclusoes.ListaExibida(cartao.Item) is { } lista && App.CapturaArquivo == null)
            {
                if (MessageBox.Show(Window.GetWindow(this), "Você reconhece todas estas exclusões?\n\n" + string.Join("\n", lista)
                    + "\n\nIsso salva apenas uma referência no Sentinela. O Defender não será alterado.", "Sentinela",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
                var gravado = await Task.Run(() => ServicoSeguranca.GravarReferencia(ServicoSeguranca.CaminhoExclusoes, lista));
                if (gravado) await Verificar();
                else TxtErro.Text = "Não consegui salvar a referência das exclusões.";
            }
        }
        catch (Exception ex) { TxtErro.Text = $"Não consegui abrir ou concluir a ação: {ex.Message}"; }
    }
    internal static bool AlvoPermitido(string? alvo) => alvo is "windowsdefender://network" or "ms-settings:windowsupdate"
        or "windowsdefender://appbrowser" or "windowsdefender://ransomwareprotection" or "windowsdefender://devicesecurity"
        or "windowsdefender://threatsettings" or "ms-settings:otherusers" or "uac";
    public sealed class CartaoVerificacao(ResultadoVerificacao item)
    {
        public ResultadoVerificacao Item { get; } = item;
        public Brush CorNivel => Cor(Item.Nivel == NivelVerificacao.Problema ? "Vermelho" : Item.Nivel == NivelVerificacao.Atencao ? "Ambar" : "Verde");
        public Visibility VisibilidadeAcao => Item.Acao == null ? Visibility.Collapsed : Visibility.Visible;
        public AcaoResolver Aceitar { get; } = new("Eu reconheço estas exclusões", "aceitar-exclusoes", null);
        public Visibility VisibilidadeAceitar => App.CapturaArquivo == null && VerificacaoExclusoes.ListaExibida(Item) is not null ? Visibility.Visible : Visibility.Collapsed;
    }
}
