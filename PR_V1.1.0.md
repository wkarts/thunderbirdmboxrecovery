# Pull Request

## Branch

```text
feature/thunderbird-mbox-recovery
```

## Título

```text
feat: adiciona recuperação genérica de arquivos MBOX do Thunderbird
```

## Descrição

### Objetivo

Adicionar um utilitário Windows portátil para reconstrução e recuperação de caixas MBOX do Mozilla Thunderbird, sem restringir o processamento ao arquivo `Inbox`.

A aplicação aceita diretamente qualquer arquivo MBOX descompactado, inclusive arquivos sem extensão, e mantém suporte opcional a caixas armazenadas dentro de backups compactados.

### Arquivos suportados

A origem pode ser qualquer caixa MBOX do Thunderbird, por exemplo:

- `Inbox`
- `Sent`
- `Drafts`
- `Archives`
- `Trash`
- `Templates`
- `Junk`
- `Unsent Messages`
- pastas e subpastas personalizadas, como `Clientes`, `Financeiro` ou `2025`
- arquivos exportados com extensão `.mbox`

Arquivos auxiliares como `.msf`, bancos SQLite e arquivos de configuração são rejeitados porque não armazenam as mensagens completas.

### Recuperação

- abre a origem somente para leitura;
- identifica separadores MBOX reconhecíveis;
- reconstrói as mensagens encontradas em novos arquivos;
- divide somente entre mensagens completas;
- processa arquivos grandes em fluxo, sem carregá-los integralmente na memória;
- preserva o nome da caixa na saída;
- gera SHA-256, manifesto JSON, log e instruções de importação;
- não modifica o arquivo original.

Exemplos de saída:

```text
Inbox     -> Inbox_Recuperada_001
Sent      -> Sent_Recuperada_001
Archives  -> Archives_Recuperada_001
Clientes  -> Clientes_Recuperada_001
```

### Backups compactados

Continua disponível a leitura de `.7z`, `.zip`, `.rar`, `.tar`, `.gz`, `.bz2` e `.xz`, inclusive com senha. O uso de arquivo compactado é opcional; uma caixa já descompactada pode ser selecionada diretamente.

### Build e distribuição

- executável autossuficiente e em arquivo único;
- builds `win-x86` e `win-x64`;
- validação de PR sem upload de artifacts;
- publicação direta em GitHub Releases para evitar falha por cota de armazenamento do GitHub Actions;
- geração de checksums SHA-256.

### Critérios de validação

- selecionar diretamente arquivos MBOX sem extensão;
- processar `Inbox`, `Sent`, `Drafts`, `Archives`, `Trash` e pastas personalizadas;
- rejeitar arquivos `.msf` e bancos auxiliares;
- preservar o nome da caixa nos arquivos recuperados;
- processar arquivos maiores que 4 GB;
- gerar partes menores sem cortar mensagens;
- compilar para `win-x86` e `win-x64`;
- publicar os binários diretamente em GitHub Releases.

## Commit recomendado

```text
feat: generaliza recuperação para qualquer caixa MBOX do Thunderbird
```
