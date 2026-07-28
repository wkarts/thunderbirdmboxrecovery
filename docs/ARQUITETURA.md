# Arquitetura técnica

## Camadas

```text
UI Windows Forms
├── Visão geral
├── Explorar
├── Testar
├── Reparar
├── Extrair
├── Indexar
├── Backup
└── Restaurar

Core
├── MboxSourceService / ArchiveService
├── MboxStreamParser / MboxHeaderParser
├── MboxAnalyzer
├── MboxSplitter / MessageRepairWriter / ChunkWriter
├── MboxExtractor
├── ThunderbirdLocator / ThunderbirdProfileService
├── ThunderbirdIndexService / ThunderbirdUiAutomation
├── MsfValidator / SqliteFileValidator
├── ProfileBackupService / ProfileFileClassifier
└── ProfileRestoreService
```

## Processamento em fluxo

O parser lê linhas binárias com buffer de 4 MiB. Corpos e anexos não são decodificados durante diagnóstico, reparo ou indexação. O consumo de memória permanece independente do tamanho total da caixa, exceto pelos metadados que o módulo Explorar conserva na grade até o limite configurado.

## Recuperação

O `MboxSplitter` identifica separadores `From ` reconhecíveis e delega cada mensagem ao `MessageRepairWriter`. O escritor:

- preserva o separador MBOX;
- mantém corpo e MIME sem decodificação;
- normaliza cabeçalhos internos;
- remove somente as flags de exclusão configuradas;
- preserva as demais flags;
- garante separação entre cabeçalho e corpo;
- grava em `ChunkWriter`, que finaliza hashes e renomeia `.partial` após flush físico.

## Indexação assistida

O `ThunderbirdIndexService` não implementa Mork por conta própria. O fluxo é:

1. criar pasta de trabalho e perfil isolado;
2. configurar uma conta de Pastas Locais sem rede;
3. desabilitar telemetria, indexação global e Panorama no perfil temporário, quando a preferência for reconhecida;
4. copiar o MBOX e contar mensagens estruturalmente;
5. iniciar `thunderbird.exe -profile <perfil> -new-instance -no-remote`;
6. tentar selecionar a pasta pela API Windows UI Automation;
7. aguardar `.msf` Mork válido ou, como fallback prospectivo, `panorama.sqlite` válido;
8. exigir estabilidade de tamanho/data;
9. comparar `numMsgs` com a contagem do MBOX e aplicar uma tolerância temporal adicional quando houver divergência;
10. encerrar apenas a instância isolada e entregar os arquivos produzidos.

A automação expande primeiro `Pastas Locais`/`Local Folders` quando o controle expõe `ExpandCollapsePattern`. Se a árvore não estiver acessível, a interface solicita seleção manual e mantém o monitoramento automático.

## Backup

O backup nativo usa ZIP. Arquivos são classificados por categoria e o manifesto registra se o perfil estava em uso. Por padrão, locks impedem a operação; a substituição emergencial precisa ser selecionada explicitamente.

## Restauração

A leitura de compactados usa SharpCompress. Cada caminho é normalizado e validado contra a raiz de destino. O arquivo é extraído para `.restore-partial`, validado e movido ao nome final.

A restauração pode filtrar conteúdo pelos mesmos modos do backup. O registro de perfil:

- exige o Thunderbird fechado;
- valida a estrutura restaurada;
- detecta registro já existente;
- cria cópia única do `profiles.ini`;
- grava um arquivo temporário e faz substituição atômica;
- limpa o temporário em caso de falha;
- permite definir `Default=1` somente no `profiles.ini`.

## Compatibilidade e distribuição

- Executáveis separados para `win-x86` e `win-x64`.
- Detecção de Thunderbird x86, x64 e ARM64.
- Mork/MSF validado por assinatura e metadados auxiliares.
- Panorama reconhecido por banco SQLite válido.
- Leitura de 7z, ZIP, RAR, TAR, GZip, BZip2 e XZ.
- Build normal é framework-dependent para permitir testes; self-contained/single-file é aplicado apenas no publish por RID.
