using DbMigrator;
using Microsoft.EntityFrameworkCore;

var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__EventStore")
    ?? "Host=localhost;Database=eventstore;Username=postgres;Password=postgres";

Console.WriteLine($"Running migrations against: {connectionString}");

var optionsBuilder = new DbContextOptionsBuilder<MigrationDbContext>();
optionsBuilder.UseNpgsql(connectionString);

await using var context = new MigrationDbContext(optionsBuilder.Options);
await context.Database.MigrateAsync();

Console.WriteLine("Migrations completed successfully.");
