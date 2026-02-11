# Event Sourcing Checking Account System

## Overview

This project implements an event sourcing pattern for a checking account system (credits/debits) using .NET 8 minimal APIs, PostgreSQL, and Kafka. The core problem being solved is: we need to notify downstream systems of calculated account balances after transactions, but calculating on the write path causes race conditions under high concurrency. Event sourcing with an async projector solves this by separating the write (append-only) from the calculation (sequential per account).

## Architecture

Four separate .NET 8 minimal API services, orchestrated via Docker Compose alongside PostgreSQL and Kafka (KRaft mode, no Zookeeper).

```
┌──────────────────┐    account-transactions (Kafka)
│   Requests API   │────────────────────────────────┐
│   (passthrough)  │                                 │
└──────────────────┘                                 ▼
                                            ┌──────────────────┐        ┌─────────────────────┐
                                            │   Transaction    │───────►│    PostgreSQL        │
                                            │   Service        │        │  - event_store       │
                                            └────────┬─────────┘        └─────────▲────────────┘
                                                     │                            │
                                          event-stored (Kafka)                    │
                                                     │                            │
                                                     ▼                            │
                                            ┌──────────────────┐                  │
                                            │    Projector     │──────────────────┘
                                            │    Service       │  reads events, updates
                                            └────────┬─────────┘  projected_state
                                                     │
                                          account-balance-events (Kafka)
                                                     ▼
                                            ┌──────────────────┐
                                            │   Notification   │
                                            │   Consumer API   │
                                            └──────────────────┘
```

### Flow

1. **Requests API** receives an HTTP request (deposit or debit), validates the basic shape, and publishes a raw event to the `account-transactions` Kafka topic. It is purely a passthrough — no database interaction. Returns 202 Accepted.
2. **Transaction Service** consumes from `account-transactions` in bulk (thousands of events concurrently). It appends events to the `event_store` table in PostgreSQL (append-only inserts, no reads, no calculations). After successfully writing to the event store, it publishes to the `event-stored` Kafka topic. This is safe at high concurrency because it's only doing inserts.
3. **Projector Service** consumes from `event-stored`, partitioned by `account_id`. It processes events **one at a time per account** (Kafka partition ordering guarantees this). For each event it: reads the current projected balance, calculates the new balance, updates the `projected_state` table, and publishes an enriched event (with `balance_before` and `balance_after`) to the `account-balance-events` Kafka topic. Steps 2-4 happen in a single database transaction.
4. **Notification Consumer** consumes from `account-balance-events`. This represents downstream systems. For this demo, it should log the enriched events and expose a simple GET endpoint to see recent events for an account.

### Why This Separation Matters

- The Transaction Service can handle thousands of concurrent writes because it's append-only — no read-calculate-write cycle, no contention.
- The Projector processes events sequentially per account (via Kafka partition key on `account_id`), eliminating race conditions on balance calculation.
- The Projector can still process thousands of events concurrently across *different* accounts — just not for the same account simultaneously.
- If the Projector falls behind, no data is lost. It tracks `last_processed_sequence` and resumes from where it left off.

## Technology Stack

- .NET 8 minimal APIs
- PostgreSQL 16 with EF Core (Npgsql provider)
- Confluent.Kafka for .NET (producer/consumer)
- Docker Compose for orchestration
- Kafka in KRaft mode (no Zookeeper)

## Solution Structure

Each service follows **onion architecture** with four layers. Dependencies point inward: Presentation → Application → Domain ← Infrastructure. The Domain layer has zero external dependencies. Infrastructure implements interfaces defined in Domain/Application.

```
event-sourcing-demo/
├── docker-compose.yml
├── docker/
│   └── init.sql                        # PostgreSQL schema initialization
├── src/
│   ├── Shared/                         # Shared class library — cross-cutting contracts only
│   │   └── Shared.csproj
│   │   ├── Events/                     # Kafka message contracts (DTOs only, no logic)
│   │   │   ├── TransactionRequested.cs
│   │   │   ├── EventStored.cs
│   │   │   └── BalanceChanged.cs
│   │   └── Kafka/                      # Reusable Kafka producer/consumer abstractions
│   │       ├── IKafkaProducer.cs       # Interface
│   │       ├── KafkaProducerService.cs # Implementation
│   │       └── KafkaConsumerBase.cs    # Abstract BackgroundService base
│   │
│   ├── RequestsApi/
│   │   ├── RequestsApi.Api/            # Presentation — minimal API endpoints, Program.cs, DI registration
│   │   │   ├── Endpoints/
│   │   │   │   └── TransactionEndpoints.cs
│   │   │   ├── Program.cs
│   │   │   ├── Dockerfile
│   │   │   └── RequestsApi.Api.csproj
│   │   ├── RequestsApi.Application/    # Application — use cases, validation, DTOs
│   │   │   ├── DTOs/
│   │   │   │   ├── CreateTransactionRequest.cs
│   │   │   │   └── CreateTransactionResponse.cs
│   │   │   ├── Interfaces/
│   │   │   │   └── ITransactionPublisher.cs
│   │   │   ├── Validators/
│   │   │   │   └── CreateTransactionValidator.cs
│   │   │   ├── Services/
│   │   │   │   └── TransactionRequestService.cs
│   │   │   └── RequestsApi.Application.csproj
│   │   ├── RequestsApi.Domain/         # Domain — entities, value objects (thin for this service)
│   │   │   ├── Enums/
│   │   │   │   └── TransactionType.cs
│   │   │   └── RequestsApi.Domain.csproj
│   │   └── RequestsApi.Infrastructure/ # Infrastructure — Kafka producer implementation
│   │       ├── Messaging/
│   │       │   └── KafkaTransactionPublisher.cs  # Implements ITransactionPublisher
│   │       ├── DependencyInjection.cs
│   │       └── RequestsApi.Infrastructure.csproj
│   │
│   ├── TransactionService/
│   │   ├── TransactionService.Api/            # Presentation — Program.cs, health endpoint, hosts the consumer
│   │   │   ├── Consumers/
│   │   │   │   └── TransactionRequestedConsumer.cs  # BackgroundService, delegates to Application layer
│   │   │   ├── Program.cs
│   │   │   ├── Dockerfile
│   │   │   └── TransactionService.Api.csproj
│   │   ├── TransactionService.Application/    # Application — orchestration logic
│   │   │   ├── Interfaces/
│   │   │   │   ├── IEventStoreRepository.cs
│   │   │   │   └── IEventStoredPublisher.cs
│   │   │   ├── Services/
│   │   │   │   └── TransactionStorageService.cs    # Receives event, calls repo to persist, calls publisher
│   │   │   └── TransactionService.Application.csproj
│   │   ├── TransactionService.Domain/         # Domain — EventStoreEntry entity, sequence logic
│   │   │   ├── Entities/
│   │   │   │   └── EventStoreEntry.cs
│   │   │   └── TransactionService.Domain.csproj
│   │   └── TransactionService.Infrastructure/ # Infrastructure — EF Core repo, Kafka publisher
│   │       ├── Persistence/
│   │       │   ├── EventStoreDbContext.cs
│   │       │   └── EventStoreRepository.cs         # Implements IEventStoreRepository
│   │       ├── Messaging/
│   │       │   └── KafkaEventStoredPublisher.cs     # Implements IEventStoredPublisher
│   │       ├── DependencyInjection.cs
│   │       └── TransactionService.Infrastructure.csproj
│   │
│   ├── ProjectorService/
│   │   ├── ProjectorService.Api/              # Presentation — endpoints for balance/events, hosts consumer
│   │   │   ├── Consumers/
│   │   │   │   └── EventStoredConsumer.cs           # BackgroundService, delegates to Application layer
│   │   │   ├── Endpoints/
│   │   │   │   └── AccountEndpoints.cs              # GET balance, GET events
│   │   │   ├── Program.cs
│   │   │   ├── Dockerfile
│   │   │   └── ProjectorService.Api.csproj
│   │   ├── ProjectorService.Application/      # Application — projection logic, queries
│   │   │   ├── Interfaces/
│   │   │   │   ├── IProjectedStateRepository.cs
│   │   │   │   ├── IEventStoreReadRepository.cs
│   │   │   │   └── IBalanceChangedPublisher.cs
│   │   │   ├── Services/
│   │   │   │   ├── ProjectionService.cs             # Core projection: read state, calculate, update, publish
│   │   │   │   └── AccountQueryService.cs           # Serves GET balance / GET events
│   │   │   └── ProjectorService.Application.csproj
│   │   ├── ProjectorService.Domain/           # Domain — ProjectedState entity, balance calculation logic
│   │   │   ├── Entities/
│   │   │   │   └── ProjectedState.cs
│   │   │   ├── Services/
│   │   │   │   └── BalanceCalculator.cs             # Pure domain logic: given current balance + event → new balance
│   │   │   └── ProjectorService.Domain.csproj
│   │   └── ProjectorService.Infrastructure/   # Infrastructure — EF Core repos, Kafka publisher
│   │       ├── Persistence/
│   │       │   ├── ProjectorDbContext.cs
│   │       │   ├── ProjectedStateRepository.cs      # Implements IProjectedStateRepository
│   │       │   └── EventStoreReadRepository.cs      # Implements IEventStoreReadRepository (read-only)
│   │       ├── Messaging/
│   │       │   └── KafkaBalanceChangedPublisher.cs   # Implements IBalanceChangedPublisher
│   │       ├── DependencyInjection.cs
│   │       └── ProjectorService.Infrastructure.csproj
│   │
│   └── NotificationConsumer/
│       ├── NotificationConsumer.Api/          # Presentation — endpoints, hosts consumer
│       │   ├── Consumers/
│       │   │   └── BalanceChangedConsumer.cs         # BackgroundService, delegates to Application layer
│       │   ├── Endpoints/
│       │   │   └── NotificationEndpoints.cs         # GET notifications for account
│       │   ├── Program.cs
│       │   ├── Dockerfile
│       │   └── NotificationConsumer.Api.csproj
│       ├── NotificationConsumer.Application/  # Application — logging, storage orchestration
│       │   ├── Interfaces/
│       │   │   └── IBalanceEventLogRepository.cs
│       │   ├── Services/
│       │   │   ├── NotificationProcessingService.cs # Logs event, persists to balance_events_log
│       │   │   └── NotificationQueryService.cs      # Serves GET notifications
│       │   └── NotificationConsumer.Application.csproj
│       ├── NotificationConsumer.Domain/       # Domain — BalanceEventLog entity
│       │   ├── Entities/
│       │   │   └── BalanceEventLog.cs
│       │   └── NotificationConsumer.Domain.csproj
│       └── NotificationConsumer.Infrastructure/ # Infrastructure — EF Core repo
│           ├── Persistence/
│           │   ├── NotificationDbContext.cs
│           │   └── BalanceEventLogRepository.cs      # Implements IBalanceEventLogRepository
│           ├── DependencyInjection.cs
│           └── NotificationConsumer.Infrastructure.csproj
│
└── EventSourcingDemo.sln
```

### Onion Architecture Rules

These rules MUST be followed strictly for every service:

1. **Domain** — innermost layer, zero project references, zero NuGet packages (except primitives). Contains entities, value objects, enums, and pure domain logic. No `using` statements for EF Core, Kafka, or any infrastructure concern. Domain entities should NOT have EF Core data annotations — mapping is done in Infrastructure via `IEntityTypeConfiguration<T>`.

2. **Application** — references only Domain. Contains use case orchestration (services), DTOs, validators, and interface definitions for anything the use case needs from the outside world (repositories, publishers). These interfaces are the "ports" that Infrastructure will implement. Application services are where business workflows live. NuGet packages allowed: FluentValidation or similar, but NOT EF Core, NOT Kafka.

3. **Infrastructure** — references Application and Domain. Implements the interfaces defined in Application (repositories, publishers, external service clients). This is where EF Core `DbContext`, Kafka producers, and any external I/O lives. Contains a `DependencyInjection.cs` with an `AddInfrastructure(this IServiceCollection services, IConfiguration config)` extension method that registers all implementations.

4. **Api / Presentation** — references Application and Infrastructure. Contains `Program.cs`, endpoint definitions, hosted services (Kafka consumers), middleware, and DI composition root. Kafka `BackgroundService` consumers live here but they should be thin — deserialize the message and call an Application layer service. All endpoint handlers should be thin — call an Application service and map the result.

### Project Reference Rules

```
Api → Application, Infrastructure
Infrastructure → Application, Domain
Application → Domain
Domain → (nothing)

All services → Shared (for event contracts and Kafka abstractions)
```

### Dependency Injection Pattern

Each Infrastructure project exposes a single extension method:

```csharp
// In TransactionService.Infrastructure/DependencyInjection.cs
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<EventStoreDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("EventStore")));

        services.AddScoped<IEventStoreRepository, EventStoreRepository>();
        services.AddSingleton<IEventStoredPublisher, KafkaEventStoredPublisher>();

        return services;
    }
}
```

Called from `Program.cs`:

```csharp
builder.Services.AddInfrastructure(builder.Configuration);
```

### Where Things Live — Quick Reference

| Concern | Layer | Example |
|---|---|---|
| Minimal API endpoint definitions | Api/Presentation | `TransactionEndpoints.cs` |
| `Program.cs`, DI composition | Api/Presentation | `Program.cs` |
| Kafka `BackgroundService` consumers | Api/Presentation | `TransactionRequestedConsumer.cs` |
| Dockerfiles | Api/Presentation | `Dockerfile` |
| Use case orchestration | Application | `TransactionStorageService.cs` |
| Interface definitions (ports) | Application | `IEventStoreRepository.cs` |
| Request/Response DTOs | Application | `CreateTransactionRequest.cs` |
| Input validation | Application | `CreateTransactionValidator.cs` |
| Entities, value objects | Domain | `EventStoreEntry.cs` |
| Pure business logic | Domain | `BalanceCalculator.cs` |
| Enums | Domain | `TransactionType.cs` |
| EF Core DbContext | Infrastructure | `EventStoreDbContext.cs` |
| Repository implementations | Infrastructure | `EventStoreRepository.cs` |
| Kafka producer implementations | Infrastructure | `KafkaEventStoredPublisher.cs` |
| EF Core entity configuration | Infrastructure | via `OnModelCreating` or `IEntityTypeConfiguration<T>` |
| DI registration for infra | Infrastructure | `DependencyInjection.cs` |

## Database Schema

Create in `docker/init.sql`:

```sql
CREATE TABLE event_store (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id VARCHAR(50) NOT NULL,
    sequence_num BIGINT NOT NULL,
    event_type VARCHAR(50) NOT NULL,       -- 'deposit', 'debit', 'account_opened'
    amount DECIMAL(18, 2) NOT NULL,
    description VARCHAR(500),
    correlation_id UUID NOT NULL,          -- ties back to the original request
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (account_id, sequence_num)
);

CREATE INDEX idx_event_store_account_id ON event_store (account_id, sequence_num);

CREATE TABLE projected_state (
    account_id VARCHAR(50) PRIMARY KEY,
    current_balance DECIMAL(18, 2) NOT NULL DEFAULT 0,
    last_processed_sequence BIGINT NOT NULL DEFAULT 0,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Optional: for the notification consumer to store/query recent enriched events
CREATE TABLE balance_events_log (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id VARCHAR(50) NOT NULL,
    event_type VARCHAR(50) NOT NULL,
    amount DECIMAL(18, 2) NOT NULL,
    balance_before DECIMAL(18, 2) NOT NULL,
    balance_after DECIMAL(18, 2) NOT NULL,
    correlation_id UUID NOT NULL,
    received_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_balance_events_log_account ON balance_events_log (account_id, received_at DESC);
```

## Kafka Topics

Three topics, all partitioned by `account_id` as the key:

| Topic | Key | Producer | Consumer | Purpose |
|---|---|---|---|---|
| `account-transactions` | `account_id` | Requests API | Transaction Service | Raw transaction requests |
| `event-stored` | `account_id` | Transaction Service | Projector Service | Notification that event is persisted |
| `account-balance-events` | `account_id` | Projector Service | Notification Consumer | Enriched events with calculated balance |

Create topics with sufficient partitions for parallelism (e.g., 12 partitions each). Partitioning by `account_id` ensures all events for a given account land on the same partition, which gives the Projector sequential processing per account.

## Shared Event Contracts

### TransactionRequested (Requests API → Kafka)

```csharp
public record TransactionRequested
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
    public required string AccountId { get; init; }
    public required string EventType { get; init; }  // "deposit" or "debit"
    public required decimal Amount { get; init; }
    public string? Description { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
```

### EventStored (Transaction Service → Kafka)

```csharp
public record EventStored
{
    public required Guid EventId { get; init; }
    public required string AccountId { get; init; }
    public required long SequenceNum { get; init; }
    public required string EventType { get; init; }
    public required decimal Amount { get; init; }
    public string? Description { get; init; }
    public required Guid CorrelationId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
```

### BalanceChanged (Projector → Kafka)

```csharp
public record BalanceChanged
{
    public required Guid EventId { get; init; }
    public required string AccountId { get; init; }
    public required long SequenceNum { get; init; }
    public required string EventType { get; init; }
    public required decimal Amount { get; init; }
    public required decimal BalanceBefore { get; init; }
    public required decimal BalanceAfter { get; init; }
    public required Guid CorrelationId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
}
```

## Service Details

### 1. Requests API (Port 5001)

**NuGet packages:** `Confluent.Kafka`, `System.Text.Json`

**Endpoints:**

- `POST /api/accounts/{accountId}/transactions` — accepts `{ "type": "deposit|debit", "amount": 100.00, "description": "ATM deposit" }`, validates, publishes `TransactionRequested` to `account-transactions` topic with `accountId` as the Kafka message key. Returns 202 Accepted with the correlation ID.
- `GET /health` — basic health check

**Key behavior:**
- No database connection at all
- Validate that `type` is "deposit" or "debit", `amount` is positive, `accountId` is not empty
- Serialize events as JSON
- Use `accountId` as the Kafka partition key so all events for the same account go to the same partition

### 2. Transaction Service (Port 5002)

**NuGet packages:** `Confluent.Kafka`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `System.Text.Json`

**Consumer:** `BackgroundService` / `IHostedService` that consumes from `account-transactions` topic.

**Behavior:**
- Consumes events (can process in batches for throughput)
- For each event, assigns the next `sequence_num` for that account. Use a SQL approach: `SELECT COALESCE(MAX(sequence_num), 0) + 1 FROM event_store WHERE account_id = @accountId` within a transaction, or use a database sequence per account. The simpler approach is fine since the unique constraint on `(account_id, sequence_num)` will catch any conflicts.
- Inserts into `event_store`
- On successful insert, publishes `EventStored` to `event-stored` topic with `accountId` as key
- If the insert fails due to a sequence conflict, retry with a new sequence number

**Important:** The sequence number assignment and insert should be in a serializable transaction or use an upsert pattern to handle any edge cases. However, because all events for an account arrive on the same Kafka partition and this consumer processes them in order per partition, conflicts should be rare.

**Endpoints:**
- `GET /health` — health check

### 3. Projector Service (Port 5003)

**NuGet packages:** `Confluent.Kafka`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `System.Text.Json`

**Consumer:** `BackgroundService` that consumes from `event-stored` topic.

**This is the critical service.** It MUST process events sequentially per account. Kafka partition ordering handles this — as long as each consumer instance processes one partition's messages in order.

**Behavior for each consumed event:**
1. Read current `projected_state` for the account (if none exists, balance is 0, last_processed_sequence is 0)
2. Verify the event's `sequence_num` is `last_processed_sequence + 1`. If it's already processed (sequence <= last_processed), skip it (idempotency). If there's a gap, log a warning — events may be arriving out of order or one was lost.
3. Calculate: `new_balance = current_balance + amount` (where debit amounts are negative)
4. In a single database transaction:
    - Upsert `projected_state` with new balance and new `last_processed_sequence`
5. After the transaction commits, publish `BalanceChanged` to `account-balance-events` with the calculated `balance_before` and `balance_after`

**Endpoints:**
- `GET /api/accounts/{accountId}/balance` — reads from `projected_state`, returns current balance
- `GET /api/accounts/{accountId}/events` — reads from `event_store`, returns transaction history
- `GET /health` — health check

### 4. Notification Consumer (Port 5004)

**NuGet packages:** `Confluent.Kafka`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `System.Text.Json`

**Consumer:** `BackgroundService` that consumes from `account-balance-events` topic.

**Behavior:**
- Logs each enriched event (simulating downstream notification)
- Stores in `balance_events_log` table for querying

**Endpoints:**
- `GET /api/accounts/{accountId}/notifications` — returns recent balance change events for an account from `balance_events_log`
- `GET /health` — health check

## Shared Project Scope

The Shared project contains ONLY cross-cutting contracts and reusable infrastructure abstractions. It does NOT contain domain entities, business logic, or application services.

**What belongs in Shared:**
- Event contract records (`TransactionRequested`, `EventStored`, `BalanceChanged`) — these are DTOs/messages, not domain entities
- `IKafkaProducer<T>` interface
- `KafkaProducerService<T>` generic implementation (wraps `IProducer<string, string>`, serializes to JSON, uses provided key)
- `KafkaConsumerBase<T>` abstract `BackgroundService` (subscribes to topic, deserializes, calls abstract `ProcessAsync`, handles offset commits)

**What does NOT belong in Shared:**
- Domain entities (these live in each service's Domain layer)
- Repository interfaces (these live in each service's Application layer)
- DbContexts (these live in each service's Infrastructure layer)
- Service-specific publisher interfaces like `IEventStoredPublisher` (these live in the relevant Application layer, and their Kafka implementations live in Infrastructure)

### KafkaProducerService

A generic thin wrapper around `IProducer<string, string>` in Shared:
- Takes a topic name, a key (account ID), and a message object
- Serializes the value to JSON
- Uses the provided key for partitioning
- Handles delivery reports / errors

Each service's Infrastructure layer wraps this generic producer behind a service-specific interface. For example, `KafkaEventStoredPublisher` in TransactionService.Infrastructure implements `IEventStoredPublisher` from TransactionService.Application, and internally uses `KafkaProducerService<EventStored>` from Shared.

### KafkaConsumerBase

An abstract `BackgroundService` in Shared that:
- Subscribes to a topic
- Loops consuming messages, deserializes from JSON
- Calls an abstract `ProcessAsync(T message, CancellationToken ct)` method
- Does NOT auto-commit offsets — commits after `ProcessAsync` returns successfully
- Handles consumer group rebalancing

Each service's Api/Presentation layer has a concrete consumer that extends this base and delegates to an Application layer service. For example, `EventStoredConsumer` in ProjectorService.Api extends `KafkaConsumerBase<EventStored>` and calls `ProjectionService.ProjectAsync()` in its `ProcessAsync` override.

## Docker Compose

Services:
- **postgres**: PostgreSQL 16 Alpine, port 5432, with `docker/init.sql` mounted to `/docker-entrypoint-initdb.d/`
- **kafka**: `confluentinc/cp-kafka:7.6.0` in KRaft mode (no Zookeeper), port 9092 (host) / 29092 (internal)
- **kafka-init**: One-shot container that creates the three topics with 12 partitions each, depends on kafka healthy
- **requests-api**: Port 5001, depends on kafka
- **transaction-service**: Port 5002, depends on kafka, postgres
- **projector-service**: Port 5003, depends on kafka, postgres
- **notification-consumer**: Port 5004, depends on kafka, postgres

Each .NET service should have its own `Dockerfile` (multi-stage build: SDK for build, ASP.NET runtime for final image).

### Environment Variables (common config)

```
Kafka__BootstrapServers=kafka:29092
ConnectionStrings__EventStore=Host=postgres;Database=eventstore;Username=postgres;Password=postgres
```

## EF Core Setup

Each service that needs database access has its **own `DbContext` in its Infrastructure layer**, scoped to only the tables that service needs. Do NOT put a shared DbContext in the Shared project — that would violate onion architecture by coupling Domain/Application to EF Core.

**TransactionService** — `EventStoreDbContext`:
```csharp
// In TransactionService.Infrastructure/Persistence/EventStoreDbContext.cs
public class EventStoreDbContext : DbContext
{
    public DbSet<EventStoreEntry> Events => Set<EventStoreEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EventStoreEntry>(entity =>
        {
            entity.ToTable("event_store");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.AccountId, e.SequenceNum }).IsUnique();
            // Map all properties to snake_case column names
        });
    }
}
```

**ProjectorService** — `ProjectorDbContext`:
```csharp
// In ProjectorService.Infrastructure/Persistence/ProjectorDbContext.cs
public class ProjectorDbContext : DbContext
{
    public DbSet<ProjectedState> ProjectedStates => Set<ProjectedState>();

    // Also maps EventStoreEntry as READ-ONLY for the event history query endpoint
    // The Projector never writes to event_store, only reads
}
```

**NotificationConsumer** — `NotificationDbContext`:
```csharp
// In NotificationConsumer.Infrastructure/Persistence/NotificationDbContext.cs
public class NotificationDbContext : DbContext
{
    public DbSet<BalanceEventLog> BalanceEventLogs => Set<BalanceEventLog>();
}
```

**Important:** Entity classes live in the Domain layer of each service (e.g., `TransactionService.Domain/Entities/EventStoreEntry.cs`). They must NOT have EF Core data annotations. All mapping is done via `OnModelCreating` or `IEntityTypeConfiguration<T>` in the Infrastructure layer.

## Key Implementation Notes

1. **Kafka partition key is critical.** Always use `accountId` as the message key. This ensures all events for an account go to the same partition, which gives the Projector sequential ordering per account.

2. **The Projector must NOT use `EnableAutoCommit`.** It should manually commit offsets after successfully processing and persisting each event. This ensures at-least-once delivery. Combined with the idempotency check (skip if sequence already processed), this gives you effectively-once processing.

3. **Amount sign convention:** Store deposits as positive amounts and debits as negative amounts in the event store. This simplifies the projector calculation to just `new_balance = current_balance + amount`.

4. **Sequence gaps in the Projector:** If the Projector receives sequence 5 but has only processed through 3, it should NOT skip to 5. It should wait/retry for sequence 4. This can happen if the Transaction Service published to Kafka but the message for sequence 4 was delayed. A simple approach: if there's a gap, don't commit the Kafka offset and let it re-deliver.

5. **Consolidation / Snapshotting:** Not implemented in this demo, but in production you'd periodically snapshot the account balance at a certain sequence number and only replay events after that point. The `projected_state` table effectively IS the snapshot.

6. **Error handling:** Each consumer should have retry logic with exponential backoff for transient database/Kafka errors. Dead letter topics are a good addition for events that repeatedly fail processing.

## Testing the System

After `docker compose up`:

```bash
# Create a deposit
curl -X POST http://localhost:5001/api/accounts/acct-001/transactions \
  -H "Content-Type: application/json" \
  -d '{"type": "deposit", "amount": 1000.00, "description": "Initial deposit"}'

# Create another deposit
curl -X POST http://localhost:5001/api/accounts/acct-001/transactions \
  -H "Content-Type: application/json" \
  -d '{"type": "deposit", "amount": 500.00, "description": "Paycheck"}'

# Create a debit
curl -X POST http://localhost:5001/api/accounts/acct-001/transactions \
  -H "Content-Type: application/json" \
  -d '{"type": "debit", "amount": 200.00, "description": "Grocery store"}'

# Wait a moment for processing, then check balance
curl http://localhost:5003/api/accounts/acct-001/balance
# Expected: { "accountId": "acct-001", "balance": 1300.00 }

# Check transaction history
curl http://localhost:5003/api/accounts/acct-001/events

# Check downstream notifications received
curl http://localhost:5004/api/accounts/acct-001/notifications
# Should show 3 events, each with balanceBefore and balanceAfter
```

## Concurrency Test

To verify the system handles high concurrency correctly, send many simultaneous transactions to the same account and confirm the final balance is correct:

```bash
# Send 100 concurrent $10 deposits to the same account
for i in $(seq 1 100); do
  curl -s -X POST http://localhost:5001/api/accounts/acct-test/transactions \
    -H "Content-Type: application/json" \
    -d '{"type": "deposit", "amount": 10.00, "description": "Test deposit '$i'"}' &
done
wait

# Wait for processing
sleep 5

# Balance should be exactly $1000.00
curl http://localhost:5003/api/accounts/acct-test/balance
```