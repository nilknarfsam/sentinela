# Conferências do Claude

Aqui o Claude registra o que achou ao conferir cada etapa feita pelo Codex.

**Para o Codex:** antes de começar qualquer etapa nova, resolva os itens com
status **aberto**. Depois, marque o item como **resolvido**, com a data, o
commit ou PR e uma linha dizendo como verificou. Não apague itens antigos.

---

## C1 — Conferência das etapas 8.1 e 8.2 (11/11/2026)

**Resultado geral:** aprovadas, com um ajuste obrigatório antes do merge do
PR #3.

Verificado pelo Claude em 11/11/2026:
- build Release sem avisos;
- 762 testes passando e GitHub Actions verde;
- captura `--aba ajustes-sobre` conferida;
- nenhum dado pessoal fora da tela "Sobre e ajuda";
- histórico do repositório sem senhas nem tokens.

### C1.1 — Links abrem o navegador como administrador · **resolvido no código (11/11/2026, v1.6.2, branch `fase-8/c1-links`) · falta o teste prático do Franklin**
- **Onde:** `Sentinela/Paginas/PaginaAjustes.xaml.cs`, `AbrirLinkAjuda_Click`.
- **Problema:** o Sentinela roda elevado (administrador). `Process.Start` com
  `UseShellExecute = true` abre o navegador **também elevado**. Qualquer site
  aberto nessa janela, e o navegador inteiro, roda com privilégio máximo. É um
  risco de segurança.
- **Correção:**
  1. Continue checando `AlvoAjudaPermitido(alvo)` **antes** de tudo.
  2. Abra a URL pelo Explorer, que entrega o link ao navegador do usuário
     **sem elevação**:
     `new ProcessStartInfo(CaminhosSistema.Explorer) { UseShellExecute = false }`,
     com a URL como único item de `ArgumentList`.
  3. Crie um método único em `Servicos/` (por exemplo
     `NavegadorSeguro.Abrir(string url)`), que nunca lança exceção e devolve
     sucesso ou falha. Use esse método também em **todas as futuras aberturas
     de URL http/https** (página do Sentinela, relatar problema, notas da
     atualização).
  4. Os alvos `ms-settings:` e `windowsdefender://` dos botões "Como resolver"
     podem continuar como estão: eles abrem páginas do próprio Windows.
- **Teste:**
  - unitário: o método recusa o que não é `https://`;
  - prático: com o Sentinela aberto como administrador, clicar em "GitHub" e
    conferir no Gerenciador de Tarefas (aba Detalhes, coluna "Elevado") que o
    navegador **não** está elevado.

  Registre o resultado aqui.
- **Resolução (11/11/2026, feita pelo Claude porque o Codex estava sem limite):**
  `Servicos/NavegadorSeguro.cs` (`UrlValida` + `Abrir`, via `explorer.exe` com
  `ArgumentList`, sem `UseShellExecute`). `PaginaAjustes` usa o método depois da
  lista branca. 15 testes novos em `NavegadorSeguroTestes`. Build sem avisos e
  777 testes verdes. **Falta:** o Franklin instalar a 1.6.2, abrir como
  administrador, clicar em "GitHub" e conferir na aba Detalhes do Gerenciador de
  Tarefas que o navegador está com "Elevado = Não".
- **Revisão do Sonnet (11/11/2026):** sem injeção de argumento possível (a URL
  validada sempre começa com `https:`, nunca com `/` ou `,`). Ajustes feitos:
  passa `uri.AbsoluteUri` normalizado; recusa espaços nas pontas e caracteres de
  formatação invisíveis; `Montar` testável; testes novos. **Limite conhecido:** a
  descida da elevação depende do Explorer da sessão. Com "Iniciar janelas de
  pasta em processo separado" ligado, UAC desligado ou a conta Administrador
  embutida, o navegador ainda pode abrir elevado. Se um dia isso precisar ser
  garantido, use `CreateProcessWithTokenW` com o token do shell.

### C1.2 — Nome da instância mudou na 8.1 · **resolvido no código pela 8.3 (11/11/2026, v1.7.0) · falta teste no Sandbox**
- `NomeUnico` passou de `Sentinela_Franklin_Instancia` para
  `Sentinela_Instancia`. Se uma versão antiga estiver aberta durante a
  atualização, as duas podem rodar ao mesmo tempo por um instante.
- O instalador (8.3) e o atualizador (8.5) devem **fechar o Sentinela antigo**
  antes de copiar os arquivos. O MSI atende esse requisito na etapa 8.3; a
  integração do atualizador continua prevista para a 8.5.
- **Resolução na etapa 8.3 (11/11/2026, v1.7.0):** o MSI chama `taskkill.exe`
  depois de `InstallInitialize` e antes de `RemoveExistingProducts`; o retorno é
  ignorado quando o processo não está aberto. O teste prático no Sandbox ainda
  está pendente junto com a conferência da etapa.

### C1.3 — Ainda não testado na 8.2 · **aberto · fazer junto com C1.1**
- Clicar de verdade nos 4 links (com o C1.1 aplicado).
- Navegar só pelo teclado (Tab, Enter) pela aba "Sobre e ajuda".
- Conferir as escalas de 125% e 150%.

---

<!-- Próximas conferências entram abaixo, como C2, C3... -->

## C2 — Conferência da etapa 8.3, instalador MSI (11/11/2026)

**Resultado geral:** aprovada no código. Falta o teste prático no Windows
Sandbox, que fica com o Franklin.

Quem fez: o Codex escreveu a etapa até acabar o limite. O Claude conferiu e
corrigiu os achados da revisão do Sonnet.

Verificado pelo Claude em 11/11/2026:
- build Release do app e do MSI sem avisos (WiX 5.0.2, sem extensões);
- 810 testes passando;
- captura `--aba ajustes-sobre` mostrando a versão 1.7.0;
- MSI de cerca de 58 MB, com o .NET embutido.

### C2.1 — Upgrade religava a tarefa desligada nos Ajustes · **resolvido (11/11/2026)**
- `TarefasDoInstalador.Criar` lê o estado da tarefa `Sentinela` antes de
  recriar e mantém `Enabled=false` se o usuário tinha desligado.

### C2.2 — Conta Microsoft Entra e instalação por SYSTEM · **resolvido (11/11/2026)**
- O SID agora aceita `S-1-12-1-...` além de `S-1-5-21-...`.
- Instalação feita por SYSTEM (`S-1-5-18`) pula as tarefas, em vez de falhar.
- **Limite conhecido:** se outro administrador elevar a instalação com a
  senha dele, as tarefas ficam para a conta desse administrador.

### C2.3 — Atalho sem permissão de rodar a tarefa criada por SYSTEM · **resolvido no código · confirmar no Sandbox**
- As tarefas têm descritor de segurança explícito: SYSTEM e Administradores
  com controle total; o usuário só lê e executa.
- **Teste:** no Sandbox, abrir o atalho do menu Iniciar sem elevar.

### C2.4 — Abria o Sentinela em instalação silenciosa e em reparo · **resolvido (11/11/2026)**
- `AbrirSentinela` só roda com tela (`UILevel > 2`, inclusive `/passive`) e
  quando não é reparo. Atalho ganhou a descrição "Sentinela".

### C2.5 — Pontos aceitos, sem ação agora · **informativo**
- Migração de quem instalou pelo `instalar-sentinela.bat`: rodar antes o
  `desinstalar-sentinela.bat` (está no LEIAME). Só afeta o PC do Franklin.
- Reinstalar a **mesma** versão é recusado como "versão mais nova"; para
  testar o upgrade, gere outro MSI com `-p:Version=1.7.1`.
- `taskkill /IM Sentinela.exe` fecha qualquer processo com esse nome.
- Se a remoção de tarefas falhar na desinstalação, o MSI não avisa
  (`Return="ignore"`). Conferir no Sandbox que não sobra tarefa.

---

## C3 — Teste prático do MSI no Windows Sandbox (12/11/2026)

**Resultado geral:** **reprovado**. O MSI não instala: 38 falhas no roteiro,
todas em cascata de um único bug (C3.1). Os itens C1.2, C2.3 e as conferências
pendentes da C2.5 continuam sem teste prático.

Como foi testado: roteiro automático na Área Restrita (Windows 11 26100, sem
.NET), com o MSI 1.7.0 e um 1.7.1 gerado com `-p:Version=1.7.1`. Passos:
instalar com `/passive`, atualizar com `/qn` com o Sentinela aberto, tentar
voltar para a 1.7.0 e desinstalar. Os logs do `msiexec /l*v` ficaram no PC do
Franklin (fora do repositório).

### C3.1 — Erro 2613: `FecharSentinela` fora de ordem · **resolvido no código (12/11/2026, [PR #7](https://github.com/nilknarfsam/sentinela/pull/7)) · falta teste no Sandbox**
- **Onde:** `Sentinela.Instalador/Pacote.wxs`.
- **Sintoma:** `msiexec /i` termina com o código **1603**. O log mostra
  `Error 2613: RemoveExistingProducts action sequenced incorrectly`, e o
  Windows recusa o pacote antes de copiar qualquer arquivo. Por isso falharam
  também a atualização, as tarefas, o atalho, as capturas e a desinstalação
  (código 1619, produto inexistente).
- **Causa:** com `MajorUpgrade Schedule="afterInstallInitialize"`, o
  `RemoveExistingProducts` precisa vir logo depois do `InstallInitialize`,
  antes de qualquer ação adiada. A `FecharSentinela` é `Execute="deferred"` e
  foi sequenciada `Before="RemoveExistingProducts"`, ou seja, uma ação adiada
  antes dele. O build e o GitHub Actions não pegam isso: o erro só aparece na
  hora de instalar.
- **Correção pedida:**
  1. `FecharSentinela` passa a ser `Execute="immediate"` (sem `Impersonate`,
     que só vale para adiada) e é sequenciada `After="InstallValidate"`, isto
     é, antes do `InstallInitialize`. Mantenha `Return="ignore"`.
  2. Não mexa no `MajorUpgrade` nem nas outras ações.
  3. Atualize a versão para **1.7.1** e o `CHANGELOG.md`.
- **Limite conhecido da correção:** ação imediata roda com o token do usuário.
  Em dois cliques no MSI sem estar elevado, ela pode não conseguir fechar um
  Sentinela aberto como administrador; nesse caso o Windows Installer mostra o
  aviso de arquivo em uso. Pelo `.bat` de administrador, pelo atualizador
  (8.5) e no Sandbox funciona. Registrar esse limite no `LEIAME.md`.
- **Teste:**
  - automático: um teste em `Sentinela.Testes` que lê o `Pacote.wxs` e
    confirma que `FecharSentinela` é `immediate` e não está entre
    `InstallInitialize` e `RemoveExistingProducts`;
  - prático: o Franklin gera os dois MSIs (1.7.1 e 1.7.2 só para teste) e roda
    `testar-msi-sandbox.bat` de novo. Aceite: **0 falhas**.
- **Resolução no código (12/11/2026):** `FecharSentinela` agora é imediata,
  mantém `Return="ignore"` e roda depois de `InstallValidate`, antes de
  `InstallInitialize`; o comando `taskkill.exe` e as demais ações foram
  preservados. A versão do app passou para 1.7.1. Foi adicionado teste
  automatizado que lê o WiX como XML. **Verificação:** build Release do app e
  do MSI sem avisos ou erros; 811 testes aprovados. Leitura somente leitura
  das tabelas do MSI confirmou `InstallValidate=1400` <
  `FecharSentinela=1401` < `InstallInitialize=1500` <
  `RemoveExistingProducts=1501` e ação imediata `Type=98`. O Sandbox não foi
  executado; continua pendente. O teste automatizado foi ajustado após revisão
  e os 811 testes passaram novamente.

### C3.2 — Instalação parada uns 2 minutos no começo · **informativo**
- O Windows confere a assinatura do MSI (`SOFTWARE RESTRICTION POLICY`) e,
  sem internet no Sandbox, espera o tempo limite. Acontece só porque o MSI não
  é assinado. Some quando houver certificado (previsto para depois da 2.0.0).
