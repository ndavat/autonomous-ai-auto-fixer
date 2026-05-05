using AutoFixer.Audit;
using AutoFixer.Models;
using AutoFixer.VCS;
using FluentAssertions;
using System.Net;
using System.Text;

namespace AutoFixer.Tests;

public class AzureDevOpsClientTests
{
    public AzureDevOpsClientTests()
    {
        LoggerSetup.Initialize();
    }

    [Fact]
    public void Constructor_WithoutApiUrl_ShouldThrow()
    {
        var act = () => new AzureDevOpsClient(new VcsConfig
        {
            Provider = "azure-devops",
            Token = "fake-pat",
            ApiUrl = null
        });

        act.Should().Throw<ArgumentException>().WithMessage("*Azure DevOps requires VcsConfig.ApiUrl*");
    }

    [Fact]
    public async Task CreatePullRequestAsync_WithoutToken_ShouldReturnNull()
    {
        using var client = new AzureDevOpsClient(new VcsConfig
        {
            Provider = "azure-devops",
            Token = null,
            ApiUrl = "https://dev.azure.com/acme/project"
        });

        Environment.SetEnvironmentVariable("AZURE_DEVOPS_PAT", null);
        Environment.SetEnvironmentVariable("ADO_PAT", null);

        var request = new PullRequestRequest(
            RepoFullName: "my-repo",
            BaseBranch: "main",
            HeadBranch: "autofix/abc12345",
            Title: "Auto-fix: test",
            Description: "desc",
            Files: new[] { new FileChange("README.md", "new content") });

        var result = await client.CreatePullRequestAsync(request);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreatePullRequestAsync_HappyPath_ShouldIssueExpectedRequestsAndReturnUrl()
    {
        var handler = new CapturingHandler(req =>
        {
            var path = req.RequestUri!.PathAndQuery;

            // GET base branch ref
            if (req.Method == HttpMethod.Get && path.Contains("/refs") && path.Contains("heads%2Fmain"))
            {
                return TestHelpers.JsonResponse(HttpStatusCode.OK, new { value = new[] { new { name = "refs/heads/main", objectId = "base-sha-123" } } });
            }

            // GET head branch ref (for push oldObjectId)
            if (req.Method == HttpMethod.Get && path.Contains("/refs") && path.Contains("heads%2Fautofix"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{ \"value\": [] }", Encoding.UTF8, "application/json")
                };
            }

            // POST new branch ref
            if (req.Method == HttpMethod.Post && path.Contains("/refs"))
            {
                return TestHelpers.JsonResponse(HttpStatusCode.Created, new { });
            }

            // POST push
            if (req.Method == HttpMethod.Post && path.Contains("/pushes"))
            {
                return TestHelpers.JsonResponse(HttpStatusCode.Created, new { });
            }

            // POST open PR
            if (req.Method == HttpMethod.Post && path.Contains("/pullrequests"))
            {
                return TestHelpers.JsonResponse(HttpStatusCode.Created, new
                {
                    pullRequestId = 42,
                    _links = new
                    {
                        web = new { href = "https://dev.azure.com/acme/project/_git/my-repo/pullrequest/42" }
                    }
                });
            }

            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });

        using var http = new HttpClient(handler);
        using var client = new AzureDevOpsClient(
            new VcsConfig { Provider = "azure-devops", Token = "fake-pat", ApiUrl = "https://dev.azure.com/acme/project" },
            null,
            http);

        var request = new PullRequestRequest(
            RepoFullName: "my-repo",
            BaseBranch: "main",
            HeadBranch: "autofix/abc12345",
            Title: "Auto-fix: test",
            Description: "desc",
            Files: new[] { new FileChange("README.md", "new content") });

        var result = await client.CreatePullRequestAsync(request);

        result.Should().Be("https://dev.azure.com/acme/project/_git/my-repo/pullrequest/42");
        handler.Requests.Should().HaveCount(5);

        // All requests should carry Basic auth header
        handler.Requests.Should().OnlyContain(r => r.Headers.Authorization != null && r.Headers.Authorization.Scheme == "Basic");
    }

    [Fact]
    public async Task CreatePullRequestAsync_ExistingBranch_ShouldContinue()
    {
        var handler = new CapturingHandler(req =>
        {
            var path = req.RequestUri!.PathAndQuery;

            // GET base branch ref
            if (req.Method == HttpMethod.Get && path.Contains("/refs") && path.Contains("heads%2Fmain"))
            {
                return TestHelpers.JsonResponse(HttpStatusCode.OK, new { value = new[] { new { name = "refs/heads/main", objectId = "base-sha" } } });
            }

            // GET head branch ref (exists now)
            if (req.Method == HttpMethod.Get && path.Contains("/refs") && path.Contains("autofix"))
            {
                return TestHelpers.JsonResponse(HttpStatusCode.OK, new { value = new[] { new { name = "refs/heads/autofix/existing", objectId = "existing-sha" } } });
            }

            // POST new branch ref → 409 Conflict
            if (req.Method == HttpMethod.Post && path.Contains("/refs"))
            {
                return new HttpResponseMessage(HttpStatusCode.Conflict)
                {
                    Content = new StringContent("{ \"message\": \"Ref already exists\" }", Encoding.UTF8, "application/json")
                };
            }

            // POST push
            if (req.Method == HttpMethod.Post && path.Contains("/pushes"))
            {
                return TestHelpers.JsonResponse(HttpStatusCode.Created, new { });
            }

            // POST open PR
            if (req.Method == HttpMethod.Post && path.Contains("/pullrequests"))
            {
                return TestHelpers.JsonResponse(HttpStatusCode.Created, new { pullRequestId = 99 });
            }

            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });

        using var http = new HttpClient(handler);
        using var client = new AzureDevOpsClient(
            new VcsConfig { Provider = "azure-devops", Token = "fake-pat", ApiUrl = "https://dev.azure.com/acme/project" },
            null,
            http);

        var request = new PullRequestRequest(
            RepoFullName: "my-repo",
            BaseBranch: "main",
            HeadBranch: "autofix/existing",
            Title: "Auto-fix",
            Description: "desc",
            Files: new[] { new FileChange("src/foo.cs", "fix") });

        var result = await client.CreatePullRequestAsync(request);

        result.Should().NotBeNull();
        result.Should().Contain("pullrequest/99");
    }


}
