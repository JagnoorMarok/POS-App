using Microsoft.Data.Sqlite;

namespace RestaurantManagement.Infrastructure.Persistence;

/// <summary>
/// Utility to resolve environment variables in SQLite connection strings and ensure directory paths exist.
/// </summary>
public static class DatabasePathResolver
{
    /// <summary>
    /// Resolves environment variables (e.g. %LOCALAPPDATA%) in a connection string and returns a sanitized, absolute connection string.
    /// </summary>
    public static string ResolveConnectionString(string rawConnectionString)
    {
        if (string.IsNullOrWhiteSpace(rawConnectionString))
        {
            var defaultDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RestaurantManagement",
                "Data");
            var defaultFile = Path.Combine(defaultDir, "restaurant.db");
            return $"Data Source={defaultFile}";
        }

        // Expand environment variables such as %LOCALAPPDATA%, %APPDATA%, etc.
        var expanded = Environment.ExpandEnvironmentVariables(rawConnectionString);

        try
        {
            var builder = new SqliteConnectionStringBuilder(expanded);
            if (!string.IsNullOrWhiteSpace(builder.DataSource) && builder.DataSource != ":memory:")
            {
                var fullPath = Path.GetFullPath(builder.DataSource);
                builder.DataSource = fullPath;
                return builder.ConnectionString;
            }
        }
        catch
        {
            // If SqliteConnectionStringBuilder fails, return the expanded string
        }

        return expanded;
    }

    /// <summary>
    /// Ensures that the parent directory for a SQLite database file exists on disk.
    /// </summary>
    public static void EnsureDirectoryExists(string resolvedConnectionString)
    {
        try
        {
            var builder = new SqliteConnectionStringBuilder(resolvedConnectionString);
            if (!string.IsNullOrWhiteSpace(builder.DataSource) && builder.DataSource != ":memory:")
            {
                var directory = Path.GetDirectoryName(builder.DataSource);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }
        }
        catch
        {
            // Ignore directory creation errors if connection string is special (e.g. in-memory)
        }
    }
}
