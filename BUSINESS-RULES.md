# Business Rules

## Regras centrais do Toolkit

### A especificação precisa ser válida antes da geração

O pipeline só avança para geração do client quando a spec foi adquirida e validada.

Motivo:

- evitar compor solução a partir de contrato inconsistente
- falhar cedo com erro mais explicativo

### O workspace precisa estar preparado antes de qualquer operação física

Todas as etapas que escrevem ou leem artefatos dependem de um workspace isolado.

Motivo:

- evitar interferência entre execuções
- facilitar rastreabilidade
- manter artefatos temporários organizados

### O client do Kiota é uma base técnica, não o contrato final

Mesmo quando o Kiota gera tipos válidos, eles não devem vazar para o consumidor final da solução gerada.

Motivo:

- o usuário do Toolkit precisa consumir contratos mais estáveis
- o client gerado pode mudar mais do que a API de consumo desejada

### Somente a camada HTTP conhece o Kiota

No modelo atual, `Facades`, `Services`, provider de autenticação e DI vivem na camada HTTP.

Motivo:

- manter o restante da solução desacoplado
- permitir integração mais limpa em legado

### No modo Embedded, a saída precisa nascer incorporável

O modo `Embedded` usa a convenção:

- `{Prefix}.Lmt.Application.Contracts`
- `{Prefix}.Lmt.Application.ApiClient`
- `{Prefix}.Lmt.Application.Http`

Motivo:

- evitar conflitos com `Core`, `Infrastructure`, `Shared` e `Application`
- permitir inclusão direta em solutions já existentes

### O modo de autenticação altera o contrato gerado

O Toolkit suporta:

- `PerMethodToken`
- `AccessTokenAccessor`

No modo `AccessTokenAccessor`, o token não deve aparecer nas assinaturas públicas geradas.

Motivo:

- esconder detalhes de autenticação do consumidor final
- facilitar uso em aplicações com sessão ou contexto autenticado centralizado

### Wrappers paginados precisam ser tratados antes de expor contratos

Quando o Kiota retorna wrappers com `Value` ou `Items`, a camada gerada precisa transformar isso no retorno esperado pela aplicação consumidora.

Motivo:

- evitar vazamento de shape técnico da API
- entregar contratos mais previsíveis

### Fallbacks de geração devem ser explícitos e testados

Quando o Kiota ou a spec não oferecem metadados completos, o Toolkit usa estratégias de fallback controladas.

Motivo:

- preservar robustez da geração
- reduzir falhas em specs heterogêneas
- permitir regressão automatizada sobre casos sensíveis
