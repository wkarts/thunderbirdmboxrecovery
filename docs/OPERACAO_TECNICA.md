# Operação técnica — Thunderbird Recovery Suite 2.1

## Regra principal

Nunca trabalhe sobre a única cópia de um perfil ou MBOX. Preserve o arquivo original e use uma unidade NTFS com espaço suficiente para origem, saída e backup de segurança.

## Explorar e extrair EML

1. Abra a aba **Explorar**.
2. Selecione qualquer arquivo MBOX do Thunderbird, inclusive sem extensão.
3. Clique em **Listar mensagens do MBOX**.
4. Selecione uma ou mais linhas para extrair emails específicos.
5. Use **Extrair selecionada(s) para EML** ou **Extrair todas para EML**.
6. A extração gera arquivos `.eml` e um índice CSV.

A aba **Extrair EML** permanece disponível para filtros por assunto, remetente, destinatário, período, anexo e status de exclusão.

## Reparar

- Arquivo único é o padrão.
- Fracionamento é opcional.
- A origem é aberta somente para leitura.
- O reparo pode normalizar status Mozilla e recuperar mensagens excluídas ainda presentes.
- Não é criado `.msf` artificial.

## Indexar

A aba **Indexar MSF** cria um perfil temporário e usa o Thunderbird instalado para construir o índice real. Em caixas grandes, a operação pode demorar e a pasta pode permanecer ocupada durante a indexação.

## Backup ZIP ou 7Z

1. Feche o Thunderbird.
2. Selecione o perfil.
3. Escolha completo, somente mensagens ou seletivo.
4. Escolha ZIP ou 7Z.
5. Crie o backup.

ZIP é mais compatível. 7Z/LZMA2 normalmente reduz mais o tamanho.

## Restaurar sobre perfil existente

A restauração é uma mesclagem. Com sobrescrita habilitada, arquivos com o mesmo caminho podem ser substituídos. Isso pode alterar mensagens, preferências, índices, catálogos, credenciais e extensões. Arquivos que não existam no backup não são excluídos automaticamente.

Para continuar sobre um perfil que já contém dados:

1. Feche completamente o Thunderbird.
2. Marque a confirmação de compreensão do risco.
3. Digite `RESTAURAR`.
4. Confirme o alerta crítico.
5. Mantenha o backup de segurança habilitado, preferencialmente em 7Z para reduzir espaço ou ZIP para máxima compatibilidade.
6. Quando o backup estiver desabilitado, confirme novamente a operação irreversível.

## Pacotes de distribuição

- `win-x64`: recomendado para Windows moderno.
- `win-x86`: somente para Windows 32 bits.
- executável sem sufixo: portátil e self-contained.
- `runtime-required`: menor, mas exige .NET 8 Desktop Runtime da arquitetura correta.
- ZIP e 7Z: versões compactadas para transporte.
- UPX: opção experimental e desativada por padrão.
