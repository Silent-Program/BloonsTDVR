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
8. **Track attempts per task** — if stuck on same problem 3+ times, **STOP and re-evaluate** (see Loop Detection below).

---

## 🔄 Loop Detection & Prevention

### The Problem
Agents can get stuck in loops: applying the same fix repeatedly, trying variations of the same approach, or overcomplicating a simple problem.

### Detection Rule
**If you attempt the same task/fix 3+ times without success → PAUSE and re-evaluate.**

### Tracking Mechanism
Log each attempt in the plan document under a new section:

```markdown
## Attempt Log
### Task: <description>
| Attempt | Date | Approach | Result | Notes |
|---------|------|----------|--------|-------|
| 1 | 2026-10-03 | Tried X | Failed: error Y | |
| 2 | 2026-10-03 | Tried X with Z | Failed: error Y | |
| 3 | 2026-10-03 | Tried X differently | Failed: error Y | ⚠️ TRIGGER: 3 attempts |
```

### Re-evaluation Protocol (Trigger at 3 attempts)
**STOP. Do not attempt a 4th time.** Instead:

1. **Step back** — What is the actual goal? (Not the current approach)
2. **Question assumptions** — Is the problem what I think it is?
3. **Search alternatives** — Different tool? Different method? Different order?
4. **Simplify** — Am I overcomplicating? Is there a simpler path?
5. **Ask user** — "I've tried 3 approaches for X. Should I try Y instead?"
6. **Document decision** — Record the pivot in the attempt log

### Helper Script
Use `scripts/attempt-track.sh` to log and check attempts:

```bash
# Log an attempt
./scripts/attempt-track.sh log "Fix BSA packing" "Tried bsarch with --format bsa" "Failed: invalid header"

# Check if trigger threshold reached
./scripts/attempt-track.sh check "Fix BSA packing"
# Returns: "ATTEMPT 3 - TRIGGER RE-EVALUATION" or "ATTEMPT 1 - CONTINUE"
```

### Common Loop Patterns to Watch
| Pattern | Signal | Better Approach |
|---------|--------|-----------------|
| Same error, same fix | Error unchanged after fix | Fix is wrong; diagnose root cause |
| Tweaking parameters | 3+ parameter variations | Step back; understand what params do |
| Adding complexity | Each attempt adds more code | Simplify; remove layers |
| Tool fighting | Tool X fails, try tool Y, then Z | Use right tool for job; check docs |
| Revert → retry | Git revert then same approach | Don't retry same thing; change approach |

### Agent Rule
> **Three strikes = pivot.** If you've logged 3 failed attempts for the same task, you MUST re-evaluate before continuing. Document the pivot in the plan.

---

## Quick Reference Commands

```bash
# Start new plan (creates plan doc + branch)
./scripts/git-helpers.sh new_plan "Short Title"
# or: source scripts/git-helpers.sh && new_plan "Short Title"

# Validate current branch follows plan
./scripts/git-helpers.sh validate_plan

# Select version bump (interactive)
./scripts/git-helpers.sh version_bump

# Open PR for current branch (auto-generates title + version)
./scripts/git-helpers.sh open_pr

# Check current version
./scripts/git-helpers.sh get_current_version

# Suggest version bump from commits
./scripts/git-helpers.sh suggest_version_bump

# Create release tag
./scripts/git-helpers.sh create_release v1.0.0

# Clean up merged branches
./scripts/git-helpers.sh cleanup
```

---

## GitHub Title & Description Guidelines

**All GitHub fields with title/description boxes follow these conventions.**

### Reference Document
See `.github/TITLE_DESCRIPTION_GUIDELINES.md` for full details, examples, and anti-patterns.

### Quick Reference

| GitHub Item | Title Pattern | Description Template |
|-------------|---------------|----------------------|
| **Pull Request** | `type(scope): summary [vX.Y.Z]` | `.github/pull_request_template.md` |
| **Release** | `[vX.Y.Z] summary — area` | `.github/release_template.md` |
| **Issue** | `[TYPE] summary — game/component` | `.github/ISSUE_TEMPLATE/mod_issue.md` |
| **Discussion** | `[CAT] summary — context` | Free-form, follow principles |
| **Commit** | `type(scope): summary` | Body + `Plan: #PLAN-<id>` |

### Core Rules
1. **Imperative mood** — "Add feature" not "Added feature"
2. **Scope in title** — `feat(skyrim-ui):` not `feat:`
3. **Version in PR/Release titles** — `[vX.Y.Z]` suffix
4. **Specific over generic** — "Fix CTD on Skyrim launch" not "Fix bug"
5. **Actionable descriptions** — Enable action without reading code

### PR Title Types
| Type | Use For |
|------|---------|
| `feat` | New capability |
| `fix` | Bug fix |
| `refactor` | Code restructure, no behavior change |
| `docs` | Documentation only |
| `chore` | Maintenance, tooling, config |
| `test` | Test additions/changes |
| `plan` | Plan document updates |

### Issue Title Prefixes
| Type | Prefix |
|------|--------|
| Bug | `[BUG]` |
| Feature | `[FEAT]` |
| Task | `[TASK]` |
| Research | `[RFC]` |
| Docs | `[DOCS]` |

### Release Title Format
```
[vX.Y.Z] <Imperative summary> — <Capability area>
```
Examples: `[v0.3.0] Add BSA archive support — Mod Packaging`

---

## Version Control Hygiene

- `main` = production-ready, deployable state
- No commit history rewriting on shared branches
- Tag releases: `git tag -a vX.Y.Z -m "Release notes"`

---

## Pull Request & Version Bump Workflow

### PR Requirements
- **Every PR must include a version bump** (patch/minor/major)
- **PR title follows conventional commits:** `type(scope): summary [vX.Y.Z]`
- **PR description uses template** (`.github/pull_request_template.md`)
- **Version bump determined by commit analysis:**
  - `feat(...)` → **minor**
  - `fix(...)` → **patch**
  - `BREAKING CHANGE:` or `feat!...` → **major**
  - Default: **patch**

### PR Creation Flow
```bash
# 1. Implement plan on plan/<slug> branch
# 2. Commit with conventional messages + Plan: #PLAN-...
# 3. Run pre-PR validation
source scripts/git-helpers.sh
validate_plan

# 4. Select version bump (interactive)
version_bump
# Shows current version, suggested bump, options for patch/minor/major/custom

# 5. Open PR (auto-generates title with version)
open_pr
# Pushes branch, creates PR URL with title: "feat(skyrim-ui): add menu [v1.2.3]"
# Opens browser to GitHub compare view with template pre-filled
```

### Version Bump Rules
| Change Type | Bump | Example |
|-------------|------|---------|
| Bug fix, typo, small tweak | **patch** | `v1.2.3` → `v1.2.4` |
| New feature, new tool, non-breaking | **minor** | `v1.2.3` → `v1.3.0` |
| Breaking change, config restructure | **major** | `v1.2.3` → `v2.0.0` |

### Pre-1.0 Versioning Rules (0.x.y)
**Special handling for versions before v1.0.0:**

| Current Version | Change Type | Behavior |
|-----------------|-------------|----------|
| `0.0.x` (patch) | Any fix/feat | **Auto-increment patch** — no prompt, no build file |
| `0.x.0` (minor) | feat/new tool | **Requires BUILD.md** — prompts confirmation, generates build file |
| `0.x.y` (patch) | fix/tweak | Standard patch bump (interactive) |
| `0.x.y` | Breaking | Major bump to `1.0.0` (interactive) |

**Rules:**
- **0.0.x → 0.0.x+1**: Fully automatic. `version_bump` detects patch in 0.0.x, increments silently.
- **0.x.0 → 0.x+1.0**: Requires `BUILD.md`. `version_bump` prompts, generates build metadata file, stages it.
- **BUILD.md contents**: Version, plan ID, branch, commit hash, timestamp, changes since last tag, validation checklist.
- **After v1.0.0**: Standard interactive version selection (patch/minor/major/custom).

---

### After PR Merge
1. **Tag created automatically** on merge (via GitHub UI or `create_release`)
2. **Changelog updated** in PR description
3. **Downstream projects** run `./scripts/sync-template.sh` to pull changes

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