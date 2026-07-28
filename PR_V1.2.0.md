> **Histórico:** este documento descreve o comportamento da versão 1.2.0. Na linha 1.3, a criação de `.msf` vazio foi removida e o índice passou a ser reconstruído pelo próprio Thunderbird.

# Pull Request — Thunderbird MBOX Recovery 1.2.0

## Branch

`feature/mbox-single-output-msf`

## Título

`feat: adiciona saída MBOX única e criação de índice MSF`

## Descrição

### Objetivo

Aprimorar a recuperação de caixas MBOX para gerar, por padrão, um único arquivo recuperado sem fracionamento, mantendo o fracionamento como opção do operador e criando o arquivo `.msf` correspondente para reconstrução do índice pelo Thunderbird.

### Alterações

- saída única sem fracionamento por padrão;
- opção para habilitar ou desabilitar fracionamento;
- tamanho das partes configurável somente quando o fracionamento estiver habilitado;
- saída única com nome `<Caixa>_Recuperada`;
- saída fracionada com nomes `<Caixa>_Recuperada_001`, `_002` e seguintes;
- criação opcional de `.msf`, ativada por padrão;
- `.msf` vazio para o Thunderbird reconstruir o índice real;
- manifesto com modo de saída e nome do índice;
- instruções de importação atualizadas;
- manutenção do suporte genérico a Inbox, Sent, Drafts, Archives, Trash e caixas personalizadas;
- manutenção dos builds portáteis `win-x86` e `win-x64`.

### Critérios de validação

- executar sem fracionamento e produzir apenas um MBOX;
- executar com fracionamento e produzir duas ou mais partes quando o limite for alcançado;
- não dividir uma mensagem no meio;
- criar um `.msf` correspondente para cada MBOX gerado;
- permitir desativar a criação do `.msf`;
- preservar a origem somente para leitura;
- gerar manifesto, log e SHA-256;
- compilar para `win-x86` e `win-x64`.

## Commit

`feat: adiciona saída única e índice MSF reconstruível`
