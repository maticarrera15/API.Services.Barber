using Npgsql;

namespace Api.Services.Barber.Common;

public static class PostgresErrors
{
    public const string ExclusionViolation = "23P01";
    public const string UniqueViolation = "23505";
    public const string CheckViolation = "23514";

    public static bool Is(this Exception? ex, string sqlState)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is PostgresException pg && pg.SqlState == sqlState)
                return true;
        }

        return false;
    }
}
