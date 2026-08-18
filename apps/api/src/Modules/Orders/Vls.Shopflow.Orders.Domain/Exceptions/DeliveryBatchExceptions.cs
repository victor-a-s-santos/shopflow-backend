using Vls.Shopflow.Orders.Domain.Constants;

namespace Vls.Shopflow.Orders.Domain.Exceptions;

public class DeliveryBatchException : Exception
{
    public string Code { get; }

    public DeliveryBatchException(string code, string message)
        : base(message)
        => Code = code;
}

public sealed class DeliveryBatchNotFoundException : DeliveryBatchException
{
    public Guid BatchId { get; }

    public DeliveryBatchNotFoundException(Guid batchId)
        : base(DeliveryBatchErrorCodes.NotFound, $"Delivery batch {batchId} was not found.")
        => BatchId = batchId;
}

public sealed class DeliveryBatchAddressMismatchException : DeliveryBatchException
{
    public IReadOnlyList<object> AddressSummaries { get; }

    public DeliveryBatchAddressMismatchException(IReadOnlyList<object> addressSummaries)
        : base(
            DeliveryBatchErrorCodes.AddressMismatch,
            "Os pedidos selecionados possuem endereços diferentes. Confirme antes de agrupar.")
        => AddressSummaries = addressSummaries;
}
