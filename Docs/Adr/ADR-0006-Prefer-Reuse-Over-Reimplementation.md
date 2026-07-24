# ADR-0006 — Prefer Reuse Over Reimplementation

## Status

Accepted

---

# Contexto

Durante a evolução da Lessie foi identificado que diversos MCPs públicos já implementam integrações complexas com serviços externos.

Exemplos:

* JobSpy MCP
* LinkedIn MCP
* GitHub MCP
* Outros MCPs especializados

Reimplementar essas integrações aumenta significativamente:

* tempo de desenvolvimento;
* custo de manutenção;
* risco de bugs;
* divergência em relação aos projetos originais.

O diferencial competitivo da Lessie não está na implementação dessas integrações.

O diferencial está na inteligência construída sobre elas.

---

# Decisão

Sempre que existir um MCP público maduro, ativo e compatível com os objetivos da Lessie, ele deverá ser preferido em relação ao desenvolvimento de uma nova integração.

A Lessie deverá concentrar seus esforços em:

* Orquestração
* IA
* Normalização
* Deduplicação
* Ranking
* Experiência do usuário

Não em reproduzir funcionalidades já disponíveis.

---

# Processo obrigatório

Antes de desenvolver qualquer integração nova, o Codex deverá executar obrigatoriamente a seguinte sequência.

1.

Pesquisar MCPs públicos.

2.

Pesquisar bibliotecas consolidadas.

3.

Pesquisar SDKs oficiais.

4.

Avaliar licença.

5.

Avaliar manutenção.

6.

Avaliar aderência ao protocolo MCP.

7.

Somente então decidir entre reutilizar ou desenvolver.

---

# Critérios para reutilização

Um projeto poderá ser reutilizado quando possuir:

* manutenção ativa;
* documentação adequada;
* licença compatível;
* arquitetura compreensível;
* aderência ao protocolo MCP;
* facilidade de integração.

---

# Critérios para desenvolvimento próprio

Um MCP deverá ser desenvolvido pela Lessie apenas quando:

* não existir solução equivalente;

ou

* a solução existente estiver abandonada;

ou

* existir limitação técnica que impeça a utilização;

ou

* houver necessidade de diferencial competitivo.

Exemplos:

* APInfo
* ProgramaThor
* Gupy
* Integrações proprietárias

---

# Proibição

Não copiar código de terceiros para dentro da Lessie.

Não manter forks desnecessários.

Não modificar projetos externos diretamente.

Toda customização deverá ocorrer através de Providers ou Adapters próprios.

---

# Arquitetura

Lessie

↓

MCP Client

↓

Provider

↓

MCP Externo

↓

Serviço Externo

---

# Benefícios

* Redução do esforço de desenvolvimento.
* Redução do custo de manutenção.
* Aproveitamento da comunidade.
* Atualizações independentes.
* Menor acoplamento.
* Maior velocidade de evolução.

---

# Consequências

A Lessie passa a ser uma plataforma de orquestração de conhecimento e ferramentas.

Seu objetivo deixa de ser reproduzir integrações existentes e passa a ser transformar múltiplas fontes em uma única experiência inteligente para o usuário.

Este princípio deverá orientar futuras decisões arquiteturais do projeto.
