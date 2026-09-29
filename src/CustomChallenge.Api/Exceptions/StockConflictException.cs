namespace CustomChallenge.Api.Exceptions;

// Thrown when a concurrent request already changed a product's stock
// between our read and our SaveChanges (see PLAN.md — concurrency token).
public class StockConflictException(int productId)
    : Exception($"Stock for product '{productId}' changed concurrently; please retry the request.");
