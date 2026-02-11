namespace ProjectorService.Services;

public class SequenceGapException : Exception
{
    public SequenceGapException(string accountId, long expected, long received)
        : base($"Sequence gap for account {accountId}: expected {expected}, received {received}")
    {
    }
}
