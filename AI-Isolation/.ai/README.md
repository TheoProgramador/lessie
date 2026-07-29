# Universal AI Development Context - READ THIS FIRST

**For**: Copilot, Cline, Claude, Codex, Cursor, Perplexity, or any LLM-powered development tool.

## Quick Navigation

This folder contains everything you need to develop on Lessie using any AI tool. Follow this order:

1. **START HERE**: Read [universal-context.md](universal-context.md)
   - Project overview
   - Architecture
   - Mandatory standards
   - What''s been implemented

2. **THEN READ**: [guidelines.md](guidelines.md)
   - Development workflow
   - Common patterns
   - Error prevention
   - Emergency procedures

3. **ALSO CHECK**: [config.universal.json](config.universal.json)
   - Tool-specific configuration paths
   - Compatible tools list
   - Project structure

4. **REFERENCE**: [agents.registry.json](agents.registry.json)
   - Mapping of AI tools to their config files
   - Load order for different tools

## For Specific AI Tools

### If Using Copilot
- Load: .github/copilot-instructions.md
- Also load: .ai/universal-context.md

### If Using Cline
- Load: .cline/config.json
- Load: .cline/context.md
- Fallback: .ai/universal-context.md

### If Using Claude (Direct/Web)
- Load: .ai/universal-context.md
- Load: .ai/guidelines.md
- Load: .instructions.md

### If Using Any Other Tool
- Load: .ai/universal-context.md
- Load: .ai/guidelines.md
- Load: .ai/config.universal.json

## Key Rules (MANDATORY)

1. **Branch Naming**: feature/<name>/<issue>/<desc>, bugfix/<name>/<issue>/<desc>
2. **Commits**: feat: ✨ description, fix: 🐛 description (Conventional Commits + emoji)
3. **Merging**: Always squash merge, never direct commits to main/develop
4. **Code**: Small changes, preserve business logic, no secrets

## Essential Documentation

| File | Purpose |
|------|---------|
| AGENTS.md | AI collaboration rules |
| README.md | Project setup & running |
| Docs/Architecture.md | System design |
| Docs/GitFlow-and-Engineering-Standards.md | Detailed standards |
| .instructions.md | Generic development instructions |

## What Has Been Done (27/07/2026)

✅ Git Flow structure (main/develop)
✅ Conventional Commits with emoji
✅ GitHub Actions CI
✅ Branch validation
✅ Local Git hooks
✅ Repository documentation
✅ Cline integration with Polinations.ai
✅ **This universal AI context**

## What''s Still Pending

⏳ GitHub branch protection rules (requires admin)
⏳ ISSUE templates
⏳ Full contributor onboarding

## How to Get Started

1. Read [universal-context.md](universal-context.md) - complete understanding of the project
2. Understand [guidelines.md](guidelines.md) - how to work here
3. Follow the workflow when making changes
4. Commit with Conventional Commits + emoji
5. Create PR to develop

## Files That Auto-Load

When you load this repository in your AI tool, these files should be read:

`
Repository Root
├── .ai/ (Universal AI context) ← START HERE
│   ├── config.universal.json (tool mapping)
│   ├── universal-context.md (project overview)
│   ├── guidelines.md (development workflow)
│   └── agents.registry.json (agent registry)
├── .instructions.md (generic fallback)
├── AGENTS.md (collaboration rules)
├── .github/copilot-instructions.md (if Copilot)
├── .cline/ (if Cline)
└── README.md (for running locally)
`

## If You''re Lost

1. Read .ai/universal-context.md
2. Check .ai/guidelines.md for your specific scenario
3. Look at .ai/config.universal.json for tool mappings
4. Ask questions - don''t guess

---

**Universal Version**: 1.0
**Last Updated**: 2026-07-27
**Maintainer**: TheoProgramador
