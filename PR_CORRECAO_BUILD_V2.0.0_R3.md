# Pull Request — correção de System.IO

## Branch

```text
feature/thunderbird-recovery-suite-2.0
```

## Título

```text
fix: corrige resolução dos tipos System.IO no build da suíte 2.0
```

## Descrição

### Problema

O workflow `82184324062` concluiu a validação do SDK, a análise sintática dos scripts e o restore, mas falhou no build com 17 erros `CS0246` para `Stream`, `FileStream`, `MemoryStream` e `StreamWriter`.

A aplicação combina Windows Forms com WPF apenas para utilizar Windows UI Automation. Nesse contexto, o conjunto de usings implícitos efetivamente gerado não disponibilizou `System.IO` para os arquivos do projeto principal.

### Correção

- adiciona `src/ThunderbirdMboxRecovery/GlobalUsings.cs`;
- importa globalmente `System.IO`;
- preserva a configuração `UseWindowsForms` + `UseWPF`;
- mantém o build normal separado do publish self-contained;
- não altera a lógica funcional da suíte;
- não altera o versionamento ou o modelo de releases imutáveis.

### Erros cobertos

- `Stream`;
- `FileStream`;
- `MemoryStream`;
- `StreamWriter`;
- demais APIs de arquivos e diretórios utilizadas pela aplicação.

### Critérios de aceitação

- solução compila em Release no .NET 8;
- smoke tests são executados;
- restore dos grafos `win-x86` e `win-x64` é validado;
- nenhuma publicação é executada durante a PR;
- releases permanecem versionadas e imutáveis após o merge.

## Commit

```text
fix: adiciona global using de System.IO ao projeto principal
```

## Mensagem de merge

```text
fix: estabiliza tipos de I/O no build da Thunderbird Recovery Suite 2.0
```
