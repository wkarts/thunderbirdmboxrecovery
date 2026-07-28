# Changelog

## 2.2.0

- Detecção automática de diretórios de dados tradicionais, Microsoft Store e personalizados.
- Listagem automática dos perfis vinculados a cada diretório de dados.
- Seleção automática da raiz de dados compatível com o tipo registrado no manifesto.
- Disponibilização do caminho tradicional de Roaming mesmo antes da primeira inicialização, permitindo criar um perfil novo automaticamente.
- Backup opcional do Thunderbird completo, incluindo Roaming, `profiles.ini`, `installs.ini` e todos os perfis.
- Inclusão opcional do cache em AppData Local.
- Manifesto ampliado com escopo, raiz de dados, tipo de instalação e perfis incluídos.
- Restauração automática em novo perfil, perfil existente, somente mensagens ou diretório completo.
- Novo perfil criado e registrado automaticamente no `profiles.ini`.
- Caminho do novo perfil estabilizado durante a edição do nome, evitando mudanças aleatórias na interface.
- Criação de estrutura mínima válida (`prefs.js` e `Mail/Local Folders`) quando uma restauração seletiva é registrada como novo perfil.
- Substituição de perfil e de diretório completo com backup de segurança obrigatório e confirmações específicas.
- Backup de segurança e autorização de sobrescrita também validados no núcleo para destinos destrutivos já ocupados.
- Substituições destrutivas usam diretório de estágio e troca transacional, preservando o destino original até que todos os arquivos e hashes sejam validados.
- O backup de segurança da raiz completa usa o prefixo `thunderbird-root`, permitindo identificá-lo e restaurá-lo novamente como backup completo.
- Restauração de mensagens em pasta isolada dentro de Pastas Locais, sem sobrescrever caixas atuais.
- Índices `.msf` e metadados de conta obsoletos são excluídos da importação de mensagens em perfil existente.
- Inspeção automática do backup para selecionar o modo de restauração compatível.
- Novos smoke tests para backup completo, cache local, colisões de perfil, backup de segurança e importação segura de mensagens.

## 2.1.0

- Splash screen e janela Sobre com branding do desenvolvedor.
- Listagem MBOX e extração seletiva para EML.
- Backup ZIP e 7Z.
- Restauração com confirmações e backup de segurança.
- Pacotes de release ZIP, 7Z, self-contained e runtime-required.
