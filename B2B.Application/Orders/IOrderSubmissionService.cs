namespace B2B.Application.Orders;

public interface IOrderSubmissionService
{
    Task<SubmitOrderResult> SubmitAsync(SubmitOrderCommand cmd, CancellationToken ct);

    Task<UpdateOrderStatusResult> UpdateStatusAsync(UpdateOrderStatusCommand cmd, CancellationToken ct);

    Task<CancelOrderResult> CancelAsync(CancelOrderCommand cmd, CancellationToken ct);
}

