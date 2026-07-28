# Roadmap — Thunderbird Recovery Suite

## Princípios

- nunca alterar a origem;
- gerar hashes antes e depois;
- restaurar em perfil novo por padrão;
- impedir compactação automática durante análise;
- separar reconstrução do MBOX, recuperação lógica, indexação e restauração de perfil;
- manter binários `win-x86` e `win-x64` e releases imutáveis.

## 1.3.0 — estabilização do núcleo e release

Entregáveis:

- correção do PowerShell;
- análise sintática dos scripts;
- smoke tests de MBOX;
- saída única/fracionada;
- validação FAT32 e espaço;
- publicação imutável;
- MSF reconstruído pelo Thunderbird.

Conclusão objetiva: CI, testes e publish x86/x64 aprovados antes da criação da release.

## 1.4.0 — recuperação de mensagens excluídas

Entregáveis:

- leitura e normalização de `X-Mozilla-Status` e `X-Mozilla-Status2`;
- recuperação seletiva de `Expunged` e `IMAPDeleted`;
- preservação das demais flags;
- relatório de status malformado, ausente ou reparado;
- fixtures e smoke tests.

Conclusão objetiva: mensagens de teste excluídas voltam a ser indexáveis e flags não relacionadas permanecem inalteradas.

## 1.5.0 — geração correta do índice MSF

Estratégia recomendada: **indexação assistida pelo próprio Thunderbird**, não geração manual fixa do formato Mork.

Entregáveis:

1. detectar versão e arquitetura do Thunderbird instalado;
2. criar um perfil temporário e isolado;
3. copiar o MBOX recuperado para `Mail\Local Folders\`;
4. iniciar o Thunderbird nesse perfil com integração controlada;
5. solicitar/acompanhar a reparação da pasta usando os componentes internos do Thunderbird ou uma extensão dedicada;
6. esperar a conclusão real da base de resumo;
7. validar que o `.msf` pertence ao MBOX, possui registros coerentes e pode ser reaberto;
8. copiar o par MBOX + `.msf` somente após validação;
9. manter matriz de compatibilidade para Thunderbird Release e ESR;
10. oferecer modo sem interface para uso técnico e modo assistido com progresso.

Risco principal: `.msf` usa banco interno e sua implementação pode mudar. A transição da arquitetura de pastas para Panorama/SQLite torna um gravador Mork externo ainda mais frágil. Por isso, a ferramenta deve usar a versão instalada do Thunderbird como mecanismo oficial de indexação.

Conclusão objetiva: índices produzidos e reabertos com sucesso nas versões suportadas do Thunderbird, sem reparo manual adicional.

## 1.6.0 — exploração, diagnóstico e extração

Entregáveis:

- varredura sem gerar saída;
- resumo por datas, domínios, remetentes, assuntos, tamanho e anexos;
- mapa de offsets das mensagens;
- modos estrito e tolerante de detecção MBOX;
- visualização de mensagens recuperáveis antes da execução;
- extração seletiva para `.eml`;
- quarentena de blocos corrompidos;
- deduplicação opcional por `Message-ID` + hash;
- relatório HTML/JSON de integridade.

Conclusão objetiva: o operador consegue identificar, filtrar e extrair mensagens específicas sem abrir o Thunderbird.

## 1.7.0 — backup e restauração do Thunderbird

Entregáveis:

- descoberta de `profiles.ini` e perfis existentes;
- backup consistente com Thunderbird fechado;
- inclusão configurável de contas, `Mail`, `ImapMail`, `Local Folders`, preferências, filtros, catálogo de endereços e calendários locais;
- manifesto, hashes e versão do Thunderbird;
- compactação 7z opcional com senha;
- restauração sempre para novo perfil por padrão;
- pré-visualização das alterações e rollback;
- migração seletiva de uma ou mais caixas;
- validação pós-restauração e geração assistida dos índices.

Conclusão objetiva: backup validado por hash e restauração funcional em perfil isolado sem sobrescrever o perfil original.

## 2.0.0 — Recovery Suite integrada

Módulos:

- **Explorar:** inventário de perfis, contas, caixas e arquivos auxiliares;
- **Testar:** diagnóstico de integridade, permissões, disco, formato e índices;
- **Reparar:** reconstrução MBOX, flags, terminadores e fragmentos recuperáveis;
- **Extrair:** exportação MBOX/EML e seleção por filtros;
- **Backup:** perfil completo ou seletivo, com hashes e compactação;
- **Restaurar:** novo perfil, migração seletiva e rollback;
- **Indexar:** geração assistida de `.msf` pelo Thunderbird;
- **Relatórios:** logs técnicos, JSON, HTML e cadeia de custódia.

## Matriz obrigatória de testes

- Windows x86 e x64;
- Thunderbird Release e ESR suportados;
- MBOX pequeno, multi-gigabyte e arquivo esparso de teste;
- separadores LF, CRLF e arquivo sem newline final;
- status válido, ausente, duplicado e malformado;
- mensagem expurgada e IMAPDeleted;
- anexos grandes e linhas longas;
- origem direta e 7z com/sem senha;
- destino NTFS, exFAT e bloqueio FAT32;
- interrupção/cancelamento e retomada planejada;
- antivírus e arquivo bloqueado;
- backup/restauração em perfil limpo.
