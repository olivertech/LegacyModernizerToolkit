# Generated Output

## Estrutura padrão

O Toolkit gera três blocos principais:

- `ApiClient`
- `Contracts`
- `Http`

No modo `Standalone`, isso aparece como solution autônoma.

No modo `Embedded`, a convenção atual é:

- `{Prefix}.Lmt.Application.Contracts`
- `{Prefix}.Lmt.Application.ApiClient`
- `{Prefix}.Lmt.Application.Http`

## O que cada projeto entrega

### ApiClient

Contém:

- código bruto gerado pelo `Kiota`
- request builders
- modelos da API

Entrega:

- a base técnica de acesso HTTP

### Contracts

Contém:

- interfaces
- DTOs
- contratos públicos da solução gerada

Entrega:

- a superfície estável que o projeto consumidor deve usar

### Http

Contém:

- facades
- services
- mapeadores
- DI
- autenticação gerada

Entrega:

- implementação concreta do consumo da API

## Artefatos auxiliares

### generation-manifest.json

Registra:

- modo de geração
- autenticação
- nomes de projetos e namespaces
- mapeamentos DTO
- grupos e endpoints processados

### integration-manifest.json

Gerado no modo `Embedded`.

Registra:

- nomes finais dos projetos
- namespaces finais
- entrypoints principais
- orientação de consumo

### INTEGRATION.md

Gerado no modo `Embedded`.

Explica:

- como incorporar os projetos na solution hospedeira
- qual projeto referenciar
- como registrar DI
- como tratar autenticação

## Benefício da saída gerada

A solução final não é apenas um client HTTP.

Ela entrega uma camada intermediária pronta para uso, com mais organização, menos acoplamento e melhor capacidade de manutenção em projetos legados.
