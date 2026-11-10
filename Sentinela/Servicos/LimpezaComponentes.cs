using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Sentinela.Servicos;

public sealed record ResultadoComponentes(int? Codigo, string Saida);

public static class LimpezaComponentes
{
    static LimpezaComponentes() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    // Sem ResetBase: preserva a possibilidade de desinstalar as atualizações atuais.
    internal static ProcessStartInfo CriarInicio(string? executavel = null, string? argumentos = null) => new(
        executavel ?? CaminhosSistema.Dism, argumentos ?? "/Online /Cleanup-Image /StartComponentCleanup")
    {
        UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
        StandardOutputEncoding = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage),
        StandardErrorEncoding = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage)
    };

    public static Task<ResultadoComponentes> Executar() => ExecutarInterno(null, null);

    // Ponto de substituição interno: os testes nunca iniciam o DISM.
    internal static Task<ResultadoComponentes> ExecutarInterno(string? executavel, string? argumentos) => Task.Run(async () =>
    {
        ResultadoComponentes resultado;
        try
        {
            if (executavel == null && !Sistema.EhAdmin()) return new ResultadoComponentes(null, "Abra como administrador para limpar componentes.");
            using var processo = new Process { StartInfo = CriarInicio(executavel, argumentos) };
            processo.Start();
            var saida = LerFim(processo.StandardOutput);
            var erro = LerFim(processo.StandardError);
            // Não há cancelamento: interromper a manutenção do Windows não é seguro.
            await processo.WaitForExitAsync().ConfigureAwait(false);
            resultado = new(processo.ExitCode, ((await saida.ConfigureAwait(false)) + "\n" + (await erro.ConfigureAwait(false))).Trim());
        }
        catch (Exception ex) { resultado = new(null, $"Não foi possível concluir: {ex.Message}"); }
        Diario.Registrar("limpeza", $"Componentes do Windows: código {resultado.Codigo?.ToString() ?? "indisponível"}. {resultado.Saida}");
        return resultado;
    });

    private static async Task<string> LerFim(StreamReader leitor)
    {
        var fim = new StringBuilder();
        var buffer = new char[2048];
        int lidos;
        while ((lidos = await leitor.ReadAsync(buffer).ConfigureAwait(false)) > 0)
        {
            fim.Append(buffer, 0, lidos);
            if (fim.Length > 4000) fim.Remove(0, fim.Length - 4000);
        }
        return fim.ToString();
    }
}
