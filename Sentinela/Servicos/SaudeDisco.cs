using System.Management;
using Sentinela.Servicos.Checkup;

namespace Sentinela.Servicos;

public sealed record DiscoFisico(string Nome, string Tipo, long TamanhoBytes, string Saude,
    string Operacional, int? DesgastePercent, int? Temperatura, int? TemperaturaMax,
    long? HorasLigado, long? ErrosLeitura, long? ErrosEscrita, bool DadosAvancados);

public static class SaudeDisco
{
    public static Task<IReadOnlyList<DiscoFisico>> Ler(CancellationToken ct = default) => Task.Run<IReadOnlyList<DiscoFisico>>(() =>
    {
        var discos = new List<DiscoFisico>();
        if (ct.IsCancellationRequested) return discos.AsReadOnly();
        try
        {
            var admin = Sistema.EhAdmin();
            var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage",
                new ConnectionOptions { Timeout = TimeSpan.FromSeconds(2) });
            using var busca = new ManagementObjectSearcher(scope,
                new ObjectQuery("SELECT ObjectId, FriendlyName, MediaType, BusType, Size, HealthStatus, OperationalStatus FROM MSFT_PhysicalDisk"),
                Opcoes());
            using var itens = busca.Get();
            foreach (ManagementObject item in itens)
            {
                using (item)
                {
                    if (ct.IsCancellationRequested) break;
                    try
                    {
                        var disco = new DiscoFisico(item["FriendlyName"]?.ToString() ?? "Disco sem nome",
                            Tipo(Convert.ToInt32(item["MediaType"]), Convert.ToInt32(item["BusType"])),
                            Convert.ToInt64(item["Size"]), Saude(Convert.ToInt32(item["HealthStatus"])),
                            Operacional(item["OperationalStatus"] as ushort[]), null, null, null, null, null, null, false);
                        if (admin && !ct.IsCancellationRequested)
                        {
                            try
                            {
                                var resultados = LerContadores(item, scope);
                                try
                                {
                                    if (resultados.FirstOrDefault() is { } contador)
                                        disco = disco with
                                        {
                                            DesgastePercent = Desgaste(contador["Wear"]), Temperatura = Temperatura(contador["Temperature"]),
                                            TemperaturaMax = Temperatura(contador["TemperatureMax"]), HorasLigado = Longo(contador["PowerOnHours"]),
                                            ErrosLeitura = Longo(contador["ReadErrorsTotal"]), ErrosEscrita = Longo(contador["WriteErrorsTotal"]),
                                            DadosAvancados = true
                                        };
                                }
                                finally { foreach (var contador in resultados) contador.Dispose(); }
                            }
                            catch { /* O estado básico continua disponível mesmo sem contadores. */ }
                        }
                        discos.Add(disco);
                    }
                    catch { /* Um disco inválido não impede a leitura dos demais. */ }
                }
            }
        }
        catch { /* WMI indisponível: retorna o que foi possível ler. */ }
        return discos.AsReadOnly();
    });

    private static System.Management.EnumerationOptions Opcoes() => new() { Timeout = TimeSpan.FromSeconds(2), ReturnImmediately = false };
    private static List<ManagementObject> LerContadores(ManagementObject item, ManagementScope scope)
    {
        try
        {
            using var relacionados = item.GetRelated("MSFT_StorageReliabilityCounter", null, null, null, null, null, false, Opcoes());
            return Materializar(relacionados);
        }
        catch (ManagementException)
        {
            // Alguns provedores não suportam GetRelated. A chave continua escapada no fallback.
            using var busca = new ManagementObjectSearcher(scope,
                new ObjectQuery(ConsultaContadores(item["ObjectId"]?.ToString() ?? "")), Opcoes());
            using var resultados = busca.Get();
            return Materializar(resultados);
        }
    }
    private static List<ManagementObject> Materializar(ManagementObjectCollection colecao)
    {
        var objetos = new List<ManagementObject>();
        try
        {
            foreach (ManagementObject objeto in colecao) objetos.Add(objeto);
            return objetos;
        }
        catch
        {
            foreach (var objeto in objetos) objeto.Dispose();
            throw; // A fronteira Ler trata falhas; ManagementException permite tentar o fallback.
        }
    }
    private static long? Longo(object? valor) { try { return valor == null ? null : Convert.ToInt64(valor); } catch { return null; } }
    private static int? Inteiro(object? valor) { var n = Longo(valor); return n is >= 0 and <= int.MaxValue ? (int)n : null; }
    internal static int? Desgaste(object? valor) { var n = Inteiro(valor); return n is >= 0 and <= 100 ? n : null; }
    internal static int? Temperatura(object? valor) { var n = Inteiro(valor); return n is > 0 ? n : null; }
    internal static string ConsultaContadores(string objectId) =>
        $"ASSOCIATORS OF {{MSFT_PhysicalDisk.ObjectId='{objectId.Replace("\\", "\\\\").Replace("'", "\\'")}'}} WHERE AssocClass = MSFT_PhysicalDiskToStorageReliabilityCounter";
    internal static string Tipo(int mediaType, int busType) => mediaType == 3 ? "HD" :
        mediaType == 4 ? busType == 17 ? "SSD NVMe" : busType == 11 ? "SSD SATA" : busType == 7 ? "SSD USB" : "SSD" : "Desconhecido";
    internal static string Saude(int healthStatus) => healthStatus switch { 0 => "Saudável", 1 => "Com aviso", 2 => "Com defeito", _ => "Desconhecida" };
    internal static string Operacional(ushort[]? estados) => estados is not { Length: > 0 } ? "Desconhecido" :
        string.Join(", ", estados.Select(e => e switch { 2 => "Funcionando", 3 => "Degradado", 5 => "Falha prevista", 6 => "Erro", 8 => "Iniciando", 10 => "Parado", _ => $"Estado {e}" }));

    internal static (NivelVerificacao nivel, string motivo) Avaliar(DiscoFisico d)
    {
        if (d.Saude == "Com defeito") return (NivelVerificacao.Problema, "Disco com defeito: faça backup já");
        if (d.Saude == "Com aviso") return (NivelVerificacao.Problema, "O disco avisou problema: faça backup");
        if (d.DesgastePercent >= 90) return (NivelVerificacao.Problema, $"Desgaste em {d.DesgastePercent}%: faça backup");
        var ssd = d.Tipo.StartsWith("SSD", StringComparison.Ordinal);
        if (ssd && d.Temperatura >= 70) return (NivelVerificacao.Problema, $"Temperatura em {d.Temperatura} °C");
        if (d.DesgastePercent >= 70) return (NivelVerificacao.Atencao, $"Desgaste em {d.DesgastePercent}%");
        if ((ssd && d.Temperatura >= 60) || (d.Tipo == "HD" && d.Temperatura >= 55))
            return (NivelVerificacao.Atencao, $"Temperatura em {d.Temperatura} °C");
        if (d.ErrosLeitura > 0 || d.ErrosEscrita > 0) return (NivelVerificacao.Atencao, "O disco registrou erros de leitura ou escrita");
        return (NivelVerificacao.Ok, "Disco saudável");
    }

    internal static string EstadoTexto(DiscoFisico d) => d.Saude + (d.Operacional == "Funcionando" ? " · Funcionando" : "");
    internal static string Descrever(DiscoFisico d) => $"{d.Nome} · {d.Tipo} · {Limpeza.Formatar(d.TamanhoBytes)} · {EstadoTexto(d)}"
        + (d.Operacional != "Funcionando" ? $" ({d.Operacional})" : "")
        + (d.DesgastePercent is { } desgaste ? $" · desgaste {desgaste}%" : "")
        + (d.Temperatura is { } temperatura ? $" · {temperatura} °C" : "");
}
