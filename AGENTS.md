# AGENTS.md — Regras para o Codex no Sentinela

Este arquivo é para o **Codex (ChatGPT)** quando o Franklin trabalha com ele
diretamente, sem o Claude no meio. Ele resume as regras e o jeito de trabalhar que
usamos desde a versão 1.0. O Claude (Opus) continua fazendo a **conferência
final** de cada etapa.

Leia este arquivo inteiro antes de qualquer tarefa. Leia também
`CONFERENCIAS.md` (correções pedidas pelo Claude: **resolva os itens abertos
antes de qualquer etapa nova**), `PLANO-FASE-8.md`, `LEIAME.md`, `CHANGELOG.md`
e `PLANO-MELHORIAS.md`. Se `../CLAUDE.md` existir (na pasta
`C:\PROJETOS\Meu PC`), ele também vale: as regras técnicas e de segurança são
as mesmas.

Tudo em **português do Brasil**: nomes de classes, métodos, variáveis,
mensagens, comentários, commits e documentação.

---

## 1. O que é o Sentinela

App C# **WPF .NET 9** (`Nullable` ligado) que usa o **Windows Defender como
motor**. Ele roda **como administrador** e fica na bandeja.

| Pasta | O que tem |
| :--- | :--- |
| `Sentinela/Servicos/` | Toda a lógica: Defender, limpeza, checkup, segurança, rotinas, relatório... |
| `Sentinela/Paginas/` | Uma página por aba (`PaginaX.xaml` + `.xaml.cs`) |
| `Sentinela/App.xaml` | **Todas** as cores e estilos |
| `Sentinela.Testes/` | Testes xUnit (mais de 700). Funções puras `internal static` testáveis |
| `.github/workflows/compilar.yml` | Compila, testa e publica o `.exe` a cada push |
| `instalar-sentinela.bat/.ps1` | Instalador atual (será substituído pelo MSI na Fase 8) |

---

## 2. Regras de segurança (não negociáveis)

O Sentinela roda como administrador. Um erro aqui vira uma brecha no PC inteiro.

1. **Nunca** desligar, enfraquecer ou mudar configurações do Windows Defender, do
   firewall ou do UAC. Nada de `Set-MpPreference`, `Add-MpPreference`,
   `netsh advfirewall`, nem escrita nas chaves deles. O Sentinela **só lê** e pede ao
   Defender as ações que ele oferece: varrer, atualizar assinaturas, tratar
   ameaças. Para mudar algo, ele **abre a página certa do Windows**.
2. Programa que roda como admin fica em `C:\Program Files\`, nunca numa pasta que
   o usuário comum pode alterar.
3. Arquivos nas pastas do usuário só via **`ArquivosSeguros`**, que não segue
   links nem junções e tem limite de tamanho. Executáveis do Windows só via
   **`CaminhosSistema`**, com o caminho completo e nunca pelo PATH. Apagar só
   depois da trava **`PastaProibidaParaApagar`**.
4. Apagar arquivo só com **confirmação na tela** e só em pastas descartáveis
   conhecidas. Nunca seguir junctions ou atalhos de pasta.
5. Botões "Como resolver" abrem apenas alvos de uma **lista branca fechada**
   (`AlvoPermitido`). Nada vindo de arquivo ou do sistema abre sem passar por ela.
6. Texto que vem do sistema (nome de ameaça, caminho, diário) é **não confiável**:
   escape em HTML (`WebUtility.HtmlEncode`) e nunca monte linha de comando
   concatenando. Use `ArgumentList`.
7. O temporário vem de `GetFolderPath` / `Path.GetTempPath` do processo, **nunca**
   da variável `TEMP` lida como confiável para apagar.
8. **Sem telemetria.** O Sentinela não envia nada do usuário para a internet. A
   única conexão permitida é a do sistema de atualização (seção 6), só para
   consultar e baixar versões.
9. **Nenhum segredo no código** nem no repositório: token, senha, chave,
   certificado. O e-mail do Franklin não entra. A única exceção para dados
   dele é a tela "Sobre e ajuda": nome, GitHub, site, Instagram e LinkedIn
   (`PLANO-FASE-8.md`, etapa 8.2). O nome "Franklin Carvalho" foi confirmado
   pelo Franklin em 11/11/2026 e só pode aparecer nessa tela. O LinkedIn
   informado nessa data é `https://www.linkedin.com/in/franklin-carvalho-45032a164/`
   e também fica restrito à tela "Sobre e ajuda".
10. Scripts que mudam o sistema (`.bat` + `.ps1`) são entregues para o Franklin
    **executar como administrador**. O Codex cria e testa com `-Simular`, mas não
    executa a parte que altera. O `.ps1` vai **sem acentos**, porque o PowerShell
    5.1 lê errado. O `.bat` confere o administrador com `net session`, pede
    confirmação S/N e salva o log na Área de Trabalho.

---

## 3. Regras de código

- Lógica em `Servicos/`, interface em `Paginas/` e `MainWindow`. Cada aba é uma página.
- **Cores e estilos só no `App.xaml`.** Nada de cor fixa espalhada nos controles.
- Serviços que tocam disco, processo, WMI ou rede **nunca lançam exceção** e
  **rodam fora da thread da tela** (`Task.Run`). Falha vira um resultado
  "indisponível" com texto para leigo.
- Separe **leitura** (I/O) de **avaliação**: um `internal static Avaliar(...)` puro,
  com os dados como parâmetros, para os testes.
- COM liberado com `Marshal.FinalReleaseComObject`. `Process` sempre com `using`.
- **Nenhum pacote NuGet novo sem perguntar ao Franklin.** Hoje: o app usa só
  `System.Management`; os testes usam xunit, xunit.runner.visualstudio e
  Microsoft.NET.Test.Sdk. O WiX (Fase 8) também precisa do sim dele antes.
- Textos para leigos: "Varredura rápida", não "Quick scan". Sem jargão.
- Preservar serviços, eventos, bindings, comandos, comportamento assíncrono,
  permissões e confirmações que já existem. Mudança visual não pode mudar
  comportamento sem pedido.
- Não executar varredura, limpeza, restauração, exclusão, reinício ou
  encerramento de processos só para conferir o visual. Use o **modo captura**.
- Nada de pastas de teste ou capturas dentro do repositório: use `%TEMP%`.

---

## 4. Como trabalhar (passo a passo de toda tarefa)

Preferência do Franklin para o custo (11/11/2026): o agente principal planeja e
confere, **Lua (`gpt-6-luna`)** escreve, **Terra (`gpt-6.1-terra`)** revisa o
diff, só lendo. Não troque por modelos mais caros sem avisar.

1. **Conferir o Git.** `git status` limpo e numa **branch de trabalho** com nome
   claro: `fase-7/tema-claro`, `fase-8/instalador-msi`, `fase-8/atualizacao`...
   Nunca trabalhe direto na `main`. Se houver mudança local de outra tarefa,
   preserve-a e pergunte.
2. **Especificar** antes de codar: objetivo, arquivos permitidos, o que preservar
   e como será verificado. Tarefa grande vira etapas pequenas e revisáveis.
3. **Um executor por vez.** Nunca dois agentes escrevendo no projeto ao mesmo tempo.
4. **Verificar** (seção 5) até passar tudo.
5. **Revisão (Terra)** do `git diff`, procurando bug, regressão, quebra destas
   regras, segurança e arquivo fora do pedido. Corrija o que for achado.
6. **Documentar** (seção 7).
7. **Commitar e enviar** (seção 8).
8. **Avisar o Franklin** com o resumo para a conferência do Claude (seção 9).

---

## 5. Como verificar

```
cd Sentinela
dotnet build -c Release                          # zero avisos e zero erros
cd ..
dotnet test                                      # todos verdes
Sentinela\bin\Release\net9.0-windows\Sentinela.exe --captura %TEMP%\tela.png --aba inicio
```

Abas do modo captura: `inicio`, `seguranca`, `desempenho`, `varredura`,
`ameacas`, `historico`, `limpeza`, `limpeza-windows`, `inicializacao` e `ajustes`.
Abra cada PNG que a mudança tocou e confira de verdade, sem confiar só no
build. Mudança de interface: confira também em 125% e 150% de escala, e com o
teclado (Tab, Enter, Esc).

Diga sempre o que **foi testado** e o que **não foi** (por exemplo, "não testei
como administrador" ou "não testei num PC limpo").

---

## 6. Fase 8: instalador e atualização (regras extras)

**Siga o `PLANO-FASE-8.md`**: ele tem as etapas, a ordem e o aceite de cada uma.
Decisões do Franklin em 11/11/2026:
- o repositório `nilknarfsam/sentinela` é **público**, com licença **GPL-3.0 ou
  posterior**;
- os instaladores e as atualizações saem dos **Releases desse mesmo
  repositório** (não existe repositório separado de releases);
- a primeira versão pública é a 2.0.0, **sem assinatura digital**. O certificado
  vem depois (etapa 8.10), de preferência pelo SignPath Foundation, que é grátis
  para código aberto.

### Instalador (MSI com WiX)
- Instala **por máquina** em `C:\Program Files\Sentinela`, com o .NET embutido
  (*self-contained*). Funciona num PC limpo, sem .NET.
- Faz tudo o que o `instalar-sentinela.ps1` faz hoje:
  - tarefas agendadas `Sentinela` (logon, `--bandeja`, privilégio mais alto) e
    `Sentinela-Abrir`;
  - atalho no menu Iniciar;
  - clique direito "Verificar com o Sentinela".
- Aparece em **Configurações → Aplicativos** e desinstala **sem deixar sobra**,
  removendo tarefas, atalho, chaves e pasta. Os dados do usuário em
  `%LOCALAPPDATA%\Sentinela` podem ficar, mas diga isso na desinstalação.
- **Atualizar por cima** (MajorUpgrade) mantém as configurações e não duplica
  tarefas. Também é proibido instalar uma versão mais velha por cima de uma
  mais nova.
- `UpgradeCode` fixo para sempre. A versão do MSI é a mesma do `.csproj`.

### Sistema de atualização (como os softwares profissionais)
Este é o ponto mais sensível do projeto: o Sentinela roda como admin, e um
atualizador mal feito instala vírus com privilégio máximo.
- **Consulta** só por **HTTPS**, só no GitHub Releases do Sentinela, com
  `User-Agent: Sentinela/<versão>` e nenhum dado do usuário. Uma vez por dia no
  máximo, e com a opção "Procurar atualizações" nos Ajustes, que pode ser
  desligada.
- **Nunca** guarde token no app. A consulta é nos Releases do repositório
  **público** `nilknarfsam/sentinela`, que não precisa de token.
- **Mostra** a versão nova e o que mudou, tirado do CHANGELOG. **Só instala
  depois do clique do usuário.** Nada de atualização silenciosa nesta primeira
  versão.
- **Baixa** para uma pasta que só o administrador pode gravar (por exemplo
  `%ProgramData%\Sentinela\Atualizacoes`, com permissão só para Administradores e
  SYSTEM), nunca para Downloads ou `%TEMP%` do usuário.
- **Confere antes de instalar:**
  1. a versão é **maior** que a instalada (sem downgrade);
  2. o **SHA-256** do arquivo bate com o publicado no release (arquivo
     `SHA256SUMS` gerado pelo GitHub Actions);
  3. quando houver assinatura digital, a assinatura Authenticode é válida e do
     editor esperado (já existe código de assinatura em `Servicos/Assinaturas.cs`).

  Falhou qualquer item, apaga o arquivo e avisa. Não instala.
- **Instala** com `msiexec` pelo caminho completo (adicione em `CaminhosSistema`),
  com `ArgumentList`, e registra no diário.
- Se o download ou a instalação falhar, o Sentinela atual continua funcionando.
- Testes para: comparação de versões, SHA-256 certo e errado, recusa de
  downgrade, URL fora do GitHub recusada e resposta da API malformada.

### Publicação (GitHub Actions)
- Tag `v1.x.y` → o Actions compila, testa, gera o MSI, gera o `SHA256SUMS` e cria
  o **Release** com os dois arquivos e o trecho do CHANGELOG como descrição.
- Nada de segredo no workflow além dos *secrets* do GitHub, e só se o Franklin
  configurar.
- A página no site do Franklin é outro projeto, com regras próprias. Ela aponta
  para o Release e explica o aviso do SmartScreen e a privacidade.

---

## 7. Documentação (atualizar em toda etapa)

- `CHANGELOG.md`: seção da versão, para leigos. A versão sobe no `.csproj`
  (patch para correção, minor para função nova).
- `LEIAME.md`: o que mudou no uso, nas abas, no instalador e nos comandos.
- `PLANO-MELHORIAS.md`: marcar o item como feito (com data e versão) no mesmo
  estilo das Fases 2, 3 e 6, e registrar as decisões que mudaram o plano.
- Decisão do Franklin que vale para sempre: acrescente neste `AGENTS.md`.

---

## 8. Git e GitHub (manter sempre atualizado)

O Franklin autorizou em 11/11/2026 o Codex a manter o GitHub atualizado:
- **Commits pequenos** por etapa, mensagem em português, sem acento no título
  (como os anteriores: `Fase 6: Rotinas e relatorios (v1.5.0)`). Antes de
  commitar: build sem avisos e `dotnet test` verde.
- **Push da branch de trabalho** e **Pull Request** para a `main`. A descrição do
  PR tem: o que mudou, como foi testado, o que não foi testado e capturas.
- Espere o **GitHub Actions ficar verde** (`gh run watch`). Se falhar, corrija.
- **Merge na `main` só depois do "pode" do Franklin**, normalmente depois da
  conferência do Claude. Nunca use `push --force` na `main`. Nunca reescreva o
  histórico publicado.
- **Instalar no PC do Franklin, publicar Release e criar tag: só com
  autorização dele em cada vez.**
- O repositório é **público** desde 11/11/2026: **tudo o que você commita fica
  visível para o mundo.** Nunca commite token, senha, log com dados pessoais,
  capturas com dados do Franklin ou arquivos de `%LOCALAPPDATA%\Sentinela`.
- Licença **GPL-3.0-or-later**. Código de terceiros só entra com licença
  compatível com a GPL-3.0, e sempre perguntando ao Franklin antes (a regra do
  NuGet continua valendo).

---

## 9. Entrega para a conferência do Claude

Ao fim de cada etapa, deixe no PR (e diga ao Franklin):
1. **Branch e PR** (link).
2. **O que mudou**, em poucas linhas, e os arquivos principais.
3. **Testes:** quantos passaram, build sem avisos, capturas feitas (caminho em
   `%TEMP%`).
4. **O que não foi testado** e os riscos conhecidos.
5. **Decisões tomadas** diferentes do plano.
6. **Itens do `CONFERENCIAS.md`** resolvidos nesta etapa, marcados como
   resolvidos lá.

O Franklin chama o Claude, que lê o PR, roda o build, os testes e as capturas, e
aponta o que corrigir antes do merge.

---

## 10. O que o Sentinela **não** vai ter

- Motor de antivírus próprio. O Defender já é um dos melhores.
- Botões que mudam a configuração do Defender, do firewall ou do UAC.
- "Limpador de registro", "turbo de RAM", atualizador de drivers, VPN, cofre de
  senhas (o cofre é o Bitwarden).
- Telemetria ou coleta de dados.
- Anúncios: **não agora.** Ficam para uma decisão futura do Franklin, com as
  regras da etapa 8.11 do `PLANO-FASE-8.md`.
