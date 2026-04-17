Descrição do projeto
<br><br>
Uma ferramenta para acelerar a modernização de APIs legadas em .NET, padronizando consumo, reduzindo acoplamento e melhorando arquitetura.
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
• Camada Web = entrada
• Camada Application = orquestração
• Camada Domain = regras e tipos centrais
• Camadas Generation/Infrastructure = execução real
