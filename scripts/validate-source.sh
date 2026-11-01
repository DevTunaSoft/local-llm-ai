#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
tools_root="${WI_TOOLS_ROOT:-$(dirname -- "$repo_root")/.tools}"
export DOTNET_ROOT="$tools_root/dotnet" DOTNET_CLI_HOME="$tools_root/dotnet-home"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
export LD_LIBRARY_PATH="$tools_root/sqlcipher${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
export WI_TEST_RESTIC="$tools_root/restic/restic"
results="${WI_QA_RESULTS_DIR:-/tmp/workintelligence-qa}"
mkdir -p "$results"
cd "$repo_root"
run_logged() {
    local log_name="$1"
    shift
    if "$@" > "$results/$log_name.log" 2>&1; then
        cat "$results/$log_name.log"
    else
        local result=$?
        cat "$results/$log_name.log"
        return "$result"
    fi
}
run_logged build "$DOTNET_ROOT/dotnet" build WorkIntelligence.slnx -c Release --no-restore -m:1 -nr:false \
    -p:UseSharedCompilation=false -p:UseAppHost=false -p:SelfContained=false -p:PublishSingleFile=false
run_logged behavioral-tests "$DOTNET_ROOT/dotnet" test tests/Core/WorkIntelligence.Core.Tests.csproj -c Release --no-restore \
    -m:1 -nr:false -p:UseSharedCompilation=false --logger 'trx;LogFileName=core-tests.trx' --results-directory "$results"
for script_name in static_qa recheck_qa independent_round3_qa learning_qa; do
    run_logged "$script_name" "$tools_root/qa-venv/bin/python" "scripts/$script_name.py"
done
echo "Source compilation and portable service tests completed. Windows acceptance tests remain separate."
