// See https://aka.ms/new-console-template for more information

using Npgsql;

var connectionString =
    "Host=localhost;Port=5432;Database=eventstore;Username=postgres;Password=postgres";

var tablesToClear = new[]
{
    "balance_events_log",
    "event_store",
    "projected_state"
};

await using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync();

// Optional but recommended
await using var transaction = await connection.BeginTransactionAsync();

try
{
    var truncateSql = $"""
                       TRUNCATE TABLE {string.Join(", ", tablesToClear)}
                       RESTART IDENTITY
                       CASCADE;
                       """;

    await using var cmd = new NpgsqlCommand(truncateSql, connection, transaction);
    await cmd.ExecuteNonQueryAsync();

    await transaction.CommitAsync();
    Console.WriteLine("Tables cleared and identities reset.");
}
catch (Exception ex)
{
    await transaction.RollbackAsync();
    Console.Error.WriteLine("Cleanup failed:");
    Console.Error.WriteLine(ex);
}