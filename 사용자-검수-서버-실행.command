#!/usr/bin/env bash

script_dir="$(cd "$(dirname "$0")" && pwd)"
launcher="${script_dir}/scripts/start-experiment-validation.sh"

clear
printf 'EMI 프로젝트 통합관리시스템 사용자 검수 서버를 준비합니다.\n\n'

if bash "${launcher}"; then
  exit_code="0"
  printf '\n정상적으로 준비됐습니다. Enter를 누르면 이 창만 닫힙니다.\n'
else
  exit_code="$?"
  printf '\n실행하지 못했습니다. 위 안내를 확인한 뒤 다시 실행해 주세요.\n'
  printf 'Enter를 누르면 이 창이 닫힙니다.\n'
fi

read -r
exit "${exit_code}"
