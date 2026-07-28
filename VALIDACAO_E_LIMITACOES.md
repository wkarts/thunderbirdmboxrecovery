# Validação executada e limitações do ambiente

## Validações executadas neste pacote

- estrutura YAML dos workflows carregada com parser;
- arquivos `.csproj` validados como XML;
- balanceamento estrutural dos arquivos C# verificado;
- solução conferida com projeto de smoke tests incluído;
- ausência de referências removidas (`MsfIndexService`, `CreateMsfPlaceholder`, índices artificiais e validação antiga);
- ausência de `continuous`, `--clobber`, `upload-artifact` e `download-artifact` nos workflows;
- interpolação problemática `$Version:` removida;
- versões do `.csproj` conferidas;
- manifestos e hashes do pacote regenerados.

## Limitação

O ambiente usado para preparar o pacote não possui `dotnet` nem `pwsh`. Portanto, não foi possível afirmar que os binários foram compilados localmente. Para reduzir o risco, o workflow agora bloqueia a release antes da publicação caso qualquer uma destas etapas falhe:

1. análise sintática PowerShell;
2. restore;
3. build com avisos como erro;
4. smoke tests;
5. publish self-contained `win-x86`;
6. publish self-contained `win-x64`.

A release somente é criada depois das seis etapas.


## Correção da validação NETSDK1151

O build normal da solução é framework-dependent. A propriedade self-contained é aplicada somente ao publish portátil. O `global.json` impede que o runner selecione automaticamente uma versão principal do SDK diferente do .NET 8.
