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

# open_pr: Push branch and open PR creation URL with version tag
# DESCRIPTION:
#   Validates plan, runs version_bump if not done, pushes branch, generates PR URL
#   with conventional title including version, opens browser.
# USE CASES:
#   - After implementing plan: open_pr (auto-runs version_bump if needed)
#   - Automates: version bump → git push → PR URL with title
#   - Works with GitHub, GitLab (adjust URL pattern)
# PRE-REQS: validate_plan passes, branch is plan/*
open_pr() {
    local current_branch=$(git branch --show-current)
    if [[ ! "$current_branch" =~ ^plan/ ]]; then
        log_error "Not on a plan branch. Current: $current_branch"
        return 1
    fi

    validate_plan || return 1

    # Check for version bump
    if [[ ! -f .git/pr_version ]]; then
        log_info "No version bump selected. Running version_bump..."
        version_bump || return 1
    fi
    local pr_version=$(cat .git/pr_version)

    # Generate PR title
    local plan_id=$(ls plans/PLAN-*-${current_branch#plan/}.md 2>/dev/null | head -1 | xargs basename | sed 's/.md$//')
    local pr_title=$(generate_pr_title "$plan_id")

    # Push branch
    log_info "Pushing branch: $current_branch"
    git push -u origin "$current_branch"

    # Generate PR URL with title and body (GitHub)
    local remote_url=$(git remote get-url origin)
    local repo_path=$(echo "$remote_url" | sed -E 's/.*[:/]([^/]+\/[^/]+)(\.git)?$/\1/')
    local pr_url="https://github.com/${repo_path}/compare/main...${current_branch}?expand=1&title=$(echo "$pr_title" | sed 's/ /%20/g')"

    log_success "PR ready: $pr_url"
    log_info "PR Title: $pr_title"
    log_info "Version: $pr_version"
    log_info "Open this URL in browser to create the Pull Request."
    log_info "Fill in the PR template (.github/pull_request_template.md)"

    # Try to open in browser (works on Linux/macOS/WSL)
    if command -v xdg-open >/dev/null; then
        xdg-open "$pr_url" 2>/dev/null &
    elif command -v open >/dev/null; then
        open "$pr_url" 2>/dev/null &
    fi

    # Cleanup
    rm -f .git/pr_version
}

# ---------- Version Management ----------
#
# get_current_version: Extract current version from git tags
# DESCRIPTION:
#   Finds latest semantic version tag (vX.Y.Z)
# OUTPUTS: Version string (e.g., v1.2.3) or v0.0.0 if none
get_current_version() {
    git tag --sort=-v:refname | grep -E '^v[0-9]+\.[0-9]+\.[0-9]+$' | head -1 || echo "v0.0.0"
}

# bump_version: Calculate next version based on bump type
# DESCRIPTION:
#   Increments version per semantic versioning rules
# ARGS: $1 = current version (e.g., v1.2.3), $2 = bump type (patch|minor|major)
# OUTPUTS: New version string
bump_version() {
    local current="${1:-}"
    local bump_type="${2:-patch}"
    if [[ -z "$current" ]]; then
        log_error "Usage: bump_version <current-version> [patch|minor|major]"
        return 1
    fi
    local major minor patch
    major=$(echo "$current" | sed -E 's/^v([0-9]+)\..*/\1/')
    minor=$(echo "$current" | sed -E 's/^v[0-9]+\.([0-9]+)\..*/\1/')
    patch=$(echo "$current" | sed -E 's/^v[0-9]+\.[0-9]+\.([0-9]+).*/\1/')
    case "$bump_type" in
        major) major=$((major + 1)); minor=0; patch=0 ;;
        minor) minor=$((minor + 1)); patch=0 ;;
        patch) patch=$((patch + 1)) ;;
        *) log_error "Invalid bump type: $bump_type (use patch|minor|major)"; return 1 ;;
    esac
    echo "v${major}.${minor}.${patch}"
}

# suggest_version_bump: Analyze commits to suggest version bump
# DESCRIPTION:
#   Reads commit messages since last tag to suggest patch/minor/major
# USE CASE: Auto-suggest version bump for PR
suggest_version_bump() {
    local current_version=$(get_current_version)
    local commits=$(git log "$current_version..HEAD" --oneline --pretty=format:"%s" 2>/dev/null || git log --oneline --pretty=format:"%s")
    local has_breaking=0
    local has_feature=0
    local has_fix=0

    while IFS= read -r commit; do
        if [[ "$commit" =~ ^feat!\(|^BREAKING CHANGE: ]]; then
            has_breaking=1
        elif [[ "$commit" =~ ^feat\( ]]; then
            has_feature=1
        elif [[ "$commit" =~ ^fix\( ]]; then
            has_fix=1
        fi
    done <<< "$commits"

    if [[ $has_breaking -eq 1 ]]; then
        echo "major"
    elif [[ $has_feature -eq 1 ]]; then
        echo "minor"
    else
        echo "patch"
    fi
}

# generate_pr_title: Create conventional PR title from commits
# DESCRIPTION:
#   Generates a clean PR title based on commit history
# ARGS: $1 = plan ID (optional)
# OUTPUTS: PR title string
generate_pr_title() {
    local plan_id="${1:-}"
    local current_version=$(get_current_version)
    local suggested_bump=$(suggest_version_bump)
    local next_version=$(bump_version "$current_version" "$suggested_bump")
    local commits=$(git log "$current_version..HEAD" --oneline --pretty=format:"%s" 2>/dev/null | head -5)

    # Extract primary change from first commit
    local first_commit=$(echo "$commits" | head -1)
    local title=""
    if [[ "$first_commit" =~ ^(feat|fix|refactor|docs|chore|test|plan)\(([^)]+)\):\ (.+) ]]; then
        local type="${BASH_REMATCH[1]}"
        local scope="${BASH_REMATCH[2]}"
        local summary="${BASH_REMATCH[3]}"
        title="${type}(${scope}): ${summary}"
    else
        title="chore: ${first_commit:-updates}"
    fi

    echo "${title} [${next_version}]"
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

# version_bump: Interactive version bump for PR
# DESCRIPTION:
#   Prompts for bump type, calculates new version, creates tag on merge
# USE CASE: Run before open_pr to prepare version
version_bump() {
    local current_version=$(get_current_version)
    local suggested_bump=$(suggest_version_bump)
    local next_patch=$(bump_version "$current_version" "patch")
    local next_minor=$(bump_version "$current_version" "minor")
    local next_major=$(bump_version "$current_version" "major")

    echo ""
    log_info "Current version: $current_version"
    log_info "Suggested bump: $suggested_bump (based on commits)"
    echo ""
    echo "Select version bump:"
    echo "  1) Patch  $next_patch  (bug fixes, small tweaks)"
    echo "  2) Minor  $next_minor  (new features, non-breaking)"
    echo "  3) Major  $next_major  (breaking changes)"
    echo "  4) Custom version"
    echo "  5) Skip version bump"
    read -p "Choice [1-5] (default: $suggested_bump): " -n 1 -r choice
    echo ""

    local selected_version=""
    case "${choice:-1}" in
        1) selected_version="$next_patch" ;;
        2) selected_version="$next_minor" ;;
        3) selected_version="$next_major" ;;
        4)
            read -p "Enter custom version (e.g., v1.2.3): " selected_version
            if [[ ! "$selected_version" =~ ^v[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
                log_error "Invalid version format"
                return 1
            fi
            ;;
        5)
            log_info "Skipping version bump"
            return 0
            ;;
        *) log_error "Invalid choice"; return 1 ;;
    esac

    log_success "Selected version: $selected_version"

    # Store for PR creation
    echo "$selected_version" > .git/pr_version
    log_info "Version saved. Run 'open_pr' to create PR with version tag."
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
  - All merges via PR with version bump (open_pr)
  - Semantic versioning (create_release, version_bump)
  - Branch hygiene (cleanup_merged_branches)

Plan Management:
  new_plan "Title"        Create plan doc + feature branch (plan/<slug>)
                          Use: Starting any new work
  validate_plan           Verify branch follows plan, commits reference plan ID
                          Use: Pre-PR check, CI gate
  open_pr                 Version bump → push branch → PR URL with title
                          Use: Ready for review (auto-generates title + version)

Version Management:
  version_bump            Interactive version bump (patch/minor/major/custom)
                          Use: Before PR, selects semantic version
  get_current_version     Show latest version tag
  bump_version <v> <type> Calculate next version (patch|minor|major)
  suggest_version_bump    Analyze commits, suggest bump type
  generate_pr_title       Create conventional PR title from commits

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
  ./scripts/git-helpers.sh version_bump
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
        version_bump) version_bump ;;
        create_release) shift; create_release "$@" ;;
        cleanup) cleanup_merged_branches ;;
        get_current_version) get_current_version ;;
        bump_version) shift; bump_version "$@" ;;
        suggest_version_bump) suggest_version_bump ;;
        generate_pr_title) shift; generate_pr_title "$@" ;;
        *) git_helpers_help ;;
    esac
fi