> **Histórico da 1.2.0:** a criação de `.msf` vazio foi removida na linha 1.3 corrigida. A validação em campo demonstrou que o próprio Thunderbird reconstrói o índice depois de processar o MBOX. Não use as instruções antigas de criação artificial do `.msf`.

# Alterações da versão 1.2.0

- Saída única sem fracionamento definida como padrão.
- Opção visual para habilitar fracionamento.
- Tamanho das partes habilitado somente quando necessário.
- Nome único no formato `<Caixa>_Recuperada`.
- Saída fracionada preservada no formato `<Caixa>_Recuperada_001`.
- Criação opcional, ativada por padrão, do `.msf` correspondente.
- `.msf` criado como arquivo vazio para reconstrução segura pelo Thunderbird.
- Manifesto registra modo de saída e arquivos `.msf`.
- Instruções de importação adaptadas ao modo escolhido.
- Versão atualizada para 1.2.0.
