#!/usr/bin/env bash
set -euo pipefail

readonly EXPECT_BIN="/usr/bin/expect"
readonly TMP_ROOT="${TMPDIR:-/tmp}"

if ! command -v claude >/dev/null 2>&1; then
  printf '%s\n' "projectionStatus=FAILED_CLAUDE_NOT_FOUND"
  exit 1
fi

if [[ ! -x "$EXPECT_BIN" ]]; then
  printf '%s\n' "projectionStatus=FAILED_EXPECT_NOT_FOUND"
  exit 1
fi

usage_dir="$(mktemp -d "$TMP_ROOT/emi-qms-claude-usage.XXXXXX")"
raw_log="$usage_dir/raw.typescript"

cleanup() {
  rm -f "$raw_log"
  rmdir "$usage_dir" 2>/dev/null || true
}
trap cleanup EXIT INT TERM

case "$usage_dir" in
  "$TMP_ROOT"/emi-qms-claude-usage.*) ;;
  *)
    printf '%s\n' "projectionStatus=FAILED_TEMP_OWNERSHIP"
    exit 1
    ;;
esac

"$EXPECT_BIN" >"$raw_log" <<'EXPECT'
set timeout 45
log_user 1

spawn claude --safe-mode --permission-mode plan --tools "" --strict-mcp-config --mcp-config {{"mcpServers":{}}} --name usage-projection

expect {
  -re {❯} {}
  timeout { exit 21 }
  eof { exit 22 }
}

send -- "/usage\r"

expect {
  -re {Current.*week.*\(Fable\)} {}
  timeout { exit 23 }
  eof { exit 24 }
}

after 4000
send -- "\033"

expect {
  -re {❯} {}
  timeout { exit 25 }
  eof { exit 26 }
}

send -- "/exit\r"
expect eof
EXPECT

perl -0777 -e '
  use strict;
  use warnings;

  my $text = do { local $/; <> };
  my $raw_byte_count = length($text);
  $text =~ s/\e\][^\a]*(?:\a|\e\\)//gs;
  $text =~ s/\e\[[0-?]*[ -\/]*[\@-~]//g;
  my $stripped_byte_count = length($text);

  my ($all_used, $all_reset, $fable_used, $fable_reset);

  while ($text =~ /Current\s*week\s*\(all\s*models\).*?(\d+)%\s*used.*?Resets\s*([A-Z][a-z]{2}\s*\d{1,2}\s*at\s*\d{1,2}(?::\d{2})?\s*(?:am|pm)\s*\(Asia\/Seoul\))/gsi) {
    ($all_used, $all_reset) = ($1, $2);
  }

  while ($text =~ /Current\s*week\s*\(Fable\).*?(\d+)%\s*used.*?Resets\s*([A-Z][a-z]{2}\s*\d{1,2}\s*at\s*\d{1,2}(?::\d{2})?\s*(?:am|pm)\s*\(Asia\/Seoul\))/gsi) {
    ($fable_used, $fable_reset) = ($1, $2);
  }

  my @used_candidates = ($text =~ /(\d+)%\s*used/gsi);
  my @dated_reset_candidates = ($text =~ /Resets\s*([A-Z][a-z]{2}\s*\d{1,2}\s*at\s*\d{1,2}(?::\d{2})?\s*(?:am|pm)\s*\(Asia\/Seoul\))/gsi);
  my $all_label_present = $text =~ /Current\s*week\s*\(all\s*models\)/si ? 1 : 0;

  if ((!defined $all_used || !defined $fable_used)
      && $all_label_present
      && scalar(@used_candidates) >= 2) {
    $all_used = $used_candidates[-2];
    $fable_used = $used_candidates[-1];
  }

  if ((!defined $all_reset || !defined $fable_reset)
      && scalar(@dated_reset_candidates) >= 2) {
    $all_reset = $dated_reset_candidates[-2];
    $fable_reset = $dated_reset_candidates[-1];
  }

  if (!defined $all_used || !defined $fable_used) {
    my @reset_candidates = ($text =~ /Resets/gsi);
    my $fable_label_present = $text =~ /Current\s*week\s*\(Fable\)/si ? 1 : 0;
    print "projectionStatus=FAILED_PARSE\n";
    print "rawByteCount=$raw_byte_count\n";
    print "strippedByteCount=$stripped_byte_count\n";
    print "allModelsLabelPresent=$all_label_present\n";
    print "fableLabelPresent=$fable_label_present\n";
    print "usedCandidateCount=" . scalar(@used_candidates) . "\n";
    print "usedCandidates=" . join(",", @used_candidates) . "\n";
    print "resetCandidateCount=" . scalar(@reset_candidates) . "\n";
    exit 2;
  }

  $all_reset = "UNAVAILABLE_TUI_PARSE" if !defined $all_reset;
  $fable_reset = "UNAVAILABLE_TUI_PARSE" if !defined $fable_reset;

  $all_reset =~ s/\s+//g;
  $fable_reset =~ s/\s+//g;

  print "projectionStatus=READY\n";
  print "allModelsUsedPercent=$all_used\n";
  print "allModelsRemainingPercent=" . (100 - $all_used) . "\n";
  print "allModelsReset=$all_reset\n";
  print "fableUsedPercent=$fable_used\n";
  print "fableRemainingPercent=" . (100 - $fable_used) . "\n";
  print "fableReset=$fable_reset\n";
' "$raw_log"
