# Sentinela

Antes se chamava Painel do PC. O instalador remove a versão antiga sozinho.

Sentinela é um painel leve para Windows que usa o **Windows Defender** como motor. Ele não é um antivírus próprio: mostra o estado do Defender e pede a ele as ações.

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

## Verificar uma pasta ou arquivo

Na aba **Varredura**, use **Escolher pasta** ou **Escolher arquivo**, ou arraste um único item para a página quando o app estiver sem elevação. Como administrador, o Windows bloqueia arrastar do Explorer: use os botões ou o clique direito. O Sentinela mostra o caminho em andamento e registra o resultado no histórico.

Depois da instalação, clique com o botão direito em uma pasta ou arquivo e escolha **Verificar com o Sentinela**. No Windows 11, a opção aparece em **Mostrar mais opções**. O pedido é encaminhado ao Sentinela aberto, inclusive quando ele roda como administrador. A caixa de pedidos só funciona quando o administrador é a mesma conta do usuário; elevar com credenciais de outra conta usa outra pasta de pedidos. Se estiver fechado, ele abre e inicia a verificação.

## Instalar
1. Compile (precisa do .NET 9 SDK):
   ```
   cd Sentinela
   dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ..\publicado
   ```
2. Clique com o botão direito em `instalar-sentinela.bat` e escolha **Executar como administrador**.

Ele copia o programa para `C:\Program Files\Sentinela`, cria duas tarefas agendadas (`Sentinela`, que abre na bandeja ao entrar no Windows, e `Sentinela-Abrir`, usada pelo atalho) e coloca o atalho **Sentinela** no menu Iniciar. As tarefas rodam como administrador, sem mostrar o aviso do Windows.

Para remover, execute `desinstalar-sentinela.bat` como administrador.

## Teste sem abrir a janela
```
Sentinela.exe --captura tela.png --aba inicio|seguranca|desempenho|varredura|ameacas|historico|limpeza|limpeza-windows|inicializacao|ajustes
```
Salva uma imagem da aba escolhida e fecha.

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
