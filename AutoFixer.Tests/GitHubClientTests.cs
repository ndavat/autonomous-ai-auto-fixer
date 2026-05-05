using AutoFixer.Audit;
using AutoFixer.Models;
using AutoFixer.VCS;
using FluentAssertions;
using System.Net;
using System.Text;
using System.Text.Json;

namespace AutoFixer.Tests;

public class GitHubClientTests
{
    public GitHubClientTests()
    {
        LoggerSetup.Initialize();
    }

    [Fact]
    public async Task CreatePullRequestAsync_WithoutToken_ShouldReturnNull()
    {
        using var client = new GitHubClient(new VcsConfig { Provider = "github", Token = null, ApiUrl = "https://api.github.test" });
        // Ensure env var does not leak in
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);

        var request = new PullRequestRequest(
            RepoFullName: "acme/app",
            BaseBranch: "main",
            HeadBranch: "autofix/abc12345",
            Title: "Auto-fix: test",
            Description: "desc",
            Files: new[] { new FileChange("README.md", "new content") });

        var result = await client.CreatePullRequestAsync(request);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreatePullRequestAsync_InvalidRepoName_ShouldReturnNull()
    {
        var handler = new CapturingHandler();
        using var http = new HttpClient(handler) { BaseAddress = null };
        using var client = new GitHubClient(
            new VcsConfig { Provider = "github", Token = "fake-token", ApiUrl = "https://api.github.test" },
            null,
            http);

        var request = new PullRequestRequest(
            RepoFullName: "not-a-slash-repo",
            BaseBranch: "main",
            HeadBranch: "autofix/abc12345",
            Title: "Auto-fix",
            Description: "desc",
            Files: Array.Empty<FileChange>());

        var result = await client.CreatePullRequestAsync(request);

        result.Should().BeNull();
        handler.Requests.Should().BeEmpty("no HTTP calls should be made for invalid repo names");
    }

    [Fact]
    public async Task CreatePullRequestAsync_HappyPath_ShouldIssueExpectedRequestsAndReturnUrl()
    {
        var handler = new CapturingHandler(req =>
        {
            var path = req.RequestUri!.AbsolutePath;

            // GET base branch ref
            if (req.Method == HttpMethod.Get && path.EndsWith("/git/ref/heads/main"))
            {
                return TestHelpers.JsonResponse(HttpStatusCode.OK, new { @object = new { sha = "base-sha-123" } });
            }

            // POST new branch ref
            if (req.Method == HttpMethod.Post && path.EndsWith("/git/refs"))
            {
                return TestHelpers.JsonResponse(HttpStatusCode.Created, new { });
            }

            // GET existing file (should 404 so we know it's a new file)
            if (req.Method == HttpMethod.Get && path.EndsWith("/contents/README.md"))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            // PUT file contents
            if (req.Method == HttpMethod.Put && path.EndsWith("/contents/README.md"))
            {
                return TestHelpers.JsonResponse(HttpStatusCode.Created, new { content = new { sha = "file-sha" } });
            }

            // POST open PR
            if (req.Method == HttpMethod.Post && path.EndsWith("/pulls"))
            {
                return TestHelpers.JsonResponse(HttpStatusCode.Created, new { html_url = "https://github.test/acme/app/pull/1" });
            }

            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        }, bufferBody: true);

        using var http = new HttpClient(handler);
        using var client = new GitHubClient(
            new VcsConfig { Provider = "github", Token = "fake-token", ApiUrl = "https://api.github.test" },
            null,
            http);

        var request = new PullRequestRequest(
            RepoFullName: "acme/app",
            BaseBranch: "main",
            HeadBranch: "autofix/abc12345",
            Title: "Auto-fix: test",
            Description: "desc",
            Files: new[] { new FileChange("README.md", "new content") });

        var result = await client.CreatePullRequestAsync(request);

        result.Should().Be("https://github.test/acme/app/pull/1");
        handler.Requests.Should().HaveCount(5);

        // All requests should carry the bearer token
        handler.Requests.Should().OnlyContain(r => r.Headers.Authorization!.Scheme == "Bearer");

        var putRequest = handler.Requests.Single(r => r.Method == HttpMethod.Put);
        putRequest.Content.Should().NotBeNull();
        var putBody = await putRequest.Content!.ReadAsStringAsync();
        using var putDoc = JsonDocument.Parse(putBody);
        putDoc.RootElement.GetProperty("branch").GetString().Should().Be("autofix/abc12345");
        var b64 = putDoc.RootElement.GetProperty("content").GetString()!;
        Encoding.UTF8.GetString(Convert.FromBase64String(b64)).Should().Be("new content");
    }

    [Fact]
    public async Task CreatePullRequestAsync_ExistingBranch_ShouldContinue()
    {
        var handler = new CapturingHandler(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (req.Method == HttpMethod.Get && path.EndsWith("/git/ref/heads/main"))
                return TestHelpers.JsonResponse(HttpStatusCode.OK, new { @object = new { sha = "base-sha" } });

            if (req.Method == HttpMethod.Post && path.EndsWith("/git/refs"))
                return TestHelpers.JsonResponse(HttpStatusCode.UnprocessableEntity, new { message = "Reference already exists" });

            if (req.Method == HttpMethod.Get && path.Contains("/contents/"))
                return new HttpResponseMessage(HttpStatusCode.NotFound);

            if (req.Method == HttpMethod.Put && path.Contains("/contents/"))
                return TestHelpers.JsonResponse(HttpStatusCode.Created, new { });

            if (req.Method == HttpMethod.Post && path.EndsWith("/pulls"))
                return TestHelpers.JsonResponse(HttpStatusCode.Created, new { html_url = "https://github.test/acme/app/pull/2" });

            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });

        using var http = new HttpClient(handler);
        using var client = new GitHubClient(
            new VcsConfig { Provider = "github", Token = "fake-token", ApiUrl = "https://api.github.test" },
            null,
            http);

        var request = new PullRequestRequest(
            RepoFullName: "acme/app",
            BaseBranch: "main",
            HeadBranch: "autofix/existing",
            Title: "Auto-fix",
            Description: "desc",
            Files: new[] { new FileChange("src/foo.cs", "fix") });

        var result = await client.CreatePullRequestAsync(request);

        result.Should().Be("https://github.test/acme/app/pull/2");
    }


}
