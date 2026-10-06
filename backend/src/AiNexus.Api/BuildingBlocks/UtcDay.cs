namespace AiNexus.BuildingBlocks;

public static class UtcDay
{
    public static DateTimeOffset Today => Start(DateTimeOffset.UtcNow);
    public static DateTimeOffset Start(DateTimeOffset instant) => new(instant.UtcDateTime.Date, TimeSpan.Zero);
}
