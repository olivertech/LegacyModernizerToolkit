# LegacyModernizer.Generation.Tests

Este projeto concentra os testes de regressao da camada de geracao.

## Golden Files

Os snapshots da solucao gerada ficam em `GoldenFiles/` e usam extensao `.snap`.

Usamos `.snap` em vez de `.cs` porque esses arquivos:

- nao sao codigo-fonte do projeto de testes
- representam o estado esperado da saida gerada
- nao devem entrar no pipeline de compilacao do C#

Essa convencao evita que o SDK do .NET tente compilar arquivos de comparacao como se fossem parte da suite.

## Como pensar nesses arquivos

- arquivo gerado real: sai da composicao da solucao
- arquivo `.snap`: representa o conteudo esperado
- teste golden: compara os dois conteudos

## Quando atualizar um `.snap`

Atualize os snapshots apenas quando a mudanca na geracao for intencional.

Exemplos:

- alteracao deliberada na assinatura de facades ou services
- nova estrategia de DTOs ou mapeamento
- inclusao de manifesto ou novo artefato gerado
- mudanca estrutural aprovada na saida da solucao

## Convencao atual

Os golden tests cobrem arquivos como:

- `IApiFacade`
- `ApiFacade.*`
- `Dtos`
- `Mappers`
- `generation-manifest.json`
