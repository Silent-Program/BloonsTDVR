# Pull Request: <Title>

## Plan Reference
- **Plan Document:** `plans/PLAN-<YYYYMMDD>-<NNN>-<slug>.md`
- **Plan ID:** PLAN-<YYYYMMDD>-<NNN>
- **Target Branch:** `plan/<slug>`

## Summary of Changes
<!-- Describe what was implemented per phase -->

### Phase 1: <Name>
- Change 1
- Change 2

### Phase 2: <Name>
- Change 1
- Change 2

## Testing / Validation Evidence
<!-- Required: proof that changes work -->

- [ ] Game launches without errors
- [ ] Mod loads in mod manager (MO2/Vortex/manual)
- [ ] In-game functionality verified (screenshots/logs below)
- [ ] No conflicts with existing mods (list tested mods)

### Screenshots / Logs
<!-- Attach or describe validation evidence -->

```
<paste relevant log output here>
```

## Affected Games
<!-- From configs/games.toml -->
- [ ] skyrim-se
- [ ] fallout4
- [ ] other: <game-id>

## Downstream Impact
<!-- If this template is used in a downstream mod repo, note impact -->
- No downstream repos affected
- OR: Affected repos: <list>

## Checklist
- [ ] Plan approved by user before implementation
- [ ] All commits follow conventional format (`type(scope): summary`)
- [ ] Each commit references plan: `Plan: #PLAN-<YYYYMMDD>-<NNN>`
- [ ] Branch created from `main`
- [ ] `universal-modder --version` matches `configs/tools.toml`
- [ ] Game paths from `configs/games.toml` used (no hardcoded paths)
- [ ] Rollback plan tested (optional but recommended)

## Notes for Reviewer
<!-- Any special considerations, known issues, or context -->