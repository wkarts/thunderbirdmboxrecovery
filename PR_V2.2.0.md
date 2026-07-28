# Pull Request — Thunderbird Recovery Suite 2.2.0

## Branch

```text
feature/v2.2-auto-profile-backup-restore
```

## Título

```text
feat: automatiza seleção de perfis e backup completo do Thunderbird
```

## Objetivo

Aprimorar os módulos de backup e restauração para detectar automaticamente os diretórios de dados e perfis do Thunderbird, permitir criação segura de novo perfil, substituição controlada de perfil existente e backup completo do ambiente Roaming com cache Local opcional.

## Entregas

- Detecção do Thunderbird tradicional e Microsoft Store.
- Identificação automática de `%APPDATA%\Thunderbird` e da área Roaming virtualizada da Store.
- Listagem dos perfis de cada raiz de dados.
- Backup de perfil selecionado ou do Thunderbird completo.
- Inclusão de `profiles.ini`, `installs.ini` e todos os perfis no backup completo.
- Inclusão opcional do AppData Local/cache.
- Manifesto com escopo, tipo de instalação, raízes e perfis.
- Inspeção automática do backup antes da restauração.
- Seleção automática da raiz tradicional, Microsoft Store ou personalizada compatível com o manifesto.
- Caminho estável e exclusivo para criação do novo perfil.
- Criação de novo perfil como modo padrão e recomendado.
- Registro automático do perfil novo no `profiles.ini`.
- Seleção explícita do perfil a substituir.
- Backup de segurança obrigatório antes de substituições.
- Confirmações `SUBSTITUIR PERFIL` e `SUBSTITUIR THUNDERBIRD`.
- Restauração somente de mensagens em pasta isolada de Pastas Locais.
- Exclusão de `.msf` e metadados de controle obsoletos durante a importação de mensagens.
- Restauração integral do diretório de dados para backups completos.
- Cache local desativado por padrão na restauração.
- Smoke tests para os novos fluxos.

## Segurança

- Nenhuma substituição automática é permitida com Thunderbird aberto.
- Operações destrutivas exigem backup de segurança tanto na interface quanto no núcleo.
- A substituição de dados ocupados exige autorização explícita de sobrescrita.
- Arquivos são gravados como `.restore-partial` antes da confirmação.
- Hashes do manifesto são validados.
- Caminhos absolutos e traversal são rejeitados.
- Perfil e raiz completa são restaurados em estágio antes da troca transacional do destino.
- O backup não pode estar armazenado dentro do diretório que será substituído.

## Commit

```text
feat: adiciona descoberta automática e restauração segura de perfis
```

## Mensagem de merge

```text
feat: release Thunderbird Recovery Suite 2.2
```
