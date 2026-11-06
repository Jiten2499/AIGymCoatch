using System.Globalization;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Platform.BackgroundJobs;
using Microsoft.Data.SqlClient;

namespace LifeFitAI.Infrastructure.Platform;

public sealed partial class HangfireBackgroundJobMonitor
{
    private async Task<(IReadOnlyList<string> Ids, int Total)> SqlPageAsync(BackgroundJobSearch search, PageQuery page, CancellationToken cancellationToken)
    {
        var (where, parameters) = SqlFilter(search);
        var sort = search.Sort?.Equals("status", StringComparison.OrdinalIgnoreCase) == true ? "j.StateName" : "j.CreatedAt";
        var direction = search.Descending ? "DESC" : "ASC";
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var total = await ScalarAsync(connection, $"SELECT COUNT(*) FROM HangFire.Job j WHERE {where}", parameters, cancellationToken).ConfigureAwait(false);
        var ids = new List<string>();
        await using var command = new SqlCommand($"SELECT j.Id FROM HangFire.Job j WHERE {where} ORDER BY {sort} {direction} OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY", connection);
        Copy(command, parameters);
        command.Parameters.Add(new SqlParameter("@skip", page.Skip));
        command.Parameters.Add(new SqlParameter("@take", page.NormalizedSize));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            ids.Add(reader.GetInt64(0).ToString(CultureInfo.InvariantCulture));
        }

        return (ids, total);
    }

    private async Task<Dictionary<BackgroundJobStatus, int>> SqlCountsAsync(Guid? tenantId, CancellationToken cancellationToken)
    {
        var (where, parameters) = SqlFilter(new BackgroundJobSearch(null, null, null, null, tenantId, null, null, null, null, 1, 1, null, true));
        var counts = new Dictionary<BackgroundJobStatus, int>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand($"SELECT j.StateName, COUNT(*) FROM HangFire.Job j WHERE {where} GROUP BY j.StateName", connection);
        Copy(command, parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            counts[BackgroundJobSafety.Normalize(reader.IsDBNull(0) ? null : reader.GetString(0))] = reader.GetInt32(1);
        }

        return counts;
    }

    private static (string Where, List<(string Name, object Value)> Parameters) SqlFilter(BackgroundJobSearch search)
    {
        var clauses = new List<string> { "1 = 1" };
        var parameters = new List<(string Name, object Value)>();
        AddParameter(clauses, parameters, search.Status is null ? null : BackgroundJobSafety.ToHangfireState(search.Status), "j.StateName = @status", "@status");
        AddParameter(clauses, parameters, search.From, "j.CreatedAt >= @from", "@from");
        AddParameter(clauses, parameters, search.To, "j.CreatedAt <= @to", "@to");
        AddExists(clauses, parameters, "JobType", search.JobType);
        AddExists(clauses, parameters, "Queue", search.Queue);
        AddExists(clauses, parameters, "CorrelationId", search.CorrelationId);
        AddExists(clauses, parameters, "SourceType", search.SourceType);
        AddExists(clauses, parameters, "TenantId", search.TenantId?.ToString("D"));
        if (!string.IsNullOrWhiteSpace(search.Term))
        {
            clauses.Add("(CAST(j.Id AS nvarchar(30)) = @term OR EXISTS (SELECT 1 FROM HangFire.JobParameter p WHERE p.JobId = j.Id AND p.Name = N'JobType' AND REPLACE(p.Value, '\"', '') LIKE @termLike))");
            parameters.Add(("@term", search.Term.Trim()));
            parameters.Add(("@termLike", $"%{search.Term.Trim()}%"));
        }

        return (string.Join(" AND ", clauses), parameters);
    }

    private static void AddExists(List<string> clauses, List<(string Name, object Value)> parameters, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        clauses.Add($"EXISTS (SELECT 1 FROM HangFire.JobParameter p WHERE p.JobId = j.Id AND p.Name = N'{name}' AND REPLACE(p.Value, '\"', '') = @{name})");
        parameters.Add(('@' + name, value));
    }

    private static void AddParameter(List<string> clauses, List<(string Name, object Value)> parameters, object? value, string clause, string name)
    {
        if (value is null)
        {
            return;
        }

        clauses.Add(clause);
        parameters.Add((name, value));
    }

    private static void Copy(SqlCommand command, List<(string Name, object Value)> parameters)
    {
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(new SqlParameter(parameter.Name, parameter.Value));
        }
    }

    private static async Task<int> ScalarAsync(SqlConnection connection, string sql, List<(string Name, object Value)> parameters, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(sql, connection);
        Copy(command, parameters);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }
}
