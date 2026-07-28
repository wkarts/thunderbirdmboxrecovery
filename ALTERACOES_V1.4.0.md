# Alterações da versão 1.4.0

- Corrige o cenário em que o MBOX recuperado apresenta tamanho e contagem de mensagens, mas a lista fica vazia.
- Remove a geração de arquivo `.msf` vazio.
- Deixa o Thunderbird criar um índice `.msf` válido.
- Adiciona recuperação das mensagens com flag `Expunged`.
- Adiciona recuperação das mensagens com flag `IMAPDeleted`.
- Normaliza `X-Mozilla-Status` e `X-Mozilla-Status2`.
- Insere cabeçalhos de status ausentes.
- Corrige mensagens cujo bloco de cabeçalhos não foi encerrado antes da próxima mensagem.
- Torna a identificação do separador `From ` mais estrita para reduzir falsos cortes no corpo da mensagem.
- Adiciona métricas de reparo ao manifesto e ao log.
- Reforça que a importação deve ser feita em `Mail\Local Folders\`, com o Thunderbird fechado.
- Mantém saída única como padrão e fracionamento opcional.
- Mantém releases automáticas versionadas e imutáveis para x86 e x64.


## Correção adicional do build NETSDK1151

- corrige incompatibilidade entre o executável self-contained e o projeto de smoke tests;
- move `SelfContained` e `PublishSingleFile` para as etapas de `dotnet publish`;
- fixa a família do SDK em .NET 8 por `global.json`;
- valida o SDK efetivamente selecionado no runner;
- mantém publish portátil self-contained para x86 e x64.
