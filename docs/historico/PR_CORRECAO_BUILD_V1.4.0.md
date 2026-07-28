# Branch

```text
fix/v1.4-build-self-contained-tests
```

# Título

```text
fix: corrige validação self-contained da linha 1.4
```

# Descrição

## Objetivo

Aplicar preventivamente à versão 1.4.0 a correção de build identificada na validação da linha 1.3.

## Correções

- remove `SelfContained` e `PublishSingleFile` da configuração global do aplicativo;
- mantém essas opções somente no publish portátil;
- adiciona `global.json` para uso do SDK .NET 8;
- valida o SDK selecionado pelo GitHub Actions;
- mantém smoke tests framework-dependent;
- preserva executáveis finais self-contained para x86 e x64.

## Commit

```text
fix: separa build de testes do publish self-contained
```
