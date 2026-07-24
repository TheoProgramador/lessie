# EPIC-0005 — JobSpy Opportunity Discovery Integration

## Objetivo

Integrar um MCP baseado em JobSpy ao ecossistema Lessie para ampliar significativamente a cobertura de oportunidades de emprego utilizando um único Provider.

A Lessie deve reutilizar um MCP maduro sempre que possível ao invés de reimplementar integrações já existentes.

O APInfo continuará sendo um Provider próprio e independente.

---

# Motivação

O ecossistema JobSpy já suporta diversos portais de vagas.

Entre eles:

* LinkedIn Jobs
* Indeed
* Google Jobs
* Glassdoor
* ZipRecruiter
* Bayt
* Naukri
* BDJobs

A Lessie não deve gastar esforço reimplementando essas integrações.

Seu diferencial é:

* Orquestração
* Normalização
* Deduplicação
* Ranking
* IA

Não scraping.

---

# Estratégia

Antes da implementação, o Codex deverá realizar uma avaliação técnica dos principais MCPs baseados em JobSpy.

Candidato inicial:

Repository:

https://github.com/borgius/jobspy-mcp-server

Caso exista outro projeto claramente superior, justificar tecnicamente a substituição antes da implementação.

Não criar fork.

Não copiar código para dentro da Lessie.

---

# Primeira etapa obrigatória

O Codex deverá:

1. Clonar o repositório escolhido.
2. Instalar dependências.
3. Executar localmente.
4. Confirmar que o servidor MCP inicia corretamente.
5. Descobrir dinamicamente todas as ferramentas expostas.
6. Documentar as ferramentas encontradas.
7. Somente após isso iniciar a integração.

Nunca assumir nomes de ferramentas apenas lendo README.

---

# Estrutura

Lessie

↓

Opportunity Discovery Tool

↓

Opportunity Provider Registry

↓

Providers

* APInfo Provider
* JobSpy MCP Provider

↓

Normalize

↓

Deduplicate

↓

Ranking

↓

AI

---

# Provider

Criar:

JobSpyOpportunityProvider

Responsabilidade:

Conversar exclusivamente com o MCP.

Nenhuma lógica de scraping deve existir dentro da Lessie.

---

# MCP Client

Reutilizar integralmente:

IMcpClient

Não criar cliente específico para JobSpy.

---

# Descoberta dinâmica

Durante inicialização:

ListTools()

↓

Registrar ferramentas disponíveis

↓

Selecionar automaticamente a ferramenta equivalente à pesquisa de vagas.

Nunca hardcodar nomes.

---

# Pesquisa

Entrada conceitual

{
"query": ".NET",
"location": "Brazil",
"remote": true,
"limit": 20
}

O payload real deverá seguir exatamente o contrato exposto pelo MCP.

---

# DTO

Todos os resultados deverão ser convertidos para:

OpportunityDto

Campos:

* Id
* Title
* Company
* Location
* Country
* RemoteType
* EmploymentType
* Salary
* PublishedAt
* Description
* Requirements
* Url
* Source
* Provider

Provider:

JobSpy

Source:

LinkedIn
Indeed
Google Jobs
Glassdoor
ZipRecruiter
Bayt
Naukri
BDJobs

---

# Deduplicação

Após normalização:

Agrupar por:

* Empresa
* Cargo
* Localização
* Similaridade textual

Nunca retornar vagas duplicadas ao usuário.

---

# Ranking

Critérios:

* Recência
* Salário
* Remoto
* Compatibilidade com pesquisa
* Fonte

Nesta etapa utilizar algoritmo determinístico.

Não utilizar IA.

---

# Cache

Cada Provider possuirá cache independente.

Sugestão:

5 minutos

---

# Configuração

Adicionar:

OpportunityProviders

↓

JobSpy

↓

Enabled

Command

Arguments

WorkingDirectory

TimeoutSeconds

Nunca utilizar caminhos hardcoded.

---

# Resiliência

Se o JobSpy estiver indisponível:

Continuar utilizando APInfo.

Nenhum Provider pode interromper os demais.

---

# Logs

Registrar:

* Provider
* Fonte
* Tempo
* Quantidade de resultados

Nunca registrar:

* Cookies
* Tokens
* Credenciais

---

# Frontend

Nenhuma alteração estrutural.

Opportunity Discovery continuará sendo a única tela.

Adicionar apenas identificação visual da origem:

LinkedIn

Indeed

Glassdoor

Google Jobs

Etc.

---

# Critérios de aceite

Concluído quando:

* MCP clonado.
* MCP executando.
* Ferramentas descobertas automaticamente.
* Pesquisa funcionando.
* Resultados normalizados.
* Deduplicação funcionando.
* Ranking funcionando.
* Tela exibindo resultados.
* Origem da vaga exibida.
* Falha do JobSpy não interrompe APInfo.
* Nenhum dado mockado.

---

# Restrições

Não implementar:

* candidatura automática
* envio automático de currículo
* IA de matching
* favoritos
* persistência
* notificações

---

# Definition of Done

O usuário pesquisa apenas uma vez.

A Lessie consulta o JobSpy.

Normaliza.

Deduplica.

Classifica.

Entrega uma única lista de oportunidades sem que o usuário precise conhecer os diversos portais consultados.
