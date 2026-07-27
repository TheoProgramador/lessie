# Copilot Instructions

## Project purpose
Lessie is an AI-assisted platform for professional prospecting, resume improvement, and interview analysis. Keep changes aligned with that product identity.

## Architecture guidance
- Backend work should stay within the .NET API structure under Backend.
- Frontend work should stay within the Angular structure under FrontEnd.
- Reuse existing patterns before introducing new abstractions.

## Documentation expectations
- Keep the main repository documentation accurate and polished.
- When a change affects features, configuration, or developer workflow, update the relevant documentation in the same task.

## Security expectations
- Never commit secrets, local credentials, or environment overrides.
- Prefer environment-based configuration and keep examples non-sensitive.

## Validation
- Prefer a backend build check after API-related changes.
- Prefer an Angular build or lint check after frontend-related changes.
