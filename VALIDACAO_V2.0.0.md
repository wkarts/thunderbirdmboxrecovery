# Relatório de validação — Thunderbird Recovery Suite 2.0

## Escopo

Este relatório registra as verificações executadas no pacote-fonte final. Ele não substitui o build no runner Windows.

## Validações executadas neste ambiente

- Estrutura do repositório e referências de projeto.
- Parse XML dos dois arquivos `.csproj`.
- Parse JSON do `global.json`.
- Parse YAML dos workflows de CI e release.
- Verificação de delimitadores léxicos nos arquivos C#.
- Verificação de inicializadores para propriedades declaradas como `required`.
- Busca por padrões proibidos nos workflows: `continuous`, `--clobber`, `upload-artifact` e `download-artifact`.
- Verificação de que self-contained/single-file é aplicado apenas no publish por RID.
- Integridade do ZIP final e geração de SHA-256.

## Cobertura dos smoke tests incluídos

Os testes serão executados pelo GitHub Actions em Windows com .NET 8 e cobrem:

- normalização de status Mozilla;
- recuperação de `Expunged` e `IMAPDeleted`;
- preservação da flag de mensagem lida;
- saída única sem `.msf` artificial;
- fracionamento somente entre mensagens;
- diagnóstico, SHA-256 e detecção de exclusão;
- extração EML sanitizada;
- validação Mork e leitura de `numMsgs`;
- validação de cabeçalho SQLite;
- backup completo com manifesto e hashes;
- restauração completa com hashes;
- restauração somente de mensagens, sem `prefs.js`;
- garantia de que os smoke tests não registrem perfis reais.

## Gate do GitHub Actions

Uma release somente é criada quando:

1. o SDK ativo é .NET 8;
2. scripts PowerShell possuem sintaxe válida;
3. restore e build da solução passam com warnings como erro;
4. todos os smoke tests passam;
5. publish self-contained/single-file passa em `win-x86` e `win-x64`;
6. os executáveis existem e possuem tamanho mínimo esperado;
7. a tag calculada ainda não existe.

## Limitação desta validação

O ambiente utilizado para preparar o pacote não possui `dotnet`, `pwsh`, compilador C# Windows ou uma instalação gráfica do Thunderbird. Portanto:

- nenhum `.exe` foi compilado localmente;
- UI Automation não foi executada localmente;
- criação real de `.msf` deve ser validada em computador Windows com Thunderbird instalado;
- compatibilidade Panorama deve ser confirmada em build do Thunderbird que exponha esse armazenamento.

Essas limitações são tratadas pelo CI Windows e pelos testes manuais de aceitação descritos na PR.
