# Alterações v1.1.0

- A origem direta deixou de ser específica para `Inbox`.
- Seleção de qualquer arquivo MBOX do Thunderbird, inclusive arquivos sem extensão.
- Suporte nominal a `Inbox`, `Sent`, `Drafts`, `Archives`, `Trash`, `Templates`, `Junk` e pastas personalizadas.
- Preservação do nome da caixa nos arquivos recuperados.
- Rejeição explícita de `.msf`, bancos SQLite e outros arquivos auxiliares.
- O seletor exibe todos os arquivos por padrão para permitir escolher MBOX sem extensão.
- Diretório de resultado e instruções de importação agora usam o nome real da caixa.

Exemplos:

```text
Inbox     -> Inbox_Recuperada_001
Sent      -> Sent_Recuperada_001
Clientes  -> Clientes_Recuperada_001
```
