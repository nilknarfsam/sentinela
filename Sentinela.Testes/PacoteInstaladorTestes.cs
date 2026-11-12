using System.Xml.Linq;

namespace Sentinela.Testes;

public class PacoteInstaladorTestes
{
    [Fact]
    public void FecharSentinela_EImediataESequenciadaDepoisDeInstallValidate()
    {
        var caminhoPacote = LocalizarPacote();
        var documento = XDocument.Load(caminhoPacote);
        XNamespace ns = "http://wixtoolset.org/schemas/v4/wxs";

        var acao = documento.Descendants(ns + "CustomAction")
            .Single(elemento => (string?)elemento.Attribute("Id") == "FecharSentinela");
        Assert.Equal("immediate", (string?)acao.Attribute("Execute"));
        Assert.Null(acao.Attribute("Impersonate"));
        Assert.Equal("ignore", (string?)acao.Attribute("Return"));

        var pacote = documento.Root!.Element(ns + "Package")!;
        var sequenciaInstalacao = pacote.Element(ns + "InstallExecuteSequence")!;
        var sequencia = sequenciaInstalacao.Elements(ns + "Custom")
            .Single(elemento => (string?)elemento.Attribute("Action") == "FecharSentinela");
        Assert.Equal("InstallValidate", (string?)sequencia.Attribute("After"));
        Assert.Null(sequencia.Attribute("Before"));
        Assert.Null(sequencia.Attribute("Sequence"));

        Assert.DoesNotContain(sequenciaInstalacao.Elements(), elemento =>
            elemento.Name.LocalName is "InstallValidate" or "InstallInitialize" or "RemoveExistingProducts");
        Assert.Equal("afterInstallInitialize",
            (string?)pacote.Element(ns + "MajorUpgrade")!.Attribute("Schedule"));
    }

    private static string LocalizarPacote()
    {
        var diretorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (diretorio is not null)
        {
            var caminho = Path.Combine(diretorio.FullName, "Sentinela.Instalador", "Pacote.wxs");
            if (File.Exists(caminho))
            {
                return caminho;
            }

            diretorio = diretorio.Parent;
        }

        throw new FileNotFoundException("Não foi possível localizar Sentinela.Instalador/Pacote.wxs subindo da pasta dos testes.");
    }
}
