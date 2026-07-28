# Pull Request — Correção Thunderbird Recovery Suite 2.2.1

## Branch

```text
fix/v2.2.1-validation-cs0136
```

## Título

```text
fix: corrige conflitos de variáveis locais na validação da versão 2.2
```

## Descrição

### Objetivo

Corrigir os três erros `CS0136` identificados no GitHub Actions, preservando integralmente o comportamento funcional da restauração automática de perfis introduzido na versão 2.2.0.

### Alterações

- Renomeia a confirmação da importação de mensagens para `messagesConfirmation`.
- Renomeia a confirmação destrutiva para `criticalConfirmation`.
- Renomeia o caminho relativo da raiz do Thunderbird para `dataRootRelativePath`.
- Renomeia o caminho relativo do cache local para `localCacheRelativePath`.
- Atualiza a versão-base para 2.2.1.
- Adiciona análise do log `82217130534`.

### Critérios de aceite

- Solução compila sem `CS0136`.
- Smoke tests permanecem executáveis.
- Restauração de mensagens continua não destrutiva.
- Substituição de perfil continua exigindo backup e confirmação crítica.
- Restauração completa continua separando a raiz Roaming do cache local opcional.

## Commit

```text
fix: elimina conflitos de escopo na restauração de perfis
```

## Merge

```text
fix: estabiliza validação da Thunderbird Recovery Suite 2.2.1
```
