namespace Vls.Shopflow.IdentityAccess.Domain.Constants;

public static class StoreAccessErrorCodes
{
    public const string CustomerLoginRequired = "CUSTOMER_LOGIN_REQUIRED";
    public const string GuestCheckoutDisabled = "GUEST_CHECKOUT_DISABLED";
    public const string CustomerApprovalPending = "CUSTOMER_APPROVAL_PENDING";
    public const string CustomerAccessRejected = "CUSTOMER_ACCESS_REJECTED";
    public const string CustomerAccessSuspended = "CUSTOMER_ACCESS_SUSPENDED";
    public const string CustomerAccessNotApproved = "CUSTOMER_ACCESS_NOT_APPROVED";
    public const string StoreAccessRequiresLogin = "STORE_ACCESS_REQUIRES_LOGIN";
    public const string StoreAccessRequiresApproval = "STORE_ACCESS_REQUIRES_APPROVAL";
    public const string CustomerApprovalInvalidStatus = "CUSTOMER_APPROVAL_INVALID_STATUS";
    public const string CustomerApprovalReasonTooLong = "CUSTOMER_APPROVAL_REASON_TOO_LONG";
    public const string CustomerNotFound = "CUSTOMER_NOT_FOUND";

    /// <summary>Alias of <see cref="CustomerApprovalInvalidStatus"/>.</summary>
    public const string CustomerAccessInvalidTransition = CustomerApprovalInvalidStatus;
}

public static class StoreAccessMessages
{
    public const string LoginRequiredToBuy = "Para comprar, entre com uma conta aprovada.";
    public const string ApprovalPending = "Seu cadastro ainda está em análise.";
    public const string AccessRejected = "Seu cadastro não foi aprovado neste momento.";
    public const string AccessSuspended = "Seu acesso está temporariamente bloqueado.";
    public const string GuestCheckoutDisabled = "O checkout como convidado está desabilitado.";
    public const string StoreRequiresApprovedCustomer = "Esta loja está disponível apenas para clientes aprovados.";
    public const string InvalidApprovalStatus = "Não foi possível alterar o status deste cliente.";
    public const string ReasonTooLong = "O motivo não pode ter mais de 1000 caracteres.";
    public const string CustomerNotFound = "Cliente não encontrado.";
    public const string RegisterPending = "Cadastro enviado para aprovação.";
    public const string RegisterApproved = "Conta criada com sucesso.";
    public const string InvalidStatusFilter = "Filtro de status de aprovação inválido.";
}
