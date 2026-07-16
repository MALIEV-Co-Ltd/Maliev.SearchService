using System.Text.RegularExpressions;

namespace Maliev.SearchService.Tests.Unit;

/// <summary>
/// Guards the immutable SearchService container build and promotion contract.
/// </summary>
public sealed class DeploymentWorkflowPolicyTests
{
    private static readonly string[] DeploymentWorkflows =
    [
        "ci-develop.yml",
        "ci-staging.yml",
        "ci-main.yml"
    ];

    /// <summary>
    /// Pull requests must prove the production image, its liveness, SBOM, and vulnerability posture.
    /// </summary>
    [Fact]
    public void PullRequestWorkflow_ValidatesProductionContainerWithoutPublishing()
    {
        var source = ReadWorkflow("pr-validation.yml");

        Assert.Contains("name: Build and scan production image", source, StringComparison.Ordinal);
        Assert.Contains("file: ./Maliev.SearchService.Api/Dockerfile", source, StringComparison.Ordinal);
        Assert.Contains("push: false", source, StringComparison.Ordinal);
        Assert.Contains("load: true", source, StringComparison.Ordinal);
        Assert.Contains("Smoke test production image liveness", source, StringComparison.Ordinal);
        Assert.Contains("/search/liveness", source, StringComparison.Ordinal);
        Assert.Contains("Generate image SBOM", source, StringComparison.Ordinal);
        Assert.Contains("trivy-sbom.cdx.json", source, StringComparison.Ordinal);
        Assert.Contains("severity: HIGH,CRITICAL", source, StringComparison.Ordinal);
        Assert.Contains("exit-code: \"1\"", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Develop is the only environment workflow allowed to build the deployable image.
    /// </summary>
    [Fact]
    public void DevelopWorkflow_BuildsAttestsAndPublishesImmutableImageDigest()
    {
        var source = ReadWorkflow("ci-develop.yml");

        Assert.Contains("environment: development", source, StringComparison.Ordinal);
        Assert.Contains("workload_identity_provider: ${{ secrets.GCP_WORKLOAD_IDENTITY_PROVIDER }}", source, StringComparison.Ordinal);
        Assert.Contains("service_account: ${{ secrets.GCP_SERVICE_ACCOUNT }}", source, StringComparison.Ordinal);
        Assert.Contains("id: build", source, StringComparison.Ordinal);
        Assert.Contains("push: true", source, StringComparison.Ordinal);
        Assert.Contains("dev-${{ steps.version.outputs.short_sha }}", source, StringComparison.Ordinal);
        Assert.Contains("subject-digest: ${{ steps.build.outputs.digest }}", source, StringComparison.Ordinal);
        Assert.Contains("push-to-registry: true", source, StringComparison.Ordinal);
        Assert.Contains("IMAGE_DIGEST: ${{ steps.build.outputs.digest }}", source, StringComparison.Ordinal);
        Assert.Contains("@$IMAGE_DIGEST", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Release promotion must copy the verified develop digest instead of rebuilding source.
    /// </summary>
    [Fact]
    public void StagingWorkflow_PromotesVerifiedDevelopDigestWithoutRebuild()
    {
        var source = ReadWorkflow("ci-staging.yml");

        Assert.Contains("tags: [\"release/v*\"]", source, StringComparison.Ordinal);
        Assert.Contains("environment: staging", source, StringComparison.Ordinal);
        Assert.Contains("gh attestation verify", source, StringComparison.Ordinal);
        Assert.Contains("docker buildx imagetools create", source, StringComparison.Ordinal);
        Assert.Contains("--prefer-index=false", source, StringComparison.Ordinal);
        Assert.Contains("source_digest", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("promoted_digest", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("docker build ", source, StringComparison.Ordinal);
        Assert.DoesNotContain("docker/build-push-action", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Production promotion must require an approved environment and preserve the staging digest.
    /// </summary>
    [Fact]
    public void ProductionWorkflow_PromotesVerifiedStagingDigestWithoutRebuild()
    {
        var source = ReadWorkflow("ci-main.yml");

        Assert.Contains("branches: [main]", source, StringComparison.Ordinal);
        Assert.Contains("environment: production", source, StringComparison.Ordinal);
        Assert.Contains("release/v*", source, StringComparison.Ordinal);
        Assert.Contains("gh attestation verify", source, StringComparison.Ordinal);
        Assert.Contains("docker buildx imagetools create", source, StringComparison.Ordinal);
        Assert.Contains("--prefer-index=false", source, StringComparison.Ordinal);
        Assert.Contains("source_digest", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("promoted_digest", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("docker build ", source, StringComparison.Ordinal);
        Assert.DoesNotContain("docker/build-push-action", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Generated GitOps changes are evidence-only and cannot activate or deploy SearchService.
    /// </summary>
    [Fact]
    public void EnvironmentWorkflows_CreateDraftDisabledOverlayEvidenceOnly()
    {
        foreach (var workflow in DeploymentWorkflows)
        {
            var source = ReadWorkflow(workflow);

            Assert.Contains("3-apps/maliev-search-service/overlays/", source, StringComparison.Ordinal);
            Assert.Contains("--draft", source, StringComparison.Ordinal);
            Assert.Contains("[DO NOT MERGE]", source, StringComparison.Ordinal);
            Assert.DoesNotContain("argocd/environments/", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("kubectl ", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("argocd ", source, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Environment workflows must use WIF and never accept a long-lived service-account JSON key.
    /// </summary>
    [Fact]
    public void EnvironmentWorkflows_UseWorkloadIdentityFederation()
    {
        foreach (var workflow in DeploymentWorkflows)
        {
            var source = ReadWorkflow(workflow);

            Assert.Contains("id-token: write", source, StringComparison.Ordinal);
            Assert.Contains("workload_identity_provider: ${{ secrets.GCP_WORKLOAD_IDENTITY_PROVIDER }}", source, StringComparison.Ordinal);
            Assert.Contains("service_account: ${{ secrets.GCP_SERVICE_ACCOUNT }}", source, StringComparison.Ordinal);
            Assert.DoesNotContain("credentials_json", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("GCP_SA_KEY", source, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Every workflow must bound permissions, duplicate execution, and runtime duration.
    /// </summary>
    [Theory]
    [InlineData("pr-validation.yml", "cancel-in-progress: true")]
    [InlineData("ci-develop.yml", "cancel-in-progress: true")]
    [InlineData("ci-staging.yml", "cancel-in-progress: false")]
    [InlineData("ci-main.yml", "cancel-in-progress: false")]
    public void Workflow_HasLeastPrivilegeConcurrencyAndTimeout(
        string workflow,
        string expectedCancellationPolicy)
    {
        var source = ReadWorkflow(workflow);

        Assert.Contains("permissions:", source, StringComparison.Ordinal);
        Assert.Contains("contents: read", source, StringComparison.Ordinal);
        Assert.Contains("concurrency:", source, StringComparison.Ordinal);
        Assert.Contains(expectedCancellationPolicy, source, StringComparison.Ordinal);
        Assert.Contains("timeout-minutes:", source, StringComparison.Ordinal);
        Assert.DoesNotContain("permissions: write-all", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Third-party actions must be immutable commit references rather than mutable tags.
    /// </summary>
    [Theory]
    [InlineData("_build-and-test.yml")]
    [InlineData("pr-validation.yml")]
    [InlineData("ci-develop.yml")]
    [InlineData("ci-staging.yml")]
    [InlineData("ci-main.yml")]
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
    /// The vulnerability audit must authenticate to the private package source just like restore.
    /// </summary>
    [Fact]
    public void BuildWorkflow_AuthenticatesPrivateFeedDuringVulnerabilityAudit()
    {
        var source = ReadWorkflow("_build-and-test.yml");
        var auditStep = source[source.IndexOf("- name: Audit resolved NuGet graph", StringComparison.Ordinal)..];

        Assert.Contains("NUGET_USERNAME: ${{ github.actor }}", auditStep, StringComparison.Ordinal);
        Assert.Contains("NUGET_PASSWORD: ${{ secrets.gitops_pat }}", auditStep, StringComparison.Ordinal);
    }

    /// <summary>
    /// The production Docker build must restore exact platform dependencies without local sibling projects.
    /// </summary>
    [Fact]
    public void Dockerfile_IsNonRootAndRestoresExactPublishedDependencies()
    {
        var source = ReadRepositoryFile("Maliev.SearchService.Api", "Dockerfile");

        Assert.Contains("ARG SERVICE_DEFAULTS_VERSION=1.0.89-alpha", source, StringComparison.Ordinal);
        Assert.Contains("ARG MESSAGING_CONTRACTS_VERSION=1.0.94-alpha", source, StringComparison.Ordinal);
        Assert.Contains("/p:GITHUB_ACTIONS=true", source, StringComparison.Ordinal);
        Assert.Contains("--mount=type=secret,id=nuget_username,required=true", source, StringComparison.Ordinal);
        Assert.Contains("--mount=type=secret,id=nuget_password,required=true", source, StringComparison.Ordinal);
        Assert.Contains("USER app", source, StringComparison.Ordinal);
        Assert.Contains("ENTRYPOINT [\"dotnet\", \"Maliev.SearchService.Api.dll\"]", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HEALTHCHECK", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectReference", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ephemeral WIF credential files and CI caches must never enter source control or Docker context.
    /// </summary>
    [Fact]
    public void RepositoryIgnoresGeneratedCloudCredentialsAndCiCaches()
    {
        foreach (var file in new[] { ".gitignore", ".dockerignore" })
        {
            var source = ReadRepositoryFile(file);

            Assert.Contains("gha-creds-*.json", source, StringComparison.Ordinal);
            Assert.Contains(".ci-nuget", source, StringComparison.Ordinal);
        }
    }

    private static string ReadWorkflow(string fileName) =>
        ReadRepositoryFile(".github", "workflows", fileName);

    private static string ReadRepositoryFile(params string[] segments)
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            Path.Combine(segments)));

        Assert.True(File.Exists(path), $"Could not find repository file: {path}");
        return File.ReadAllText(path);
    }
}
