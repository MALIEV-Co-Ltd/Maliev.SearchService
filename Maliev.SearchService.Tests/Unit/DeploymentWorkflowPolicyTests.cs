using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Maliev.SearchService.Tests.Unit;

/// <summary>
/// Guards the fail-closed SearchService validation workflow contract.
/// </summary>
public sealed class DeploymentWorkflowPolicyTests
{
    private static readonly string[] AllWorkflows =
    [
        "_build-and-test.yml",
        "pr-validation.yml",
        "ci-develop.yml",
        "ci-staging.yml",
        "ci-main.yml"
    ];

    private static readonly string[] BranchWorkflows =
    [
        "ci-develop.yml",
        "ci-staging.yml",
        "ci-main.yml"
    ];

    /// <summary>
    /// Branch and release workflows may validate source but may not publish or deploy it.
    /// </summary>
    [Fact]
    public void BranchAndReleaseWorkflows_AreValidationOnly()
    {
        foreach (var workflow in BranchWorkflows)
        {
            var source = ReadWorkflow(workflow);

            Assert.Contains("uses: ./.github/workflows/_build-and-test.yml", source, StringComparison.Ordinal);
            Assert.Contains("contents: read", source, StringComparison.Ordinal);
            Assert.DoesNotContain("environment:", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("id-token: write", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("packages: write", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("push: true", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("gcloud", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("maliev-gitops", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("kubectl", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("argocd", source, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Pull requests must prove the production image without publishing it.
    /// </summary>
    [Fact]
    public void PullRequestWorkflow_ValidatesProductionContainerWithoutPublishing()
    {
        var source = ReadWorkflow("pr-validation.yml");

        Assert.Contains("name: Build and scan production image", source, StringComparison.Ordinal);
        Assert.Contains("push: false", source, StringComparison.Ordinal);
        Assert.Contains("load: true", source, StringComparison.Ordinal);
        Assert.Contains("Smoke test production image liveness", source, StringComparison.Ordinal);
        Assert.Contains("severity: HIGH,CRITICAL", source, StringComparison.Ordinal);
        Assert.DoesNotContain("environment:", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("id-token: write", source, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Third-party actions must be immutable commit references rather than mutable tags.
    /// </summary>
    [Theory]
    [MemberData(nameof(WorkflowNames))]
    public void Workflow_UsesCommitPinnedThirdPartyActions(string workflow)
    {
        var source = ReadWorkflow(workflow);
        var mutableAction = Regex.Match(
            source,
            @"(?m)^\s*-?\s*uses:\s*(?!\./)(?<action>[^\s@]+)@(?<reference>(?![0-9a-f]{40}(?:\s|$))[^\s#]+)");

        Assert.False(
            mutableAction.Success,
            $"{workflow} contains mutable action reference {mutableAction.Groups["action"].Value}@{mutableAction.Groups["reference"].Value}.");
    }

    /// <summary>
    /// Provides every workflow name to the immutable-action test.
    /// </summary>
    public static TheoryData<string> WorkflowNames => [.. AllWorkflows];

    /// <summary>
    /// A known high advisory must make the package audit gate fail.
    /// </summary>
    [Fact]
    public void NuGetAuditGate_HighSeverityMicrosoftOpenApiFixtureReturnsFailure()
    {
        var script = RepositoryPath(".github", "scripts", "Assert-NoVulnerablePackages.ps1");
        var fixture = RepositoryPath(
            "Maliev.SearchService.Tests",
            "Fixtures",
            "microsoft-openapi-2.0.0-high.json");

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "pwsh",
            ArgumentList = { "-NoProfile", "-File", script, "-AuditJsonPath", fixture },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        });

        Assert.NotNull(process);
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.NotEqual(0, process.ExitCode);
        Assert.Contains("Microsoft.OpenApi", output, StringComparison.Ordinal);
        Assert.Contains("High", output, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadWorkflow(string fileName) =>
        ReadRepositoryFile(".github", "workflows", fileName);

    private static string ReadRepositoryFile(params string[] segments)
    {
        var path = RepositoryPath(segments);
        Assert.True(File.Exists(path), $"Could not find repository file: {path}");
        return File.ReadAllText(path);
    }

    private static string RepositoryPath(params string[] segments) =>
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            Path.Combine(segments)));
}
