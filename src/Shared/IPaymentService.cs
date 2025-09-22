namespace BackgroundServicePatterns.Shared;

/// <summary>
/// Shared interface for payment processing examples.
/// In a real application, replace this with your actual business service interfaces.
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// Gets all pending payments that need to be processed.
    /// </summary>
    Task<List<Payment>> GetPendingPaymentsAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Gets a limited number of pending payments that need to be processed.
    /// </summary>
    Task<List<Payment>> GetPendingPaymentsAsync(int maxCount, CancellationToken cancellationToken = default);
    /// <summary>
    /// Processes a specific payment by its ID.
    /// </summary>
    Task ProcessPaymentAsync(int paymentId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Shared model for payment examples.
/// In a real application, replace this with your actual domain models.
/// </summary>
public class Payment
{
    /// <summary>
    /// Gets or sets the unique identifier for the payment.
    /// </summary>
    public int Id { get; set; }
    /// <summary>
    /// Gets or sets the current status of the payment.
    /// </summary>
    public required string Status { get; set; } = "";
    /// <summary>
    /// Gets or sets the payment amount.
    /// </summary>
    public decimal Amount { get; set; }
    /// <summary>
    /// Gets or sets when the payment was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Example scoped service interface that would cause memory leaks if captured.
/// Used in scope management examples.
/// </summary>
public interface IPaymentDbContext
{
    /// <summary>
    /// Saves all pending changes to the database.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}