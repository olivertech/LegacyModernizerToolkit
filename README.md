Descrição do projeto
<br><br>
Testes de geraÃ§Ã£o
<br><br>
O projeto possui testes de regressÃ£o da soluÃ§Ã£o gerada em `LegacyModernizer.Generation.Tests`.
<br><br>
Os arquivos de comparaÃ§Ã£o dos golden tests usam extensÃ£o `.snap` em vez de `.cs`.
<br><br>
Motivo:
<br><br>
â€¢ `.snap` deixa claro que o arquivo Ã© um snapshot de comparaÃ§Ã£o<br>
â€¢ evita que o SDK do .NET trate o arquivo como cÃ³digo-fonte compilÃ¡vel<br>
â€¢ reduz conflito com os globs padrÃ£o de `Compile` do projeto de testes<br>
â€¢ facilita a manutenÃ§Ã£o de artefatos esperados como `IApiFacade`, `ApiFacade.*`, `Dtos`, `Mappers` e `generation-manifest.json`
<br><br>
Na prÃ¡tica, os golden tests validam o conteÃºdo da saÃ­da gerada comparando os arquivos reais com os snapshots versionados.
<br><br>
Uma ferramenta para acelerar a modernização de APIs legadas em .NET, padronizando consumo, reduzindo acoplamento e melhorando arquitetura. Ou seja, é gerar uma solução .NET modernizada, a partir de uma spec Swagger/OpenAPI.
<br><br>
O que o toolkit entrega:
<br><br>
1 - Gera client com Microsoft Kiota
<br><br>
2 - Cria uma camada intermediária padronizada:<br>
    • Services<br>
    • Facade<br>
    • DTOs organizados
<br><br>
3 - Aplica boas práticas automaticamente:<br>
    • separação de responsabilidades<br>
    • centralização de chamadas<br>
    • estrutura pronta para DI
<br><br>
4 - Entrega um projeto base pronto para uso
<br><br>
Esse projeto adota o padrão de arquitetura "Clean Architecture" simplificada.
<br><br>
Na prática:
<br><br>
• Camada Web = entrada<br>
• Camada Application = orquestração<br>
• Camada Domain = regras e tipos centrais<br>
• Camadas Generation/Infrastructure = execução real
<br><br>
Stack do projeto:
<br><br>
• ASP.NET Core<br>
• .NET 10<br>
• Kiota CLI<br>
• filesystem local<br>
• zip packaging<br>
• Razor Pages
<br><br>
O projeto segue as seguintes definições por camadas:
<br><br>
Camada central - Domain
<br><br>
    • representar os conceitos centrais do processo<br>
    • sustentar regras de estado e consistência
<br><br>
Camada de orquestração - Application
<br><br>
    • orquestrar o fluxo inteiro<br>
    • validar o request de uso<br>
    • chamar os serviços necessários<br>
    • montar a resposta final
<br><br>
Camada de implementação 1 - Generation
<br><br>
    • executar a geração e composição estrutural
<br><br>
Camada de implementação 2 - Infrastructure
<br><br>
    • baixar spec<br>
    • criar diretórios<br>
    • executar processos externos<br>
    • zipar arquivos
<br><br>
Camada de entrada - Web
<br><br>
    • receber a URL e dados do formulário<br>
    • disparar a operação<br>
    • exibir resultado
<br><br>
