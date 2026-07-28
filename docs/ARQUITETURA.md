# Arquitetura — Thunderbird Recovery Suite 2.2

## Camadas

- `UI`: Windows Forms, splash, About e páginas operacionais.
- `Core/MBOX`: parser em fluxo, diagnóstico, reparo e extração EML.
- `Core/Indexação`: detecção do Thunderbird, perfil temporário, UI Automation e validação MSF/Panorama.
- `Core/Perfis`: descoberta de raízes de dados, leitura de `profiles.ini`, registro e estimativas.
- `Core/Backup`: criação ZIP/7Z com manifesto e SHA-256.
- `Core/Restauração`: inspeção do backup, seleção automática de destino, segurança, hashes e registro de perfil.

## Diretórios de dados

A suíte trata separadamente:

- **Roaming/dados principais**: mensagens, perfis, contas, preferências, credenciais e arquivos de associação.
- **Local/cache**: conteúdo temporário e regenerável, incluído somente quando solicitado.

Tipos reconhecidos:

- `TraditionalRoaming`;
- `MicrosoftStore`;
- `Custom`.

## Layout dos backups

### Perfil selecionado

```text
profile/<arquivo do perfil>
ThunderbirdRecoverySuite/manifesto_backup.json
```

### Thunderbird completo

```text
thunderbird-root/<profiles.ini, installs.ini, Profiles/...>
local-cache/<cache opcional>
ThunderbirdRecoverySuite/manifesto_backup.json
```

## Destinos de restauração

- `CreateNewProfile`: pasta nova em `Profiles` e registro automático.
- `ReplaceExistingProfile`: substituição controlada com snapshot obrigatório.
- `RestoreMessagesToExisting`: remapeamento para `Mail/Local Folders/<nome>.sbd`.
- `RestoreThunderbirdDataRoot`: restauração integral da raiz de dados.
- `ManualFolder`: destino avançado informado pelo operador.

## Segurança

- Origem aberta somente para leitura.
- Arquivo parcial antes da confirmação.
- SHA-256 opcional por arquivo e obrigatório para o pacote.
- Rejeição de caminhos absolutos e traversal.
- Thunderbird fechado para operações destrutivas.
- Backup de segurança obrigatório em substituições automáticas.

## Descoberta e proteção de destino 2.2

A camada `ThunderbirdProfileService` mantém a raiz tradicional `%APPDATA%\Thunderbird` disponível como destino mesmo quando ainda não existe, detecta raízes da Microsoft Store e admite caminhos personalizados. A tela de restauração associa o tipo registrado no manifesto à raiz compatível encontrada no computador.

A camada `ProfileRestoreService` reforça as confirmações da interface: destinos destrutivos ocupados não podem ser alterados sem backup de segurança e autorização de sobrescrita. Na importação não destrutiva de mensagens, índices e metadados operacionais antigos são descartados para impedir que resumos incompatíveis ocultem as caixas restauradas.
