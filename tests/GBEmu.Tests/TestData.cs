namespace GBEmu.Tests;

/// <summary>Locates third-party test data fetched by scripts/fetch-test-data.sh.</summary>
public static class TestData
{
    public static string Root { get; } = Locate();

    public static string Sm83Dir => Path.Combine(Root, "sm83");
    public static string CpuInstrsDir => Path.Combine(Root, "cpu_instrs", "individual");

    private static string Locate()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "GBEmu.sln")))
                return Path.Combine(dir.FullName, "tests", "data");
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Could not find repository root (GBEmu.sln).");
    }
}

/// <summary>Theory that is skipped when the SingleStepTests sm83 data is absent.</summary>
public sealed class Sm83SuiteTheoryAttribute : TheoryAttribute
{
    public Sm83SuiteTheoryAttribute()
    {
        if (!Directory.Exists(TestData.Sm83Dir))
            Skip = "sm83 test data not found — run scripts/fetch-test-data.sh";
    }
}

/// <summary>Theory that is skipped when the Blargg test ROMs are absent.</summary>
public sealed class BlarggRomTheoryAttribute : TheoryAttribute
{
    public BlarggRomTheoryAttribute()
    {
        if (!Directory.Exists(TestData.CpuInstrsDir))
            Skip = "Blargg ROMs not found — run scripts/fetch-test-data.sh";
    }
}
