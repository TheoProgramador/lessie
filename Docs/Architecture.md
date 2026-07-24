# Architecture

## Visão Geral

A aplicação é composta por três pilares:

1. Authentication Layer
2. MCP Layer
3. AI Orchestration Layer

O produto é centrado na camada MCP.

---

## Frontend

Tecnologia:

* Angular
* Datta Able

Responsabilidades:

* Login
* Dashboard
* Configuração de MCPs
* Exibição de resultados
* Gerenciamento de sessões

---

## Backend

Tecnologia:

* .NET
* SQL Server

Arquitetura:

src/

* Api
* Application
* Domain
* Infrastructure

---

## Módulos de Negócio

### Authentication

Responsável por:

* Login Google
* JWT
* Refresh Token
* Sessão

### MCP

Responsável por:

* Registro de MCPs
* Execução
* Permissões
* Logs
* Configuração

### Discovery

Responsável por:

* Pessoas
* Empresas
* Oportunidades

### Enrichment

Responsável por:

* Consolidação
* Classificação
* Priorização

### AI Orchestration

Responsável por:

* Seleção de MCPs
* Planejamento de execução
* Consolidação de respostas

---

## Estrutura Conceitual

User

↓

AI Orchestrator

↓

MCP Registry

↓

MCP Runner

↓

MCP Providers

↓

Results

---

## Princípios

1. MCP é o núcleo do produto.
2. Novos MCPs não devem exigir alterações na aplicação principal.
3. Toda integração externa deve passar pela camada MCP.
4. A IA nunca acessa integrações diretamente.
5. A IA consome apenas MCPs registrados.
6. O sistema deve permitir crescimento gradual sem acoplamento excessivo.

---

## Isolamento de Dados

V1:

UserId

Sem multi-tenant.

Caso exista necessidade futura, o modelo poderá ser expandido sem impacto na arquitetura MCP.
