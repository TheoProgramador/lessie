# GitHub repository setup

This document collects the repository settings that should be applied in GitHub to complete the Git Flow implementation.

## Required settings

### Branch protection for main
- Require pull request before merging
- Require approvals: 1
- Require conversation resolution before merging
- Require linear history
- Require status checks to pass before merging
  - Backend build
  - Frontend build
- Do not allow bypassing the above settings for others
- Allow bypassing only for the repository owner and the current maintainer
- Do not allow force pushes
- Do not allow deletions
- Use squash merge as the default merge method

### Branch protection for develop
- Require pull request before merging
- Require approvals: 1
- Require conversation resolution before merging
- Require status checks to pass before merging
  - Backend build
  - Frontend build
- Do not allow force pushes
- Do not allow deletions

### Repository defaults
- Enable automatic deletion of head branches after merge
- Enable squash merge by default
- Disable merge commits by default
- Disable rebase merges by default

## Recommended GitHub Actions behavior

- The workflow in .github/workflows/ci.yml should validate backend and frontend changes.
- The workflow in .github/workflows/branch-name-validation.yml should validate branch naming.
- The workflow in .github/workflows/backmerge-develop.yml should open a PR from main to develop after main receives a merge.

## Notes

The current environment has GitHub CLI access, but the account does not have repository administration privileges. The steps in this file should be applied by the repository owner or an administrator.
