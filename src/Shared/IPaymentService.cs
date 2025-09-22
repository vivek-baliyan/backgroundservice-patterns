namespace BackgroundServicePatterns.Shared;

/// <summary>
/// Shared interface for payment processing examples.
/// In a real application, replace this with your actual business service interfaces.
/// </summary>
public interface IPaymentService
{
    Task<List<Payment>> GetPendingPaymentsAsync(CancellationToken cancellationToken = default);
    Task<List<Payment>> GetPendingPaymentsAsync(int maxCount, CancellationToken cancellationToken = default);
    Task ProcessPaymentAsync(int paymentId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Shared model for payment examples.
/// In a real application, replace this with your actual domain models.
/// </summary>
public class Payment
{
    public int Id { get; set; }
    public required string Status { get; set; } = "";
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Example scoped service interface that would cause memory leaks if captured.
/// Used in scope management examples.
/// </summary>
public interface IPaymentDbContext
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}