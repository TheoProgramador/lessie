# Contexto Lessie - Git Flow Setup e Modernização de Repositório

## Data: 27/07/2026

### Implementação Concluída
✅ Modernização de documentação (README.md atualizado com foco em IA)
✅ Criação de AGENTS.md para colaboração com IA
✅ Estrutura de automação GitHub (.github/workflows/ com CI, validação de branches, retro-merge)
✅ Configuração local de Git (hooks, fast-forward-only merge, squash merge)
✅ Documentação de convenções (Docs/GitFlow-and-Engineering-Standards.md)
✅ Branch develop criada e vinculada remotamente
✅ PR #2 mergeada em develop (feature/ai-repo-structure/001/repository-modernization)
✅ Gitignore atualizado com artefatos de IA e automação

### Pendências (Requer Admin do GitHub)
⏳ Proteção de branches (main e develop)
⏳ GitHub Rulesets com bypass para TheoProgramador
⏳ PR obrigatório e CI obrigatório em branches protegidas
⏳ Retro-merge automático main → develop

## Convenções de Desenvolvimento

### Branches
- main: código em produção (protegida)
- develop: integração (protegida)
- feature/<colaborador>/<numero-issue>/<nome>: novas features
- bugfix/<colaborador>/<numero-issue>/<nome>: correções
- release/<versao>: preparação de versão
- hotfix/<colaborador>/<numero-issue>/<nome>: correções urgentes

### Commits
Conventional Commits com emoji semântico:
- feat: ✨ nova feature
- fix: 🐛 correção
- docs: 📝 documentação
- chore: 🧹 tarefas
- refactor: ♻️ refatoração
- perf: ⚡ performance
- test: ✅ testes
- build: 🔧 build
- ci: 👷 CI/CD

### Merge Policy
- Squash merge obrigatório
- Linear history em branches protegidas
- Sem force push em main/develop
- Forward-only (sem ações destrutivas)

## Arquitetura

### Backend
- .NET 10 com Entity Framework Core
- SQL Server
- JWT authentication
- Mercado Pago integrations
- Groq e Pollinations IA
- Estrutura em camadas: Api, Application, Domain, Infrastructure

### Frontend
- Angular 21
- Rotas protegidas (auth, payment, admin guards)
- Bootstrap 5
- ApexCharts para gráficos

### External
- MCPs para integrações (LinkedIn, JobSpy, Apinfo, etc.)
- Cada MCP em seu próprio diretório

## Documentação Principal
- README.md: visão geral e instruções de execução
- AGENTS.md: guia para colaboração com agentes IA
- .github/copilot-instructions.md: instruções Copilot
- Docs/GitFlow-and-Engineering-Standards.md: fluxo de trabalho
- .github/docs/github-setup.md: guia de setup GitHub (pending owner)

## Workflows GitHub Actions
- ci.yml: valida backend e frontend em PR
- branch-name-validation.yml: valida nomenclatura de branches
- backmerge-develop.yml: retro-merge automático main → develop

## Regras Locais de Git
- core.hooksPath = .githooks
- pull.rebase = false
- merge.ff = only
- merge.squash = true

## Próximos Passos
1. Configurar proteção de branches no GitHub (admin)
2. Validar workflows em primeira PR
3. Adicionar template de ISSUE no .github
4. Onboarding de colaboradores no fluxo Git Flow
