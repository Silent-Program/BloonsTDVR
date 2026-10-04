# Release Template — UniversalModderPriv & Mod Projects

## Release Title Format
```
[vX.Y.Z] <Short imperative summary> — <Capability area>
```

### Examples
```
[v0.3.0] Add BSA archive support — Mod Packaging
[v1.2.4] Fix CTD on Skyrim launch — Stability
[v2.0.0] Migrate to universal-modder v1.0 API — Breaking Changes
```

---

## Release Description Template

Copy and fill out when creating a GitHub Release:

```markdown
## 🎯 Overview
<One-paragraph summary of what this release delivers. Focus on user-facing value.>

## ✨ New Capabilities / Features
<!-- List each new capability with user-facing description -->

### <Capability Area 1>
- **Feature:** <Name>
- **Description:** <What it does for the user>
- **Usage:** <Command/example>

### <Capability Area 2>
- **Feature:** <Name>
- **Description:** <What it does for the user>
- **Usage:** <Command/example>

## 🔧 Improvements & Fixes
<!-- Group by area -->

### <Area>
- fix(scope): <Description>
- refactor(scope): <Description>

## 📦 Build Artifacts
<!-- List downloadable assets -->
- `<mod-name>-vX.Y.Z.mod` — Mod package for <game>
- `source-vX.Y.Z.tar.gz` — Source code
- `BUILD.md` — Build metadata (for 0.x.0 releases)

## ⚠️ Known Limitations / Issues
<!-- Be honest about what doesn't work yet -->

| Issue | Severity | Workaround | Tracking |
|-------|----------|------------|----------|
| <Description> | High/Med/Low | <Steps to mitigate> | #<issue-num> |
| <Description> | High/Med/Low | <Steps to mitigate> | #<issue-num> |

## 🔄 Migration Guide (Breaking Changes Only)
<!-- Required for major version bumps -->

### Breaking Change: <Name>
- **What changed:** <Description>
- **Impact:** <Who/what is affected>
- **Migration:** <Step-by-step instructions>
- **Example:** <Before/after code or config>

## 📋 Checklist
- [ ] All PR checks pass
- [ ] Mod validated with `universal-modder validate`
- [ ] Tested on target game(s): <list>
- [ ] Changelog updated
- [ ] BUILD.md created (for 0.x.0)
- [ ] Downstream projects notified (template releases)

## 🔗 Related
- Plan: `plans/PLAN-<YYYYMMDD>-<NNN>-<slug>.md`
- PR: #<number>
- Issues: #<numbers>
```

---

## Release Creation Checklist

| Step | Command / Action |
|------|------------------|
| 1. Merge PR | Via GitHub UI (squash & merge) |
| 2. Create tag | `git tag -a vX.Y.Z -m "Release vX.Y.Z"` |
| 3. Push tag | `git push origin vX.Y.Z` |
| 4. Create Release | GitHub → Releases → "Create a new release" → Select tag |
| 5. Fill template | Use this template |
| 6. Attach assets | Upload .mod, source, BUILD.md |
| 7. Publish | "Publish release" |

---

## GitHub Release UI Fields

| Field | Value |
|-------|-------|
| **Tag version** | `vX.Y.Z` (select existing tag) |
| **Release title** | `[vX.Y.Z] <Summary> — <Area>` |
| **Description** | Paste filled template above |
| **Pre-release** | ✅ Check if 0.x.y or RC |
| **Latest release** | ✅ Check if stable 1.0.0+ |