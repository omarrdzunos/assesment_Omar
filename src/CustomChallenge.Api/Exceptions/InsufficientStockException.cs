namespace CustomChallenge.Api.Exceptions;

public class InsufficientStockException(int productId, int requested, int available)
    : Exception($"Product '{productId}' has insufficient stock. Requested: {requested}, available: {available}.");
