using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Sentinela.Servicos;

namespace Sentinela.Paginas;

public partial class PaginaLimpeza : UserControl
{
    private string _secao = "Windows";
    private List<ItemLimpeza> _itens = [];
    private readonly Dictionary<ItemLimpeza, SobraProjeto> _sobras = [];
    private CancellationTokenSource? _cancelamento;
    private string? _pastaEscolhida;
    private bool _limpandoComponentes;
    public event Action? EspacoMudou;

    public PaginaLimpeza()
    {
        InitializeComponent();
        BtnComponentes.IsEnabled = Sistema.EhAdmin();
        if (!BtnComponentes.IsEnabled) TxtComponentes.Text = "Abra como administrador para limpar componentes.";
        Discos.ItemsSource = DriveInfo.GetDrives().Select(d => d.Name).Append("Escolher pasta").ToArray();
        Discos.SelectedIndex = 0;
        Mostrar(Limpeza.Itens());
        Loaded += async (_, _) => await Historico();
    }

    private void Mostrar(List<ItemLimpeza> itens)
    {
        _itens = itens;
        foreach (var item in itens) item.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ItemLimpeza.Marcado)) Total(); };
        ListaLimpeza.ItemsSource = itens;
        Total();
    }
    private void Total() => TxtTotal.Text = _secao == "Maiores pastas" ? "Somente leitura • percentual do total das subpastas diretas." :
        $"Total marcado: {Limpeza.Formatar(_itens.Where(i => i.Marcado && i.Disponivel).Sum(i => Math.Max(0, i.Bytes)))}";
    private async Task Historico()
    {
        var bytes = await Task.Run(() => Diario.SomarLiberado(Diario.Ler(30), DateTime.Now));
        TxtHistorico.Text = $"Já liberado pelo Sentinela: {Limpeza.Formatar(bytes)} nos últimos 30 dias";
    }
    private void Secao(string secao)
    {
        _secao = secao;
        CartaoComponentes.Visibility = secao == "Windows" ? Visibility.Visible : Visibility.Collapsed;
        foreach (var b in Filtros.Children.OfType<Button>()) b.Style = (Style)FindResource((string)b.Content == secao ? "BotaoPrincipal" : "BotaoBase");
        bool maiores = secao == "Maiores pastas";
        Discos.Visibility = GradePastas.Visibility = maiores ? Visibility.Visible : Visibility.Collapsed;
        Rolagem.Visibility = BtnLimpar.Visibility = maiores ? Visibility.Collapsed : Visibility.Visible;
    }
    private async void Filtrar_Click(object sender, RoutedEventArgs e)
    {
        Secao((string)((Button)sender).Content);
        Mostrar(_secao == "Windows" ? Limpeza.Itens() : []);
        TxtResultadoLimpeza.Text = "Clique em Analisar para medir.";
        if (_secao is "Caches" or "Navegadores") await Analisar();
    }
    private async void Analisar_Click(object sender, RoutedEventArgs e) => await Analisar();
    private void Cancelar_Click(object sender, RoutedEventArgs e) => _cancelamento?.Cancel();
    private void Ocupado(bool valor)
    {
        Filtros.IsEnabled = BtnAnalisar.IsEnabled = BtnLimpar.IsEnabled = Discos.IsEnabled = !valor;
        ListaLimpeza.IsEnabled = !valor;
        BtnCancelar.IsEnabled = valor;
    }
    public async Task Analisar()
    {
        if (_cancelamento != null) return;
        if (App.CapturaArquivo != null) Secao("Projetos");
        if (App.CapturaArquivo != null && App.CapturaAba?.Equals("limpeza-windows", StringComparison.OrdinalIgnoreCase) == true) Secao("Windows");
        using var cancelamento = new CancellationTokenSource();
        _cancelamento = cancelamento;
        var ct = cancelamento.Token;
        Ocupado(true);
        var tempo = Stopwatch.StartNew();
        var progresso = new Progress<string>(p => TxtResultadoLimpeza.Text = "Medindo: " + p);
        try
        {
            TxtResultadoLimpeza.Text = "Medindo...";
            if (_secao == "Projetos")
            {
                var lista = await LimpezaProjetos.Analisar(Configuracoes.Atual.PastasProjetosValidas(), ct, progresso);
                _sobras.Clear();
                var itens = lista.Select(s =>
                {
                    var item = new ItemLimpeza { Nome = $"{Path.GetFileName(s.Projeto)} • {s.Tipo} • {Path.GetFileName(s.Pasta)}", Descricao = $"{s.Pasta}\nRecriar: {s.ComoRecriar}", Bytes = s.Bytes, Marcado = false };
                    _sobras[item] = s;
                    return item;
                }).ToList();
                Mostrar(itens);
            }
            else if (_secao == "Maiores pastas")
            {
                var raiz = Discos.SelectedItem as string;
                if (raiz == "Escolher pasta") raiz = _pastaEscolhida;
                GradePastas.ItemsSource = raiz == null ? Array.Empty<PastaGrande>() : await MaioresPastas.Analisar(raiz, ct, progresso);
            }
            else
            {
                var itens = await Task.Run(() => _secao == "Windows" ? Limpeza.Itens() : _secao == "Caches" ? CachesDesenvolvimento.Itens() : CachesNavegadores.Itens());
                foreach (var item in itens)
                {
                    if (ct.IsCancellationRequested) break;
                    if (item.Processo != null)
                    {
                        item.Disponivel = await Task.Run(() => CachesNavegadores.Fechado(item.Processo));
                        if (!item.Disponivel) item.Descricao = $"Feche o {(item.Processo == "chrome" ? "Chrome" : "Edge")} para limpar";
                    }
                    item.Bytes = await Task.Run(() => item.EhLixeira ? Limpeza.Medir(item) : item.Pastas.Sum(p => ArvoreDescartavel.Percorrer(p, false, ct, f => Limpeza.Velho(f, item))));
                }
                Mostrar(itens);
            }
            if (!ct.IsCancellationRequested && _secao != "Maiores pastas") ResumoOtimizacao.Atualizar(_secao, _itens.Where(i => i.Disponivel).Sum(i => Math.Max(0, i.Bytes)));
            TxtResultadoLimpeza.Text = ct.IsCancellationRequested ? "Análise cancelada. Resultados podem estar incompletos." : $"Análise concluída em {tempo.Elapsed.TotalSeconds:0.00} s. {_itens.Count} item(ns).";
            Total();
        }
        catch { TxtResultadoLimpeza.Text = "Não foi possível concluir a análise."; }
        finally { _cancelamento = null; Ocupado(false); }
    }
    private async void Limpar_Click(object sender, RoutedEventArgs e)
    {
        if (_cancelamento != null) return;
        var lista = _itens.Where(i => i.Marcado && i.Disponivel).ToArray();
        if (lista.Length == 0) { TxtResultadoLimpeza.Text = "Marque pelo menos um item."; return; }
        var texto = string.Join("\n", lista.Select(i => "• " + i.Nome + " — " + i.Tamanho + (_sobras.TryGetValue(i, out var s) ? "\n  " + s.Pasta : "")));
        if (MessageBox.Show(Window.GetWindow(this), $"Apagar os itens abaixo?\n\n{texto}\n\nTotal: {Limpeza.Formatar(lista.Sum(i => Math.Max(0, i.Bytes)))}\nFeche o VS Code, o Android Studio e os terminais antes.\nOs arquivos não vão para a Lixeira.", "Confirmar limpeza", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        using var cancelamento = new CancellationTokenSource();
        _cancelamento = cancelamento;
        var resultado = new ResultadoLimpeza();
        Ocupado(true);
        try
        {
            long bytes = await Task.Run(() => lista.Sum(i => _sobras.TryGetValue(i, out var sobra)
                ? LimpezaProjetos.Limpar(sobra, cancelamento.Token, resultado) : Limpeza.Limpar(i, cancelamento.Token, resultado)));
            await Task.Run(() => Diario.Registrar("limpeza", "Itens limpos: " + string.Join(", ", lista.Select(i => i.Nome)), bytes));
            _cancelamento = null;
            if (!cancelamento.IsCancellationRequested) await Analisar();
            await Historico();
            TxtResultadoLimpeza.Text = $"{Limpeza.Formatar(bytes)} liberados. {resultado.NaoApagados} arquivos estavam em uso e ficaram."
                + (cancelamento.IsCancellationRequested ? " Limpeza cancelada." : "");
            EspacoMudou?.Invoke();
        }
        finally { _cancelamento = null; Ocupado(false); }
    }
    private void Discos_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Discos.SelectedItem as string != "Escolher pasta") return;
        var dialogo = new OpenFolderDialog { Title = "Escolha a pasta para analisar" };
        if (dialogo.ShowDialog() == true) { _pastaEscolhida = dialogo.FolderName; Discos.ToolTip = _pastaEscolhida; }
    }
    private async void Abrir_Click(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).DataContext is not PastaGrande pasta) return;
        await Task.Run(() => { try { Process.Start(new ProcessStartInfo(CaminhosSistema.Explorer) { UseShellExecute = true, ArgumentList = { pasta.Pasta } }); } catch { } });
    }

    private async void Componentes_Click(object sender, RoutedEventArgs e)
    {
        if (_limpandoComponentes || !Sistema.EhAdmin()) return;
        if (MessageBox.Show(Window.GetWindow(this), "Limpar componentes antigos do Windows?\n\nRemove versões antigas de atualizações já instaladas. Pode levar de 10 a 30 minutos e não dá para cancelar no meio.",
            "Confirmar limpeza de componentes", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        _limpandoComponentes = true;
        BtnComponentes.IsEnabled = false;
        var relogio = Stopwatch.StartNew();
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        void MostrarTempo() => TxtComponentes.Text = $"Em andamento... {Formatos.Duracao(relogio.Elapsed)}";
        timer.Tick += (_, _) => MostrarTempo();
        MostrarTempo();
        timer.Start();
        try
        {
            var resultado = await LimpezaComponentes.Executar();
            TxtComponentes.Text = $"Concluído em {Formatos.Duracao(relogio.Elapsed)} · código: {resultado.Codigo?.ToString() ?? "indisponível"}\n{resultado.Saida}";
        }
        finally
        {
            timer.Stop();
            _limpandoComponentes = false;
            BtnComponentes.IsEnabled = Sistema.EhAdmin();
        }
    }
}
