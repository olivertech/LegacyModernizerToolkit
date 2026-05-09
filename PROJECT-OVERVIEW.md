# Project Overview

## O que é o projeto

O `Legacy Modernizer Toolkit` é um gerador de soluções de integração para APIs documentadas por `Swagger/OpenAPI`.

Ele foi pensado para cenários em que uma aplicação legado precisa consumir APIs modernas sem espalhar chamadas HTTP, autenticação, parsing e regras de integração pelo código da solução principal.

## Objetivo de negócio

O objetivo do Toolkit é transformar uma especificação OpenAPI em uma camada de acesso organizada, reutilizável e previsível.

Essa camada gerada serve para:

- centralizar o acesso às APIs externas
- reduzir acoplamento do legado com o contrato bruto da API
- acelerar manutenção quando endpoints mudarem
- melhorar onboarding técnico
- permitir evolução incremental de sistemas antigos

## O que entra no Toolkit

O Toolkit recebe como entrada:

- uma especificação `Swagger/OpenAPI`
- nome do projeto de saída
- namespace base
- target framework
- modo de geração
- modo de autenticação

## O que sai do Toolkit

O Toolkit gera uma solução `.NET` com:

- projeto de client HTTP baseado em `Kiota`
- projeto de contratos e DTOs
- projeto HTTP com facades, services, mapeadores e DI
- manifestos com metadados da geração
- guia de integração no modo `Embedded`

## Quem deve usar

O Toolkit é útil para equipes que:

- mantêm sistemas legados em `.NET`
- precisam consumir APIs externas com frequência
- querem padronizar integrações
- precisam reduzir risco de manutenção
- desejam reutilizar a camada gerada em dashboard, MVC, Razor, Blazor ou MAUI

## Benefícios para quem usa

- menos código HTTP espalhado
- menos dependência direta do Swagger original
- contratos mais claros para o sistema consumidor
- integração mais rápida em soluções existentes
- melhor previsibilidade em mudanças futuras de API
- base mais limpa para evolução arquitetural
