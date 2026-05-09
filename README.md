# Legacy Modernizer Toolkit

O `Legacy Modernizer Toolkit` é uma ferramenta para gerar uma camada moderna de consumo de APIs a partir de uma especificação `Swagger/OpenAPI`, com foco em integração rápida com soluções legadas `.NET`.

Na prática, o Toolkit recebe uma spec, gera um client com `Microsoft Kiota`, interpreta esse client e entrega uma solução organizada com contratos, DTOs, facades, services, injeção de dependência e artefatos de apoio para integração.

## O que o Toolkit resolve

Em muitos projetos legados, o acesso a APIs cresce de forma despadronizada:

- chamadas HTTP espalhadas pelo código
- ausência de contratos claros
- acoplamento direto aos endpoints
- dificuldade para reagir a mudanças no Swagger
- manutenção lenta e arriscada

O Toolkit foi criado para atacar esse problema gerando uma camada intermediária padronizada entre o sistema legado e a API externa.

## O que ele entrega

Ao final da geração, o Toolkit entrega:

- client gerado pelo `Kiota`
- contratos próprios para consumo
- DTOs próprios
- facades e services organizados por grupo de API
- mapeadores internos entre Kiota e contratos expostos
- bootstrap de `Dependency Injection`
- manifestos de geração e, no modo `Embedded`, artefatos de integração

## Modos de geração

O Toolkit suporta dois modos principais:

- `Standalone`
  Gera uma solution completa e autônoma, útil para avaliação isolada.

- `Embedded`
  Gera um módulo incorporável para entrar em uma solution já existente, com a convenção:
  - `{Prefix}.Lmt.Application.Contracts`
  - `{Prefix}.Lmt.Application.ApiClient`
  - `{Prefix}.Lmt.Application.Http`

## Como navegar na documentação

Os arquivos abaixo detalham o projeto por tema:

- [PROJECT-OVERVIEW.md](/E:/2-PROJETOS/____LEGACY_MONITOR_TOOLKIT/LegacyModernizerToolkit/PROJECT-OVERVIEW.md)
- [ARCHITECTURE.md](/E:/2-PROJETOS/____LEGACY_MONITOR_TOOLKIT/LegacyModernizerToolkit/ARCHITECTURE.md)
- [BUSINESS-RULES.md](/E:/2-PROJETOS/____LEGACY_MONITOR_TOOLKIT/LegacyModernizerToolkit/BUSINESS-RULES.md)
- [GENERATED-OUTPUT.md](/E:/2-PROJETOS/____LEGACY_MONITOR_TOOLKIT/LegacyModernizerToolkit/GENERATED-OUTPUT.md)

## Stack principal

- `.NET 10`
- `ASP.NET Core MVC`
- `Razor`
- `C#`
- `Microsoft Kiota`
- `System.Text.Json`
- `HttpClient`
- `Microsoft.Extensions.DependencyInjection`

## Resultado esperado para o usuário

Quem usa o Toolkit passa a ter uma forma mais previsível de levar APIs modernas para ambientes legados, reduzindo adaptação manual, melhorando organização e criando uma base mais segura para evolução futura.
