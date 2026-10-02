#!/usr/bin/env bash
# shellcheck disable=SC2016,SC1003
# The coverage gate must ignore a coverage.cobertura.xml that lives in the
# checkout. Narrowing the search to tests/EdsDcfNet.Tests/TestResults is not
# enough: that file can be force-added (it is gitignored) or left behind by
# an older run, and a checker report under examples/ can sit in the tree too.
# CI writes the test run to a fresh directory outside the checkout and passes
# only that directory to tools/enforce-coverage-threshold.sh.
set -euo pipefail

root=$(cd "$(dirname "$0")/.." && pwd)
script="$root/tools/enforce-coverage-threshold.sh"
build_workflow="$root/.github/workflows/build.yml"
release_workflow="$root/.github/workflows/semantic-release.yml"
contributing="$root/CONTRIBUTING.md"

export LC_ALL=C

failures=0

fail() {
  echo "FAIL: $*" >&2
  failures=$((failures + 1))
}

# --- workflow wiring --------------------------------------------------------

extract_run_body() {
  local file="$1"
  local step="$2"
  awk -v step="$step" '
    $0 ~ "- name: " step "$" { in_step = 1; next }
    in_step && /run: \|[[:space:]]*$/ { capture = 1; next }
    capture {
      if ($0 ~ /^[[:space:]]*$/) {
        print ""
        next
      }
      match($0, /^[[:space:]]*/)
      if (body_indent == 0) {
        body_indent = RLENGTH
      }
      if (RLENGTH < body_indent) {
        exit
      }
      print substr($0, body_indent + 1)
    }
  ' "$file"
}

build_test_body=$(extract_run_body "$build_workflow" "Test")
release_test_body=$(extract_run_body "$release_workflow" "Test")
if [[ -z "$build_test_body" || -z "$release_test_body" ]]; then
  fail "could not extract the Test step from both workflows"
elif [[ "$build_test_body" != "$release_test_body" ]]; then
  fail "build.yml and semantic-release.yml Test steps differ"
  diff -u <(printf '%s\n' "$build_test_body") <(printf '%s\n' "$release_test_body") >&2 || true
fi

for needle in \
  'mktemp -d "${temp_root}/coverage.XXXXXX"' \
  'MSYS_NO_PATHCONV=1 dotnet test' \
  '--results-directory "$dotnet_results"' \
  'results_directory=${results}' \
  'RUNNER_TEMP'
do
  if [[ "$build_test_body" != *"$needle"* ]]; then
    fail "Test step is missing: $needle"
  fi
done

gate_line='bash tools/enforce-coverage-threshold.sh "${{ steps.test.outputs.results_directory }}"'
for workflow in "$build_workflow" "$release_workflow"; do
  if [[ $(grep -F -c "$gate_line" "$workflow") -ne 1 ]]; then
    fail "$(basename "$workflow") must pass the fresh results directory to the gate exactly once"
  fi
  if grep -F -q 'enforce-coverage-threshold.sh .' "$workflow"; then
    fail "$(basename "$workflow") still searches the checkout"
  fi
done

if grep -F -q 'codecov/patch' "$build_workflow" || grep -F -q 'codecov/patch' "$release_workflow"; then
  fail "workflows still post or wait on codecov/patch"
fi
if grep -E -q 'Waiting for Codecov|codecov_baseline|CODECOV_AFTER' "$build_workflow"; then
  fail "build.yml still waits for Codecov"
fi
if [[ $(grep -c 'context="coverage/threshold"' "$build_workflow") -ne 2 ]]; then
  fail "build.yml must post coverage/threshold from the build job and the release relay"
fi
if grep -F -q 'context=' "$release_workflow"; then
  fail "semantic-release.yml posts a commit status; the relay in build.yml owns that"
fi

if ! grep -F -q '| `develop` | Yes | `build`; `coverage/threshold` (GitHub Actions) | Yes |' "$contributing"; then
  fail "CONTRIBUTING.md branch-protection table does not require coverage/threshold on develop"
fi
if ! grep -F -q '| `main` | Yes | `build`; `codecov/patch` (Codecov app) | Yes |' "$contributing"; then
  fail "CONTRIBUTING.md branch-protection table dropped Codecov's check on main"
fi

# --- script: a committed high-counter report must not move the result ------

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

checkout="$work/checkout"
results="$work/runner-temp/coverage-run"
mkdir -p "$checkout" "$results"

write_report() {
  local path="$1"
  local covered="$2"
  local valid="$3"
  mkdir -p "$(dirname "$path")"
  cat >"$path" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<coverage lines-covered="${covered}" lines-valid="${valid}" line-rate="0" branch-rate="0" version="1.9" timestamp="1" branches-covered="0" branches-valid="0">
</coverage>
EOF
}

# Two host reports, 1840/2000 = 92.00%, under the fresh results directory.
write_report "$results/net10/coverage.cobertura.xml" 900 1000
write_report "$results/net48/coverage.cobertura.xml" 940 1000

run_gate() {
  local search_root="$1"
  local log="$2"
  local github_output="$3"
  gate_status=0
  GITHUB_OUTPUT="$github_output" COVERAGE_MIN_PERCENT=95 \
    bash "$script" "$search_root" >"$log" 2>"$work/gate.err" || gate_status=$?
}

percent_of() {
  sed -n 's/^Line coverage: \([0-9.][0-9.]*\)%.*/\1/p' "$1"
}

before_log="$work/before.log"
before_output="$work/before.out"
run_gate "$results" "$before_log" "$before_output"
before_percent=$(percent_of "$before_log")
if [[ "$gate_status" -ne 1 || "$before_percent" != "92.00" ]]; then
  fail "results directory should fail at 92.00% (status=$gate_status percent=$before_percent)"
  cat "$before_log" "$work/gate.err" >&2 || true
fi

# Commit the attack the old checkout-wide search (and a TestResults-only
# search) would count. .gitignore ignores the reports; git add -f is the
# force-add a pull request can do.
mkdir -p "$checkout/tests/EdsDcfNet.Tests/TestResults/library" \
  "$checkout/examples/EdsDcfNet.Checker/bin/Release/net10.0"
printf 'coverage*.xml\n' >"$checkout/.gitignore"
write_report "$checkout/tests/EdsDcfNet.Tests/TestResults/library/net10/coverage.cobertura.xml" 900 1000
write_report "$checkout/tests/EdsDcfNet.Tests/TestResults/library/net48/coverage.cobertura.xml" 940 1000
write_report "$checkout/coverage.cobertura.xml" 1000000 1000000
write_report "$checkout/tests/EdsDcfNet.Tests/TestResults/committed-high-counters/coverage.cobertura.xml" 1000000 1000000
write_report "$checkout/examples/EdsDcfNet.Checker/bin/Release/net10.0/coverage.cobertura.xml" 1000000 1000000
git -C "$checkout" init -q
# The breaking-intent runner has no user.name or user.email. These values
# belong to this throwaway repository only; they do not change global config.
git -C "$checkout" config --local user.name "test"
git -C "$checkout" config --local user.email "test@example.invalid"
git -C "$checkout" config --local commit.gpgsign false
git -C "$checkout" add -f -A
git -C "$checkout" commit -q -m "commit inflated coverage reports"

after_log="$work/after.log"
after_output="$work/after.out"
run_gate "$results" "$after_log" "$after_output"
after_percent=$(percent_of "$after_log")
if [[ "$gate_status" -ne 1 || "$after_percent" != "$before_percent" ]]; then
  fail "committed high-counter report changed the gate (before=$before_percent after=$after_percent status=$gate_status)"
  cat "$after_log" "$work/gate.err" >&2 || true
fi
if grep -F -q 'committed-high-counters' "$after_log" || grep -F -q 'EdsDcfNet.Checker' "$after_log"; then
  fail "gate listed a working-tree report"
fi
if ! grep -F -q "coverage_percent=92.00" "$after_output"; then
  fail "GITHUB_OUTPUT coverage_percent is not 92.00"
fi
if grep -F -q 'committed-high-counters' "$after_output" || grep -F -q 'EdsDcfNet.Checker' "$after_output"; then
  fail "GITHUB_OUTPUT coverage_files includes a working-tree report"
fi
for real in "$results/net10/coverage.cobertura.xml" "$results/net48/coverage.cobertura.xml"; do
  if ! grep -F -q "$real" "$after_output"; then
    fail "GITHUB_OUTPUT is missing $real"
  fi
done

# The same committed file inside TestResults would lift 92% over the threshold.
# That is why the gate must not be pointed at the checkout.
narrow_log="$work/narrow.log"
narrow_output="$work/narrow.out"
run_gate "$checkout/tests/EdsDcfNet.Tests/TestResults" "$narrow_log" "$narrow_output"
narrow_percent=$(percent_of "$narrow_log")
if [[ "$gate_status" -eq 0 && "$narrow_percent" == "99.98" ]]; then
  :
else
  fail "TestResults search should pass at 99.98% because of the committed file (status=$gate_status percent=$narrow_percent)"
  cat "$narrow_log" "$work/gate.err" >&2 || true
fi

# Usage stays explicit so a caller cannot fall back to searching ".".
usage_status=0
bash "$script" >"$work/usage.out" 2>"$work/usage.err" || usage_status=$?
if [[ "$usage_status" -eq 0 ]]; then
  fail "gate without a results directory should fail"
fi
missing_status=0
bash "$script" "$work/does-not-exist" >"$work/missing.out" 2>"$work/missing.err" || missing_status=$?
if [[ "$missing_status" -eq 0 ]]; then
  fail "gate should fail when the results directory is missing"
fi

if [[ "$failures" -ne 0 ]]; then
  echo "$failures failure(s)" >&2
  exit 1
fi

echo "coverage gate ignores a committed high-counter report and posts coverage/threshold."
