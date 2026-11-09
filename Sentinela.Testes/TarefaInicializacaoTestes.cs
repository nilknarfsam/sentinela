using System.Xml;
using Sentinela.Servicos;

namespace Sentinela.Testes;

public class TarefaInicializacaoTestes
{
    [Theory]
    [InlineData("<Task><Settings><Enabled>true</Enabled></Settings></Task>", true)]
    [InlineData("<Task><Settings><Enabled>false</Enabled></Settings></Task>", false)]
    [InlineData("<Task><Settings /></Task>", true)]
    [InlineData("<Task />", true)]
    [InlineData("<Task><Settings><Enabled> FALSE </Enabled></Settings></Task>", false)]
    [InlineData("<Task xmlns='http://schemas.microsoft.com/windows/2004/02/mit/task'><Settings><Enabled>true</Enabled></Settings></Task>", true)]
    [InlineData("<Task xmlns='http://schemas.microsoft.com/windows/2004/02/mit/task'><Settings><Enabled>false</Enabled></Settings></Task>", false)]
    [InlineData("<Task xmlns='http://schemas.microsoft.com/windows/2004/02/mit/task'><Settings /></Task>", true)]
    public void InterpretarXml_LeHabilitacao(string xml, bool esperado)
        => Assert.Equal(esperado, TarefaInicializacao.InterpretarXml(xml));

    [Theory]
    [InlineData("<Task>")]
    [InlineData("")]
    [InlineData("não é XML")]
    public void InterpretarXml_InvalidoLanca(string xml)
        => Assert.Throws<XmlException>(() => TarefaInicializacao.InterpretarXml(xml));
}
