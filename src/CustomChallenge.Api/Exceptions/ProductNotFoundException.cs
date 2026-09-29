namespace CustomChallenge.Api.Exceptions;

public class ProductNotFoundException(int productId)
    : Exception($"Product with id '{productId}' was not found.");
