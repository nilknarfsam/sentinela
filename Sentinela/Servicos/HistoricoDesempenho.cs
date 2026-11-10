namespace Sentinela.Servicos;

public sealed record PontoDesempenho(DateTime Instante, double CpuPercent, double MemoriaPercent, bool IniciaSegmento = false);

/// <summary>Usado pela janela, apenas enquanto visível. Retém até uma hora.</summary>
public sealed class HistoricoDesempenho
{
    private readonly Queue<PontoDesempenho> _pontos = new();
    public int Quantidade => _pontos.Count;
    public IReadOnlyList<PontoDesempenho> Pontos => _pontos.ToArray();

    public void Adicionar(DateTime instante, double cpu, double memoria)
    {
        while (_pontos.TryPeek(out var antigo) && instante - antigo.Instante >= TimeSpan.FromHours(1)) _pontos.Dequeue();
        // O DispatcherTimer pode adiantar alguns milissegundos; não perder um ciclo inteiro.
        if (_pontos.TryPeek(out _) && instante - _pontos.Last().Instante < TimeSpan.FromMilliseconds(4950)) return;
        while (_pontos.Count >= 720) _pontos.Dequeue();
        var novoSegmento = _pontos.Count == 0 || instante - _pontos.Last().Instante > TimeSpan.FromSeconds(15);
        _pontos.Enqueue(new(instante, Math.Clamp(cpu, 0, 100), Math.Clamp(memoria, 0, 100), novoSegmento));
    }
}
