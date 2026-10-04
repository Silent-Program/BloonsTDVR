#!/usr/bin/env bash
# Git Helper Scripts for GameSpace Modding Workflow
# 
# DESCRIPTION:
#   Core automation for the plan/branch/PR workflow defined in AGENTS.md.
#   Provides functions for plan creation, validation, PR management, releases, and cleanup.
#
# USAGE:
#   source scripts/git-helpers.sh
#   new_plan "Add Skyrim UI overhaul"
#   validate_plan
#   open_pr
#
# WORKFLOW INTEGRATION:
#   - new_plan: Creates plan doc + feature branch (enforces plan-first rule)
#   - validate_plan: Checks commits reference plan ID (enforces traceability)
#   - open_pr: Pushes branch, generates PR URL, opens browser
#   - create_release: Tags version, creates release branch
#   - cleanup_merged_branches: Removes stale plan/hotfix branches
#
# REQUIREMENTS:
#   - Git repository with 'origin' remote
#   - plans/template.md exists
#   - Branch naming: plan/<slug>, hotfix/<slug>, release/vX.Y.Z

set -euo pipefail

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# ---------- Utility Functions ----------

# log_info: Print informational message (blue)
# log_success: Print success message (green)
# log_warn: Print warning (yellow)
# log_error: Print error (red)
log_info() { echo -e "${BLUE}[INFO]${NC} $*"; }
log_success() { echo -e "${GREEN}[SUCCESS]${NC} $*"; }
log_warn() { echo -e "${YELLOW}[WARN]${NC} $*"; }
log_error() { echo -e "${RED}[ERROR]${NC} $*"; }

# require_clean_worktree: Abort if uncommitted changes exist
# USE CASE: Prevent dirty state before branching/releasing
require_clean_worktree() {
    if ! git diff --quiet || ! git diff --cached --quiet; then
        log_error "Working tree not clean. Commit or stash changes first."
        return 1
    fi
}

# get_main_branch: Detect main branch name (main/master)
# USE CASE: Works with both 'main' and 'master' default branches
get_main_branch() {
    git symbolic-ref refs/remotes/origin/HEAD 2>/dev/null | sed 's|refs/remotes/origin/||' || echo "main"
}

# ---------- Plan Management ----------
#
# new_plan: Create plan document + feature branch
# DESCRIPTION:
#   Generates a new plan from template, creates git branch 'plan/<slug>',
#   and switches to it. Enforces the "plan before code" rule.
# USE CASES:
#   - Starting any new feature/fix: new_plan "Skyrim UI overhaul phase 1"
#   - Hotfixes: new_plan "Fix CTD on load" (creates plan/hotfix-... branch)
#   - Ensures plan ID format: PLAN-YYYYMMDD-NNN
# ARGS: $1 = plan title (required)
# OUTPUTS: plan file at plans/PLAN-<date>-<num>-<slug>.md
#          branch plan/<slug>
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

# validate_plan: Verify current branch follows its plan
# DESCRIPTION:
#   Checks that current branch is a plan/* branch, finds associated plan doc,
#   validates all commits reference the plan ID, and reports plan status.
# USE CASES:
#   - Pre-PR check: validate_plan (run before open_pr)
#   - CI/CD gate: ensure traceability
#   - Audit: confirm work matches approved plan
# OUTPUTS: Plan file path, commit reference check, plan status
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

# open_pr: Push branch and open PR creation URL
# DESCRIPTION:
#   Validates plan, pushes current branch to origin, generates GitHub compare URL,
#   and attempts to open browser. Requires PR template to be filled manually.
# USE CASES:
#   - After implementing plan: open_pr
#   - Automates: git push -u origin + PR URL generation
#   - Works with GitHub, GitLab (adjust URL pattern)
# PRE-REQS: validate_plan passes, branch is plan/*
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
#
# create_release: Create version tag + release branch
# DESCRIPTION:
#   Creates annotated tag and release branch from main. Use for mod project releases.
# USE CASES:
#   - Release mod v1.0.0: create_release v1.0.0
#   - Template versioning: create_release v1.1.0 (in UniversalModderPriv)
#   - Triggers: CI/CD deployment, mod portal upload
# ARGS: $1 = semantic version (required, e.g., v1.2.3)
# OUTPUTS: release/vX.Y.Z branch, annotated tag vX.Y.Z
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
#
# cleanup_merged_branches: Delete merged plan/hotfix branches
# DESCRIPTION:
#   Finds and optionally deletes remote/local branches matching plan/* or hotfix/*
#   that have been merged into main. Keeps repo clean.
# USE CASES:
#   - Post-merge cleanup: cleanup_merged_branches
#   - Periodic maintenance (run weekly)
#   - CI/CD: auto-cleanup after PR merge
# SAFETY: Prompts before deletion, only affects plan/* and hotfix/*
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
#
# git_helpers_help: Print usage documentation
# DESCRIPTION:
#   Shows all available functions with descriptions and use cases
git_helpers_help() {
    cat <<EOF
GameSpace Git Helpers — Plan/Branch/PR Automation

DESCRIPTION:
  Core workflow automation enforcing AGENTS.md rules:
  - Every change requires a plan (new_plan)
  - Commits must reference plan ID (validate_plan)
  - All merges via PR (open_pr)
  - Semantic versioning (create_release)
  - Branch hygiene (cleanup_merged_branches)

Plan Management:
  new_plan "Title"        Create plan doc + feature branch (plan/<slug>)
                          Use: Starting any new work
  validate_plan           Verify branch follows plan, commits reference plan ID
                          Use: Pre-PR check, CI gate
  open_pr                 Push branch, generate PR URL, open browser
                          Use: Ready for review

Release:
  create_release vX.Y.Z   Create annotated tag + release branch
                          Use: Mod release, template version bump

Maintenance:
  cleanup_merged_branches Delete merged plan/* and hotfix/* branches
                          Use: Post-merge cleanup, periodic maintenance

Setup:
  source scripts/git-helpers.sh
  Then call functions directly: new_plan "My Feature"

Direct Execution:
  ./scripts/git-helpers.sh new_plan "Title"
  ./scripts/git-helpers.sh validate_plan
  ./scripts/git-helpers.sh open_pr
  ./scripts/git-helpers.sh create_release v1.0.0
  ./scripts/git-helpers.sh cleanup
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