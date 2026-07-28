# Thunderbird MBOX Recovery 1.3.0 — linha corrigida

Utilitário portátil para recuperar e reconstruir arquivos MBOX do Mozilla Thunderbird, incluindo caixas grandes e arquivos sem extensão como `Inbox`, `Sent`, `Drafts`, `Archives`, `Trash` e pastas personalizadas.

## Escopo da linha 1.3

- seleciona MBOX descompactado diretamente ou uma entrada dentro de arquivo compactado;
- processa a origem somente para leitura e em fluxo;
- saída única sem fracionamento por padrão;
- fracionamento opcional apenas entre mensagens completas;
- preserva o nome da caixa na saída;
- gera hashes, manifesto e log;
- valida espaço livre e impede saída única maior que 4 GiB em FAT32;
- publica releases novas e imutáveis para `win-x86` e `win-x64`;
- não fabrica arquivo `.msf` vazio.

## Índice MSF

A aplicação entrega o MBOX recuperado sem `.msf`. O índice deve ser criado pelo próprio Thunderbird depois que o MBOX for copiado, com o Thunderbird fechado, para `Mail\\Local Folders\\`.

Validação prática registrada: uma caixa recuperada pela versão 1.2.0, com aproximadamente 27,4 GiB, inicialmente permaneceu ocupada e sem listar as mensagens; depois de o Thunderbird continuar aberto e processando a pasta por algum tempo, ele próprio reconstruiu o arquivo `.msf`. Esse comportamento foi incorporado às instruções operacionais da 1.3.0. O tempo necessário varia conforme tamanho da caixa, disco, antivírus e indexação global.

## Limite funcional desta versão

A 1.3.0 reconstrói o contêiner MBOX, mas não altera `X-Mozilla-Status` ou `X-Mozilla-Status2`. Mensagens ainda presentes no arquivo, porém marcadas logicamente como excluídas/expurgadas, são tratadas na linha 1.4.0.

## Build

O erro observado no workflow original foi corrigido em `scripts/Publish-Portable.ps1`: a interpolação PowerShell `$Version:` foi substituída por `${Version}:`.

Antes de compilar ou publicar, o CI agora executa:

1. análise sintática de todos os scripts PowerShell;
2. restore e build com avisos tratados como erro;
3. smoke tests do reparador MBOX;
4. publicação portátil de validação para `win-x86` e `win-x64`;
5. criação de release somente após todas as validações.

## Uso seguro

1. Preserve o backup original.
2. Execute a recuperação para outra unidade NTFS ou exFAT com espaço suficiente.
3. Feche completamente o Thunderbird.
4. Copie somente o MBOX recuperado para `Mail\\Local Folders\\`.
5. Não copie um `.msf` antigo e não crie um `.msf` vazio.
6. Abra o Thunderbird e aguarde a reconstrução do índice.
7. Não compacte nem exclua o original até conferir assuntos, corpos e anexos.
