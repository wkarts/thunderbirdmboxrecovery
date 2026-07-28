# Pull Request

## Branch

```text
fix/repair-empty-recovered-mbox
```

## Título

```text
fix: recupera mensagens ocultas e remove índice MSF artificial
```

## Descrição

### Problema

A caixa MBOX reconstruída podia ser reconhecida pelo Thunderbird, apresentar tamanho em disco e quantidade de mensagens, mas continuar com a lista vazia. A versão anterior apenas preservava os cabeçalhos `X-Mozilla-Status` e `X-Mozilla-Status2` e criava um `.msf` vazio.

Mensagens ainda presentes fisicamente no MBOX, mas marcadas como expurgadas ou excluídas, permaneciam fora do índice. Além disso, o `.msf` vazio não representa um banco de resumo válido do Thunderbird.

### Correções

- remove a criação do `.msf` artificial;
- deixa o Thunderbird gerar o índice válido após a importação;
- remove a flag `Expunged` das mensagens recuperadas;
- remove a flag `IMAPDeleted` das mensagens recuperadas;
- normaliza os cabeçalhos internos de status;
- insere cabeçalhos de status quando ausentes;
- corrige blocos de cabeçalho sem terminador;
- reduz falsos separadores MBOX no corpo da mensagem;
- adiciona métricas detalhadas ao manifesto e ao log;
- orienta importação exclusiva em `Mail\Local Folders\` com o Thunderbird fechado.

### Validação esperada

- compilar para `win-x86` e `win-x64`;
- recuperar MBOX direto ou entrada compactada;
- gerar saída única por padrão;
- permitir fracionamento opcional;
- não gerar arquivo `.msf` vazio;
- apresentar no manifesto quantas mensagens expurgadas/excluídas foram recuperadas;
- permitir que o Thunderbird crie um `.msf` válido e exiba as mensagens recuperadas.

## Commit

```text
fix: normaliza status MBOX e recupera mensagens expurgadas
```
