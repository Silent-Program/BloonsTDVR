#!/usr/bin/env bash
# mod-utils.sh — Universal Modder Wrapper Functions
# Source this file: source scripts/mod-utils.sh
# Provides high-level modding operations using universal-modder submodule

set -euo pipefail

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

log_info() { echo -e "${BLUE}[INFO]${NC} $*"; }
log_success() { echo -e "${GREEN}[SUCCESS]${NC} $*"; }
log_warn() { echo -e "${YELLOW}[WARN]${NC} $*"; }
log_error() { echo -e "${RED}[ERROR]${NC} $*"; }

# ---------- Configuration ----------

# Path to universal-modder binary (built from submodule)
UNIVERSAL_MODDER_BIN="${UNIVERSAL_MODDER_BIN:-tools/universal-modder/target/release/universal-modder}"

# Load game config
load_game_config() {
    local game_id="${1:-}"
    if [[ -z "$game_id" ]]; then
        log_error "Usage: load_game_config <game-id>"
        return 1
    fi
    if [[ ! -f "configs/games.toml" ]]; then
        log_error "configs/games.toml not found"
        return 1
    fi
    # Simple TOML parsing for game config
    GAME_INSTALL_PATH=$(awk -F'=' "/^\[$game_id\]/,/^\[/{if(\$1 ~ /install_path/) print \$2}" configs/games.toml | tr -d ' "')
    GAME_MOD_DIR=$(awk -F'=' "/^\[$game_id\]/,/^\[/{if(\$1 ~ /mod_dir/) print \$2}" configs/games.toml | tr -d ' "')
    GAME_MANAGER=$(awk -F'=' "/^\[$game_id\]/,/^\[/{if(\$1 ~ /mod_manager/) print \$2}" configs/games.toml | tr -d ' "')
    if [[ -z "$GAME_INSTALL_PATH" ]]; then
        log_error "Game '$game_id' not found in configs/games.toml"
        return 1
    fi
    export GAME_INSTALL_PATH GAME_MOD_DIR GAME_MANAGER
    log_info "Loaded config for $game_id: $GAME_INSTALL_PATH/$GAME_MOD_DIR ($GAME_MANAGER)"
}

# Ensure universal-modder is built
ensure_modder_built() {
    if [[ ! -x "$UNIVERSAL_MODDER_BIN" ]]; then
        log_info "Building universal-modder..."
        (cd tools/universal-modder && cargo build --release) || {
            log_error "Failed to build universal-modder"
            return 1
        }
    fi
    # Verify version matches config
    local version=$("$UNIVERSAL_MODDER_BIN" --version 2>/dev/null | awk '{print $2}')
    local expected=$(grep '^version' configs/tools.toml | head -1 | cut -d'"' -f2)
    if [[ "$version" != "$expected" ]]; then
        log_warn "universal-modder version mismatch: built=$version, expected=$expected"
    fi
}

# ---------- Core Operations ----------

# mod_init <mod-name> <game-id>
# Initialize a new mod project structure
mod_init() {
    local mod_name="${1:-}"
    local game_id="${2:-}"
    if [[ -z "$mod_name" || -z "$game_id" ]]; then
        log_error "Usage: mod_init <mod-name> <game-id>"
        return 1
    fi
    load_game_config "$game_id" || return 1
    ensure_modder_built || return 1

    log_info "Initializing mod: $mod_name for $game_id"
    "$UNIVERSAL_MODDER_BIN" init "$mod_name" --game "$game_id"
    log_success "Mod initialized at: $mod_name/"
}

# mod_patch <patch-file> <game-id>
# Apply a patch file to game installation
mod_patch() {
    local patch_file="${1:-}"
    local game_id="${2:-}"
    if [[ -z "$patch_file" || -z "$game_id" ]]; then
        log_error "Usage: mod_patch <patch-file> <game-id>"
        return 1
    fi
    load_game_config "$game_id" || return 1
    ensure_modder_built || return 1

    local target_path="$GAME_INSTALL_PATH/$GAME_MOD_DIR"
    log_info "Applying patch: $patch_file -> $target_path"
    "$UNIVERSAL_MODDER_BIN" patch apply "$patch_file" --target "$target_path"
    log_success "Patch applied"
}

# mod_package <mod-path> <output-file>
# Create a distributable mod package
mod_package() {
    local mod_path="${1:-}"
    local output_file="${2:-}"
    if [[ -z "$mod_path" || -z "$output_file" ]]; then
        log_error "Usage: mod_package <mod-path> <output-file.mod>"
        return 1
    fi
    ensure_modder_built || return 1

    log_info "Creating package: $output_file from $mod_path"
    "$UNIVERSAL_MODDER_BIN" package create --output "$output_file" "$mod_path"
    log_success "Package created: $output_file"
}

# mod_validate <mod-path>
# Validate mod structure
mod_validate() {
    local mod_path="${1:-}"
    if [[ -z "$mod_path" ]]; then
        log_error "Usage: mod_validate <mod-path>"
        return 1
    fi
    ensure_modder_built || return 1

    log_info "Validating mod: $mod_path"
    "$UNIVERSAL_MODDER_BIN" validate "$mod_path"
    log_success "Validation passed"
}

# mod_deploy <mod-path> <game-id>
# Deploy mod to game installation
mod_deploy() {
    local mod_path="${1:-}"
    local game_id="${2:-}"
    if [[ -z "$mod_path" || -z "$game_id" ]]; then
        log_error "Usage: mod_deploy <mod-path> <game-id>"
        return 1
    fi
    load_game_config "$game_id" || return 1
    ensure_modder_built || return 1

    log_info "Deploying $mod_path to $game_id"
    "$UNIVERSAL_MODDER_BIN" deploy "$mod_path" --game "$game_id"
    log_success "Mod deployed"
}

# mod_build <mod-path> <game-id> <output-file>
# Full pipeline: validate -> package -> deploy
mod_build() {
    local mod_path="${1:-}"
    local game_id="${2:-}"
    local output_file="${3:-}"
    if [[ -z "$mod_path" || -z "$game_id" || -z "$output_file" ]]; then
        log_error "Usage: mod_build <mod-path> <game-id> <output-file.mod>"
        return 1
    fi
    mod_validate "$mod_path" || return 1
    mod_package "$mod_path" "$output_file" || return 1
    mod_deploy "$mod_path" "$game_id" || return 1
    log_success "Build & deploy complete: $output_file"
}

# ---------- Archive Operations ----------

# archive_create <source-dir> <output-archive> [format]
# Create BSA/BA2 archive from loose files
archive_create() {
    local source_dir="${1:-}"
    local output_archive="${2:-}"
    local format="${3:-bsa}"
    if [[ -z "$source_dir" || -z "$output_archive" ]]; then
        log_error "Usage: archive_create <source-dir> <output-archive> [bsa|ba2]"
        return 1
    fi
    if command -v bsarch >/dev/null; then
        bsarch create "$source_dir" "$output_archive" --format "$format"
    else
        log_error "bsarch not installed. Install with: cargo install bsarch"
        return 1
    fi
}

# archive_extract <archive> <output-dir>
# Extract BSA/BA2 archive
archive_extract() {
    local archive="${1:-}"
    local output_dir="${2:-}"
    if [[ -z "$archive" || -z "$output_dir" ]]; then
        log_error "Usage: archive_extract <archive> <output-dir>"
        return 1
    fi
    if command -v bsarch >/dev/null; then
        bsarch extract "$archive" "$output_dir"
    else
        log_error "bsarch not installed"
        return 1
    fi
}

# ---------- Helpers ----------

mod_utils_help() {
    cat <<EOF
GameSpace Mod Utils — Universal Modder Wrappers

Setup:
  source scripts/mod-utils.sh

Core Operations:
  mod_init <name> <game-id>      Initialize new mod project
  mod_patch <patch> <game-id>    Apply patch to game
  mod_package <mod> <out.mod>    Create distributable package
  mod_validate <mod>             Validate mod structure
  mod_deploy <mod> <game-id>     Deploy mod to game
  mod_build <mod> <game> <out>   Full pipeline: validate + package + deploy

Archive Operations:
  archive_create <dir> <out> [fmt]  Create BSA/BA2 archive
  archive_extract <arc> <dir>       Extract archive

Config:
  load_game_config <game-id>     Load game paths from configs/games.toml

Requirements:
  - universal-modder built (tools/universal-modder/target/release/)
  - bsarch for archive ops (cargo install bsarch)
  - configs/games.toml populated with game paths
EOF
}

# Allow direct execution
if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
    case "${1:-help}" in
        init) shift; mod_init "$@" ;;
        patch) shift; mod_patch "$@" ;;
        package) shift; mod_package "$@" ;;
        validate) shift; mod_validate "$@" ;;
        deploy) shift; mod_deploy "$@" ;;
        build) shift; mod_build "$@" ;;
        archive-create) shift; archive_create "$@" ;;
        archive-extract) shift; archive_extract "$@" ;;
        load-config) shift; load_game_config "$@" ;;
        *) mod_utils_help ;;
    esac
fi