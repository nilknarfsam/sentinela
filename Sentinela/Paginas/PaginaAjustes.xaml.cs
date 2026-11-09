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
        Directory.CreateDirectory(pasta);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{pasta}\"") { UseShellExecute = true });
    });
}
