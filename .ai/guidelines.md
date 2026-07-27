# Universal AI Development Guidelines - Lessie

**Target Audience**: All AI-assisted development tools (Copilot, Cline, Claude, Codex, Cursor, etc.)

## Quick Start for Any AI Tool

1. **Load Context**: Read .ai/universal-context.md first
2. **Load Config**: Read .ai/config.universal.json for tool-specific paths
3. **Load Rules**: Understand AGENTS.md for collaboration guidelines
4. **Load Instructions**: Use .instructions.md as generic guide
5. **Start Work**: Follow the workflow below

## Development Workflow

### Step 1: Understand the Task
- Read relevant documentation
- Identify affected modules and layers
- Check existing patterns and precedents
- Ask questions if context is unclear

### Step 2: Create Feature Branch
`ash
git checkout develop
git pull origin develop --ff-only
git checkout -b feature/<your-name>/<issue-number>/<feature-name>
`

### Step 3: Implement Changes
- Keep changes small and atomic
- Preserve existing business logic
- Write code following project patterns
- Test locally if possible

### Step 4: Commit with Conventional Format
`ash
git add <files>
git commit -m "feat: ✨ add new capability"
`

### Step 5: Create Pull Request
`ash
git push -u origin feature/<your-name>/<issue-number>/<feature-name>
gh pr create --base develop --title "feat: ✨ description" --body "Detailed explanation"
`

### Step 6: Review and Merge
- Wait for CI to pass (backend build + frontend build)
- Implement requested changes if any
- Merge with squash only (automatic)

## Code Preservation Rules

### ❌ NEVER Modify These Without Permission
- Backend/src/Api/Program.cs - Authentication and CORS setup
- Backend/src/Infrastructure/* - Payment and IA integrations
- FrontEnd/src/app/guards/* - Route protection logic
- Any payment-related code
- Any authentication code
- Database schema without migration review

### ✅ ALWAYS Update These When Relevant
- README.md - if setup/run instructions change
- AGENTS.md - if collaboration rules change
- Docs/GitFlow-and-Engineering-Standards.md - if standards evolve
- .ai/universal-context.md - if implementation status changes

## Common Tasks and Patterns

### Adding a New API Endpoint
1. Create controller method in Backend/src/Api/Controllers/
2. Define DTO in Backend/src/Application/Contracts/
3. Implement service in Backend/src/Application/Services/
4. Add domain logic in Backend/src/Domain/
5. Use existing patterns (dependency injection, error handling)

### Adding a New Angular Feature
1. Generate component in FrontEnd/src/app/
2. Add route to routing module
3. Apply appropriate guards (auth, payment, admin)
4. Use existing service patterns
5. Follow Bootstrap 5 styling conventions

### Integrating External Service
1. Create adapter in Backend/src/Infrastructure/
2. Define interface in Backend/src/Application/
3. Register in dependency injection
4. Document configuration requirements
5. Add environment variable for API keys

## Files to Reference During Work

| Scenario | Read This File |
|----------|----------------|
| Need architecture overview | Docs/Architecture.md |
| Need product roadmap | Docs/Roadmap.md |
| Need Git Flow details | Docs/GitFlow-and-Engineering-Standards.md |
| Need to run locally | README.md |
| Need IA guidelines | AGENTS.md |
| Need tool-specific setup | .github/copilot-instructions.md or .cline/context.md |
| Unsure about conventions | .ai/universal-context.md (this folder) |

## Error Prevention Checklist

Before submitting a PR, verify:
- [ ] Branch name follows pattern (feature/bugfix/release/hotfix)
- [ ] All commits follow Conventional Commits + emoji
- [ ] No direct commits to main/develop
- [ ] No secrets or credentials in code
- [ ] CI passes (backend + frontend)
- [ ] Documentation updated if needed
- [ ] Changes are atomic and small
- [ ] No existing business logic broken

## Emergency Procedures

### If You Accidentally Committed to main/develop
`ash
git revert <commit-hash>
git push origin main  # or develop
`

### If You Need to Delete a Local Branch
`ash
git branch -D <branch-name>
`

### If You Need to Update develop from main
`ash
git checkout develop
git merge --ff-only origin/main
git push origin develop
`

## Tool-Specific Context

If using a specific tool, also reference:
- **Copilot**: .github/copilot-instructions.md
- **Cline**: .cline/config.json and .cline/context.md
- **Claude**: This folder (.ai/)
- **Cursor**: Create .cursor/instructions.md if needed
- **Generic/Other**: Use .instructions.md and .ai/ files

## Questions and Uncertainty

When uncertain about something:
1. Check .ai/universal-context.md for implementation status
2. Check AGENTS.md for collaboration rules
3. Check relevant documentation in Docs/
4. Check existing code patterns in the codebase
5. Ask the human directly if still uncertain

---

**Last Updated**: 2026-07-27
**Universal Version**: 1.0
