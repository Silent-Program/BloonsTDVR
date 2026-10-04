# Agent Knowledge Base — UniversalModderPriv Template Maintainer

## Purpose
Private notes for maintaining the **UniversalModderPriv template repository** (in `template/`). This file is **NOT pushed to git** — it's my persistent context across sessions.

---

## Repository Structure

```
GameSpace/DefaultWorkspace/
├── template/                 # THE TEMPLATE REPO (pushed to GitHub)
│   ├── AGENTS.md             # Agent instructions for mod projects
│   ├── plans/template.md     # Plan template
│   ├── configs/
│   │   ├── games.toml        # Game registry example
│   │   └── tools.toml        # Tool versions (universal-modder submodule)
│   ├── scripts/
│   │   ├── git-helpers.sh    # Branch/PR automation
│   │   ├── sync-template.sh  # Pull template updates into downstream projects
│   │   └── mod-utils.sh      # (TODO) Universal-modder wrappers
│   ├── .github/
│   │   └── pull_request_template.md
│   └── tools/
│       └── universal-modder/ # Git submodule (rehan-remade/universal-modder)
├── .opencode/
│   └── AGENTS.md             # THIS FILE (private, not pushed)
└── .gitignore                # Should ignore .opencode/
```

---

## Template Maintainer Workflow

### When Improving the Template
```bash
cd template
# 1. Create plan branch
source scripts/git-helpers.sh
new_plan "Describe improvement"

# 2. Make changes in template/
# 3. Test with fresh clone elsewhere
# 4. PR & merge to template/main

# 5. Tag release
git tag -a v1.1.0 -m "Template: <description>"
git push origin v1.1.0
```

### Propagating to Downstream Projects
Each mod project has `scripts/sync-template.sh`:
```bash
cd my-mod-project
./scripts/sync-template.sh
# Resolves conflicts, updates submodules
```

---

## Key Files to Watch

| File | Why It Matters |
|------|----------------|
| `template/AGENTS.md` | Core workflow rules — update when process changes |
| `template/scripts/git-helpers.sh` | Automation — add new helpers here |
| `template/scripts/sync-template.sh` | Propagation mechanism — keep working |
| `template/plans/template.md` | Plan structure — evolve as needed |
| `template/configs/tools.toml` | Tool versions — bump when submodule updates |
| `template/tools/universal-modder` | Submodule — update deliberately |

---

## Current State (as of last session)

- **Template repo**: `git@github.com:Silent-Program/UniversalModderPriv.git` (pushed to `main`)
- **Submodule**: `tools/universal-modder` → rehan-remade/universal-modder @ v0.12.0
- **SSH key**: `~/.ssh/id_ed25519_gamespace` added to GitHub
- **Tagged releases**: v1.0.0 (initial), ready for v1.1.0
- **Downstream projects**: None yet — first will be created from template

---

## Next Improvements (Planned)

1. **`mod-utils.sh`** — Wrapper functions for common universal-modder operations
2. **`sync-games.sh`** — Fetch game paths from central config (if user sets one up)
3. **CI/CD template** — GitHub Actions for validation (optional)
4. **Better plan validation** — Check phases, dependencies in `validate_plan`
5. **Mod project generator** — `./scripts/new-project.sh "My Mod"` scaffolds everything

---

## User Preferences

- **Branch/PR workflow**: Strict — no direct commits to main
- **Plan-first**: Every change requires approved plan document
- **Universal Modder**: Submodule, pinned version, build from source
- **Game paths**: Per-project `configs/games.toml` (no global config)
- **CI/CD**: None yet — manual validation only
- **Template updates**: Explicit propagation via `sync-template.sh`

---

## Session Resumption

When user says *"working on the template repo"* or references UniversalModderPriv:
1. Read `template/AGENTS.md` for current workflow
2. Check `template/configs/tools.toml` for tool versions
3. Check git log for recent template changes
4. Apply maintainer workflow above