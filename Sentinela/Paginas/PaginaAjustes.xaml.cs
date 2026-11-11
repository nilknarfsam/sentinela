using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Sentinela.Servicos;
using static Sentinela.Servicos.Formatos;

namespace Sentinela.Paginas;

public partial class PaginaAjustes : UserControl
{
    public bool Carregada { get; private set; }
    private bool _preparandoSemanal;

    internal static bool AlvoAjudaPermitido(string? alvo) => alvo is
        "https://github.com/nilknarfsam" or
        "https://samfieldsoak.com.br" or
        "https://www.instagram.com/samfieldsoak" or
        "https://www.linkedin.com/in/franklin-carvalho-45032a164/";

    public PaginaAjustes() => InitializeComponent();

    internal void SelecionarAbaSobreAjuda() => AbasAjustes.SelectedItem = AbaSobreAjuda;

    public async Task Carregar()
    {
        Carregada = true;
        _preparandoSemanal = true;
        DiaSemanal.ItemsSource = new[] { "Domingo", "Segunda-feira", "Terça-feira", "Quarta-feira", "Quinta-feira", "Sexta-feira", "Sábado" };
        HoraSemanal.ItemsSource = Enumerable.Range(0, 24).Select(h => $"{h:00}:00");
        ChkSemanal.IsChecked = Configuracoes.Atual.VarreduraSemanalLigada;
        DiaSemanal.SelectedIndex = (int)Configuracoes.Atual.DiaVarreduraSemanal;
        HoraSemanal.SelectedIndex = Configuracoes.Atual.HoraVarreduraSemanal;
        _preparandoSemanal = false;
        AjustarControlesSemanais();
        await AtualizarUltimaCompleta();
        ListaProjetos.ItemsSource = Configuracoes.Atual.ListarPastasProjetos();
        // schtasks pode demorar: roda fora da thread da tela.
        MostrarInicializacao(await Task.Run(TarefaInicializacao.EstaLigada));
        ChkBandeja.IsChecked = Configuracoes.Atual.FecharParaBandeja;
        ChkAvisos.IsChecked = Configuracoes.Atual.AvisosDeRotina;
        TxtDiario.Text = $"{await Task.Run(() => Diario.Ler(30).Count)} entrada(s) nos últimos 30 dias.";
        TxtVersao.Text = $"Versão {Versao}";
        TxtExecutavel.Text = Environment.ProcessPath;
    }

    public async Task AtualizarUltimaCompleta()
    {
        var status = await Task.Run(() => { try { return Defender.LerStatus(); } catch { return null; } });
        TxtUltimaCompleta.Text = "Última varredura completa: " + (status == null ? "não consegui consultar" :
            status.UltimaVarreduraCompleta is { } data ? data.ToString("dd/MM/yyyy HH:mm") : "nunca feita");
    }

    private void AjustarControlesSemanais() => DiaSemanal.IsEnabled = HoraSemanal.IsEnabled = ChkSemanal.IsChecked == true;

    private async void Semanal_Changed(object sender, RoutedEventArgs e)
    {
        if (_preparandoSemanal || DiaSemanal == null || HoraSemanal == null || DiaSemanal.SelectedIndex < 0 || HoraSemanal.SelectedIndex < 0) return;
        var config = Configuracoes.Atual;
        config.AlterarRotinaSemanal(ChkSemanal.IsChecked == true,
            (DayOfWeek)DiaSemanal.SelectedIndex, HoraSemanal.SelectedIndex, DateTime.Now);
        AjustarControlesSemanais();
        await Task.Run(config.Salvar);
    }

    private void MostrarInicializacao(bool? ligada)
    {
        ChkInicializacao.IsChecked = ligada == true;
        ChkInicializacao.IsEnabled = ligada != null && Sistema.EhAdmin();
        TxtInicializacao.Text = ligada == null
            ? "O Sentinela não está instalado. Rode o instalar-sentinela.bat como administrador."
            : !Sistema.EhAdmin() ? "Abra como administrador para mudar."
            : "Abre na bandeja, perto do relógio.";
    }

    private async void Inicializacao_Click(object sender, RoutedEventArgs e)
    {
        var ligar = ChkInicializacao.IsChecked == true;
        ChkInicializacao.IsEnabled = false;
        var resultado = await Task.Run(() => TarefaInicializacao.Definir(ligar));
        MostrarInicializacao(await Task.Run(TarefaInicializacao.EstaLigada));
        TxtInicializacao.Text = resultado.mensagem;
        Configuracoes.Atual.Salvar();
    }

    private void Bandeja_Click(object sender, RoutedEventArgs e)
    {
        Configuracoes.Atual.FecharParaBandeja = ChkBandeja.IsChecked == true;
        Configuracoes.Atual.Salvar();
    }

    private void Avisos_Click(object sender, RoutedEventArgs e)
    {
        Configuracoes.Atual.AvisosDeRotina = ChkAvisos.IsChecked == true;
        Configuracoes.Atual.Salvar();
    }

    private void AbrirDiario_Click(object sender, RoutedEventArgs e) => Tentar(() =>
    {
        var pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sentinela");
        if (!ArquivosSeguros.PastaSegura(pasta)) return;
        Process.Start(new ProcessStartInfo(CaminhosSistema.Explorer, $"\"{pasta}\"") { UseShellExecute = true });
    });

    private async void AbrirLinkAjuda_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string alvo } || !AlvoAjudaPermitido(alvo))
        {
            TxtResultadoLinkAjuda.Text = "Este link não está disponível.";
            return;
        }

        TxtResultadoLinkAjuda.Text = "";
        var abriu = await Task.Run(() =>
        {
            try
            {
                using var processo = Process.Start(new ProcessStartInfo(alvo) { UseShellExecute = true });
                return true;
            }
            catch { return false; }
        });
        if (!abriu) TxtResultadoLinkAjuda.Text = "Não foi possível abrir o link. Confira o navegador padrão e tente novamente.";
    }

    private async void AdicionarPasta_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new Microsoft.Win32.OpenFolderDialog { Title = "Pasta dos seus projetos" };
        if (dialogo.ShowDialog() != true) return;
        if (!Configuracoes.ValidarPastaProjeto(dialogo.FolderName, out var pasta, out var motivo))
        {
            MessageBox.Show(Window.GetWindow(this), motivo, "Pasta ignorada", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Configuracoes.Atual.AdicionarPastaProjeto(pasta);
        ListaProjetos.ItemsSource = Configuracoes.Atual.ListarPastasProjetos();
        await Task.Run(Configuracoes.Atual.Salvar);
    }
    private async void RemoverPasta_Click(object sender, RoutedEventArgs e)
    {
        if (ListaProjetos.SelectedItem is not Configuracoes.PastaProjeto pasta) return;
        if (MessageBox.Show(Window.GetWindow(this), $"Remover esta pasta da lista de projetos?\n\n{pasta.Caminho}",
            "Remover pasta", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        Configuracoes.Atual.RemoverPastaProjeto(pasta);
        ListaProjetos.ItemsSource = Configuracoes.Atual.ListarPastasProjetos();
        await Task.Run(Configuracoes.Atual.Salvar);
    }
}
