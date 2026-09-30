namespace CustomChallenge.Api.Exceptions;

public class DuplicateOrderItemsException(int productId)
    : Exception($"Order contains more than one line item for product '{productId}'.");
