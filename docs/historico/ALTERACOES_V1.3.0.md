# Alterações da versão 1.3.0

## Versionamento automático e imutável

- Removido o workflow de release móvel `continuous`.
- Removido o uso de tag móvel que apontava sempre para o último commit.
- Removido o uso de `gh release upload --clobber`.
- Removida a atualização de releases existentes.
- Cada push na branch `main` ou `master` cria uma nova versão.
- Cada execução manual do workflow também cria uma nova versão.
- Versões seguem o padrão `<MAJOR>.<MINOR>.<GITHUB_RUN_NUMBER>`.
- Tags seguem o padrão `v<MAJOR>.<MINOR>.<GITHUB_RUN_NUMBER>`.
- Releases anteriores são preservadas integralmente.
- Se a tag ou a release calculada já existir, o workflow falha em vez de sobrescrevê-la.

## Identificação dos binários

A versão passou a fazer parte do nome de todos os arquivos distribuídos:

```text
ThunderbirdMboxRecovery-v1.3.27-win-x64.exe
ThunderbirdMboxRecovery-v1.3.27-win-x86.exe
ThunderbirdMboxRecovery-v1.3.27-win-x64.zip
ThunderbirdMboxRecovery-v1.3.27-win-x86.zip
```

Também são gerados:

```text
VERSION.txt
SHA256SUMS.txt
SHA256-v1.3.27-win-x64.txt
SHA256-v1.3.27-win-x86.txt
```

## Versão interna

- A versão calculada pelo workflow é incorporada ao assembly durante o `dotnet publish`.
- O manifesto de recuperação obtém a versão diretamente do assembly.
- Foi removida a versão fixa `1.2.0` do manifesto JSON.
