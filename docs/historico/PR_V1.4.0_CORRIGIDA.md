# Branch

```text
feat/v1.4-deleted-message-recovery
```

# Título

```text
feat: recupera mensagens excluídas e fortalece validações da linha 1.4
```

# Descrição

## Objetivo

Consolidar a correção de build da linha 1.3 e adicionar recuperação segura de mensagens ainda presentes no MBOX, porém marcadas como excluídas ou expurgadas.

## Alterações

- corrige a interpolação PowerShell que impedia o workflow de iniciar o publish;
- adiciona parser sintático de scripts PowerShell;
- adiciona build com avisos tratados como erro;
- adiciona smoke tests de status Thunderbird e fracionamento;
- valida `win-x86` e `win-x64` antes de publicar;
- remove a criação de `.msf` artificial;
- recupera as flags `Expunged` e `IMAPDeleted` sem remover as demais flags;
- normaliza status malformado quando a recuperação está habilitada;
- insere cabeçalhos internos ausentes;
- registra métricas de reparo no manifesto e no log;
- mantém releases novas e imutáveis.

## Critérios de aceite

- scripts PowerShell sem erro sintático;
- solução e testes compilados sem aviso;
- teste de `Expunged` aprovado;
- teste de `IMAPDeleted` aprovado;
- flag de mensagem lida preservada;
- cabeçalho malformado reparado;
- publish x86/x64 aprovado;
- nenhuma versão anterior alterada.

# Commit

```text
feat: normaliza status e recupera mensagens excluídas
```
