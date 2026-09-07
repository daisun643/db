using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;

namespace Backend.Data;

/// <summary>
/// Oracle 存储过程调用辅助：统一构造 IN/OUT 参数、执行匿名块并读取结果。
/// 存储过程约定 p_code = 0 表示成功，非 0 时 p_message 为可直接返回给前端的中文提示，
/// 这样业务规则、状态机和资金划转都留在数据库侧，应用层只负责授权与响应组装。
/// </summary>
public static class OracleProcedure
{
    public const int Success = 0;

    /// <summary>
    /// 调用带引号创建的存储过程（对象名在库中是小写，必须原样加引号，否则会被 Oracle 大写后找不到对象）。
    /// 复用 EF Core 当前连接与事务，因此可以和上下文中的其他改动保持同一个事务边界。
    /// </summary>
    public static async Task CallAsync(DbContext db, string procedure, params OracleParameter[] parameters)
    {
        var connection = db.Database.GetDbConnection();
        var connectionWasOpen = connection.State == ConnectionState.Open;
        if (!connectionWasOpen)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            var transaction = db.Database.CurrentTransaction;
            if (transaction != null)
                command.Transaction = transaction.GetDbTransaction();

            var placeholders = parameters.Select(p => ":" + p.ParameterName);
            command.CommandText = $"BEGIN \"{procedure}\"({string.Join(", ", placeholders)}); END;";
            command.CommandType = CommandType.Text;

            if (command is OracleCommand oracleCommand)
                oracleCommand.BindByName = true;

            foreach (var parameter in parameters)
                command.Parameters.Add(parameter);

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (!connectionWasOpen)
                await connection.CloseAsync();
        }
    }

    public static OracleParameter InInt32(string name, int? value) =>
        new(name, OracleDbType.Int32)
        {
            Direction = ParameterDirection.Input,
            Value = value.HasValue ? value.Value : DBNull.Value
        };

    public static OracleParameter InDecimal(string name, decimal? value) =>
        new(name, OracleDbType.Decimal)
        {
            Direction = ParameterDirection.Input,
            Value = value.HasValue ? value.Value : DBNull.Value
        };

    public static OracleParameter InText(string name, string? value) =>
        new(name, OracleDbType.Varchar2)
        {
            Direction = ParameterDirection.Input,
            Size = 4000,
            Value = string.IsNullOrEmpty(value) ? DBNull.Value : value
        };

    public static OracleParameter OutInt32(string name) =>
        new(name, OracleDbType.Int32) { Direction = ParameterDirection.Output };

    public static OracleParameter OutDecimal(string name) =>
        new(name, OracleDbType.Decimal) { Direction = ParameterDirection.Output };

    public static OracleParameter OutText(string name, int size = 500) =>
        new(name, OracleDbType.Varchar2)
        {
            Direction = ParameterDirection.Output,
            Size = size
        };

    // ODP.NET 的 OUT 参数回传 Oracle 专有值类型（NUMBER → OracleDecimal，VARCHAR2 → OracleString），
    // 它们没有实现 IConvertible，因此这里显式拆箱后再取 .NET 值。
    public static int ReadInt32(OracleParameter parameter) => ReadInt32OrNull(parameter) ?? 0;

    public static int? ReadInt32OrNull(OracleParameter parameter) => parameter.Value switch
    {
        null or DBNull => null,
        OracleDecimal value => value.IsNull ? null : (int)value.Value,
        int value => value,
        _ => Convert.ToInt32(parameter.Value)
    };

    public static decimal ReadDecimal(OracleParameter parameter) => ReadDecimalOrNull(parameter) ?? 0m;

    public static decimal? ReadDecimalOrNull(OracleParameter parameter) => parameter.Value switch
    {
        null or DBNull => null,
        OracleDecimal value => value.IsNull ? null : value.Value,
        decimal value => value,
        _ => Convert.ToDecimal(parameter.Value)
    };

    public static string? ReadText(OracleParameter parameter) => parameter.Value switch
    {
        null or DBNull => null,
        OracleString value => value.IsNull ? null : value.Value,
        string value => value,
        _ => parameter.Value.ToString()
    };
}
