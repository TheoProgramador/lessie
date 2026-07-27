# Git Flow and Engineering Standards

## Branch strategy

- `main`: production branch. Protected and reserved for stable releases.
- `develop`: integration branch for ongoing work. All feature and bugfix work is merged here before release preparation.
- `feature/<colaborador>/<numero-da-issue>/<nome-da-feature>`: short-lived branches for new features.
- `bugfix/<colaborador>/<numero-da-issue>/<nome-do-bug>`: short-lived branches for bug corrections.
- `release/<versao>`: stabilization branch before tagging and production promotion.
- `hotfix/<colaborador>/<numero-da-issue>/<nome-do-bug>`: urgent production fixes created from `main`.

## Commit policy

All commits should be:
- atomic
- short
- semantic
- written in Conventional Commits format
- prefixed with a relevant emoji

Recommended format:

```text
<type>(<scope>): <emoji> <short summary>
```

Examples:
- `feat(auth): ✨ add JWT refresh flow`
- `fix(api): 🐛 correct payment webhook validation`
- `docs(readme): 📝 update repository workflow guidance`

## Merge policy

- Prefer pull requests for all changes entering `main` and `develop`.
- Prefer squash merge for a cleaner history.
- Avoid force-pushes on protected branches.
- Keep the history linear and easy to follow.

## Forward-only policy

Destructive actions should not be performed implicitly. Force-pushes, branch deletion, and direct writes to protected branches require explicit approval and a clear reason.
