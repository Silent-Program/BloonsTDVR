# AGENTS.md — GameSpace Modding Workspace Template

## Purpose
This repository is a **template workspace** for AI agents working on game modding projects. Clone it into each new mod project to enforce consistent Git workflow, planning discipline, and Universal Modder usage.

---

## Core Workflow (Non-Negotiable)

### 1. Every Change Requires a Plan
- **No direct commits to `main`** — ever.
- Create a **plan document** first (markdown in `/plans/`) describing:
  - Goal / scope
  - Affected games (from `configs/games.toml`)
  - Step-by-step implementation phases
  - Testing/validation approach
- Plan must be approved (by user) before implementation begins.

### 2. Branch Discipline
| Branch Type | Naming | Purpose |
|-------------|--------|---------|
| Feature/Change | `plan/<short-slug>` | One plan = one branch |
| Hotfix | `hotfix/<short-slug>` | Urgent fixes only, still requires plan |
| Release | `release/vX.Y.Z` | Version tags |

- Branch **from `main`** only.
- Branch name must reference the plan (e.g., `plan/skyrim-ui-overhaul-phase1`).

### 3. Commit Rules
- **One logical change per commit** (atomic, revertible).
- Commit messages:
  ```
  <type>(<scope>): <imperative summary>

  <body: what & why, not how>
  ```
  Types: `feat`, `fix`, `refactor`, `docs`, `chore`, `test`, `plan`
- Reference plan in footer: `Plan: #<plan-id>`

### 4. Pull Requests
- **All merges to `main` via PR** — no fast-forward, no direct push.
- PR template (`.github/pull_request_template.md`) must be filled.
- Require **user approval** before merge.
- Delete branch after merge.

---

## Universal Modder Integration

**Primary tool for all game modding operations:**
- Repository: https://github.com/rehan-remade/universal-modder
- Install: `cargo install universal-modder` (Rust) or download binary from releases
- Use for: file patching, archive handling, format conversion, mod packaging, deployment

### Universal Modder Commands (Reference)
```bash
# Initialize mod project
universal-modder init <mod-name> --game <game-id>

# Apply patches
universal-modder patch apply <patch-file> --target <game-path>

# Create mod package
universal-modder package create --output <file.mod>

# Validate mod structure
universal-modder validate <mod-path>

# Deploy to game
universal-modder deploy <mod-path> --game <game-id>
```

> **Agent rule:** Always verify `universal-modder --version` matches `configs/tools.toml` before executing mod operations.

---

## Repository Structure

```
<mod-project>/
├── AGENTS.md              # This file — source of truth for agent behavior
├── plans/                 # Plan documents (one per feature/change)
│   ├── template.md        # Plan template
│   └── <plan-id>-.md     # Individual plans
├── scripts/               # Shared automation scripts
│   ├── git-helpers.sh     # Branch/PR automation
│   └── mod-utils.sh       # Universal-modder wrappers
├── configs/               # Project-specific configs
│   ├── games.toml         # Game registry: id -> install path, mod dir
│   └── tools.toml         # Tool versions (universal-modder, etc.)
├── .github/
│   └── pull_request_template.md
└── README.md              # Project-specific overview
```

---

## Plan Template (`plans/template.md`)

```markdown
# Plan: <Title>

**ID:** PLAN-<YYYYMMDD>-<NNN>
**Status:** Draft | Approved | In Progress | Done | Blocked
**Target Branch:** plan/<slug>
**Created:** <date>
**Approved By:** <user>

## Objective
<One-paragraph goal>

## Scope
- **Games affected:** <list from configs/games.toml>
- **Files/types touched:** <patterns>

## Phases
### Phase 1: <Name>
- [ ] Task 1
- [ ] Task 2
**Validation:** <how to verify>

### Phase 2: <Name>
...

## Risks / Unknowns
- <Item>

## Rollback Plan
<Steps to revert if issues arise>
```

---

## Agent Operating Rules

1. **Read `AGENTS.md` first** — every session.
2. **Check `configs/games.toml`** for game paths before any mod operation.
3. **Create plan → Get approval → Branch → Implement → PR → Merge.**
4. **Never assume game install paths** — always read from config or ask.
5. **Log all universal-modder commands** with output to plan document.
6. **Update this file** when workflow gaps are discovered.
7. **Ask user** if:
   - Plan conflicts with existing work
   - Game version/compatibility unclear
   - Multiple valid approaches exist

---

## Quick Reference Commands

```bash
# Start new plan (creates plan doc + branch)
./scripts/new-plan.sh "Short Title"

# Validate current branch follows plan
./scripts/validate-plan.sh

# Open PR for current branch
./scripts/open-pr.sh
```

---

## Version Control Hygiene

- `main` = production-ready, deployable state
- No commit history rewriting on shared branches
- Tag releases: `git tag -a vX.Y.Z -m "Release notes"`

---

## Setup for New Project (Run Once After Clone)

```bash
# 1. Initialize git (if not already)
git init
git add .
git commit -m "chore(template): initial workspace template"

# 2. Configure game paths
# Edit configs/games.toml with your game install locations

# 3. Pin universal-modder version
# Edit configs/tools.toml

# 4. Create first plan
./scripts/new-plan.sh "Initial project setup"
```

---

## Config File Examples

### `configs/games.toml`
```toml
[skyrim-se]
name = "Skyrim Special Edition"
install_path = "C:/Games/Steam/steamapps/common/Skyrim Special Edition"
mod_dir = "Data"
mod_manager = "mo2"  # or "vortex", "manual"

[fallout4]
name = "Fallout 4"
install_path = "C:/Games/Steam/steamapps/common/Fallout 4"
mod_dir = "Data"
mod_manager = "vortex"
```

### `configs/tools.toml`
```toml
[universal-modder]
version = "0.12.0"  # Pin exact version
install_method = "cargo"  # or "binary"
```

---

## For Template Maintainers

If improving this template:
1. Make changes on a `plan/template-update-<slug>` branch
2. Test with a fresh clone
3. PR to `main` with updated version tag
4. Document breaking changes in `CHANGELOG.md`

---

## 🔴 KEEP THIS TEMPLATE REPO UPDATED — Critical

**This repo (`UniversalModderPriv`) is the source of truth.** Every mod project clones from it. If you improve the workflow, fix a script, update the plan template, or add a new tool — **update this repo first**, then propagate to downstream projects.

### Update Flow
```
1. Make change in THIS repo (UniversalModderPriv)
   → plan/template-update-<slug> branch → PR → main

2. Tag the release
   git tag -a v1.1.0 -m "Template: added mod-utils.sh, fixed PR template"

3. In EACH downstream mod project:
   cd my-mod-project
   git remote add template git@github.com:Silent-Program/UniversalModderPriv.git
   git fetch template
   git merge template/main --allow-unrelated-histories  # or rebase
   # Resolve conflicts, test, commit
```

### What to Watch For
- **`AGENTS.md`** — Workflow changes, new rules, updated commands
- **`scripts/`** — New helpers (`mod-utils.sh`, `sync-games.sh`, etc.)
- **`plans/template.md`** — Better structure, new fields
- **`.github/pull_request_template.md`** — New validation steps
- **`configs/tools.toml`** — New tools, version bumps (universal-modder, xEdit, etc.)
- **Submodule updates** — `tools/universal-modder` version bumps

### Propagation Script (Add to `scripts/sync-template.sh`)
```bash
#!/usr/bin/env bash
# Run in each downstream mod project to pull template updates
git fetch template
git merge template/main --no-edit -m "chore(template): sync from UniversalModderPriv@$(git rev-parse template/main)"
git submodule update --init --recursive
```

### Agent Rule
> **Before starting any new mod project:** Verify the template is current. If `UniversalModderPriv` has newer commits than your project's `template` remote, **sync first** — don't rediscover solved problems.

### Version Tagging
Tag template releases semantically:
- `v1.0.0` — Initial stable template
- `v1.1.0` — New scripts, non-breaking workflow tweaks
- `v2.0.0` — Breaking changes (e.g., plan format change, config restructure)

Downstream projects can then `git merge template/v1.1.0` selectively.