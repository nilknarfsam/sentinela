using System.Text.Json;

namespace Sentinela.Servicos;

public sealed class VigiaInicializacao : IDisposable
{
    private readonly object _trava = new();
    private Timer? _timer;
    private bool _descartado;
    private int _lendo;
    private HashSet<string>? _anteriores;
    private readonly string _caminho = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sentinela", "inicializacao.json");
    public event Action<string>? Chegou;

    internal static IReadOnlyList<string> Novos(IEnumerable<string> anteriores, IEnumerable<string> atuais)
    {
        var conhecidos = new HashSet<string>(anteriores, StringComparer.OrdinalIgnoreCase);
        return atuais.Where(a => !conhecidos.Contains(a)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public void Iniciar()
    {
        lock (_trava)
        {
            if (_descartado || _timer != null) return;
            try { _timer = new Timer(_ => Comparar(), null, TimeSpan.Zero, TimeSpan.FromSeconds(60)); }
            catch { /* O vigia não impede a abertura do aplicativo. */ }
        }
    }

    private void Comparar()
    {
        if (Interlocked.Exchange(ref _lendo, 1) != 0) return;
        try
        {
            var programas = Inicializacao.Listar(false, out var programasCompletos);
            var tarefas = InicializacaoSistema.Tarefas(false, out var tarefasCompletas);
            if (!programasCompletos || !tarefasCompletas) return;
            var todos = programas.Select(p => (Chave: $"{p.Origem}|{p.Nome}|{p.Comando}", p.Nome, p.Ativo))
                .Concat(tarefas.Select(t => (Chave: $"{t.Origem}|{t.Nome}|{t.Comando}", t.Nome, Ativo: true))).ToArray();
            ProcessarRetrato(todos);
        }
        catch { /* Uma leitura ou gravação indisponível será tentada de novo em 60 s. */ }
        finally { Volatile.Write(ref _lendo, 0); }
    }

    // Separado do inventário real para testar falha de persistência sem alterar itens do Windows.
    internal void ProcessarRetrato(IEnumerable<(string Chave, string Nome, bool Ativo)> itens,
        Func<string, string?>? ler = null, Func<string, string, bool>? gravar = null,
        Action<string>? registrar = null)
    {
        lock (_trava)
        {
            if (_descartado) return;
            var todos = itens.ToArray();
            if (_anteriores == null)
            {
                try
                {
                    var texto = (ler ?? (c => ArquivosSeguros.LerTexto(c, 1024 * 1024)))(_caminho);
                    if (texto != null) _anteriores = new(JsonSerializer.Deserialize<string[]>(texto) ?? [], StringComparer.OrdinalIgnoreCase);
                }
                catch { /* Retrato ilegível: cria uma base sem avisar. */ }
            }
            var novos = _anteriores == null ? [] : Novos(_anteriores, todos.Where(t => t.Ativo).Select(t => t.Chave));
            // Guarda também os desativados; atualiza a memória mesmo se o disco falhar.
            _anteriores = new(todos.Select(t => t.Chave), StringComparer.OrdinalIgnoreCase);
            foreach (var chave in novos)
            {
                var nome = todos.First(t => StringComparer.OrdinalIgnoreCase.Equals(t.Chave, chave)).Nome;
                var texto = $"{nome} passou a abrir com o Windows. Se não foi você, desligue em Inicialização.";
                try { (registrar ?? (t => Diario.Registrar("inicializacao", t)))(texto); } catch { }
                try { Chegou?.Invoke(texto); } catch { /* Um assinante não interrompe a vigia. */ }
            }
            try { (gravar ?? ArquivosSeguros.GravarTexto)(_caminho, JsonSerializer.Serialize(_anteriores)); }
            catch { /* A próxima comparação usa a memória, sem repetir os avisos. */ }
        }
    }

    public void Dispose()
    {
        lock (_trava) { _descartado = true; _timer?.Dispose(); _timer = null; }
    }
}
