using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;

namespace PropertyManagement.Tests.Web;

/// <summary>No-op antiforgery so POST controller tests exercise business guards, not tokens.</summary>
public sealed class TestAntiforgery : IAntiforgery
{
    public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) =>
        new("test", "test", "test", "test");

    public AntiforgeryTokenSet GetTokens(HttpContext httpContext) =>
        new("test", "test", "test", "test");

    public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(true);

    public void SetCookieTokenAndHeader(HttpContext httpContext)
    {
    }

    public Task ValidateRequestAsync(HttpContext httpContext) => Task.CompletedTask;
}
