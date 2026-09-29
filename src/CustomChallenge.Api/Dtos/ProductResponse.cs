namespace CustomChallenge.Api.Dtos;

public record ProductResponse(int Id, string Name, decimal Price, int Stock, bool IsActive);
