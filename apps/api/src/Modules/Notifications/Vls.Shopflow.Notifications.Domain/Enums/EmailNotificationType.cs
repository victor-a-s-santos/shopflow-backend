namespace Vls.Shopflow.Notifications.Domain.Enums;

public enum EmailNotificationType
{
    ConfirmEmail = 1,
    ResetPassword = 2,
    OrderCreated = 3,
    PaymentConfirmed = 4,
    OrderShipped = 5,
    OrderDelivered = 6,
    CustomerApprovalRequestAdmin = 7,
    CustomerRegistrationReceived = 8,
    CustomerApproved = 9,
    CustomerRejected = 10,
    CustomerSuspended = 11
}
