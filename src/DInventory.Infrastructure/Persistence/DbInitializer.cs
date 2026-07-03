using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DInventory.Infrastructure.Persistence;

/// <summary>
/// On application startup, ensures the DInventoryDB database and its schema/seed data exist by
/// executing database/Database.sql (copied into App_Data) against the configured LocalDB server.
/// This lets the app be run immediately after a git clone without a manual database setup step.
/// </summary>
public static class DbInitializer
{
    private static readonly Regex UseStatementRegex =
        new(@"^\s*USE\s+\[?(?<db>\w+)\]?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static void EnsureDatabaseCreated(IConfiguration configuration, string contentRootPath, ILogger logger)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogWarning("No 'DefaultConnection' connection string configured. Skipping automatic database setup.");
            return;
        }

        var scriptPath = Path.Combine(contentRootPath, "App_Data", "Database.sql");
        if (!File.Exists(scriptPath))
        {
            logger.LogWarning("Database script not found at {ScriptPath}. Skipping automatic database setup.", scriptPath);
            return;
        }

        var script = File.ReadAllText(scriptPath);
        var batches = Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)
            .Select(b => b.Trim())
            .Where(b => b.Length > 0)
            .ToList();

        try
        {
            // Every batch gets its own short-lived connection (instead of one long-lived connection for the
            // whole script) with a couple of retries each. This is what actually fixes "the connection is
            // broken and recovery is not possible": LocalDB can drop the session right after CREATE DATABASE
            // on a first run, which SqlClient's built-in resiliency can't recover mid-script - a broken
            // connection here just means "open a fresh one and keep going" instead of aborting the setup.
            // We track "USE <db>" batches ourselves and point each new connection at the right database,
            // since a fresh connection doesn't remember a previous connection's USE statement.
            var currentDatabase = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
            if (string.IsNullOrWhiteSpace(currentDatabase))
            {
                currentDatabase = "master";
            }

            for (var i = 0; i < batches.Count; i++)
            {
                var batch = batches[i];
                var builder = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" };

                // The script starts against master (to create the database itself); switch the target
                // catalog for every batch from here on once we hit the script's own "USE DInventoryDB;".
                if (i > 0)
                {
                    builder.InitialCatalog = currentDatabase;
                }

                ExecuteBatchWithRetry(builder.ConnectionString, batch, i + 1, batches.Count, logger);

                var useMatch = UseStatementRegex.Match(batch);
                if (useMatch.Success)
                {
                    currentDatabase = useMatch.Groups["db"].Value;
                }
            }

            logger.LogInformation("Database check complete: DInventoryDB is ready.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Automatic database setup failed. Please run database/Database.sql manually against your SQL Server instance, then restart the app.");
        }
    }

    private static void ExecuteBatchWithRetry(string batchConnectionString, string batchSql, int batchNumber, int totalBatches, ILogger logger)
    {
        const int maxAttempts = 3;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var connection = new SqlConnection(batchConnectionString);
                connection.Open();

                using var command = new SqlCommand(batchSql, connection) { CommandTimeout = 90 };
                command.ExecuteNonQuery();
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logger.LogWarning(ex,
                    "Database setup batch {BatchNumber}/{TotalBatches} failed on attempt {Attempt}/{MaxAttempts} - reconnecting and retrying.",
                    batchNumber, totalBatches, attempt, maxAttempts);
                Thread.Sleep(500 * attempt);
            }
        }
    }
}
