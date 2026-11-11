using System.Windows;
using System.Windows.Controls;
using Sentinela.Servicos;
using static Sentinela.Servicos.Formatos;
using System.Windows.Media;
using Sentinela.Apresentacao;

namespace Sentinela.Paginas;

public partial class PaginaInicio : UserControl
{
    public event Action<TipoVarredura>? PediuVarredura;
    public event Action? PediuAtualizarStatus;
    public event Action? PediuSeguranca;
    private void Seguranca_Click(object sender, RoutedEventArgs e) => PediuSeguranca?.Invoke();
    public event Action? PediuDesempenho;
    private void Desempenho_Click(object sender, RoutedEventArgs e) => PediuDesempenho?.Invoke();
    public void AtualizarCheckup()
    {
        var ultimoSeguranca = Sentinela.Servicos.Seguranca.Seguranca.Ultimo;
        var seguranca = TextoCheckup(ultimoSeguranca, DateTime.Now);
        var rotuloSeguranca = ultimoSeguranca == null ? "" : EstadoApresentacao.Rotulo(ultimoSeguranca);
        TxtSeguranca.Text = string.IsNullOrEmpty(rotuloSeguranca) ? seguranca.Texto : $"{seguranca.Texto} · {rotuloSeguranca}";
        TxtSeguranca.Foreground = Cor(ultimoSeguranca == null ? "TextoFraco" : EstadoApresentacao.Cor(ultimoSeguranca, seguranca.Cor));
        TxtNovaSeguranca.Visibility = seguranca.Antigo ? Visibility.Visible : Visibility.Collapsed;
        var ultimoDesempenho = Sentinela.Servicos.Checkup.Checkup.Ultimo;
        var cartao = TextoCheckup(ultimoDesempenho, DateTime.Now);
        var rotuloDesempenho = ultimoDesempenho == null ? "" : EstadoApresentacao.Rotulo(ultimoDesempenho);
        TxtDesempenho.Text = string.IsNullOrEmpty(rotuloDesempenho) ? cartao.Texto : $"{cartao.Texto} · {rotuloDesempenho}";
        TxtDesempenho.Foreground = Cor(ultimoDesempenho == null ? "TextoFraco" : EstadoApresentacao.Cor(ultimoDesempenho, cartao.Cor));
        TxtNovoCheckup.Visibility = cartao.Antigo ? Visibility.Visible : Visibility.Collapsed;
    }
    internal static (string Texto, string Cor, bool Antigo) TextoCheckup(Sentinela.Servicos.Checkup.ResultadoCheckup? ultimo, DateTime agora)
    {
        if (ultimo == null) return ("Ainda não verificado", "TextoFraco", false);
        var data = ultimo.Data;
        var quando = data.Date == agora.Date ? "hoje" : data.Date == agora.Date.AddDays(-1) ? "ontem" : $"em {data:dd/MM/yyyy}";
        return ($"{ultimo.Nota} de 100 · verificado {quando} às {data:HH:mm}",
            ultimo.Nota >= 85 ? "Verde" : ultimo.Nota >= 60 ? "Ambar" : "Vermelho", agora - data > TimeSpan.FromDays(7));
    }

    private App App => (App)Application.Current;

    public PaginaInicio()
    {
        InitializeComponent();
    }

    public event Action<double, double>? SistemaAtualizado;
    private bool _lendoSistema;
    public async void AtualizarSistema()
    {
        try { AtualizarCheckup(); } catch { /* o cartão do Checkup não derruba o Início */ }
        if (_lendoSistema) return;
        _lendoSistema = true;
        try
        {
            var leitura = await Task.Run(() => (cpu: Sistema.UsoCpu(), memoria: Sistema.Memoria()));
            var cpu = leitura.cpu;
            BarraCpu.Value = cpu;
            TxtCpu.Text = $"{cpu:0}%";
            var (usado, total, pct) = leitura.memoria;
            BarraRam.Value = pct;
            TxtRam.Text = $"{usado:0.0} de {total:0} GB";
            TxtUptime.Text = Sistema.TempoLigado();
            BarraCpu.Foreground = Cor(cpu > 85 ? "Ambar" : "Destaque");
            BarraRam.Foreground = Cor(pct > 85 ? "Ambar" : "Destaque");
            SistemaAtualizado?.Invoke(cpu, pct);
        }
        catch { /* Falhas de leitura não escapam do timer. */ }
        finally { _lendoSistema = false; }
    }

    public void AtualizarDiscos() => ListaDiscos.ItemsSource = Sistema.Discos();

    public void MostrarStatus(StatusDefender? s, string? erro)
    {
        GradeProtecao.Children.Clear();
        if (s == null)
        {
            TxtTituloProtecao.Visibility = GradeProtecao.Visibility = Visibility.Collapsed;
            DefinirStatus("Não foi possível consultar o Defender", (erro ?? "O Windows Defender não respondeu.") + " O estado da proteção não foi confirmado.", "Ambar", "\uE783");
            GradeProtecao.Children.Add(CartaoInfo("\uE783", "Estado da proteção", "Não confirmado", "A consulta do Defender falhou. Tente novamente ou abra como administrador.", "Ambar"));
            App.AtualizarIcone(NivelStatus.Atencao, "Sentinela: consulta do Defender indisponível",
                "Não foi possível confirmar o estado da proteção do Windows Defender.");
            return;
        }

        TxtTituloProtecao.Visibility = GradeProtecao.Visibility = Visibility.Visible;

        GradeProtecao.Children.Add(CartaoProtecao("\uE9F5", "Proteção em tempo real", s.TempoReal));
        GradeProtecao.Children.Add(CartaoProtecao("\uE9D9", "Análise de comportamento", s.Comportamento));
        GradeProtecao.Children.Add(CartaoProtecao("\uE896", "Verificação de downloads", s.Downloads));
        GradeProtecao.Children.Add(CartaoProtecao("\uE72E", "Proteção contra adulteração", s.ProtecaoAdulteracao));
        GradeProtecao.Children.Add(CartaoInfo("\uE895", "Assinaturas de vírus",
            s.AssinaturasAtualizadas is { } d ? $"Atualizadas {Quando(d)}" : "Sem data",
            $"Versão {s.VersaoAssinaturas}", s.AssinaturasVelhas ? "Ambar" : "Verde"));
        var ultima = new[] { s.UltimaVarreduraRapida, s.UltimaVarreduraCompleta }.Where(x => x != null).Max();
        GradeProtecao.Children.Add(CartaoInfo("\uE721", "Última varredura",
            ultima is { } u ? Quando(u) : "Nenhuma ainda",
            s.UltimaVarreduraCompleta is { } c ? $"Completa: {Quando(c)}" : "Completa: nunca",
            ultima == null || (DateTime.Now - ultima.Value).TotalDays > 7 ? "Ambar" : "Verde"));


        if (!s.TudoLigado)
        {
            DefinirStatus("Atenção: alguma proteção está desligada",
                "Abra a Segurança do Windows para religar. O Sentinela não muda configurações de segurança.", "Vermelho", "\uE7BA");
            App.AtualizarIcone(NivelStatus.Alerta, "Sentinela: proteção desligada!",
                "Atenção: uma proteção do Windows Defender está desligada. Abra o Sentinela para ver.");
        }
        else if (s.AssinaturasVelhas)
        {
            DefinirStatus("Protegido, mas as assinaturas estão velhas", "Clique em Atualizar assinaturas.", "Ambar", "\uE7BA");
            App.AtualizarIcone(NivelStatus.Atencao, "Sentinela: atualize as assinaturas");
        }
        else
        {
            DefinirStatus("Seu PC está protegido",
                $"Windows Defender ativo  •  assinaturas {Quando(s.AssinaturasAtualizadas)}", "Verde", "\uEA18");
            App.AtualizarIcone(NivelStatus.Ok, "Sentinela: protegido");
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

    private void VarreduraRapida_Click(object sender, RoutedEventArgs e)
        => PediuVarredura?.Invoke(TipoVarredura.Rapida);

    private async void AtualizarAssinaturas_Click(object sender, RoutedEventArgs e)
    {
        BtnAtualizar.IsEnabled = false;
        TxtStatusDetalhe.Text = "Baixando assinaturas novas...";
        try
        {
            var (codigo, _) = await Defender.AtualizarAssinaturas(CancellationToken.None);
            var mensagem = codigo == 0 ? "Assinaturas atualizadas." : $"A atualização terminou com o código {codigo}.";
            Diario.Registrar("assinaturas", mensagem);
            App.Avisar("Sentinela", mensagem, codigo != 0, rotina: codigo == 0);
        }
        catch (Exception ex)
        {
            Diario.Registrar("assinaturas", ex.Message);
            App.Avisar("Sentinela", ex.Message, true);
        }
        BtnAtualizar.IsEnabled = true;
        PediuAtualizarStatus?.Invoke();
    }
}
