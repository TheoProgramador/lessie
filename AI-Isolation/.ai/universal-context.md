# Universal AI Development Context - Lessie

**IMPORTANT**: This file is intentionally generic and AI-tool-agnostic. It is designed to work with any AI assistant: Copilot, Cline, Claude, Codex, Cursor, or any other LLM-powered development tool.

## Project Identity

- **Name**: Lessie
- **Type**: Monorepo with .NET backend and Angular frontend
- **Purpose**: AI-assisted platform for professional prospecting, resume improvement, and interview analysis
- **Repository**: https://github.com/TheoProgramador/lessie
- **Owner**: TheoProgramador
- **Status**: Active development (Git Flow implemented 2026-07-27)

## Core Architecture

### Backend (.NET 10)
`
Backend/src/
├── Api/ → Controllers, middleware, HTTP configuration
├── Application/ → DTOs, contracts, service interfaces
├── Domain/ → Business entities
└── Infrastructure/ → EF Core, SQL Server, integrations
`

**Key Services**: JWT Auth, Mercado Pago Payments, Groq/Pollinations AI, SQL Server

### Frontend (Angular 21)
`
FrontEnd/src/app/
├── Protected routes (auth, payment, admin guards)
├── Dashboard, People Discovery, Opportunity Search
├── Resume Improvement, Interview Analysis
└── Chatbot (admin-only)
`

### External (MCPs and Integrations)
`
external/
├── linkedin-mcp-server/
├── jobspy-mcp-server/
├── apinfo-mcp-server/
└── Other integrations
`

## Mandatory Development Standards

### 1. Branch Naming (REQUIRED)
- eature/<user>/<issue>/<name> - New features
- ugfix/<user>/<issue>/<name> - Bug fixes
- elease/<version> - Release preparation
- hotfix/<user>/<issue>/<name> - Emergency fixes
- main - Production (protected)
- develop - Integration (protected)

### 2. Commit Style (REQUIRED)
**Format**: <type>: <emoji> <description>

**Types and Emoji**:
- eat: ✨ new feature
- ix: 🐛 bug fix
- docs: 📝 documentation
- style: 🎨 code style
- efactor: ♻️ refactor
- perf: ⚡ performance
- 	est: ✅ tests
- uild: 🔧 build config
- ci: 👷 CI/CD
- chore: 🧹 maintenance
- evert: ⏪ revert commit

### 3. Git Merge Policy (REQUIRED)
- **Always** squash merge
- **Always** use PR, never commit directly
- **Linear history** enforced
- **No force push** on main/develop
- **CI must pass** before merge
- **Approval required** (1 minimum)

### 4. Code Organization Principles
- Small, atomic changes
- Preserve existing business logic (auth, payments)
- Document integration points
- No secrets in version control
- Reuse existing patterns

## What Has Been Implemented

### ✅ Completed (27/07/2026)
- Git Flow structure with main/develop branches
- Conventional Commits with emoji
- GitHub Actions CI (backend + frontend build)
- Branch validation workflow
- Local Git hooks (fast-forward-only, squash merge)
- Repository documentation (README, AGENTS.md)
- AI development guidelines (AGENTS.md, copilot-instructions.md)
- Cline integration with Polinations.ai
- Universal AI configuration (.ai/config.universal.json)

### ⏳ Pending (Requires GitHub Admin)
- Branch protection rules
- Rulesets with bypass for TheoProgramador
- Automatic retro-merge main → develop
- ISSUE templates

## Files You Must Know

| File | Purpose | Audiences |
|------|---------|-----------|
| .ai/config.universal.json | Universal configuration for all AI tools | All agents |
| .ai/universal-context.md | This file - complete context | All agents |
| AGENTS.md | AI collaboration guidelines | All agents |
| .instructions.md | Generic development instructions | All agents |
| .github/copilot-instructions.md | Copilot-specific | Copilot |
| .cline/config.json | Cline-specific config | Cline |
| .cline/context.md | Cline-specific context | Cline |
| Docs/GitFlow-and-Engineering-Standards.md | Detailed standards | Developers/agents |

## Before Any Implementation

1. **Load this context** - Understand what exists
2. **Read AGENTS.md** - Understand collaboration rules
3. **Check conventions** - Follow branch/commit/merge rules
4. **Plan atomically** - Small, reversible changes
5. **Document changes** - Update relevant docs

## Forbidden Actions

❌ Commit directly to main or develop
❌ Include secrets, API keys, credentials
❌ Force push or delete branches
❌ Use merge commit or rebase merge
❌ Make large non-atomic changes
❌ Break existing business logic

## Technology Stack Summary

| Component | Tech |
|-----------|------|
| Backend API | .NET 10, C# |
| Database | SQL Server, Entity Framework Core |
| Frontend | Angular 21, TypeScript |
| UI Framework | Bootstrap 5 |
| Authentication | JWT + Google OAuth |
| Payments | Mercado Pago |
| AI | Groq API, Pollinations.ai |
| External APIs | LinkedIn, JobSpy, Apinfo MCPs |

## Key Build Commands

`ash
# Backend
cd Backend
dotnet build src/Api/Lessie.Api.csproj

# Frontend
cd FrontEnd
npm install
npm run build
npm run lint
`

## When You Encounter Questions

- **Architecture**: See Docs/Architecture.md
- **Roadmap**: See Docs/Roadmap.md
- **Standards**: See Docs/GitFlow-and-Engineering-Standards.md
- **AI guidelines**: See AGENTS.md
- **Setup/Run**: See README.md

---

**Last Updated**: 2026-07-27
**Git Flow Version**: 1.0
**Maintainer**: TheoProgramador
