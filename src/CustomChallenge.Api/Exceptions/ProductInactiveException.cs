namespace CustomChallenge.Api.Exceptions;

public class ProductInactiveException(int productId)
    : Exception($"Product with id '{productId}' is not active.");
