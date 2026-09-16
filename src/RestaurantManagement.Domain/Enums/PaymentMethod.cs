namespace RestaurantManagement.Domain.Enums;

/// <summary>
/// Supported payment settlement methods for offline/local restaurant recording.
/// </summary>
public enum PaymentMethod
{
    Cash = 1,
    Card = 2,
    UPI = 3,
    CreditCard = 4,
    DebitCard = 5,
    DigitalWallet = 6,
    BankTransfer = 7,
    Other = 8
}
