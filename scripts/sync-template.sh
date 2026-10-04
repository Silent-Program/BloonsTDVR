#!/usr/bin/env bash
# sync-template.sh — Pull Template Updates into Downstream Mod Project
#
# DESCRIPTION:
#   Synchronizes a downstream mod project with the UniversalModderPriv template repo.
#   Fetches latest template changes, shows diff, merges with conflict handling,
#   and updates submodules. Essential for propagating workflow improvements.
#
# USAGE:
#   ./scripts/sync-template.sh
#   # Run from mod project root (cloned from template)
#
# WORKFLOW:
#   1. Adds 'template' remote if missing (points to UniversalModderPriv)
#   2. Fetches latest template/main
#   3. Shows new commits not in current project
#   4. Prompts before merging (handles unrelated histories for first sync)
#   5. Updates git submodules (universal-modder, etc.)
#
# USE CASES:
#   - Template maintainer pushed v1.1.0 with new scripts
#   - New mod-utils.sh added to template
#   - universal-modder submodule updated
#   - PR template improved
#   - First-time sync after cloning template
#
# REQUIREMENTS:
#   - Git repo with template remote (auto-added)
#   - SSH key for git@github.com (or HTTPS token)
#   - Mod project cloned from template with --recurse-submodules
#
# INTEGRATION:
#   Called by agents before starting new work:
#   "Before starting any new mod project: Verify the template is current.
#   If UniversalModderPriv has newer commits than your project's template
#   remote, sync first — don't rediscover solved problems." (AGENTS.md)

set -euo pipefail

TEMPLATE_REMOTE="template"
TEMPLATE_URL="git@github.com:Silent-Program/UniversalModderPriv.git"
TEMPLATE_BRANCH="main"

log_info() { echo -e "\033[0;34m[INFO]\033[0m $*"; }
log_success() { echo -e "\033[0;32m[SUCCESS]\033[0m $*"; }
log_warn() { echo -e "\033[1;33m[WARN]\033[0m $*"; }
log_error() { echo -e "\033[0;31m[ERROR]\033[0m $*"; }

# Check we're in a git repo
if ! git rev-parse --git-dir >/dev/null 2>&1; then
    log_error "Not a git repository. Run from your mod project root."
    exit 1
fi

# Add template remote if missing
if ! git remote | grep -q "^${TEMPLATE_REMOTE}$"; then
    log_info "Adding template remote: $TEMPLATE_URL"
    git remote add "$TEMPLATE_REMOTE" "$TEMPLATE_URL"
fi

# Fetch latest
log_info "Fetching template updates..."
git fetch "$TEMPLATE_REMOTE"

# Show what's new
log_info "Template commits not in this project:"
git log --oneline HEAD.."$TEMPLATE_REMOTE/$TEMPLATE_BRANCH" 2>/dev/null | head -20 || log_warn "No new commits or history unrelated"

# Ask before merging
read -p "Merge template/main into current branch? (y/N) " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    log_info "Aborted. You can merge manually later with:"
    echo "  git merge $TEMPLATE_REMOTE/$TEMPLATE_BRANCH --allow-unrelated-histories"
    exit 0
fi

# Merge (allow unrelated histories for first sync)
log_info "Merging template/main..."
if git merge "$TEMPLATE_REMOTE/$TEMPLATE_BRANCH" --allow-unrelated-histories -m "chore(template): sync from UniversalModderPriv@$(git rev-parse "$TEMPLATE_REMOTE/$TEMPLATE_BRANCH")"; then
    log_success "Template merged successfully"
else
    log_warn "Merge conflicts detected. Resolve them, then commit."
    log_info "Files with conflicts:"
    git diff --name-only --diff-filter=U
    exit 1
fi

# Update submodules
log_info "Updating submodules..."
git submodule update --init --recursive

log_success "Sync complete. Review changes and commit if needed."