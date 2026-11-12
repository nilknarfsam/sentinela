# Sentinela

**Um painel grátis para Windows que usa o próprio antivírus do Windows (Microsoft
Defender) como motor, e junta só o que funciona de verdade para manter o PC
protegido e leve.**

O Sentinela **não é um antivírus novo** e **não troca o seu antivírus**. Ele
mostra o estado do Defender, avisa na hora quando aparece uma ameaça e pede ao
Defender as ações que ele já sabe fazer: varrer, atualizar e tratar ameaças.
Ele **nunca desliga nem muda** as configurações do Defender, do firewall ou do
Controle de Conta de Usuário.

## Por que ele existe
- O antivírus que já vem no Windows (Microsoft Defender) tem bom desempenho
  nos testes independentes de laboratórios como o
  [AV-TEST](https://www.av-test.org/) e o
  [AV-Comparatives](https://www.av-comparatives.org/). Para muitos usos
  domésticos, ele costuma bastar.
- Muito programa de "deixar o PC mais rápido" é marketing: limpador de
  registro, "turbo de RAM", atualizador automático de drivers.
- O que funciona é simples: menos programas abrindo com o Windows, espaço livre
  no disco, reiniciar quando há atualização pendente, saber o que está pesando e
  ter um disco saudável. O Sentinela junta isso num lugar só, de graça.

## Baixar
A primeira versão pública (2.0.0, com instalador) ainda está sendo preparada.
Quando sair, ela vai aparecer na aba **Releases** deste repositório.

Requisitos: Windows 10 ou 11, 64 bits.

> **Aviso do Windows ("O Windows protegeu o computador"):** por ser um programa
> novo e ainda sem assinatura digital, o SmartScreen pode mostrar esse aviso.
> Clique em **Mais informações** e depois em **Executar assim mesmo**. O código
> está todo aqui, aberto, para quem quiser conferir.

## Privacidade
- **Lê:** o estado do Defender, discos, programas que abrem com o Windows e
  processos em uso.
- **Guarda:** configurações, diário, notas e a lista de referência das
  exclusões do Defender (que pode ter caminhos de pastas suas), **só no seu
  PC**, em `%LOCALAPPDATA%\Sentinela`.
- **Envia:** **nada.** Não tem telemetria, conta nem anúncios.

## Licença
Sentinela. Copyright (C) 2026 Franklin Carvalho.

Este programa é software livre: você pode redistribuí-lo e/ou modificá-lo sob
os termos da **GNU General Public License, versão 3 ou posterior**, publicada
pela Free Software Foundation. Ele é distribuído na esperança de ser útil, mas
**SEM NENHUMA GARANTIA**, nem mesmo a garantia implícita de COMERCIALIZAÇÃO ou
ADEQUAÇÃO A UM PROPÓSITO ESPECÍFICO. Veja o arquivo [LICENSE](LICENSE).

## Criador
Feito por **Franklin Carvalho**, com ajuda de IA (Claude e Codex), para o
próprio PC, e compartilhado de graça.
[GitHub](https://github.com/nilknarfsam) ·
[Site](https://samfieldsoak.com.br) ·
[Instagram](https://www.instagram.com/samfieldsoak) ·
[LinkedIn](https://www.linkedin.com/in/franklin-carvalho-45032a164/)

Encontrou um problema? [Relate aqui](https://github.com/nilknarfsam/sentinela/issues/new/choose).
Quer contribuir? Veja o [CONTRIBUTING.md](CONTRIBUTING.md).

---

# Guia de uso

## O que faz
- **Início:** se o PC está protegido, as proteções do Defender, a data das assinaturas, a última varredura, CPU e memória ao vivo e o espaço dos discos. O escudo da bandeja fica verde, âmbar ou vermelho.
- **Segurança:** oito verificações da configuração do Windows, nota de 0 a 100 e atalhos para resolver. Só leitura, com alerta para exclusões novas do Defender e reconhecimento após confirmação. Último resultado no Início e verificação pela bandeja. A lista de referência das exclusões fica na pasta do usuário; o alerta é um aviso a mais, não uma garantia.
- **Aviso na hora:** escuta o próprio Windows Defender e avisa em segundos (mesmo com a janela fechada) quando uma ameaça é encontrada, quando a proteção em tempo real é desligada ou quando aparece uma exclusão ou um desligamento suspeito.
- **Varredura:** rápida, completa, de uma pasta ou arquivo escolhido, e offline (reinicia e verifica antes do Windows carregar). Mostra o tempo e uma estimativa baseada nas últimas varreduras.
- **Clique direito:** "Verificar com o Sentinela" no Explorer (no Windows 11, em "Mostrar mais opções").
- **Ameaças e quarentena:** histórico de detecções, quarentena em lista, **Restaurar** com dupla confirmação e atalho para excluir na Segurança do Windows.
- **Histórico:** eventos do Defender e do Sentinela dos últimos 30 dias, com filtros.
- **Otimização:** limpa temporários (só arquivos com mais de 24 h), o cache do Windows Update, os relatórios de erro e a Lixeira, sempre com confirmação.
- **Inicialização:** mostra o que abre com o Windows. Permite ligar ou desligar itens compatíveis com confirmação, mostra tarefas e serviços de terceiros e avisa quando surge um item novo.
- **Ajustes:** iniciar com o Windows, manter na bandeja ao fechar, avisos de rotina, diário e versão.

O Sentinela **nunca desliga nem muda** configurações de segurança do Defender.

**Desempenho:** checkup com nota de 0 a 100, último resultado salvo e cartão no Início. Também pode ser iniciado pela bandeja. O Histórico tem filtro de desempenho e os avisos de rotina incluem lembrete diário de reinício após 7 dias ligado com atualização pendente. Veja `PLANO-MELHORIAS.md`. Histórico de versões em `CHANGELOG.md`.

## Sobre e ajuda (versão 1.7.0 em preparação)

Em **Ajustes → Sobre e ajuda** há uma apresentação do Sentinela, a versão, uma explicação breve de cada área e as informações do diário e do executável. Os botões abrem os perfis autorizados do criador no GitHub, site, Instagram e LinkedIn. Dentro do app, a página do Sentinela no site, o atalho para relatar problema e a privacidade detalhada ainda aparecem como **em breve**. Enquanto isso, use os [problemas do GitHub](https://github.com/nilknarfsam/sentinela/issues/new/choose) e a seção **Privacidade** no topo deste arquivo.

## Preparação da versão 1.6.0

Na primeira execução, a lista de pastas de projetos começa vazia, a menos que `C:\PROJETOS` exista. Configure as pastas em **Ajustes → Pastas de projetos**. A limpeza do temporário do Windows e a leitura do BitLocker acompanham a unidade onde o Windows está instalado.

## Rotina e relatório semanal (v1.5.0)

Em **Ajustes → Varredura completa semanal**, ligue ou desligue a rotina e escolha o dia e a hora (padrão: domingo às 12h). O Sentinela precisa estar aberto, inclusive só na bandeja. Ele confere a cada 5 minutos, sem criar tarefa no Agendador nem mudar a configuração do Defender. Na primeira hora após o horário, pode começar enquanto você usa o PC; depois disso, espera 10 minutos sem uso. Se a varredura falhar ou for cancelada, não tenta novamente no mesmo ciclo semanal. Na primeira execução da versão 1.5.0 e ao ligar ou alterar o dia/hora, a rotina passa a considerar o momento da alteração, sem recuperar um horário anterior. O progresso e o botão Cancelar ficam na aba **Varredura**.

No topo do **Histórico**, **Relatório da semana** permite escolher **Semana passada** (segunda a domingo) ou **Últimos 7 dias**, abrir os detalhes e **Exportar em HTML** (até 2 MB; somente .html/.htm em unidade local, fora da raiz e de pastas protegidas do Windows e dos programas; aceita pastas de nuvem, mas recusa links e junções). O relatório reúne o diário e as detecções do Defender no período, conciliadas por nome e proximidade de até dois minutos, além do estado atual da proteção, com as datas das últimas notas. Abra o arquivo no navegador para imprimir ou salvar como PDF; não há exportação nativa para PDF.

Na segunda-feira, a partir das 9h, ou quando o Sentinela voltar a rodar naquela semana, um aviso mostra o resumo da semana anterior. Clicar abre o Histórico. O aviso só aparece se houver registros no período e se **Avisos de rotina** estiver ligado; o relatório continua disponível com os avisos desligados.

## Verificar uma pasta ou arquivo

Na aba **Varredura**, use **Escolher pasta** ou **Escolher arquivo**, ou arraste um único item para a página quando o app estiver sem elevação. Como administrador, o Windows bloqueia arrastar do Explorer: use os botões ou o clique direito. O Sentinela mostra o caminho em andamento e registra o resultado no histórico.

Depois da instalação, clique com o botão direito em uma pasta ou arquivo e escolha **Verificar com o Sentinela**. No Windows 11, a opção aparece em **Mostrar mais opções**. O pedido é encaminhado ao Sentinela aberto, inclusive quando ele roda como administrador. A caixa de pedidos só funciona quando o administrador é a mesma conta do usuário; elevar com credenciais de outra conta usa outra pasta de pedidos. Se estiver fechado, ele abre e inicia a verificação.

## Instalar
Quando a etapa 8.3 for aprovada, a forma recomendada será instalar `Sentinela.Instalador.msi` com duplo clique. Ele instala o Sentinela por máquina em `C:\Program Files\Sentinela`, cria as tarefas, o atalho e os menus de clique direito, e aparece em **Configurações → Aplicativos**. As configurações em `%LOCALAPPDATA%\Sentinela` podem permanecer depois da desinstalação.

Ao atualizar com dois cliques sem elevação, o instalador pode não fechar o Sentinela aberto como administrador e mostrar um aviso de arquivo em uso; feche o Sentinela antes de atualizar.

Para compilar o instalador (precisa do .NET 9 SDK e de acesso ao NuGet para restaurar o WiX):
   ```
   dotnet build Sentinela.Instalador -c Release
   ```

Antes de instalar o MSI pela primeira vez num PC que usa o instalador antigo, execute `desinstalar-sentinela.bat` como administrador. Depois, abra o MSI e confirme a instalação quando o Windows pedir. O arquivo `instalar-sentinela.bat` continua disponível como **modo antigo** enquanto a migração é validada.

O build salva o MSI em `Sentinela.Instalador\bin\x64\Release\Sentinela.Instalador.msi`.

Para remover a versão MSI, use **Configurações → Aplicativos → Sentinela → Desinstalar**. A desinstalação remove as tarefas, o atalho, os menus de clique direito e os arquivos do programa.

## Teste sem abrir a janela
```
Sentinela.exe --captura tela.png --aba inicio|seguranca|desempenho|varredura|ameacas|historico|limpeza|limpeza-windows|inicializacao|ajustes|ajustes-sobre
```
Salva uma imagem da aba escolhida e fecha. O alias `ajustes-sobre` seleciona a subaba **Sobre e ajuda** para a captura. Para registrar o tamanho padrão ou mínimo da janela, passe também `--captura-largura 1100 --captura-altura 720` ou `--captura-largura 900 --captura-altura 600`. As dimensões precisam ser informadas juntas, só são aceitas com `--captura` e ficam limitadas a 900–3000 × 600–2000. Capturas com tamanho explícito mostram a área visível e a rolagem normal; sem essas opções, Segurança, Desempenho e Ajustes continuam sendo capturados por inteiro, como antes. A execução normal mantém o tamanho padrão da janela.

## Testes

Na raiz do projeto, execute os testes automáticos com o .NET 9 SDK:

```
dotnet test
```

## Estrutura
- `Sentinela/Servicos/Defender.cs`: lê o status pelo WMI e roda varredura, atualização e quarentena pelo `MpCmdRun.exe`.
- `Sentinela/Servicos/Sistema.cs`: CPU, memória, discos e tempo ligado.
- `Sentinela/Servicos/Limpeza.cs`: medição e limpeza de arquivos descartáveis.
- `Sentinela/Servicos/Inicializacao.cs`: programas que abrem com o Windows.
- `Sentinela/MainWindow.xaml`: a interface.
- `Sentinela/App.xaml.cs`: instância única e ícone na bandeja.

## Para quem programa
As regras de trabalho e de segurança para agentes de IA (Codex) estão em `AGENTS.md`.
