# Thunderbird MBOX Recovery

Utilitário portátil para diagnóstico, reparo estrutural e recuperação de arquivos MBOX do Mozilla Thunderbird.

## O que a versão 1.4.0 corrige

A aplicação não apenas copia ou divide o arquivo. Durante a reconstrução ela:

- identifica mensagens pelos separadores MBOX;
- aceita `Inbox`, `Sent`, `Drafts`, `Archives`, `Trash` e caixas personalizadas, com ou sem extensão;
- processa arquivos diretos ou entradas de backups `.7z`, `.zip`, `.rar`, `.tar`, `.gz`, `.bz2` e `.xz`;
- normaliza `X-Mozilla-Status` e `X-Mozilla-Status2`;
- remove das mensagens recuperadas as marcações internas de expurgada e excluída no IMAP;
- insere cabeçalhos internos ausentes quando necessário;
- corrige blocos de cabeçalho sem terminador antes da próxima mensagem;
- mantém corpos e anexos em fluxo, sem carregar a caixa inteira na memória;
- gera um único MBOX por padrão ou permite fracionamento opcional;
- gera manifesto JSON, hashes SHA-256 e log técnico.

## Sobre o arquivo `.msf`

A versão anterior criava um `.msf` vazio. Esse comportamento foi removido.

O `.msf` é o banco de resumo interno do Thunderbird. Um arquivo vazio não é um índice válido e pode causar reconstruções inconsistentes. A aplicação entrega somente o MBOX reparado; o Thunderbird deve gerar um `.msf` válido ao abrir a caixa.

## Importação correta

1. Desative temporariamente a pesquisa/indexação global do Thunderbird.
2. Feche completamente o Thunderbird.
3. Confirme no Gerenciador de Tarefas que `thunderbird.exe` não está em execução.
4. Copie somente o MBOX recuperado para:

```text
Mail\Local Folders\
```

5. Não coloque a caixa recuperada na pasta da conta POP/IMAP e não use `ImapMail`.
6. Exclua qualquer `.msf` antigo com o mesmo nome.
7. Abra o Thunderbird e aguarde a criação do novo `.msf`.
8. Não use **Reparar pasta** enquanto o Thunderbird informar que outra operação está usando a pasta.

## Saída

Sem fracionamento:

```text
Inbox_Recuperada
manifesto_recuperacao.json
recuperacao.log
COMO_IMPORTAR_NO_THUNDERBIRD.txt
```

Com fracionamento:

```text
Inbox_Recuperada_001
Inbox_Recuperada_002
Inbox_Recuperada_003
```

## Builds

As releases incluem executáveis portáteis e autossuficientes para:

- `win-x64`;
- `win-x86`.

Cada build bem-sucedido da branch principal cria uma nova tag e uma nova release imutável. Versões anteriores não são apagadas nem sobrescritas.

## Limitações

A ferramenta não recria bytes fisicamente ausentes, sobrescritos ou truncados. Mensagens com danos graves no conteúdo MIME podem aparecer parcialmente, mesmo quando o contêiner MBOX é reconstruído.


## Build normal e publicação portátil

A solução é compilada normalmente como framework-dependent para permitir os smoke tests. Os executáveis entregues ao cliente continuam autossuficientes: `SelfContained=true` e `PublishSingleFile=true` são aplicados exclusivamente durante o publish `win-x86` e `win-x64`.
