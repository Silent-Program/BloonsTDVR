# Plan: <Title>

**ID:** PLAN-<YYYYMMDD>-<NNN>
**Status:** Draft | Approved | In Progress | Done | Blocked
**Target Branch:** plan/<slug>
**Created:** <date>
**Approved By:** <user>

## Objective
<One-paragraph goal describing what this plan accomplishes>

## Scope
- **Games affected:** <list from configs/games.toml, e.g., skyrim-se, fallout4>
- **Files/types touched:** <patterns, e.g., "*.esp, *.bsa, scripts/*.pex">

## Phases
### Phase 1: <Name>
- [ ] Task 1 description
- [ ] Task 2 description
**Validation:** <how to verify phase 1 works, e.g., "Launch game, verify UI loads without errors">

### Phase 2: <Name>
- [ ] Task 1 description
- [ ] Task 2 description
**Validation:** <how to verify>

## Risks / Unknowns
- <Risk item 1>
- <Risk item 2>

## Rollback Plan
<Steps to revert if issues arise, e.g.:
1. `git checkout main`
2. `universal-modder deploy <backup-mod-path> --game <game-id>`
3. Verify game launches cleanly>