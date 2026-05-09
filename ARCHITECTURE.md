# Architecture

## Visão geral

O Toolkit foi estruturado em camadas para separar claramente entrada, orquestração, domínio, infraestrutura e geração especializada.

Essa separação existe para que cada parte do projeto tenha uma responsabilidade clara e para que a evolução das regras de geração não fique misturada com UI, filesystem ou detalhes operacionais.

## Camadas

### Web

Responsável por:

- receber a solicitação do usuário
- validar dados de entrada de interface
- acionar o caso de uso principal
- apresentar resultado e download do pacote

Recebe:

- dados de formulário

Entrega:

- `GenerateModernizedClientRequest`

### Application

Responsável por:

- orquestrar o pipeline de modernização
- validar o request funcional
- chamar infraestrutura e geração na ordem correta
- consolidar o resultado final

Recebe:

- request vindo da camada `Web`

Entrega:

- resposta estruturada para a UI
- instruções de execução para `Infrastructure` e `Generation`

### Domain

Responsável por:

- representar conceitos centrais do processo
- manter enums, value objects e entidades do fluxo
- proteger regras básicas de consistência

Recebe:

- dados já normalizados pela aplicação

Entrega:

- tipos centrais usados por todas as outras camadas

### Infrastructure

Responsável por:

- baixar ou ler a spec
- preparar o workspace
- executar operações de filesystem
- gerar pacote final
- controlar download seguro do artefato

Recebe:

- instruções do caso de uso

Entrega:

- arquivos locais
- artefatos intermediários
- pacote final

### Generation

Responsável por:

- analisar a especificação OpenAPI
- agrupar endpoints
- inspecionar a saída do Kiota
- resolver tipos, requests, responses e navegação
- compor a solução final

Recebe:

- spec validada
- client Kiota gerado
- metadados do request

Entrega:

- solução `.NET` pronta para build

## Recursos e por que eles existem

### Microsoft Kiota

Usado para gerar um client HTTP fortemente tipado a partir da spec.

Por que usar:

- evita geração manual de clients
- segue o contrato OpenAPI
- traz request builders e modelos tipados

### DTOs próprios

Usados para impedir que o restante da solução consumidora dependa diretamente dos tipos do Kiota.

Por que usar:

- reduz acoplamento técnico
- melhora estabilidade dos contratos
- facilita evolução futura da camada gerada

### Facades

Usadas para encapsular o acesso ao client gerado.

Por que usar:

- concentram chamadas aos endpoints
- escondem detalhes do Kiota
- padronizam autenticação, query, header e mapeamento

### Services

Usados para expor uma superfície funcional por grupo de API.

Por que usar:

- melhoram organização por contexto de negócio
- dão uma entrada mais amigável para a aplicação consumidora

### Manifestos

Usados para registrar o que foi gerado e como foi resolvido.

Por que usar:

- ajudam em troubleshooting
- facilitam auditoria
- melhoram entendimento do resultado final

## Patterns usados

- `Separation of Concerns`
- `Layered Architecture`
- `Facade`
- `Service Layer`
- `DTO`
- `Mapper`
- `Dependency Injection`
- `Generated Client Adapter`

## Resultado arquitetural

No fim do processo, o Toolkit entrega uma camada de integração que separa o sistema legado dos detalhes brutos da API externa, sem exigir que o consumidor trabalhe diretamente com o Kiota.
