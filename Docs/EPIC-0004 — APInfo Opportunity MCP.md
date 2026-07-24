# EPIC-0004 — APInfo Opportunity MCP

## Objetivo

Implementar o primeiro MCP proprietário da Lessie para busca de oportunidades no APInfo.

O MCP deverá permitir:

* Login no APInfo.
* Pesquisa de vagas.
* Leitura de detalhes.
* Retorno estruturado.
* Integração com o MCP Client já existente.

O MCP deverá ser implementado como servidor MCP real.

---

## Motivação

O APInfo é uma das maiores bases históricas de vagas de TI do Brasil.

Possui:

* Pesquisa de vagas.
* Banco de currículos.
* Filtros por cargo.
* Filtros por localização.
* Vagas Home Office.
* Vagas PJ.
* Vagas CLT.

O objetivo inicial será apenas consumo de vagas.

Não consumir currículos nesta etapa.

---

## Arquitetura

Lessie

↓

MCP Client

↓

APInfo MCP Server

↓

Playwright

↓

APInfo

---

## Tecnologia

Servidor MCP:

```text
.NET 9
ModelContextProtocol SDK
Playwright
```

Não utilizar Python.

Não utilizar Node.

Implementar diretamente em .NET.

---

## Estrutura

src/

```text
ApInfoMcpServer

├── Tools
├── Models
├── Services
├── Scrapers
├── Playwright
└── Program.cs
```

---

## Ferramentas MCP

### apinfo.search_jobs

Responsável por pesquisar vagas.

Entrada:

```json
{
  "query": ".NET",
  "location": "Home Office",
  "limit": 20
}
```

Saída:

```json
[
  {
    "id": "85297",
    "title": "Desenvolvedor .NET",
    "company": "Empresa",
    "location": "Home Office",
    "date": "2026-06-24",
    "url": "..."
  }
]
```

---

### apinfo.get_job_details

Responsável por abrir uma vaga.

Entrada:

```json
{
  "jobId": "85297"
}
```

Saída:

```json
{
  "id": "85297",
  "title": "...",
  "company": "...",
  "location": "...",
  "description": "...",
  "requirements": "...",
  "url": "..."
}
```

---

### apinfo.search_jobs_by_stack

Entrada:

```json
{
  "technology": ".NET"
}
```

Retorna vagas relacionadas.

---

## Login

Criar configuração opcional:

```json
{
  "ApInfo": {
    "Username": "",
    "Password": ""
  }
}
```

Caso credenciais existam:

* Realizar login.
* Reutilizar sessão.

Caso não existam:

* Operar em modo público.

O MCP deve funcionar sem login sempre que possível.

---

## Sessão

Persistir sessão Playwright.

Local:

```text
storage/apinfo-session.json
```

Reutilizar sessão válida.

Evitar login a cada execução.

---

## Scraper

Criar serviço:

```text
ApInfoScraper
```

Responsável por:

* Navegação.
* Pesquisa.
* Extração.
* Paginação.

Nenhuma lógica MCP dentro do scraper.

---

## Normalização

Criar:

```csharp
JobOpportunityDto
```

Campos:

```csharp
Id
Title
Company
Location
Date
Description
Requirements
Url
Source
```

Source:

```text
APInfo
```

---

## Integração com Lessie

Criar ferramenta:

```text
opportunity.search
```

Implementação:

```text
OpportunitySearchTool

↓

APInfo MCP
```

---

## Frontend

Criar menu:

```text
Opportunity Discovery
```

Rota:

```text
/opportunity-discovery
```

Campo de busca.

Tabela de resultados.

Cards opcionais.

---

## Exibição

Mostrar:

* Cargo
* Empresa
* Localização
* Data
* Link

Botão:

```text
Ver detalhes
```

---

## Critérios de aceite

Concluído quando:

* MCP Server inicia.
* MCP Client conecta.
* Ferramenta apinfo.search_jobs funciona.
* Ferramenta apinfo.get_job_details funciona.
* Tela Opportunity Discovery funciona.
* Resultados aparecem na interface.
* Nenhum dado mockado é utilizado.
* Erros reais são exibidos.
* Login opcional funciona.
* Sessão Playwright é reutilizada.

---

## Restrições

Não implementar:

* Candidatura automática.
* Envio automático de currículos.
* Automações de contato.
* Empresas.
* Currículos.
* IA de matching.

Foco exclusivo:

Buscar vagas reais e exibir resultados reais.

---

## Definition of Done

Usuário acessa:

```text
Opportunity Discovery
```

Pesquisa:

```text
.NET remoto
```

A Lessie consulta o APInfo através do MCP.

Resultados reais aparecem na tela.

Sem mocks.

Sem dados fictícios.

Sem dependência do chatbot.

```
```
