namespace RequestApi.Models;

public record CreateTransactionRequest(string Type, decimal Amount, string? Description);
