using Npgsql;
using NpgsqlTypes;
using WeightVerificationQR.Core.Interfaces;
using WeightVerificationQR.Core.Models;

namespace WeightVerificationQR.Data;

public sealed class PostgresCentralSyncStore : ICentralSyncStore
{
    private readonly CentralSyncSettings _settings;

    public PostgresCentralSyncStore(CentralSyncSettings settings) => _settings = settings;

    public bool IsEnabled =>
        _settings.Enabled &&
        !string.IsNullOrWhiteSpace(_settings.ConnectionString);

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled) return;

        const string sql =
            """
            CREATE TABLE IF NOT EXISTS qr_serial_state (
                id integer PRIMARY KEY,
                next_value bigint NOT NULL
            );

            INSERT INTO qr_serial_state (id, next_value)
            VALUES (1, 1)
            ON CONFLICT (id) DO NOTHING;

            CREATE TABLE IF NOT EXISTS qr_weigh_records (
                global_record_id uuid PRIMARY KEY,
                kit_number varchar(120) NOT NULL,
                qr_id varchar(120) NOT NULL,
                site_code varchar(20) NOT NULL,
                line_code varchar(20) NOT NULL,
                machine_code varchar(20) NOT NULL,
                serial_number bigint NOT NULL,
                product_id integer NOT NULL,
                product_name varchar(200) NOT NULL,
                quantity varchar(50) NOT NULL,
                weight_kg numeric(10,3) NOT NULL,
                result integer NOT NULL,
                fail_reason integer NOT NULL,
                record_date timestamp with time zone NOT NULL,
                operator_name varchar(100) NOT NULL,
                qr_generated boolean NOT NULL,
                printed_successfully boolean NOT NULL,
                printer_status varchar(50) NOT NULL,
                reprint_count integer NOT NULL,
                remarks varchar(500) NOT NULL,
                source_updated_at timestamp with time zone NOT NULL,
                synced_at timestamp with time zone NOT NULL
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ux_qr_weigh_records_qr_id
                ON qr_weigh_records (qr_id)
                WHERE qr_id <> '';
            CREATE INDEX IF NOT EXISTS ix_qr_weigh_records_record_date
                ON qr_weigh_records (record_date);
            CREATE INDEX IF NOT EXISTS ix_qr_weigh_records_station
                ON qr_weigh_records (site_code, line_code, machine_code);
            """;

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<SerialNumberBlock?> TryAllocateSerialBlockAsync(
        int blockSize,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled) return null;

        blockSize = Math.Max(1, blockSize);

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var ensure = new NpgsqlCommand(
            """
            INSERT INTO qr_serial_state (id, next_value)
            VALUES (1, 1)
            ON CONFLICT (id) DO NOTHING;
            """,
            connection,
            transaction))
        {
            await ensure.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var allocate = new NpgsqlCommand(
            """
            UPDATE qr_serial_state
            SET next_value = next_value + @block_size
            WHERE id = 1
            RETURNING next_value - @block_size, next_value - 1;
            """,
            connection,
            transaction);
        allocate.Parameters.AddWithValue("block_size", blockSize);

        await using var reader = await allocate.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var block = new SerialNumberBlock(reader.GetInt64(0), reader.GetInt64(1));
        await reader.CloseAsync();
        await transaction.CommitAsync(cancellationToken);
        return block;
    }

    public async Task UpsertWeighRecordAsync(
        WeighRecord record,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled) return;

        const string sql =
            """
            INSERT INTO qr_weigh_records (
                global_record_id, kit_number, qr_id, site_code, line_code, machine_code,
                serial_number, product_id, product_name, quantity, weight_kg, result,
                fail_reason, record_date, operator_name, qr_generated,
                printed_successfully, printer_status, reprint_count, remarks,
                source_updated_at, synced_at)
            VALUES (
                @global_record_id, @kit_number, @qr_id, @site_code, @line_code, @machine_code,
                @serial_number, @product_id, @product_name, @quantity, @weight_kg, @result,
                @fail_reason, @record_date, @operator_name, @qr_generated,
                @printed_successfully, @printer_status, @reprint_count, @remarks,
                @source_updated_at, @synced_at)
            ON CONFLICT (global_record_id) DO UPDATE SET
                kit_number = EXCLUDED.kit_number,
                qr_id = EXCLUDED.qr_id,
                site_code = EXCLUDED.site_code,
                line_code = EXCLUDED.line_code,
                machine_code = EXCLUDED.machine_code,
                serial_number = EXCLUDED.serial_number,
                product_id = EXCLUDED.product_id,
                product_name = EXCLUDED.product_name,
                quantity = EXCLUDED.quantity,
                weight_kg = EXCLUDED.weight_kg,
                result = EXCLUDED.result,
                fail_reason = EXCLUDED.fail_reason,
                record_date = EXCLUDED.record_date,
                operator_name = EXCLUDED.operator_name,
                qr_generated = EXCLUDED.qr_generated,
                printed_successfully = EXCLUDED.printed_successfully,
                printer_status = EXCLUDED.printer_status,
                reprint_count = EXCLUDED.reprint_count,
                remarks = EXCLUDED.remarks,
                source_updated_at = EXCLUDED.source_updated_at,
                synced_at = EXCLUDED.synced_at;
            """;

        var now = DateTime.UtcNow;
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("global_record_id", record.GlobalRecordId);
        command.Parameters.AddWithValue("kit_number", record.KitNumber);
        command.Parameters.AddWithValue("qr_id", record.QrId);
        command.Parameters.AddWithValue("site_code", record.SiteCode);
        command.Parameters.AddWithValue("line_code", record.LineCode);
        command.Parameters.AddWithValue("machine_code", record.MachineCode);
        command.Parameters.AddWithValue("serial_number", record.SerialNumber);
        command.Parameters.AddWithValue("product_id", record.ProductId);
        command.Parameters.AddWithValue("product_name", record.ProductName);
        command.Parameters.AddWithValue("quantity", record.Quantity);
        command.Parameters.AddWithValue("weight_kg", record.WeightKg);
        command.Parameters.AddWithValue("result", (int)record.Result);
        command.Parameters.AddWithValue("fail_reason", (int)record.FailReason);
        command.Parameters.Add(
            new NpgsqlParameter("record_date", NpgsqlDbType.TimestampTz)
            {
                Value = record.RecordDate.ToUniversalTime()
            });
        command.Parameters.AddWithValue("operator_name", record.OperatorName);
        command.Parameters.AddWithValue("qr_generated", record.QrGenerated);
        command.Parameters.AddWithValue("printed_successfully", record.PrintedSuccessfully);
        command.Parameters.AddWithValue("printer_status", record.PrinterStatus);
        command.Parameters.AddWithValue("reprint_count", record.ReprintCount);
        command.Parameters.AddWithValue("remarks", record.Remarks);
        command.Parameters.Add(
            new NpgsqlParameter("source_updated_at", NpgsqlDbType.TimestampTz) { Value = now });
        command.Parameters.Add(
            new NpgsqlParameter("synced_at", NpgsqlDbType.TimestampTz) { Value = now });

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private string GetConnectionString()
    {
        var value = _settings.ConnectionString.Trim();
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "postgres" && uri.Scheme != "postgresql"))
        {
            return value;
        }

        var userInfo = uri.UserInfo.Split(':', 2);
        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = uri.AbsolutePath.Trim('/'),
            Username = Uri.UnescapeDataString(userInfo.ElementAtOrDefault(0) ?? string.Empty),
            Password = Uri.UnescapeDataString(userInfo.ElementAtOrDefault(1) ?? string.Empty),
            SslMode = SslMode.Require,
            Timeout = 5,
            CommandTimeout = 10
        }.ConnectionString;
    }
}
