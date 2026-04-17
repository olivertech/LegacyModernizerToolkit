Descrição do projeto
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
Camadas centrais<br>
• Shared<br>
• Domain
<br><br>
    • representar os conceitos centrais do processo<br>
    • sustentar regras de estado e consistência
<br><br>
Camada de orquestração<br>
• Application
<br><br>
    • orquestrar o fluxo inteiro<br>
    • validar o request de uso<br>
    • chamar os serviços necessários<br>
    • montar a resposta final
<br><br>
Camadas de implementação<br>
• Generation
<br><br>
    • executar a geração e composição estrutural
<br><br>
• Infrastructure
<br><br>
    • baixar spec<br>
    • criar diretórios<br>
    • executar processos externos<br>
    • zipar arquivos
<br><br>
Camada de entrada<br>
• Web
<br><br>
    • receber a URL e dados do formulário<br>
    • disparar a operação<br>
    • exibir resultado
<br><br>
