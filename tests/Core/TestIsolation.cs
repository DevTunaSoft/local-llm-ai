using System.Runtime.CompilerServices;

namespace WorkIntelligence.Tests;

internal static class TestIsolation
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // No test can point at an existing user's vault, even if this variable was inherited.
        Environment.SetEnvironmentVariable("WORKINTELLIGENCE_DATA_ROOT",
            Path.Combine(Path.GetTempPath(), "wi-tests-" + Guid.NewGuid().ToString("N")));
    }
}
