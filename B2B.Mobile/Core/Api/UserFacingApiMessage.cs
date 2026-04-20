using B2B.Contracts;

namespace B2B.Mobile.Core.Api;

public static class UserFacingApiMessage
{
    public const string TraceCaption = "Destek kodu";

    /// <summary>Yalnızca kullanıcıya gösterilecek metin (TraceId ayrı özellikte).</summary>
    public static string Message(ApiError? error, string fallback)
    {
        if (error is null) return fallback;

        // Common transport / gateway layer errors
        switch (error.Code)
        {
            case "timeout":
                return "Bağlantı zaman aşımına uğradı. Ağı kontrol edip tekrar deneyin.";
            case "network_error":
                return "API’ye ulaşılamadı. İnternet bağlantınızı kontrol edip tekrar deneyin.";
            case "empty_response":
            case "invalid_response":
                return "Sunucu yanıtı alınamadı veya okunamadı. Bir süre sonra tekrar deneyin.";
            case "server_error":
                return "Sunucu geçici bir hata verdi. Biraz sonra tekrar deneyin.";
            case "unauthorized":
                return "Oturum süresi dolmuş olabilir. Yeniden giriş yapın.";
            case "forbidden":
                return "Bu işlem için yetkiniz yok.";
        }

        return string.IsNullOrWhiteSpace(error.Message) ? fallback : error.Message;
    }

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
