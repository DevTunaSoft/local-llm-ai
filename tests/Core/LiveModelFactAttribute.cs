using Xunit;

namespace WorkIntelligence.Tests;

/// <summary>Missing optional native fixtures are reported as SKIPPED, never as a passing no-op.
/// Mandatory acceptance runs do not skip: the test body fails if required inputs are absent.</summary>
public sealed class LiveModelFactAttribute : FactAttribute
{
    public LiveModelFactAttribute(string modelVariable,string requiredVariable)
    {
        if (Environment.GetEnvironmentVariable(requiredVariable) != "1" &&
            (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(modelVariable)) ||
             string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WI_DEV_RUNTIME_DIR"))))
            Skip = "NOT EXECUTED: requires a real local GGUF and a vetted native runtime.";
    }
}
