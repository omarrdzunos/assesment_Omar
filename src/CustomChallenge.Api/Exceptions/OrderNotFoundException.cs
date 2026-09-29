namespace CustomChallenge.Api.Exceptions;

public class OrderNotFoundException(int orderId)
    : Exception($"Order with id '{orderId}' was not found.");
