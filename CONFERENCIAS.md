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

### C1.1 — Links abrem o navegador como administrador · **aberto · obrigatório**
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

### C1.2 — Nome da instância mudou na 8.1 · **informativo, sem ação**
- `NomeUnico` passou de `Sentinela_Franklin_Instancia` para
  `Sentinela_Instancia`. Se uma versão antiga estiver aberta durante a
  atualização, as duas podem rodar ao mesmo tempo por um instante.
- O instalador (8.3) e o atualizador (8.5) devem **fechar o Sentinela antigo**
  antes de copiar os arquivos. Não precisa mudar o código agora; só lembre disso
  na 8.3.

### C1.3 — Ainda não testado na 8.2 · **aberto · fazer junto com C1.1**
- Clicar de verdade nos 4 links (com o C1.1 aplicado).
- Navegar só pelo teclado (Tab, Enter) pela aba "Sobre e ajuda".
- Conferir as escalas de 125% e 150%.

---

<!-- Próximas conferências entram abaixo, como C2, C3... -->
