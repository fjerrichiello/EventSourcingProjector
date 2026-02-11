namespace Common.Kafka;

public static class KafkaConstants
{
    public static class Topics
    {
        public const string AccountTransactions = "account-transactions";

        public const string AccountBalanceEvents = "account-balance-events";

        public const string EventStored = "event-stored";
    }

    public static class GroupId
    {
        public const string NotificationConsumerGroup = "notification-consumer-group";
        public const string ProjectorServiceGroup = "projector-service-group";
        public const string TransactionServiceGroup = "transaction-service-group";
    }
}