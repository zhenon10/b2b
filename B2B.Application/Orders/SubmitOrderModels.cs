using B2B.Contracts;
using B2B.Domain.Enums;

namespace B2B.Application.Orders;

public sealed record SubmitOrderCommand(
    Guid BuyerUserId,
    SubmitOrderRequest Request,
    string? IdempotencyKey,
    string TraceId
);

public sealed record SubmitOrderResult(
    int HttpStatusCode,
    ApiResponse<SubmitOrderResponse> Response
);

public sealed record UpdateOrderStatusCommand(
    Guid OrderId,
    OrderStatus Status,
    string TraceId
);

public sealed record UpdateOrderStatusResult(
    int HttpStatusCode,
    ApiResponse<object> Response
);

public sealed record CancelOrderCommand(
    Guid BuyerUserId,
    Guid OrderId,
    string TraceId
);

public sealed record CancelOrderResult(
    int HttpStatusCode,
    ApiResponse<object> Response
);

