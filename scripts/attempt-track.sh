#!/usr/bin/env bash
# attempt-track.sh — Track agent attempts to detect stuck loops
#
# DESCRIPTION:
#   Simple attempt tracking to prevent agents from getting stuck in loops.
#   Logs attempts per task, checks threshold (default 3), triggers re-evaluation.
#
# USAGE:
#   ./scripts/attempt-track.sh log "Task name" "Approach tried" "Result"
#   ./scripts/attempt-track.sh check "Task name"
#   ./scripts/attempt-track.sh show "Task name"
#   ./scripts/attempt-track.sh reset "Task name"
#
# INTEGRATION:
#   - Logs stored in plans/.attempts.log (JSON lines)
#   - Called by agent after each significant attempt
#   - Triggers re-evaluation protocol at 3 attempts (AGENTS.md)
#   - Plan document should also have Attempt Log section for human review

set -euo pipefail

ATTEMPTS_LOG="plans/.attempts.log"
THRESHOLD=3

log_info() { echo -e "\033[0;34m[INFO]\033[0m $*"; }
log_success() { echo -e "\033[0;32m[SUCCESS]\033[0m $*"; }
log_warn() { echo -e "\033[1;33m[WARN]\033[0m $*"; }
log_error() { echo -e "\033[0;31m[ERROR]\033[0m $*"; }

# Ensure log file exists
touch "$ATTEMPTS_LOG"

# ---------- Core Functions ----------

attempt_log() {
    local task="${1:-}"
    local approach="${2:-}"
    local result="${3:-}"
    if [[ -z "$task" || -z "$approach" || -z "$result" ]]; then
        log_error "Usage: attempt_log \"Task name\" \"Approach tried\" \"Result\""
        return 1
    fi
    local timestamp=$(date -u +"%Y-%m-%dT%H:%M:%SZ")
    local count=$(attempt_count "$task")
    local next=$((count + 1))
    # JSON line: {"task":"...","attempt":N,"timestamp":"...","approach":"...","result":"..."}
    printf '{"task":"%s","attempt":%d,"timestamp":"%s","approach":"%s","result":"%s"}\n' \
        "$task" "$next" "$timestamp" "$approach" "$result" >> "$ATTEMPTS_LOG"
    log_info "Logged attempt $next for: $task"
    if [[ $next -ge $THRESHOLD ]]; then
        log_warn "⚠️  THRESHOLD REACHED ($next/$THRESHOLD) — TRIGGER RE-EVALUATION (AGENTS.md)"
    fi
}

attempt_count() {
    local task="${1:-}"
    if [[ -z "$task" ]]; then
        log_error "Usage: attempt_count \"Task name\""
        return 1
    fi
    grep -c "\"task\":\"$task\"" "$ATTEMPTS_LOG" 2>/dev/null || echo 0
}

attempt_check() {
    local task="${1:-}"
    if [[ -z "$task" ]]; then
        log_error "Usage: attempt_check \"Task name\""
        return 1
    fi
    local count=$(attempt_count "$task")
    if [[ $count -ge $THRESHOLD ]]; then
        log_warn "ATTEMPT $count — TRIGGER RE-EVALUATION (threshold: $THRESHOLD)"
        return 1
    else
        log_info "ATTEMPT $count — CONTINUE (threshold: $THRESHOLD)"
        return 0
    fi
}

attempt_show() {
    local task="${1:-}"
    if [[ -z "$task" ]]; then
        log_error "Usage: attempt_show \"Task name\""
        return 1
    fi
    log_info "Attempt history for: $task"
    grep "\"task\":\"$task\"" "$ATTEMPTS_LOG" 2>/dev/null | while IFS= read -r line; do
        local attempt=$(echo "$line" | sed -n 's/.*"attempt":\([0-9]*\).*/\1/p')
        local timestamp=$(echo "$line" | sed -n 's/.*"timestamp":"\([^"]*\)".*/\1/p')
        local approach=$(echo "$line" | sed -n 's/.*"approach":"\([^"]*\)".*/\1/p')
        local result=$(echo "$line" | sed -n 's/.*"result":"\([^"]*\)".*/\1/p')
        printf "  Attempt %s [%s]\n    Approach: %s\n    Result: %s\n\n" "$attempt" "$timestamp" "$approach" "$result"
    done
}

attempt_reset() {
    local task="${1:-}"
    if [[ -z "$task" ]]; then
        log_error "Usage: attempt_reset \"Task name\""
        return 1
    fi
    # Create temp file without the task entries
    grep -v "\"task\":\"$task\"" "$ATTEMPTS_LOG" > "${ATTEMPTS_LOG}.tmp" 2>/dev/null || true
    mv "${ATTEMPTS_LOG}.tmp" "$ATTEMPTS_LOG"
    log_success "Reset attempts for: $task"
}

# ---------- Help ----------

attempt_help() {
    cat <<EOF
Attempt Tracker — Loop Detection for Agents

DESCRIPTION:
  Tracks attempts per task to prevent stuck loops.
  Triggers re-evaluation at 3 attempts (configurable).

USAGE:
  ./scripts/attempt-track.sh log "Task name" "Approach" "Result"
  ./scripts/attempt-track.sh check "Task name"
  ./scripts/attempt-track.sh show "Task name"
  ./scripts/attempt-track.sh reset "Task name"

COMMANDS:
  log     Record an attempt (increments counter)
  check   Check if threshold reached (exit 1 if triggered)
  show    Display attempt history for task
  reset   Clear attempts for task (use after pivot)

EXAMPLES:
  # After each significant attempt:
  ./scripts/attempt-track.sh log "Fix BSA packing" "bsarch --format bsa" "Failed: invalid header"

  # Before next attempt:
  ./scripts/attempt-track.sh check "Fix BSA packing"
  # If returns 1 → STOP, re-evaluate per AGENTS.md

  # View history:
  ./scripts/attempt-track.sh show "Fix BSA packing"

  # After pivoting to new approach:
  ./scripts/attempt-track.sh reset "Fix BSA packing"

INTEGRATION WITH PLAN DOCUMENT:
  Also update plan's "Attempt Log" section for human review:
  | Attempt | Date | Approach | Result | Notes |
  |---------|------|----------|--------|-------|
  | 1 | 2026-10-03 | bsarch --format bsa | Failed: invalid header | |
  | 2 | 2026-10-03 | bsarch --format ba2 | Failed: compression error | |
  | 3 | 2026-10-03 | manual BSA creation | Failed: checksum mismatch | ⚠️ TRIGGER |

THRESHOLD:
  Default: 3 attempts (modify THRESHOLD variable to change)
EOF
}

# ---------- Main ----------

case "${1:-help}" in
    log) shift; attempt_log "$@" ;;
    check) shift; attempt_check "$@" ;;
    show) shift; attempt_show "$@" ;;
    reset) shift; attempt_reset "$@" ;;
    *) attempt_help ;;
esac