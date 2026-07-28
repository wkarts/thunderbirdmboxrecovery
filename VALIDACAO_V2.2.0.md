# Validação prevista — Thunderbird Recovery Suite 2.2.0

## Automatizada no CI

- SDK .NET 8.
- Sintaxe dos scripts PowerShell.
- Build da solução com warnings tratados como erros.
- Smoke tests existentes de MBOX, reparo, EML, MSF e SQLite.
- Backup e restauração ZIP e 7Z de perfil.
- Importação de mensagens em pasta isolada sem sobrescrever Inbox existente.
- Rejeição de índices `.msf` antigos durante a importação de mensagens.
- Criação e validação do esqueleto mínimo de um novo perfil.
- Backup completo da raiz de dados com `profiles.ini`, `installs.ini`, perfil e cache local.
- Inspeção do manifesto e restauração integral da raiz.
- Backup de segurança da raiz reconhecido novamente como backup completo.
- Rejeição de novo perfil quando o destino calculado já contém dados.
- Troca transacional de destinos destrutivos por diretório de estágio.
- Validação dos grafos `win-x86` e `win-x64`.

## Manual recomendada

- Instalação tradicional com um e vários perfis.
- Instalação Microsoft Store.
- Perfil em caminho absoluto fora da pasta `Profiles`.
- Criação de novo perfil sem alterar o padrão atual.
- Definição opcional do novo perfil como padrão.
- Substituição de perfil com backup de segurança ZIP e 7Z.
- Restauração somente de mensagens e abertura da pasta em Pastas Locais.
- Backup completo acima de 20 GB.
- Restauração completa com cache local desmarcado e marcado.

## Limitação do ambiente de geração

O pacote foi validado estruturalmente, mas este ambiente não possui SDK .NET nem runner Windows. A compilação e os smoke tests reais devem ser confirmados pelo GitHub Actions antes da release.
