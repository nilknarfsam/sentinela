using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Sentinela.Servicos;
using Sentinela.Servicos.Checkup;
using static Sentinela.Servicos.Formatos;

namespace Sentinela.Paginas;

public partial class PaginaDesempenho : UserControl
{
    private ResultadoCheckup? _ultimo;
    private Task? _execucao;

    public PaginaDesempenho() => InitializeComponent();

    // A página é mantida pela janela; navegar nunca dispara uma nova leitura.
    public void Carregar()
    {
        if (_ultimo != null) Mostrar(_ultimo);
    }

    public Task Verificar() => _execucao ??= Executar();

    private async Task Executar()
    {
        BtnVerificar.IsEnabled = false;
        BtnVerificar.Content = "Verificando...";
        try
        {
            _ultimo = await Checkup.Executar();
            Mostrar(_ultimo);
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
        TxtNota.Foreground = Cor(cor);
        CartaoNota.Background = Cor(cor + "Fundo");
        TxtFaixa.Text = resultado.Nota >= 85 ? "Seu PC está em ótima forma" : resultado.Nota >= 60 ? "Dá para melhorar algumas coisas" : "Seu PC precisa de atenção";
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
}
