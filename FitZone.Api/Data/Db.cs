using MySqlConnector;

namespace FitZone.Api.Data;

/// <summary>
/// Small helper around MySqlConnector so controllers can run parameterized SQL
/// and get rows back as dictionaries (column name -> value), which serialize
/// straight to JSON for the front end.
/// </summary>
public class Db(MySqlDataSource dataSource)
{
    public async Task<MySqlConnection> OpenAsync()
    {
        return await dataSource.OpenConnectionAsync();
    }

    public async Task<List<Dictionary<string, object?>>> QueryAsync(string sql, object? parameters = null,
        MySqlConnection? conn = null, MySqlTransaction? tx = null)
    {
        var ownsConnection = conn == null;
        conn ??= await OpenAsync();
        try
        {
            await using var cmd = BuildCommand(conn, tx, sql, parameters);
            await using var reader = await cmd.ExecuteReaderAsync();
            var rows = new List<Dictionary<string, object?>>();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                rows.Add(row);
            }
            return rows;
        }
        finally
        {
            if (ownsConnection) await conn.DisposeAsync();
        }
    }

    public async Task<Dictionary<string, object?>?> QuerySingleAsync(string sql, object? parameters = null,
        MySqlConnection? conn = null, MySqlTransaction? tx = null)
    {
        var rows = await QueryAsync(sql, parameters, conn, tx);
        return rows.FirstOrDefault();
    }

    public async Task<object?> ScalarAsync(string sql, object? parameters = null,
        MySqlConnection? conn = null, MySqlTransaction? tx = null)
    {
        var ownsConnection = conn == null;
        conn ??= await OpenAsync();
        try
        {
            await using var cmd = BuildCommand(conn, tx, sql, parameters);
            var result = await cmd.ExecuteScalarAsync();
            return result is DBNull ? null : result;
        }
        finally
        {
            if (ownsConnection) await conn.DisposeAsync();
        }
    }

    /// <summary>Runs INSERT/UPDATE/DELETE. Returns rows affected.</summary>
    public async Task<int> ExecuteAsync(string sql, object? parameters = null,
        MySqlConnection? conn = null, MySqlTransaction? tx = null)
    {
        var ownsConnection = conn == null;
        conn ??= await OpenAsync();
        try
        {
            await using var cmd = BuildCommand(conn, tx, sql, parameters);
            return await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            if (ownsConnection) await conn.DisposeAsync();
        }
    }

    /// <summary>Runs an INSERT and returns the new AUTO_INCREMENT id.</summary>
    public async Task<long> InsertAsync(string sql, object? parameters = null,
        MySqlConnection? conn = null, MySqlTransaction? tx = null)
    {
        var ownsConnection = conn == null;
        conn ??= await OpenAsync();
        try
        {
            await using var cmd = BuildCommand(conn, tx, sql, parameters);
            await cmd.ExecuteNonQueryAsync();
            return cmd.LastInsertedId;
        }
        finally
        {
            if (ownsConnection) await conn.DisposeAsync();
        }
    }

    // Turns an anonymous object like new { id = 5 } into @id parameters
    private static MySqlCommand BuildCommand(MySqlConnection conn, MySqlTransaction? tx, string sql, object? parameters)
    {
        var cmd = new MySqlCommand(sql, conn, tx);
        if (parameters != null)
        {
            foreach (var prop in parameters.GetType().GetProperties())
            {
                cmd.Parameters.AddWithValue("@" + prop.Name, prop.GetValue(parameters) ?? DBNull.Value);
            }
        }
        return cmd;
    }
}
