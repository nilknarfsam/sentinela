using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Sentinela.Servicos;
using static Sentinela.Servicos.Formatos;

namespace Sentinela.Paginas;

public partial class PaginaAjustes : UserControl
{
    public bool Carregada { get; private set; }

    public PaginaAjustes() => InitializeComponent();

    public async Task Carregar()
    {
        Carregada = true;
        ListaProjetos.ItemsSource = Configuracoes.Atual.ListarPastasProjetos();
        // schtasks pode demorar: roda fora da thread da tela.
        MostrarInicializacao(await Task.Run(TarefaInicializacao.EstaLigada));
        ChkBandeja.IsChecked = Configuracoes.Atual.FecharParaBandeja;
        ChkAvisos.IsChecked = Configuracoes.Atual.AvisosDeRotina;
        TxtDiario.Text = $"{Diario.Ler(30).Count} entrada(s) nos últimos 30 dias.";
        TxtVersao.Text = $"Sentinela versão {Versao}";
        TxtExecutavel.Text = Environment.ProcessPath;
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
    private async void AdicionarPasta_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new Microsoft.Win32.OpenFolderDialog { Title = "Pasta dos seus projetos" };
        if (dialogo.ShowDialog() != true) return;
        if (!Configuracoes.ValidarPastaProjeto(dialogo.FolderName, out var pasta, out var motivo))
        {
            MessageBox.Show(Window.GetWindow(this), motivo, "Pasta ignorada", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!Configuracoes.Atual.PastasProjetos.Contains(pasta, StringComparer.OrdinalIgnoreCase))
            Configuracoes.Atual.PastasProjetos.Add(pasta);
        ListaProjetos.ItemsSource = Configuracoes.Atual.ListarPastasProjetos();
        await Task.Run(Configuracoes.Atual.Salvar);
    }
    private async void RemoverPasta_Click(object sender, RoutedEventArgs e)
    {
        if (ListaProjetos.SelectedItem is not Configuracoes.PastaProjeto pasta) return;
        if (MessageBox.Show(Window.GetWindow(this), $"Remover esta pasta da lista de projetos?\n\n{pasta.Caminho}",
            "Remover pasta", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        Configuracoes.Atual.PastasProjetos.Remove(pasta.Caminho);
        Configuracoes.Atual.PastasIgnoradas.Remove(pasta);
        ListaProjetos.ItemsSource = Configuracoes.Atual.ListarPastasProjetos();
        await Task.Run(Configuracoes.Atual.Salvar);
    }
}
