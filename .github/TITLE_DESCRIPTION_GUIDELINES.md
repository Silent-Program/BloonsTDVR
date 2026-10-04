# GitHub Title & Description Guidelines
## For All GitHub Fields with Title/Description Boxes

---

## Universal Principles

| Principle | Application |
|-----------|-------------|
| **Imperative mood** | "Add feature" not "Added feature" or "Adds feature" |
| **Specific over generic** | "Fix CTD on Skyrim launch" not "Fix bug" |
| **Scope in title** | `feat(skyrim-ui):` not just `feat:` |
| **Version in title (PRs/Releases)** | `[v1.2.3]` suffix |
| **Actionable description** | Description enables action without reading code |

---

## Pull Requests

### Title Format
```
<type>(<scope>): <imperative summary> [vX.Y.Z]
```

| Type | When |
|------|------|
| `feat` | New capability |
| `fix` | Bug fix |
| `refactor` | Code restructure, no behavior change |
| `docs` | Documentation only |
| `chore` | Maintenance, tooling, config |
| `test` | Test additions/changes |
| `plan` | Plan document updates |

### Examples
```
feat(skyrim-ui): add custom inventory menu [v1.3.0]
fix(fallout4): resolve CTD on load with mods [v1.2.4]
refactor(mod-utils): simplify archive_create function [v1.2.5]
docs: update AGENTS.md loop detection protocol [v1.2.5]
chore(template): add sync-template.sh script [v1.1.0]
```

### Description Must Include
1. **Plan reference** — `plans/PLAN-<id>.md`
2. **Summary per phase** — What changed, why, files touched
3. **Testing evidence** — Logs, screenshots, validation steps
4. **Version bump rationale** — Why patch/minor/major
5. **Breaking changes** — Migration steps if any
6. **Downstream impact** — Other repos affected

---

## GitHub Releases

### Title Format
```
[vX.Y.Z] <Imperative summary> — <Capability area>
```

### Examples
```
[v0.3.0] Add BSA archive support — Mod Packaging
[v1.2.4] Fix CTD on Skyrim launch — Stability
[v2.0.0] Migrate to universal-modder v1.0 API — Breaking Changes
[v0.5.0] Introduce mod validation pipeline — Quality Assurance
```

### Description Must Include (use `.github/release_template.md`)
1. **Overview** — One-paragraph user-facing value
2. **New Capabilities** — Feature name, description, usage example
3. **Improvements & Fixes** — Grouped by area
4. **Build Artifacts** — Downloadable files listed
5. **Known Limitations** — Table: Issue, Severity, Workaround, Tracking
6. **Migration Guide** — Required for breaking changes
7. **Checklist** — Validation steps completed

---

## GitHub Issues

### Title Format
```
[<type>] <Imperative summary> — <Game/Component>
```

### Types
| Type | Prefix | Example |
|------|--------|---------|
| Bug | `[BUG]` | `[BUG] CTD when loading BSA archive — Skyrim` |
| Feature | `[FEAT]` | `[FEAT] Add BA2 archive support — universal-modder` |
| Task | `[TASK]` | `[TASK] Create mod validation pipeline — scripts` |
| Research | `[RFC]` | `[RFC] Investigate xEdit automation — tooling` |
| Docs | `[DOCS]` | `[DOCS] Document mod-utils.sh functions — docs` |

### Description Must Include (use `.github/ISSUE_TEMPLATE/mod_issue.md`)
1. **Issue Type** — Bug/Feature/Task/Research/Docs
2. **Game/Component** — Target
3. **Summary** — One-paragraph problem/desire
4. **Context** — Plan ref, related PR, trigger
5. **Current vs Expected** — For bugs
6. **Proposed Solution** — For features/tasks
7. **Acceptance Criteria** — Measurable "done" definition
8. **Affected Files** — Scope estimate
9. **Known Constraints** — Blockers, dependencies

---

## GitHub Discussions (if enabled)

### Title Format
```
[<category>] <Question/Proposal summary> — <Context>
```

### Categories
| Category | Prefix |
|----------|--------|
| Question | `[Q]` |
| Proposal | `[PROPOSAL]` |
| Announcement | `[ANNOUNCE]` |
| Showcase | `[SHOWCASE]` |

---

## Commit Messages (for reference)

### Format
```
<type>(<scope>): <imperative summary>

<body: what & why, not how>

Plan: #PLAN-<id>
```

### Scope Guidelines
| Scope | Covers |
|-------|--------|
| `skyrim-ui` | Skyrim UI mod work |
| `fallout4` | Fallout 4 mod work |
| `mod-utils` | `scripts/mod-utils.sh` |
| `git-helpers` | `scripts/git-helpers.sh` |
| `template` | Template repo changes |
| `config` | `configs/` files |
| `ci` | CI/CD (future) |
| `deps` | Dependencies/submodules |

---

## Quick Reference Card

| GitHub Item | Title Pattern | Description Template |
|-------------|---------------|----------------------|
| **PR** | `type(scope): summary [vX.Y.Z]` | `.github/pull_request_template.md` |
| **Release** | `[vX.Y.Z] summary — area` | `.github/release_template.md` |
| **Issue** | `[TYPE] summary — game/component` | `.github/ISSUE_TEMPLATE/mod_issue.md` |
| **Discussion** | `[CAT] summary — context` | Free-form, follow principles |
| **Commit** | `type(scope): summary` | Body + `Plan: #PLAN-<id>` |

---

## Anti-Patterns (Don't Do)

| ❌ Bad | ✅ Good |
|--------|---------|
| "Fix stuff" | `fix(skyrim-ui): resolve texture flicker [v1.2.4]` |
| "Update" | `docs: document mod_build function [v1.2.5]` |
| "WIP" | `feat(universal-modder): add BA2 write support [v1.3.0]` |
| "Bug fix" | `fix(mod-utils): handle empty archive list [v1.2.5]` |
| "v1.2.3" (release title) | `[v1.2.3] Fix CTD on launch — Stability` |
| "Release v1.2.3" | `[v1.2.3] Fix CTD on launch — Stability` |

---

## Enforcement
- **PRs:** `validate_plan` checks commit format; `open_pr` generates title
- **Releases:** Manual — use template
- **Issues:** Template enforced via `.github/ISSUE_TEMPLATE/`
- **Commits:** Manual — convention documented in AGENTS.md