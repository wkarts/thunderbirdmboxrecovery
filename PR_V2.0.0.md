# Pull Request — Thunderbird Recovery Suite 2.0

## Branch

```text
feature/thunderbird-recovery-suite-2.0
```

## Título

```text
feat: consolida suíte completa de recuperação e manutenção do Thunderbird
```

## Descrição

### Objetivo

Consolidar, sem entregas intermediárias, os roadmaps 1.5, 1.6 e 1.7 em uma aplicação 2.0 única para diagnóstico, exploração, reparo, extração, indexação, backup e restauração de caixas e perfis do Mozilla Thunderbird.

### Entregas

- Leitura de MBOX direto ou dentro de ZIP, 7z, RAR, TAR, GZip, BZip2 e XZ.
- Parser binário em fluxo para caixas grandes.
- Exploração estrutural, inventário CSV/JSON e diagnóstico SHA-256.
- Reparo em arquivo único por padrão, com fracionamento opcional entre mensagens.
- Recuperação de mensagens `Expunged` e `IMAPDeleted` ainda presentes no MBOX.
- Extração filtrada para EML.
- Detecção de instalações, versões e arquiteturas do Thunderbird.
- Perfil temporário isolado para criação real do índice pelo Thunderbird.
- Automação assistida por Windows UI Automation com fallback manual.
- Validação de Mork/MSF, comparação de contagem e fallback prospectivo para Panorama/SQLite.
- Backup completo, somente mensagens ou seletivo, com bloqueio de perfil aberto por padrão.
- Restauração completa, somente mensagens ou seletiva, com proteção contra path traversal.
- Registro opcional do perfil restaurado no `profiles.ini`, backup anterior e gravação atômica.
- Interface Windows Forms integrada em oito módulos.
- Publicação portátil self-contained/single-file para `win-x86` e `win-x64`.
- Releases imutáveis e versionadas, sem `continuous`, `--clobber` ou armazenamento de artifacts.

### Segurança

- Origens MBOX abertas somente para leitura.
- Saídas gravadas em arquivos temporários antes da confirmação.
- Hashes SHA-256 e manifestos para auditoria.
- Rejeição de arquivos auxiliares quando a operação exige MBOX.
- Validação de espaço e bloqueio de saída única acima de 4 GiB em FAT32.
- Backup de segurança antes da restauração em destino ocupado.
- Registro de perfil somente com Thunderbird fechado.
- Backup único do `profiles.ini` e limpeza do arquivo temporário em falhas.

### Critérios de aceitação automatizados

- SDK selecionado deve ser .NET 8.
- Todos os scripts PowerShell devem passar na análise sintática.
- Solução deve compilar com warnings tratados como erros.
- Smoke tests devem validar reparo, exclusões, divisão, diagnóstico, EML, Mork, SQLite, backup e restauração completa/somente mensagens.
- Publicação deve produzir executável single-file maior que 1 MiB para x86 e x64.
- Release só pode ser criada depois de todas as validações.
- Tag existente deve causar falha, nunca substituição.

### Testes manuais recomendados

1. MBOX pequeno com mensagens normais, lidas e excluídas.
2. MBOX acima de 4 GiB em NTFS.
3. Caixa real de aproximadamente 28 GiB com timeout ampliado.
4. Backup 7z protegido por senha.
5. Thunderbird x64 e, quando disponível, x86.
6. Indexação com automação e com fallback manual.
7. Perfil completo com Mail, ImapMail, catálogos, calendários e credenciais.
8. Restauração em perfil vazio, somente mensagens e seletiva.
9. Registro no `profiles.ini` com cópia de segurança.
10. Validação futura em build que utilize Panorama.

## Commit principal

```text
feat: entrega suíte integrada de recuperação do Thunderbird
```

## Mensagem de merge

```text
feat: release Thunderbird Recovery Suite 2.0
```
