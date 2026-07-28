# Changelog

## 2.0.0

- Consolidação dos roadmaps 1.5, 1.6 e 1.7 na suíte final 2.0.
- Módulos Visão geral, Explorar, Testar, Reparar, Extrair, Indexar, Backup e Restaurar.
- Detecção de instalações, versões e arquiteturas x86, x64 e ARM64 do Thunderbird.
- Detecção e registro de perfis pelo `profiles.ini`.
- Indexação assistida em perfil temporário isolado com Thunderbird real.
- Inicialização por `-profile`, `-new-instance` e `-no-remote`.
- Automação de seleção de pasta por Windows UI Automation com fallback manual.
- Preferência de Mork/MSF no perfil isolado e detecção prospectiva de Panorama/SQLite.
- Validação de estabilidade e tolerância adicional para divergência de contagem do índice.
- Exploração e diagnóstico em fluxo de MBOXs grandes.
- Extração filtrada para EML e índice CSV.
- Recuperação opcional de mensagens `Expunged` e `IMAPDeleted`.
- Saída única por padrão e fracionamento opcional entre mensagens.
- Backup completo, somente mensagens ou seletivo, com manifesto e SHA-256.
- Bloqueio de perfil aberto por padrão e registro da substituição emergencial no manifesto.
- Restauração completa, somente mensagens ou seletiva.
- Proteção contra path traversal, arquivos parciais e backup de segurança.
- Registro opcional do perfil restaurado com backup atômico do `profiles.ini`.
- Leitura de ZIP, 7z, RAR, TAR, GZip, BZip2 e XZ.
- Releases automáticas imutáveis `v2.0.<run_number>`.
- Builds portáteis Windows x86 e x64.
- Smoke tests ampliados para reparo, análise, EML, Mork, SQLite, backup e restauração seletiva.
