using System.Windows;
using System.Windows.Controls;
using Sentinela.Servicos;
using static Sentinela.Servicos.Formatos;

namespace Sentinela.Paginas;

public partial class PaginaInicializacao : UserControl
{
    public bool Carregada { get; private set; }

    public PaginaInicializacao()
    {
        InitializeComponent();
    }

    public void Carregar()
    {
        Carregada = true;
        var lista = Inicializacao.Listar();
        GradeInicializacao.ItemsSource = lista;
        TxtResumoInicializacao.Text = $"{lista.Count(p => p.Ativo)} programa(s) abrem junto com o Windows, {lista.Count(p => !p.Ativo)} desativado(s).";
    }

    private void AtualizarInicializacao_Click(object sender, RoutedEventArgs e) => Carregar();
    private void AbrirGerenciador_Click(object sender, RoutedEventArgs e) => Tentar(Inicializacao.AbrirGerenciadorDeTarefas);
}
