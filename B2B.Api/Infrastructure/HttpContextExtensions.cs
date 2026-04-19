namespace B2B.Api.Infrastructure;

public static class HttpContextExtensions
{
    public static string TraceId(this HttpContext ctx) => ctx.TraceIdentifier;
}

