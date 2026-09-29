namespace CustomChallenge.Api.Exceptions;

public class OrderNotPendingException(int orderId)
    : Exception($"Order '{orderId}' can only be cancelled while it is Pending.");
