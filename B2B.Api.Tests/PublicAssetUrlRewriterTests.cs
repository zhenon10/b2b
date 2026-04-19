using B2B.Api.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace B2B.Api.Tests;

public sealed class PublicAssetUrlRewriterTests
{
    private static HttpRequest Request(string scheme, string host)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = scheme;
        ctx.Request.Host = new HostString(host);
        return ctx.Request;
    }

    private static HttpRequest RequestWithOptions(string scheme, string host, PublicAssetUrlOptions options)
    {
        var services = new ServiceCollection();
        services.AddSingleton(Options.Create(options));
        var ctx = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        ctx.Request.Scheme = scheme;
        ctx.Request.Host = new HostString(host);
        return ctx.Request;
    }

    [Fact]
    public void Relative_uploads_path_becomes_absolute_with_request_host()
    {
        var req = Request("http", "192.168.2.115:5237");
        PublicAssetUrlRewriter.RewriteForRequest("/uploads/products/a.png", req)
            .Should().Be("http://192.168.2.115:5237/uploads/products/a.png");
    }

    [Theory]
    [InlineData("http://localhost:5237/uploads/products/x.png")]
    [InlineData("http://127.0.0.1:5237/uploads/products/x.png")]
    [InlineData("http://[::1]:5237/uploads/products/x.png")]
    public void Loopback_absolute_uploads_url_uses_request_host(string stored)
    {
        var req = Request("http", "192.168.2.115:5237");
        PublicAssetUrlRewriter.RewriteForRequest(stored, req)
            .Should().Be("http://192.168.2.115:5237/uploads/products/x.png");
    }

    [Fact]
    public void Non_uploads_absolute_url_is_unchanged()
    {
        var req = Request("http", "192.168.2.115:5237");
        PublicAssetUrlRewriter.RewriteForRequest("https://cdn.example.com/logo.png", req)
            .Should().Be("https://cdn.example.com/logo.png");
    }

    [Fact]
    public void Private_lan_uploads_host_is_unchanged_when_rewrite_disabled()
    {
        var req = Request("http", "192.168.2.115:5237");
        PublicAssetUrlRewriter.RewriteForRequest("http://10.0.0.5:5237/uploads/products/x.png", req)
            .Should().Be("http://10.0.0.5:5237/uploads/products/x.png");
    }

    [Fact]
    public void Private_lan_uploads_rewritten_when_option_enabled()
    {
        var req = RequestWithOptions("http", "192.168.2.115:5237",
            new PublicAssetUrlOptions { RewritePrivateLanUploadUrls = true });
        PublicAssetUrlRewriter.RewriteForRequest("http://192.168.1.50:5237/uploads/products/x.png", req)
            .Should().Be("http://192.168.2.115:5237/uploads/products/x.png");
    }

    [Fact]
    public void Non_private_host_uploads_unchanged_even_when_lan_rewrite_enabled()
    {
        var req = RequestWithOptions("http", "192.168.2.115:5237",
            new PublicAssetUrlOptions { RewritePrivateLanUploadUrls = true });
        PublicAssetUrlRewriter.RewriteForRequest("https://cdn.example.com/uploads/x.png", req)
            .Should().Be("https://cdn.example.com/uploads/x.png");
    }

    [Fact]
    public void Null_returns_null()
    {
        var req = Request("http", "localhost:5237");
        PublicAssetUrlRewriter.RewriteForRequest(null, req).Should().BeNull();
    }
}
