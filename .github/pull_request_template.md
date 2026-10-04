# Pull Request: <type>(<scope>): <imperative summary>

> **Title Format:** `feat(skyrim-ui): add custom inventory menu` | `fix(fallout4): resolve CTD on load` | `docs: update AGENTS.md loop detection`
> **Version:** This PR includes version bump: `vX.Y.Z` (see Version Bump section)
> **Guidelines:** See `.github/TITLE_DESCRIPTION_GUIDELINES.md` for full conventions

---

## Plan Reference
- **Plan Document:** `plans/PLAN-<YYYYMMDD>-<NNN>-<slug>.md`
- **Plan ID:** PLAN-<YYYYMMDD>-<NNN>
- **Target Branch:** `plan/<slug>`
- **Plan Status:** Approved | In Progress | Done

---

## Summary of Changes
<!-- Describe what was implemented per phase. Be specific. -->

### Phase 1: <Name>
- **Change:** Specific change made
- **Why:** Reason for change
- **Files:** `path/to/file.ext`, `another/file.ext`

### Phase 2: <Name>
- **Change:** Specific change made
- **Why:** Reason for change
- **Files:** `path/to/file.ext`

---

## Version Bump
<!-- REQUIRED: Every PR must specify version impact -->

### Version Type
- [ ] **Patch** (`vX.Y.Z+1`) — Bug fixes, small tweaks, no breaking changes
- [ ] **Minor** (`vX.Y+1.0`) — New features, new tools, non-breaking additions
- [ ] **Major** (`vX+1.0.0`) — Breaking changes, config restructure, workflow overhaul

### New Version
**Target Version:** `v<major>.<minor>.<patch>` (e.g., `v1.2.3`)

### Changelog Entry
```markdown
## [vX.Y.Z] - YYYY-MM-DD
### Added
- feat(scope): description

### Changed
- fix(scope): description

### Fixed
- fix(scope): description

### Removed
- chore(scope): description
```

---

## Testing / Validation Evidence
<!-- REQUIRED: Proof that changes work -->

### Automated Checks
- [ ] `./scripts/validate-plan.sh` passes
- [ ] `universal-modder --version` matches `configs/tools.toml`
- [ ] `./scripts/sync-template.sh` (dry-run) shows no conflicts

### Manual Testing
- [ ] Game launches without errors
- [ ] Mod loads in mod manager (MO2/Vortex/manual)
- [ ] In-game functionality verified (screenshots/logs below)
- [ ] No conflicts with existing mods (list tested mods)
- [ ] Rollback plan tested (optional but recommended)

### Screenshots / Logs
<!-- Attach or describe validation evidence -->

```
<paste relevant log output here>
```

---

## Affected Games
<!-- From configs/games.toml -->
- [ ] skyrim-se
- [ ] fallout4
- [ ] other: <game-id>

---

## Affected Configs / Tools
<!-- Check all that apply -->
- [ ] `configs/games.toml` — Game paths/managers
- [ ] `configs/tools.toml` — Tool versions
- [ ] `plans/template.md` — Plan structure
- [ ] `scripts/git-helpers.sh` — Git automation
- [ ] `scripts/mod-utils.sh` — Mod operations
- [ ] `scripts/sync-template.sh` — Template sync
- [ ] `scripts/attempt-track.sh` — Loop detection
- [ ] `.github/pull_request_template.md` — This template
- [ ] `AGENTS.md` — Workflow rules
- [ ] Submodule: `tools/universal-modder` — Version bump

---

## Downstream Impact
<!-- If this template is used in a downstream mod repo, note impact -->
- [ ] No downstream repos affected
- [ ] Affected repos: <list>
- [ ] Requires `./scripts/sync-template.sh` in downstream projects

---

## Breaking Changes
<!-- List any breaking changes and migration steps -->
- **None** — OR —
- **Breaking:** Description of change
  - **Migration:** Steps for downstream projects

---

## Checklist
- [ ] Plan approved by user before implementation
- [ ] All commits follow conventional format (`type(scope): summary`)
- [ ] Each commit references plan: `Plan: #PLAN-<YYYYMMDD>-<NNN>`
- [ ] Branch created from `main`
- [ ] `universal-modder --version` matches `configs/tools.toml`
- [ ] Game paths from `configs/games.toml` used (no hardcoded paths)
- [ ] Version bump type selected above
- [ ] Changelog entry drafted above
- [ ] Rollback plan tested (optional but recommended)

---

## Notes for Reviewer
<!-- Any special considerations, known issues, or context -->

---

## Reviewer Guidelines
- Verify version bump matches change scope (patch/minor/major)
- Confirm changelog entry is accurate
- Check that plan phases are complete
- Validate testing evidence
- Approve → merge → tag will be created automatically