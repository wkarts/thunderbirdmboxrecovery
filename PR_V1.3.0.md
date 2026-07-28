# Pull Request

## Branch

```text
feature/immutable-versioned-releases
```

## Título

```text
feat: cria releases versionadas e preserva todas as versões anteriores
```

## Descrição

### Objetivo

Substituir a release móvel `continuous` por um fluxo de publicação imutável, no qual cada atualização da branch principal gera uma nova versão, uma nova tag e uma nova GitHub Release sem apagar ou substituir arquivos anteriores.

### Alterações implementadas

- Remove o workflow `build.yml` responsável pela release `continuous`.
- Atualiza `release.yml` para executar após push em `main` ou `master`.
- Permite criação de nova versão também por execução manual.
- Gera versões automáticas no formato `<MAJOR>.<MINOR>.<GITHUB_RUN_NUMBER>`.
- Cria tags exclusivas no formato `v<MAJOR>.<MINOR>.<GITHUB_RUN_NUMBER>`.
- Publica os binários diretamente em uma nova GitHub Release.
- Remove qualquer atualização de tag existente.
- Remove o parâmetro `--clobber`.
- Impede alteração de releases anteriores.
- Interrompe a execução se a tag ou release calculada já existir.
- Inclui a versão no nome dos executáveis, ZIPs e arquivos de checksum.
- Incorpora a versão calculada ao assembly durante a publicação.
- Faz o manifesto da recuperação utilizar a versão real do assembly.
- Mantém os builds autossuficientes para `win-x86` e `win-x64`.
- Continua sem utilizar `actions/upload-artifact`.

### Exemplo de arquivos publicados

```text
ThunderbirdMboxRecovery-v1.3.27-win-x64.exe
ThunderbirdMboxRecovery-v1.3.27-win-x86.exe
ThunderbirdMboxRecovery-v1.3.27-win-x64.zip
ThunderbirdMboxRecovery-v1.3.27-win-x86.zip
SHA256-v1.3.27-win-x64.txt
SHA256-v1.3.27-win-x86.txt
SHA256SUMS.txt
VERSION.txt
```

### Critérios de conclusão

- Cada push na branch principal cria uma tag inédita.
- Cada execução cria uma GitHub Release nova.
- Releases anteriores permanecem disponíveis.
- Nenhuma tag existente é movida.
- Nenhum asset é sobrescrito.
- Os nomes dos arquivos incluem a versão.
- A versão interna do executável corresponde à versão da release.
- O build continua gerando `win-x86` e `win-x64`.

## Commit sugerido

```text
feat: cria releases automáticas imutáveis e versionadas
```
