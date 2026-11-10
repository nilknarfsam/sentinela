using Sentinela.Servicos;
using System.Buffers.Binary;
using Microsoft.Win32;

namespace Sentinela.Testes;

public class InicializacaoTestes
{
    [Fact]
    public void AtivoPeloValor_NuloEstaAtivo() => Assert.True(Inicializacao.AtivoPeloValor(null));

    [Fact]
    public void AtivoPeloValor_VazioEstaAtivo() => Assert.True(Inicializacao.AtivoPeloValor(Array.Empty<byte>()));

    [Theory]
    [InlineData(0x00, true)]
    [InlineData(0x01, false)]
    [InlineData(0x02, true)]
    [InlineData(0x03, false)]
    [InlineData(0x06, true)]
    [InlineData(0x07, false)]
    public void AtivoPeloValor_UsaParidadeDoPrimeiroByte(byte primeiro, bool esperado)
        => Assert.Equal(esperado, Inicializacao.AtivoPeloValor(new byte[] { primeiro, 0xFF, 0x00 }));

    [Fact]
    public void AtivoPeloValor_StringEstaAtiva() => Assert.True(Inicializacao.AtivoPeloValor("valor"));

    [Fact]
    public void ValorStartupApproved_LigadoTemDozeBytesEZeroDepoisDoEstado()
        => Assert.Equal(new byte[] { 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, Inicializacao.ValorStartupApproved(true, DateTime.UnixEpoch));

    [Fact]
    public void ValorStartupApproved_DesligadoGuardaFiletimeUtcLittleEndian()
    {
        var agora = new DateTime(2026, 11, 10, 15, 23, 41, DateTimeKind.Utc).AddTicks(1234);
        var valor = Inicializacao.ValorStartupApproved(false, agora);
        Assert.Equal(12, valor.Length);
        Assert.Equal(new byte[] { 3, 0, 0, 0 }, valor[..4]);
        Assert.Equal(agora.ToFileTimeUtc(), BinaryPrimitives.ReadInt64LittleEndian(valor.AsSpan(4)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void StartupApproved_IdaEVolta(bool ligado)
        => Assert.Equal(ligado, Inicializacao.AtivoPeloValor(Inicializacao.ValorStartupApproved(ligado, DateTime.UnixEpoch)));

    [Theory]
    [InlineData("\"C:\\Program Files\\App\\app.exe\" --silencioso", @"C:\Program Files\App\app.exe")]
    [InlineData(@"C:\Program Files\App\app.exe --silencioso", @"C:\Program Files\App\app.exe")]
    [InlineData("rundll32.exe \"C:\\Program Files\\App\\app.dll\",Iniciar", @"C:\Program Files\App\app.dll")]
    [InlineData(@"C:\Windows\System32\rundll32.exe C:\App\app.dll,Iniciar", @"C:\App\app.dll")]
    [InlineData(@"C:\Inicializar\Meu App.lnk", @"C:\Inicializar\Meu App.lnk")]
    [InlineData("app.exe", null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("\"C:\\App\\app.exe", null)]
    public void ExtrairExecutavel_InterpretaComandos(string comando, string? esperado)
        => Assert.Equal(esperado, Inicializacao.ExtrairExecutavel(comando));

    [Theory]
    [InlineData("%ProgramFiles%", @"\App\app.exe")]
    [InlineData("%windir%", @"\app.exe")]
    public void ExtrairExecutavel_ExpandeAmbiente(string variavel, string sufixo)
        => Assert.Equal(Environment.ExpandEnvironmentVariables(variavel) + sufixo,
            Inicializacao.ExtrairExecutavel($"\"{variavel}{sufixo}\" --iniciar"));

    [Theory]
    [InlineData("Usuário", RegistryHive.CurrentUser, "Run")]
    [InlineData("Todos os usuários", RegistryHive.LocalMachine, "Run")]
    [InlineData("Todos (32 bits)", RegistryHive.LocalMachine, "Run32")]
    [InlineData("Pasta Inicializar", RegistryHive.CurrentUser, "StartupFolder")]
    [InlineData("Pasta Inicializar (todos)", RegistryHive.LocalMachine, "StartupFolder")]
    public void ChaveStartupApproved_MapeiaOrigem(string origem, RegistryHive raiz, string final)
    {
        var chave = Inicializacao.ChaveStartupApproved(origem);
        Assert.Equal(raiz, chave.Raiz);
        Assert.Equal(@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\" + final, chave.Subchave);
    }
}
