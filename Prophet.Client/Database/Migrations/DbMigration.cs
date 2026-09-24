namespace Prophet.Client.Database.Migrations;

/// <summary>
/// 数据库迁移定义（幂等：通过 schema_migrations 表记录是否已执行）
/// </summary>
public sealed record DbMigration(
    string Id,
    string Description,
    string Sql,
    bool DisableForeignKeys = false
);


