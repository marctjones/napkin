#!/bin/zsh
# The local landing gate from CLAUDE.md: build, test with coverage, ratchet check.
# Exits non-zero if the build has errors, any test fails, or ratchet check fails.
set -o pipefail
cd "$(git rev-parse --show-toplevel)" || exit 3
mkdir -p artifacts
rm -rf artifacts/test-results

echo "== build =="
nice -n 19 dotnet build napkin.sln --configuration Debug 2>&1 | tee artifacts/gate-build.log \
  | grep -E "Warn|Error|error|warning" | grep -v "^\s*0 "
build_status=${pipestatus[1]}

echo "== test =="
nice -n 19 dotnet test napkin.sln --configuration Debug --no-build \
  --settings ratchet/coverage.runsettings --collect:"XPlat Code Coverage" \
  --logger trx --results-directory artifacts/test-results 2>&1 | tee artifacts/gate-test.log \
  | grep -E "Passed!|Failed!|Failed |error"
test_status=${pipestatus[1]}

echo "== ratchet check =="
nice -n 19 dotnet run --project tools/Napkin.Tools -- ratchet check 2>&1 | tee artifacts/gate-ratchet.log | tail -15
ratchet_status=${pipestatus[1]}

passed=$(grep -oE '[0-9]+ Passed' artifacts/gate-test.log | tail -1)
failed=$(grep -oE '[0-9]+ Failed' artifacts/gate-test.log | tail -1)

if [ "$build_status" -ne 0 ] || [ "$test_status" -ne 0 ] || [ "$ratchet_status" -ne 0 ]; then
  echo "GATE FAILED: build=$build_status test=$test_status ratchet=$ratchet_status (${passed:-? Passed}, ${failed:-0 Failed})"
  exit 1
fi
echo "GATE PASSED: build ok, ${passed:-tests passed}, ${failed:-0 Failed}, ratchet ok"
exit 0
