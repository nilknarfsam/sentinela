# Painel do PC

Painel leve para Windows que usa o **Windows Defender** como motor. Ele não é um antivírus próprio: mostra o estado do Defender e pede a ele as ações.

## O que faz
- **Início:** mostra se o PC está protegido, as proteções do Defender, a data das assinaturas, a última varredura, o uso de CPU e memória ao vivo e o espaço dos discos.
- **Varredura:** faz a varredura rápida ou completa, com tempo decorrido e opção de cancelar. Avisa na bandeja quando termina.
- **Ameaças e quarentena:** mostra o histórico de detecções e o que está na quarentena, e pede ao Defender para tratar as ameaças ativas.
- **Otimização:** limpa temporários (só arquivos com mais de 24 h), o cache do Windows Update, os relatórios de erro e a Lixeira, sempre com confirmação.
- **Inicialização:** mostra o que abre com o Windows. Para ligar ou desligar, abre o Gerenciador de Tarefas.

O painel **nunca desliga nem muda** configurações de segurança do Defender.

## Instalar
1. Compile (precisa do .NET 9 SDK):
   ```
   cd PainelPC
   dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ..\publicado
   ```
2. Clique com o botão direito em `instalar-painel.bat` e escolha **Executar como administrador**.

Ele copia o programa para `C:\Program Files\PainelPC`, cria duas tarefas agendadas (`PainelPC`, que abre na bandeja ao entrar no Windows, e `PainelPC-Abrir`, usada pelo atalho) e coloca o atalho **Painel do PC** no menu Iniciar. As tarefas rodam como administrador, sem mostrar o aviso do Windows.

Para remover, execute `desinstalar-painel.bat` como administrador.

## Teste sem abrir a janela
```
PainelPC.exe --captura tela.png --aba inicio|varredura|ameacas|limpeza|inicializacao
```
Salva uma imagem da aba escolhida e fecha.

## Estrutura
- `PainelPC/Servicos/Defender.cs`: lê o status pelo WMI e roda varredura, atualização e quarentena pelo `MpCmdRun.exe`.
- `PainelPC/Servicos/Sistema.cs`: CPU, memória, discos e tempo ligado.
- `PainelPC/Servicos/Limpeza.cs`: medição e limpeza de arquivos descartáveis.
- `PainelPC/Servicos/Inicializacao.cs`: programas que abrem com o Windows (só leitura).
- `PainelPC/MainWindow.xaml`: a interface.
- `PainelPC/App.xaml.cs`: instância única e ícone na bandeja.
