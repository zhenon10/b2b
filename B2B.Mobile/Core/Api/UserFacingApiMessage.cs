using B2B.Contracts;

namespace B2B.Mobile.Core.Api;

public static class UserFacingApiMessage
{
    public const string TraceCaption = "Destek kodu";

    /// <summary>Yalnızca kullanıcıya gösterilecek metin (TraceId ayrı özellikte).</summary>
    public static string Message(ApiError? error, string fallback) =>
        string.IsNullOrWhiteSpace(error?.Message) ? fallback : error!.Message;

    /// <summary>Tek satırda gösterim: mesaj + izlenebilirlik (API <c>TraceId</c>).</summary>
    public static string WithTrace(string message, string traceId)
    {
        if (string.IsNullOrWhiteSpace(traceId))
            return message;
        return $"{message}\n\n{TraceCaption}: {traceId}";
    }

    /// <summary><see cref="Message"/> ile aynı; eski imza uyumluluğu.</summary>
    public static string Format(ApiError? error, string traceId, string fallback) =>
        Message(error, fallback);
}
