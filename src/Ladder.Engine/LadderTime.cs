namespace Ladder;

public static class LadderTime
{
    public static DateTime AsUtc(DateTime value)
    {
        if (value.Kind == DateTimeKind.Local)
        {
            return value.ToUniversalTime();
        }

        if (value.Kind == DateTimeKind.Unspecified)
        {
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        return value;
    }
}
