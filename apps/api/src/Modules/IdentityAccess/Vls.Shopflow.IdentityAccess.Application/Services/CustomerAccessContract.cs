using Vls.Shopflow.IdentityAccess.Domain.Enums;

namespace Vls.Shopflow.IdentityAccess.Application.Services;

public static class CustomerAccessContract
{
    public const int MaxDecisionReasonLength = 1000;
    public const string PublicPendingStatus = "Pending";
    public const string PublicClosedMode = "Closed";
    public const string PublicOpenMode = "Open";
    public const string RegisterPendingMessage = "Cadastro enviado para aprovação.";
    public const string RegisterApprovedMessage = "Conta criada com sucesso.";

    public static string ToPublicApprovalStatus(CustomerAccessStatus status)
        => status == CustomerAccessStatus.PendingApproval ? PublicPendingStatus : status.ToString();

    public static string ToPublicMode(StoreAccessMode mode)
        => mode == StoreAccessMode.PrivateCatalogApprovedOnly ? PublicClosedMode : PublicOpenMode;
}
