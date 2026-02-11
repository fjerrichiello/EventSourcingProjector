namespace RequestSender;

public record TransactionRequest(string Type, decimal Amount, string? Description);