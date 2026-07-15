#!/usr/bin/env bash

set -euo pipefail
umask 077

script_started_at="$(date +%s)"
session_mode="NONE"
baseline_reused="false"
drift_status="NOT_CHECKED"
question_count="0"
cli_started_at="$script_started_at"
cli_finished_at="$script_started_at"
current_uid="$(id -u)"
artifact_path=""
artifact_written="false"
direct_write_tmp=""
redraft_approval_digest=""
redraft_approval_file=""
redraft_approval_receipt_key=""
redraft_claim_path=""
redraft_claim_token=""
redraft_claim_consumed="false"
revision_target_digest=""
primary_approval_digest=""
primary_approval_file=""
approval_path=""
redraft_review_path=""
redraft_review_file=""
legacy_user_flow_revision="false"

# shellcheck disable=SC2329
cleanup_direct_write_tmp() {
  if [[ -n "$direct_write_tmp" && -f "$direct_write_tmp" ]]; then
    rm "$direct_write_tmp"
  fi
  if [[ -n "$redraft_claim_path" && "$redraft_claim_consumed" != "true" && -f "$redraft_claim_path" && ! -L "$redraft_claim_path" ]]; then
    if [[ "$(< "$redraft_claim_path")" == "$redraft_claim_token" ]]; then
      rm "$redraft_claim_path"
    fi
  fi
}

trap cleanup_direct_write_tmp EXIT

fail() {
  local failure_code="$1"
  local exit_code="$2"

  printf 'result=FAILED\n'
  printf 'failureCode=%s\n' "$failure_code"
  exit "$exit_code"
}

emit_artifact_result() {
  local result="$1"
  local status_code="$2"
  local exit_code="$3"
  local finished_at stdout_bytes stderr_bytes
  local preflight_seconds model_seconds postflight_seconds

  finished_at="$(date +%s)"
  stdout_bytes="$(wc -c < "$stdout_file" | tr -d '[:space:]')"
  stderr_bytes="$(wc -c < "$stderr_file" | tr -d '[:space:]')"
  preflight_seconds="$((cli_started_at - script_started_at))"
  model_seconds="$((cli_finished_at - cli_started_at))"
  postflight_seconds="$((finished_at - cli_finished_at))"

  printf 'result=%s\n' "$result"
  printf 'statusCode=%s\n' "$status_code"
  printf 'mode=%s\n' "$mode"
  printf 'sessionMode=%s\n' "$session_mode"
  printf 'baselineReused=%s\n' "$baseline_reused"
  printf 'driftStatus=%s\n' "$drift_status"
  printf 'questionCount=%s\n' "$question_count"
  printf 'preflightSeconds=%s\n' "$preflight_seconds"
  printf 'modelSeconds=%s\n' "$model_seconds"
  printf 'postflightSeconds=%s\n' "$postflight_seconds"
  printf 'stdoutPath=%s\n' "$stdout_file"
  printf 'stderrPath=%s\n' "$stderr_file"
  printf 'stdoutBytes=%s\n' "$stdout_bytes"
  printf 'stderrBytes=%s\n' "$stderr_bytes"
  printf 'artifactPath=%s\n' "${artifact_path:-N/A}"
  printf 'artifactWritten=%s\n' "$artifact_written"
  printf 'cleanupRequired=true\n'
  exit "$exit_code"
}

write_private_state() {
  local path="$1"
  local value="$2"

  if [[ -e "$path" || -L "$path" ]]; then
    if [[ ! -f "$path" || -L "$path" ]]; then
      fail "FABLE_SESSION_STATE_FILE_TYPE_INVALID" 74
    fi
    if [[ "$(stat -f %u "$path")" != "$current_uid" ]]; then
      fail "FABLE_SESSION_STATE_FILE_OWNER_INVALID" 74
    fi
  fi
  printf '%s\n' "$value" > "$path"
  chmod 600 "$path"
}

validate_private_directory() {
  local path="$1"

  if [[ ! -d "$path" || -L "$path" ]]; then
    fail "FABLE_SESSION_STATE_DIRECTORY_INVALID" 74
  fi
  if [[ "$(stat -f %u "$path")" != "$current_uid" ]]; then
    fail "FABLE_SESSION_STATE_DIRECTORY_OWNER_INVALID" 74
  fi
}

validate_private_state_file() {
  local path="$1"

  if [[ -e "$path" || -L "$path" ]]; then
    if [[ ! -f "$path" || -L "$path" ]]; then
      fail "FABLE_SESSION_STATE_FILE_TYPE_INVALID" 74
    fi
    if [[ "$(stat -f %u "$path")" != "$current_uid" ]]; then
      fail "FABLE_SESSION_STATE_FILE_OWNER_INVALID" 74
    fi
  fi
}

find_session_transcript() {
  local requested_session_id="$1"

  if [[ ! -d "$claude_projects_root" ]]; then
    return 0
  fi

  find "$claude_projects_root" -type f -name "${requested_session_id}.jsonl" -print -quit 2>/dev/null || true
}

if [[ $# -lt 2 || $# -gt 3 ]]; then
  fail "FABLE_READONLY_USAGE_INVALID" 64
fi

mode="$1"
interview_path="$2"
third_argument="${3:-}"
round=""

if [[ "$mode" != "interview" && "$mode" != "planning" && "$mode" != "draft" && "$mode" != "revise" && "$mode" != "cleanup" ]]; then
  fail "FABLE_READONLY_MODE_INVALID" 64
fi

if [[ ! "$interview_path" =~ ^tasks/[a-z0-9][a-z0-9-]*-interview\.md$ ]]; then
  fail "FABLE_READONLY_INTERVIEW_PATH_INVALID" 64
fi

if [[ "$mode" == "interview" ]]; then
  round="$third_argument"
  if [[ $# -ne 3 || ! "$round" =~ ^[1-9][0-9]*$ ]]; then
    fail "FABLE_READONLY_ROUND_INVALID" 64
  fi
elif [[ "$mode" == "draft" || "$mode" == "revise" ]]; then
  artifact_path="$third_argument"
  if [[ $# -ne 3 || ! "$artifact_path" =~ ^docs/[0-9][0-9]-[a-z0-9][a-z0-9-]*\.md$ ]]; then
    fail "FABLE_READONLY_ARTIFACT_PATH_INVALID" 64
  fi
elif [[ $# -ne 2 ]]; then
  fail "FABLE_READONLY_MODE_ARGUMENT_INVALID" 64
fi

repo_root="$(git rev-parse --show-toplevel 2>/dev/null)" || fail "FABLE_READONLY_REPOSITORY_UNAVAILABLE" 65
if [[ "$PWD" != "$repo_root" ]]; then
  fail "FABLE_READONLY_REPOSITORY_ROOT_REQUIRED" 65
fi

required_repository_files=(
  "AGENTS.md"
  "CLAUDE.md"
  "scripts/AGENTS.md"
  "docs/00-product-roadmap.md"
  "docs/12-task-completion-policy.md"
  "docs/development/privacy-safe-evidence.md"
  "tasks/_templates/new-feature-interview-template.md"
  "tasks/_templates/new-feature-planning-template.md"
)

for required_file in "${required_repository_files[@]}"; do
  if [[ ! -f "$repo_root/$required_file" ]]; then
    fail "FABLE_READONLY_REPOSITORY_CONTRACT_MISSING" 65
  fi
done

interview_file="$repo_root/$interview_path"
if [[ ! -f "$interview_file" ]]; then
  fail "FABLE_READONLY_INTERVIEW_NOT_FOUND" 66
fi

task_stem="${interview_path%-interview.md}"
planning_path="${task_stem}-planning.md"
review_path="${task_stem}-review.md"
preview_review_path="${task_stem}-preview-review.md"
planning_file="$repo_root/$planning_path"
review_file="$repo_root/$review_path"
preview_review_file="$repo_root/$preview_review_path"

if [[ "$mode" == "interview" ]]; then
  artifact_path="${task_stem}-interview-round-${round}-fable.md"
  if [[ -e "$repo_root/$artifact_path" ]]; then
    fail "FABLE_READONLY_INTERVIEW_ARTIFACT_EXISTS" 67
  fi
elif [[ "$mode" == "planning" ]]; then
  artifact_path="$planning_path"
fi

script_path="$(cd "$(dirname "$0")" && pwd -P)/$(basename "$0")"
git_common_dir="$(git rev-parse --git-common-dir 2>/dev/null)" || fail "FABLE_READONLY_GIT_COMMON_DIR_UNAVAILABLE" 65
if [[ "$git_common_dir" != /* ]]; then
  git_common_dir="$repo_root/$git_common_dir"
fi
git_common_dir="$(cd "$git_common_dir" && pwd -P)"
repo_key="$(printf '%s' "$git_common_dir" | shasum -a 256 | awk '{print substr($1,1,16)}')"
task_key="$(basename "$interview_path" -interview.md)"
task_id="TASK-$(printf '%s' "$task_key" | tr '[:lower:]' '[:upper:]')"
if [[ "$mode" == "revise" && "$task_key" == "user-flow-001" && "$artifact_path" == "docs/13-user-flow-baseline.md" ]]; then
  legacy_user_flow_revision="true"
fi
state_parent="${XDG_STATE_HOME:-$HOME/.local/state}"
if [[ "$state_parent" != /* ]]; then
  fail "FABLE_SESSION_STATE_ROOT_INVALID" 68
fi
state_root="$state_parent/emi-qms-fable-readonly"
state_dir="$state_root/$repo_key/$task_key"
sessions_dir="$state_dir/sessions"
redraft_receipts_dir="$state_root/$repo_key/redraft-approvals/$task_key"
claude_root="${CLAUDE_CONFIG_DIR:-$HOME/.claude}"
if [[ "$claude_root" != /* ]]; then
  fail "FABLE_SESSION_CLAUDE_ROOT_INVALID" 68
fi
claude_projects_root="$claude_root/projects"

for private_directory in "$state_root" "$state_root/$repo_key" "$state_dir" "$sessions_dir" "$state_root/$repo_key/redraft-approvals" "$redraft_receipts_dir"; do
  if [[ -e "$private_directory" || -L "$private_directory" ]]; then
    validate_private_directory "$private_directory"
  fi
done

if [[ "$mode" == "cleanup" ]]; then
  removed_session_count=0
  removed_transcript_count=0
  missing_transcript_count=0
  cleanup_marker_paths=()
  cleanup_transcript_paths=()

  for state_file in current-session-id baseline-head contract-digest last-round; do
    validate_private_state_file "$state_dir/$state_file"
  done

  if [[ -d "$sessions_dir" ]]; then
    shopt -s nullglob
    session_markers=("$sessions_dir"/*)
    shopt -u nullglob

    for marker_path in "${session_markers[@]}"; do
      cleanup_session_id="$(basename "$marker_path")"
      if [[ ! "$cleanup_session_id" =~ ^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}$ ]]; then
        fail "FABLE_SESSION_CLEANUP_MARKER_INVALID" 74
      fi
      if [[ ! -f "$marker_path" || -L "$marker_path" ]]; then
        fail "FABLE_SESSION_CLEANUP_MARKER_TYPE_INVALID" 74
      fi
      if [[ "$(stat -f %u "$marker_path")" != "$current_uid" ]]; then
        fail "FABLE_SESSION_CLEANUP_MARKER_OWNER_INVALID" 74
      fi

      transcript_path="$(find_session_transcript "$cleanup_session_id")"
      if [[ -n "$transcript_path" ]]; then
        case "$transcript_path" in
          "$claude_projects_root"/*) ;;
          *) fail "FABLE_SESSION_TRANSCRIPT_SCOPE_INVALID" 74 ;;
        esac
        if [[ ! -f "$transcript_path" || -L "$transcript_path" ]]; then
          fail "FABLE_SESSION_TRANSCRIPT_TYPE_INVALID" 74
        fi
        if [[ "$(stat -f %u "$transcript_path")" != "$current_uid" ]]; then
          fail "FABLE_SESSION_TRANSCRIPT_OWNER_INVALID" 74
        fi
      else
        missing_transcript_count="$((missing_transcript_count + 1))"
      fi

      cleanup_marker_paths+=("$marker_path")
      cleanup_transcript_paths+=("$transcript_path")
    done

    for marker_index in "${!cleanup_marker_paths[@]}"; do
      transcript_path="${cleanup_transcript_paths[$marker_index]}"
      if [[ -n "$transcript_path" ]]; then
        rm "$transcript_path"
        removed_transcript_count="$((removed_transcript_count + 1))"
      fi
      rm "${cleanup_marker_paths[$marker_index]}"
      removed_session_count="$((removed_session_count + 1))"
    done
  fi

  for state_file in current-session-id baseline-head contract-digest last-round; do
    if [[ -f "$state_dir/$state_file" ]]; then
      rm "$state_dir/$state_file"
    fi
  done
  if [[ -d "$sessions_dir" ]]; then
    rmdir "$sessions_dir"
  fi
  if [[ -d "$state_dir" ]]; then
    rmdir "$state_dir"
  fi

  printf 'result=READY\n'
  printf 'statusCode=FABLE_TASK_SESSION_CLEANED\n'
  printf 'sessionsRemoved=%s\n' "$removed_session_count"
  printf 'transcriptsRemoved=%s\n' "$removed_transcript_count"
  printf 'transcriptsMissing=%s\n' "$missing_transcript_count"
  exit 0
fi

if [[ "$mode" == "planning" ]]; then
  if ! grep -Fqx -- '- interviewStatus: `COMPLETED_CONFIRMED`' "$interview_file" ||
     ! grep -Fqx -- '- userConfirmed: true' "$interview_file" ||
     ! grep -Fqx -- '- openBlockingDecisionCount: 0' "$interview_file"; then
    fail "FABLE_READONLY_PLANNING_GATE_INCOMPLETE" 67
  fi
  if [[ -e "$planning_file" || -L "$planning_file" ]]; then
    fail "FABLE_READONLY_PLANNING_TARGET_EXISTS" 67
  fi
fi

if [[ "$mode" == "draft" || "$mode" == "revise" ]]; then
  if ! grep -Fqx -- '- interviewStatus: `COMPLETED_CONFIRMED`' "$interview_file" ||
     ! grep -Fqx -- '- userConfirmed: true' "$interview_file" ||
     ! grep -Fqx -- '- openBlockingDecisionCount: 0' "$interview_file"; then
    fail "FABLE_READONLY_DRAFT_GATE_INCOMPLETE" 67
  fi

  shopt -s nullglob
  approval_change_files=("$repo_root/${task_stem}-change-"*.md)
  shopt -u nullglob
  if [[ "${#approval_change_files[@]}" -eq 0 ]]; then
    if [[ "$mode" == "draft" ]]; then
      fail "FABLE_READONLY_PRIMARY_DRAFT_USER_APPROVAL_MISSING" 67
    fi
    fail "FABLE_READONLY_REVISION_USER_APPROVAL_MISSING" 67
  fi

  if [[ "$mode" == "draft" ]]; then
    primary_approval_file="${approval_change_files[${#approval_change_files[@]}-1]}"
    approval_path="${primary_approval_file#"$repo_root/"}"
    if [[ ! -f "$primary_approval_file" || -L "$primary_approval_file" ]] ||
       ! grep -Fqx -- '- fablePrimaryDraftApproved: true' "$primary_approval_file" ||
       ! grep -Fqx -- '- fablePrimaryDraftSource: `USER_EXPLICIT_REQUEST`' "$primary_approval_file" ||
       ! grep -Fqx -- "- fablePrimaryDraftTarget: \`$artifact_path\`" "$primary_approval_file"; then
      fail "FABLE_READONLY_PRIMARY_DRAFT_USER_APPROVAL_MISSING" 67
    fi
    primary_approval_digest="$(shasum -a 256 "$primary_approval_file" | awk '{print $1}')"
    if [[ -e "$repo_root/$artifact_path" || -L "$repo_root/$artifact_path" ]]; then
      fail "FABLE_READONLY_DRAFT_TARGET_EXISTS" 67
    fi
  else
    if [[ ! -f "$repo_root/$artifact_path" || -L "$repo_root/$artifact_path" ]]; then
      fail "FABLE_READONLY_REVISION_TARGET_MISSING" 67
    fi
    if [[ "$legacy_user_flow_revision" == "true" ]]; then
      redraft_review_path="$preview_review_path"
      redraft_review_file="$preview_review_file"
    else
      redraft_review_path="$review_path"
      redraft_review_file="$review_file"
    fi
    if [[ ! -f "$redraft_review_file" || -L "$redraft_review_file" ]]; then
      fail "FABLE_READONLY_REVISION_REVIEW_MISSING" 67
    fi
    shopt -s nullglob
    redraft_change_files=("$repo_root/${task_stem}-change-"*.md)
    shopt -u nullglob
    if [[ "${#redraft_change_files[@]}" -eq 0 ]]; then
      fail "FABLE_READONLY_REVISION_USER_APPROVAL_MISSING" 67
    fi
    redraft_approval_file="${redraft_change_files[${#redraft_change_files[@]}-1]}"
    approval_path="${redraft_approval_file#"$repo_root/"}"
    if [[ ! -f "$redraft_approval_file" || -L "$redraft_approval_file" ]] ||
       ! grep -Fqx -- '- fableRedraftApproved: true' "$redraft_approval_file" ||
       ! grep -Fqx -- '- fableRedraftSource: `USER_EXPLICIT_REQUEST`' "$redraft_approval_file" ||
       ! grep -Fqx -- "- fableRedraftTarget: \`$artifact_path\`" "$redraft_approval_file"; then
      fail "FABLE_READONLY_REVISION_USER_APPROVAL_MISSING" 67
    fi
    redraft_approval_digest="$(shasum -a 256 "$redraft_approval_file" | awk '{print $1}')"
    redraft_approval_receipt_key="$(printf '%s:%s' "${redraft_approval_file#"$repo_root/"}" "$redraft_approval_digest" | shasum -a 256 | awk '{print $1}')"
    revision_target_digest="$(shasum -a 256 "$repo_root/$artifact_path" | awk '{print $1}')"
  fi
fi

claude_bin="$(command -v claude || true)"
if [[ -z "$claude_bin" || ! -x "$claude_bin" ]]; then
  fail "FABLE_CLI_UNAVAILABLE" 68
fi

if ! help_output="$("$claude_bin" --help 2>&1)"; then
  fail "FABLE_CLI_HELP_UNAVAILABLE" 68
fi

required_flags=(
  "--safe-mode"
  "--model"
  "--permission-mode"
  "--tools"
  "--disable-slash-commands"
  "--strict-mcp-config"
  "--mcp-config"
  "--print"
  "--resume"
  "--session-id"
)

for required_flag in "${required_flags[@]}"; do
  if ! grep -Fq -- "$required_flag" <<< "$help_output"; then
    fail "FABLE_CLI_REQUIRED_OPTION_UNAVAILABLE" 68
  fi
done

if ! grep -Fq "'fable'" <<< "$help_output" || ! grep -Fq "claude-fable-5" <<< "$help_output"; then
  fail "FABLE_CLI_FABLE_5_ALIAS_UNAVAILABLE" 69
fi
if ! command -v uuidgen >/dev/null 2>&1; then
  fail "FABLE_SESSION_UUID_GENERATOR_UNAVAILABLE" 68
fi

mkdir -p "$sessions_dir" "$redraft_receipts_dir"
validate_private_directory "$state_root"
validate_private_directory "$state_root/$repo_key"
validate_private_directory "$state_dir"
validate_private_directory "$sessions_dir"
validate_private_directory "$state_root/$repo_key/redraft-approvals"
validate_private_directory "$redraft_receipts_dir"
chmod 700 "$state_root" "$state_root/$repo_key" "$state_dir" "$sessions_dir" "$state_root/$repo_key/redraft-approvals" "$redraft_receipts_dir"

if [[ "$mode" == "revise" ]]; then
  redraft_claim_path="$redraft_receipts_dir/$redraft_approval_receipt_key"
  validate_private_state_file "$redraft_claim_path"
  if [[ -e "$redraft_claim_path" || -L "$redraft_claim_path" ]]; then
    fail "FABLE_READONLY_REVISION_APPROVAL_ALREADY_USED" 67
  fi
  redraft_claim_token="CLAIMED:$$:$script_started_at"
  if ! ( set -o noclobber; printf '%s\n' "$redraft_claim_token" > "$redraft_claim_path" ) 2>/dev/null; then
    fail "FABLE_READONLY_REVISION_APPROVAL_ALREADY_USED" 67
  fi
  chmod 600 "$redraft_claim_path"
fi

contract_files=(
  "$repo_root/AGENTS.md"
  "$repo_root/CLAUDE.md"
  "$repo_root/scripts/AGENTS.md"
  "$repo_root/docs/12-task-completion-policy.md"
  "$repo_root/docs/development/privacy-safe-evidence.md"
  "$repo_root/tasks/_templates/new-feature-interview-template.md"
  "$repo_root/tasks/_templates/new-feature-planning-template.md"
  "$script_path"
)
contract_digest="$(
  for contract_file in "${contract_files[@]}"; do
    shasum -a 256 "$contract_file"
  done | shasum -a 256 | awk '{print $1}'
)"
head_sha="$(git rev-parse HEAD)"
unexpected_dirty_count=0
while IFS= read -r status_line; do
  if [[ -z "$status_line" ]]; then
    continue
  fi
  dirty_path="${status_line:3}"
  case "$dirty_path" in
    "$interview_path"|"$planning_path"|"$review_path"|"$preview_review_path"|"$artifact_path"|"$approval_path"|"docs/00-product-roadmap.md"|"AGENTS.md"|"CLAUDE.md") ;;
    *) unexpected_dirty_count="$((unexpected_dirty_count + 1))" ;;
  esac
done < <(git status --porcelain=v1 --untracked-files=all)

current_session_id=""
stored_head=""
stored_contract_digest=""
stored_last_round="0"
for state_file in current-session-id baseline-head contract-digest last-round; do
  validate_private_state_file "$state_dir/$state_file"
done
if [[ -f "$state_dir/current-session-id" ]]; then
  current_session_id="$(tr -d '[:space:]' < "$state_dir/current-session-id")"
fi
if [[ -f "$state_dir/baseline-head" ]]; then
  stored_head="$(tr -d '[:space:]' < "$state_dir/baseline-head")"
fi
if [[ -f "$state_dir/contract-digest" ]]; then
  stored_contract_digest="$(tr -d '[:space:]' < "$state_dir/contract-digest")"
fi
if [[ -f "$state_dir/last-round" ]]; then
  stored_last_round="$(tr -d '[:space:]' < "$state_dir/last-round")"
fi

session_valid="false"
if [[ "$current_session_id" =~ ^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}$ ]]; then
  session_transcript="$(find_session_transcript "$current_session_id")"
  if [[ -n "$session_transcript" && "$stored_head" == "$head_sha" && "$stored_contract_digest" == "$contract_digest" && "$unexpected_dirty_count" -eq 0 ]]; then
    session_valid="true"
  fi
fi

if [[ "$mode" == "interview" && "$session_valid" == "true" ]]; then
  if [[ ! "$stored_last_round" =~ ^[0-9]+$ || "$round" -le "$stored_last_round" ]]; then
    fail "FABLE_SESSION_ROUND_NOT_FORWARD" 67
  fi
  session_id="$current_session_id"
  session_mode="RESUMED"
  baseline_reused="true"
  drift_status="UNCHANGED"
elif [[ "$mode" == "planning" && "$session_valid" == "true" ]]; then
  session_id="$current_session_id"
  session_mode="RESUMED_PLANNING_PREFLIGHT"
  baseline_reused="true"
  drift_status="UNCHANGED"
elif [[ ( "$mode" == "draft" || "$mode" == "revise" ) && "$session_valid" == "true" ]]; then
  session_id="$current_session_id"
  session_mode="RESUMED_ARTIFACT_PREFLIGHT"
  baseline_reused="true"
  drift_status="UNCHANGED"
else
  session_id="$(uuidgen | tr '[:upper:]' '[:lower:]')"
  if [[ "$current_session_id" == "" && "$mode" == "interview" && "$round" -gt 1 ]]; then
    session_mode="BOOTSTRAPPED_FROM_INTERVIEW"
    baseline_reused="true"
    drift_status="INTERVIEW_BASELINE"
  elif [[ "$current_session_id" == "" ]]; then
    session_mode="CREATED_FULL_BASELINE"
    baseline_reused="false"
    drift_status="NO_PRIOR_SESSION"
  else
    session_mode="REFRESHED_AFTER_DRIFT"
    baseline_reused="false"
    drift_status="SOURCE_OR_CONTRACT_CHANGED"
  fi
  if [[ -e "$sessions_dir/$session_id" || -L "$sessions_dir/$session_id" ]]; then
    fail "FABLE_SESSION_MARKER_ALREADY_EXISTS" 74
  fi
  : > "$sessions_dir/$session_id"
  chmod 600 "$sessions_dir/$session_id"
fi

if [[ "$mode" == "interview" ]]; then
  operation_contract="Generate deep-interview round ${round} only. Return 1-5 closely related questions, or a confirmation summary when no blocking question remains. Do not add filler questions and do not write a planning draft. Format every question heading exactly as ### 질문 N — 제목. End with interviewStatus: QUESTIONS_REQUIRED or interviewStatus: SUMMARY_CONFIRMATION_REQUIRED, planningStatus: NOT_STARTED, and implementationApproved: false."
elif [[ "$mode" == "planning" ]]; then
  operation_contract="The confirmed interview gate was verified by the caller. Revalidate the current Roadmap, relevant implementation and tests before writing one complete primary planning draft. This is the single Fable draft that Codex will review once; do not assume a later Fable rewrite. End with planningStatus: DRAFT, implementationApproved: false, and userDecisionRequiredCount: followed by a nonnegative integer."
elif [[ "$mode" == "draft" ]]; then
  operation_contract="The user explicitly approved ${artifact_path} as this Task's one primary draft target in ${approval_path}. Read the confirmed interview, that approval and current Repository sources, then write the complete Markdown body intended for the approved target. This is the full artifact, not a plan, outline or review. The first output byte must be '#' and the document must have one nonempty H1; do not add reasoning, a preface or any text before that H1. Include the exact metadata lines '- primaryDraftStatus: \`DRAFT_FOR_USER_REVIEW\`', '- sourceTask: \`${task_id}\`', and '- authoringModel: \`FABLE_5\`'. Do not assume a duplicate planning document exists, do not invent domain-specific sections, and do not claim publication or implementation approval. Output only the complete Markdown artifact."
elif [[ "$legacy_user_flow_revision" == "true" ]]; then
  operation_contract="The user explicitly requested one new Fable-authored redraft of the historical USER-FLOW artifact after reading the Codex content review. Read the current ${artifact_path}, the recorded user redraft instruction at ${approval_path} and the review at ${redraft_review_path}, then write one complete replacement Markdown artifact. The review alone never authorizes this call. Preserve accepted content, apply only the user's requested direction, and do not invent user decisions. The first output byte must be '#' in the exact H1 '# EMI 프로젝트 통합관리시스템 전체 유저플로우'; do not add reasoning, a preface, a review summary or any text before that H1. Retain the exact metadata lines '- previewStatus: \`DRAFT_FOR_USER_REVIEW\`', '- sourceTask: \`${task_id}\`', and '- authoringModel: \`FABLE_5\`'. Output only the complete replacement Markdown artifact."
else
  operation_contract="The user explicitly requested one new Fable-authored redraft after reading the Codex content review. Read the current ${artifact_path}, the recorded user redraft instruction at ${approval_path} and the review at ${redraft_review_path}, then write one complete replacement Markdown artifact. The review alone never authorizes this call. Preserve accepted content, apply only the user's requested direction, and do not invent user decisions. The first output byte must be '#' and the document must have one nonempty H1; do not add reasoning, a preface, a review summary or any text before that H1. Retain the exact metadata lines '- primaryDraftStatus: \`DRAFT_FOR_USER_REVIEW\`', '- sourceTask: \`${task_id}\`', and '- authoringModel: \`FABLE_5\`'. Do not invent domain-specific sections or claim publication or implementation approval. Output only the complete replacement Markdown artifact."
fi

case "$session_mode" in
  CREATED_FULL_BASELINE|REFRESHED_AFTER_DRIFT)
    baseline_contract="Read CLAUDE.md, Root and applicable AGENTS.md files, the full Product Roadmap, completion and privacy policies, the interview file, both new-feature templates, relevant Task artifacts, and directly related code and tests. Build or refresh the full Task baseline before answering."
    ;;
  BOOTSTRAPPED_FROM_INTERVIEW)
    baseline_contract="This Task completed earlier interview rounds before Task-scoped sessions were enabled. Read AGENTS.md, CLAUDE.md and the latest interview file fully; it contains the verified Repository baseline, prior Fable questions and user answers. Use Grep to confirm this Task in the Roadmap. Do not rescan unrelated code or documents unless an unresolved decision cannot be answered from the canonical interview."
    ;;
  RESUMED)
    baseline_contract="Resume the existing Task baseline. The runner verified the same HEAD, unchanged instruction contract and no unexpected product-source diff. Read the latest interview file fully to receive the new user answers, use targeted Grep only when needed, and do not rescan unchanged instructions, Roadmap sections, code or tests."
    ;;
  RESUMED_PLANNING_PREFLIGHT)
    baseline_contract="Resume the existing Task baseline, then perform a planning preflight by rereading the latest interview, current Roadmap sections and directly relevant code and tests. Session memory accelerates discovery but does not replace current Repository verification for planning."
    ;;
  RESUMED_ARTIFACT_PREFLIGHT)
    if [[ "$mode" == "draft" ]]; then
      baseline_contract="Resume the existing Task baseline, then reread the latest interview, the latest change that explicitly approves the exact primary target, current Roadmap sections and directly relevant code and tests. Do not expect or create a duplicate planning or preview document. Session memory accelerates discovery but does not replace current Repository verification."
    else
      baseline_contract="Resume the existing Task baseline, then reread the latest interview, the current artifact, the latest change that explicitly approves the exact redraft target, the Codex content review, current Roadmap sections and directly relevant code and tests. Session memory accelerates discovery but does not replace current Repository verification."
    fi
    ;;
  *) fail "FABLE_SESSION_MODE_INVALID" 68 ;;
esac

prompt="You are the Fable 5 NEW_FEATURE interview and planning owner for this trusted Repository. Work read-only. Session memory is an acceleration cache only; ${interview_path} remains the canonical source for questions, answers and decisions. ${baseline_contract} Do not use shell, Git mutation, file writes, MCP, browser or computer control, recursive agents, credentials, runtime, DB writes, or provider calls. Do not invent user answers or approvals. Do not output absolute paths, identities, credentials, raw API or DB bodies, or tool logs. ${operation_contract} Output only the requested Markdown artifact for Codex to validate; never modify the Repository."

temp_root="${TMPDIR:-/tmp}"
if [[ "$temp_root" != /* || ! -d "$temp_root" ]]; then
  fail "FABLE_READONLY_TEMP_ROOT_INVALID" 68
fi

artifact_dir="$(mktemp -d "${temp_root%/}/emi-qms-fable-readonly.XXXXXX")" || fail "FABLE_READONLY_ARTIFACT_CREATE_FAILED" 68
chmod 700 "$artifact_dir"
stdout_file="$artifact_dir/stdout.md"
stderr_file="$artifact_dir/stderr.log"
: > "$stdout_file"
: > "$stderr_file"
chmod 600 "$stdout_file" "$stderr_file"

claude_args=(
  --safe-mode
  --model fable
  --permission-mode plan
  --tools "Read,Glob,Grep"
  --disable-slash-commands
  --strict-mcp-config
  --mcp-config '{"mcpServers":{}}'
)
if [[ "$session_mode" == RESUMED || "$session_mode" == RESUMED_PLANNING_PREFLIGHT || "$session_mode" == RESUMED_ARTIFACT_PREFLIGHT ]]; then
  claude_args+=(--resume "$session_id")
else
  claude_args+=(--session-id "$session_id")
fi

cli_started_at="$(date +%s)"
set +e
"$claude_bin" "${claude_args[@]}" \
  --print "$prompt" \
  > "$stdout_file" \
  2> "$stderr_file"
claude_exit_code=$?
set -e
cli_finished_at="$(date +%s)"

if [[ $claude_exit_code -ne 0 ]]; then
  emit_artifact_result "FAILED" "FABLE_CLI_EXECUTION_FAILED" 70
fi

if [[ ! -s "$stdout_file" ]]; then
  emit_artifact_result "FAILED" "FABLE_READONLY_OUTPUT_EMPTY" 71
fi

if grep -Fq "$repo_root" "$stdout_file"; then
  emit_artifact_result "FAILED" "FABLE_READONLY_OUTPUT_ABSOLUTE_PATH" 73
fi

if [[ "$mode" == "interview" ]]; then
  if ! grep -Eq 'interviewStatus:[[:space:]]*`?(QUESTIONS_REQUIRED|SUMMARY_CONFIRMATION_REQUIRED)`?' "$stdout_file" ||
     ! grep -Eq 'planningStatus:[[:space:]]*`?NOT_STARTED`?' "$stdout_file" ||
     ! grep -Eq 'implementationApproved:[[:space:]]*`?false`?' "$stdout_file" ||
     grep -Eq 'planningStatus:[[:space:]]*`?DRAFT`?' "$stdout_file"; then
    emit_artifact_result "FAILED" "FABLE_READONLY_INTERVIEW_CONTRACT_INVALID" 72
  fi
  if grep -Eq 'interviewStatus:[[:space:]]*`?QUESTIONS_REQUIRED`?' "$stdout_file"; then
    question_count="$(grep -Ec '^### 질문 [1-5]( | —)' "$stdout_file" || true)"
    if [[ "$question_count" -lt 1 || "$question_count" -gt 5 ]]; then
      emit_artifact_result "FAILED" "FABLE_READONLY_QUESTION_COUNT_INVALID" 72
    fi
  fi
elif [[ "$mode" == "planning" ]]; then
  if ! grep -Eq 'planningStatus:[[:space:]]*`?DRAFT`?' "$stdout_file" ||
     ! grep -Eq 'implementationApproved:[[:space:]]*`?false`?' "$stdout_file" ||
     ! grep -Eq 'userDecisionRequiredCount:[[:space:]]*`?[0-9]+`?' "$stdout_file"; then
    emit_artifact_result "FAILED" "FABLE_READONLY_PLANNING_CONTRACT_INVALID" 72
  fi
elif [[ "$legacy_user_flow_revision" == "true" ]]; then
  mermaid_count="$(grep -Ec '^```mermaid$' "$stdout_file" || true)"
  first_nonblank_line="$(awk 'NF { sub(/\r$/, ""); print; exit }' "$stdout_file")"
  if [[ "$first_nonblank_line" != '# EMI 프로젝트 통합관리시스템 전체 유저플로우' ]] ||
     ! grep -Fqx -- '# EMI 프로젝트 통합관리시스템 전체 유저플로우' "$stdout_file" ||
     ! grep -Fqx -- '- previewStatus: `DRAFT_FOR_USER_REVIEW`' "$stdout_file" ||
     ! grep -Fqx -- "- sourceTask: \`${task_id}\`" "$stdout_file" ||
     ! grep -Fqx -- '- authoringModel: `FABLE_5`' "$stdout_file" ||
     [[ "$mermaid_count" -lt 2 ]]; then
    emit_artifact_result "FAILED" "FABLE_READONLY_ARTIFACT_CONTRACT_INVALID" 72
  fi
else
  first_byte="$(LC_ALL=C head -c 1 "$stdout_file")"
  h1_count="$(grep -Ec '^# [^#[:space:]].*$' "$stdout_file" || true)"
  if [[ "$first_byte" != '#' ]] ||
     [[ "$h1_count" -ne 1 ]] ||
     ! grep -Fqx -- '- primaryDraftStatus: `DRAFT_FOR_USER_REVIEW`' "$stdout_file" ||
     ! grep -Fqx -- "- sourceTask: \`${task_id}\`" "$stdout_file" ||
     ! grep -Fqx -- '- authoringModel: `FABLE_5`' "$stdout_file"; then
    emit_artifact_result "FAILED" "FABLE_READONLY_ARTIFACT_CONTRACT_INVALID" 72
  fi
fi

if grep -Eqi -- '-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----' "$stdout_file" ||
   grep -Eqi '[[:alnum:]._%+-]+@[[:alnum:].-]+\.[[:alpha:]]{2,}' "$stdout_file" ||
   grep -Eqi '[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}' "$stdout_file"; then
  emit_artifact_result "FAILED" "FABLE_READONLY_OUTPUT_PRIVACY_GUARD" 73
fi

session_transcript="$(find_session_transcript "$session_id")"
if [[ -z "$session_transcript" || ! -f "$session_transcript" || -L "$session_transcript" ]]; then
  emit_artifact_result "FAILED" "FABLE_SESSION_TRANSCRIPT_MISSING" 75
fi
if [[ "$(stat -f %u "$session_transcript")" != "$current_uid" ]]; then
  emit_artifact_result "FAILED" "FABLE_SESSION_TRANSCRIPT_OWNER_INVALID" 75
fi

if [[ ! -f "$sessions_dir/$session_id" ]]; then
  : > "$sessions_dir/$session_id"
  chmod 600 "$sessions_dir/$session_id"
fi
write_private_state "$state_dir/current-session-id" "$session_id"
write_private_state "$state_dir/baseline-head" "$head_sha"
write_private_state "$state_dir/contract-digest" "$contract_digest"
if [[ "$mode" == "interview" ]]; then
  write_private_state "$state_dir/last-round" "$round"
fi

if [[ "$mode" == "interview" || "$mode" == "planning" || "$mode" == "draft" || "$mode" == "revise" ]]; then
  artifact_target="$repo_root/$artifact_path"
  artifact_directory="$(dirname "$artifact_target")"
  direct_write_tmp="$(mktemp "$artifact_directory/.fable-direct.XXXXXX")" || emit_artifact_result "FAILED" "FABLE_DIRECT_WRITE_TEMP_CREATE_FAILED" 76
  chmod 600 "$direct_write_tmp"
  cp "$stdout_file" "$direct_write_tmp"
  if ! cmp -s "$stdout_file" "$direct_write_tmp"; then
    emit_artifact_result "FAILED" "FABLE_DIRECT_WRITE_BYTE_MISMATCH" 76
  fi
  if [[ "$mode" == "revise" ]]; then
    shopt -s nullglob
    current_redraft_change_files=("$repo_root/${task_stem}-change-"*.md)
    shopt -u nullglob
    if [[ "${#current_redraft_change_files[@]}" -eq 0 ]] ||
       [[ "${current_redraft_change_files[${#current_redraft_change_files[@]}-1]}" != "$redraft_approval_file" ]] ||
       [[ ! -f "$redraft_approval_file" || -L "$redraft_approval_file" ]] ||
       [[ "$(shasum -a 256 "$redraft_approval_file" | awk '{print $1}')" != "$redraft_approval_digest" ]]; then
      emit_artifact_result "FAILED" "FABLE_READONLY_REVISION_APPROVAL_CHANGED" 76
    fi
    if [[ ! -f "$artifact_target" || -L "$artifact_target" ]] ||
       [[ "$(shasum -a 256 "$artifact_target" | awk '{print $1}')" != "$revision_target_digest" ]]; then
      emit_artifact_result "FAILED" "FABLE_READONLY_REVISION_TARGET_CHANGED" 76
    fi
    write_private_state "$redraft_claim_path" "COMMITTING"
    redraft_claim_consumed="true"
    mv -f "$direct_write_tmp" "$artifact_target"
    direct_write_tmp=""
    write_private_state "$redraft_claim_path" "CONSUMED"
  else
    if [[ "$mode" == "draft" ]]; then
      shopt -s nullglob
      current_primary_change_files=("$repo_root/${task_stem}-change-"*.md)
      shopt -u nullglob
      if [[ "${#current_primary_change_files[@]}" -eq 0 ]] ||
         [[ "${current_primary_change_files[${#current_primary_change_files[@]}-1]}" != "$primary_approval_file" ]] ||
         [[ ! -f "$primary_approval_file" || -L "$primary_approval_file" ]] ||
         [[ "$(shasum -a 256 "$primary_approval_file" | awk '{print $1}')" != "$primary_approval_digest" ]]; then
        emit_artifact_result "FAILED" "FABLE_READONLY_PRIMARY_DRAFT_APPROVAL_CHANGED" 76
      fi
    fi
    if ! ln "$direct_write_tmp" "$artifact_target" 2>/dev/null; then
      case "$mode" in
        interview) emit_artifact_result "FAILED" "FABLE_READONLY_INTERVIEW_ARTIFACT_EXISTS" 67 ;;
        planning) emit_artifact_result "FAILED" "FABLE_READONLY_PLANNING_TARGET_EXISTS" 67 ;;
        draft) emit_artifact_result "FAILED" "FABLE_READONLY_DRAFT_TARGET_EXISTS" 67 ;;
        *) emit_artifact_result "FAILED" "FABLE_DIRECT_WRITE_TARGET_EXISTS" 67 ;;
      esac
    fi
    rm "$direct_write_tmp"
    direct_write_tmp=""
  fi
  chmod 644 "$artifact_target"
  if ! cmp -s "$stdout_file" "$artifact_target"; then
    emit_artifact_result "FAILED" "FABLE_DIRECT_WRITE_FINAL_BYTE_MISMATCH" 76
  fi
  artifact_written="true"
fi

emit_artifact_result "READY" "FABLE_READONLY_OUTPUT_READY" 0
