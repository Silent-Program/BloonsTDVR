#!/usr/bin/env bash
# Git Helper Scripts for GameSpace Modding Workflow
# Usage: source scripts/git-helpers.sh, then call functions

set -euo pipefail

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# ---------- Utility Functions ----------

log_info() { echo -e "${BLUE}[INFO]${NC} $*"; }
log_success() { echo -e "${GREEN}[SUCCESS]${NC} $*"; }
log_warn() { echo -e "${YELLOW}[WARN]${NC} $*"; }
log_error() { echo -e "${RED}[ERROR]${NC} $*"; }

require_clean_worktree() {
    if ! git diff --quiet || ! git diff --cached --quiet; then
        log_error "Working tree not clean. Commit or stash changes first."
        return 1
    fi
}

get_main_branch() {
    git symbolic-ref refs/remotes/origin/HEAD 2>/dev/null | sed 's|refs/remotes/origin/||' || echo "main"
}

# ---------- Plan Management ----------

new_plan() {
    local title="${1:-}"
    if [[ -z "$title" ]]; then
        log_error "Usage: new_plan \"Short Title\""
        return 1
    fi

    require_clean_worktree || return 1

    local date_slug=$(date +%Y%m%d)
    local existing_plans=$(ls plans/PLAN-${date_slug}-*.md 2>/dev/null | wc -l)
    local plan_num=$((existing_plans + 1))
    local plan_id="PLAN-${date_slug}-$(printf "%03d" $plan_num)"
    local slug=$(echo "$title" | tr '[:upper:]' '[:lower:]' | sed 's/[^a-z0-9]/-/g' | sed 's/--*/-/g' | sed 's/^-//;s/-$//')
    local branch_name="plan/${slug}"
    local plan_file="plans/${plan_id}-${slug}.md"

    # Create plan from template
    if [[ ! -f "plans/template.md" ]]; then
        log_error "Template not found: plans/template.md"
        return 1
    fi

    sed -e "s/<Title>/$title/" \
        -e "s/<YYYYMMDD>/${date_slug}/" \
        -e "s/<NNN>/$(printf "%03d" $plan_num)/" \
        -e "s/<slug>/${slug}/" \
        -e "s/<date>/$(date +%Y-%m-%d)/" \
        -e "s/<user>/$USER/" \
        "plans/template.md" > "$plan_file"

    log_info "Created plan: $plan_file"

    # Create and switch to branch
    local main_branch=$(get_main_branch)
    git checkout "$main_branch" >/dev/null
    git pull origin "$main_branch" >/dev/null 2>&1 || true
    git checkout -b "$branch_name"

    log_success "Created branch: $branch_name"
    log_info "Edit $plan_file to flesh out the plan, then get user approval before implementing."
}

validate_plan() {
    local current_branch=$(git branch --show-current)
    if [[ ! "$current_branch" =~ ^plan/ ]]; then
        log_error "Not on a plan branch. Current: $current_branch"
        return 1
    fi

    local plan_slug="${current_branch#plan/}"
    local plan_file=$(ls plans/PLAN-*-${plan_slug}.md 2>/dev/null | head -1)

    if [[ -z "$plan_file" ]]; then
        log_error "No plan document found for branch: $current_branch"
        return 1
    fi

    log_info "Validating against plan: $plan_file"

    # Check that all commits reference the plan
    local plan_id=$(basename "$plan_file" .md)
    local missing_refs=0

    while IFS= read -r commit; do
        local msg=$(git log -1 --format=%B "$commit")
        if [[ ! "$msg" =~ Plan:\ #${plan_id} ]]; then
            log_warn "Commit $commit missing plan reference: $plan_id"
            missing_refs=1
        fi
    done < <(git log --oneline "$(get_main_branch)..HEAD" | awk '{print $1}')

    if [[ $missing_refs -eq 0 ]]; then
        log_success "All commits reference plan: $plan_id"
    fi

    # Check plan status
    local status=$(grep '^\*\*Status:' "$plan_file" | sed 's/.*: *//')
    log_info "Plan status: $status"
}

open_pr() {
    local current_branch=$(git branch --show-current)
    if [[ ! "$current_branch" =~ ^plan/ ]]; then
        log_error "Not on a plan branch. Current: $current_branch"
        return 1
    fi

    validate_plan || return 1

    # Push branch
    log_info "Pushing branch: $current_branch"
    git push -u origin "$current_branch"

    # Generate PR URL (GitHub)
    local remote_url=$(git remote get-url origin)
    local repo_path=$(echo "$remote_url" | sed -E 's/.*[:/]([^/]+\/[^/]+)(\.git)?$/\1/')
    local pr_url="https://github.com/${repo_path}/compare/main...${current_branch}?expand=1"

    log_success "PR ready: $pr_url"
    log_info "Open this URL in browser to create the Pull Request."
    log_info "Fill in the PR template (.github/pull_request_template.md)"

    # Try to open in browser (works on Linux/macOS/WSL)
    if command -v xdg-open >/dev/null; then
        xdg-open "$pr_url" 2>/dev/null &
    elif command -v open >/dev/null; then
        open "$pr_url" 2>/dev/null &
    fi
}

# ---------- Release ----------

create_release() {
    local version="${1:-}"
    if [[ -z "$version" ]]; then
        log_error "Usage: create_release vX.Y.Z"
        return 1
    fi

    require_clean_worktree || return 1

    local main_branch=$(get_main_branch)
    git checkout "$main_branch"
    git pull origin "$main_branch"

    local release_branch="release/$version"
    git checkout -b "$release_branch"

    # Update version in configs/tools.toml if needed
    # (Add version bumping logic here if desired)

    git commit --allow-empty -m "chore(release): $version"
    git tag -a "$version" -m "Release $version"
    git push origin "$release_branch"
    git push origin "$version"

    log_success "Created release branch: $release_branch and tag: $version"
    log_info "Open PR from $release_branch to $main_branch for final review"
}

# ---------- Cleanup ----------

cleanup_merged_branches() {
    local main_branch=$(get_main_branch)
    git checkout "$main_branch" >/dev/null
    git fetch -p origin >/dev/null

    local merged_branches=$(git branch -r --merged origin/"$main_branch" | grep -E 'origin/(plan|hotfix)/' | sed 's/origin\///')

    if [[ -z "$merged_branches" ]]; then
        log_info "No merged plan/hotfix branches to clean up"
        return 0
    fi

    log_info "Merged branches to delete:"
    echo "$merged_branches"

    read -p "Delete these remote branches? (y/N) " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        echo "$merged_branches" | xargs -I {} git push origin --delete {}
        log_success "Remote branches deleted"
    fi

    # Local branches
    local local_merged=$(git branch --merged "$main_branch" | grep -E '^(plan|hotfix)/' | sed 's/^ *//')
    if [[ -n "$local_merged" ]]; then
        echo "$local_merged" | xargs git branch -d
        log_success "Local branches deleted"
    fi
}

# ---------- Help ----------

git_helpers_help() {
    cat <<EOF
GameSpace Git Helpers

Plan Management:
  new_plan "Title"        Create new plan doc + branch
  validate_plan           Verify current branch follows its plan
  open_pr                 Push branch and open PR URL

Release:
  create_release vX.Y.Z   Create release branch + tag

Maintenance:
  cleanup_merged_branches Delete merged plan/hotfix branches

Setup:
  source scripts/git-helpers.sh
  Then call functions directly: new_plan "My Feature"
EOF
}

# Allow direct execution
if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
    case "${1:-help}" in
        new_plan) shift; new_plan "$@" ;;
        validate_plan) validate_plan ;;
        open_pr) open_pr ;;
        create_release) shift; create_release "$@" ;;
        cleanup) cleanup_merged_branches ;;
        *) git_helpers_help ;;
    esac
fi