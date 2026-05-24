using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Update;
using Microsoft.Extensions.Options;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure.Internal;
using Pomelo.EntityFrameworkCore.MySql.Migrations;
using WildGoose.Domain.Options;

#pragma warning disable EF1001

#nullable enable

namespace WildGoose.Application.Ef;

public class MySqlPrefixedMigrationsSqlGenerator : MySqlMigrationsSqlGenerator
{
    private readonly string _prefix;

    public MySqlPrefixedMigrationsSqlGenerator(
        MigrationsSqlGeneratorDependencies dependencies,
        ICommandBatchPreparer commandBatchPreparer,
        IMySqlOptions options)
        : base(dependencies, commandBatchPreparer, options)
    {
        var dbOptions = dependencies.CurrentContext.Context.GetService<IOptions<DbOptions>>().Value;
        _prefix = dbOptions.TablePrefix ?? string.Empty;
    }

    protected override IReadOnlyList<MigrationOperation> FilterOperations(
        IReadOnlyList<MigrationOperation> operations,
        IModel? model)
    {
        var filtered = base.FilterOperations(operations, model);
        var noFk = new List<MigrationOperation>(filtered.Count);
        foreach (var op in filtered)
        {
            if (op is AddForeignKeyOperation or DropForeignKeyOperation)
            {
                continue;
            }

            if (op is CreateTableOperation createTable)
            {
                createTable.ForeignKeys.Clear();
            }

            noFk.Add(op);
        }

        PrefixOperations(noFk);
        return noFk;
    }

    private void PrefixOperations(IEnumerable<MigrationOperation> operations)
    {
        foreach (var op in operations)
        {
            PrefixTableReferences(op);
        }
    }

    private void PrefixTableReferences(MigrationOperation op)
    {
        switch (op)
        {
            case CreateTableOperation o:
                o.Name = Prefix(o.Name);
                if (o.PrimaryKey != null) PrefixTableReferences(o.PrimaryKey);
                PrefixOperations(o.ForeignKeys);
                PrefixOperations(o.UniqueConstraints);
                PrefixOperations(o.CheckConstraints);
                PrefixOperations(o.Columns);
                break;

            case DropTableOperation o:
                o.Name = Prefix(o.Name);
                break;

            case RenameTableOperation o:
                o.Name = Prefix(o.Name);
                o.NewName = Prefix(o.NewName);
                break;

            case AlterTableOperation o:
                o.Name = Prefix(o.Name);
                break;

            case AddColumnOperation o:
                o.Table = Prefix(o.Table);
                break;

            case DropColumnOperation o:
                o.Table = Prefix(o.Table);
                break;

            case AlterColumnOperation o:
                o.Table = Prefix(o.Table);
                break;

            case RenameColumnOperation o:
                o.Table = Prefix(o.Table);
                break;

            case AddPrimaryKeyOperation o:
                o.Table = Prefix(o.Table);
                o.Name = Prefix(o.Name);
                break;

            case DropPrimaryKeyOperation o:
                o.Table = Prefix(o.Table);
                o.Name = Prefix(o.Name);
                break;

            case AddForeignKeyOperation o:
                o.Table = Prefix(o.Table);
                o.PrincipalTable = Prefix(o.PrincipalTable);
                o.Name = Prefix(o.Name);
                break;

            case DropForeignKeyOperation o:
                o.Table = Prefix(o.Table);
                o.Name = Prefix(o.Name);
                break;

            case CreateIndexOperation o:
                o.Table = Prefix(o.Table);
                o.Name = Prefix(o.Name);
                break;

            case DropIndexOperation o:
                o.Table = Prefix(o.Table);
                o.Name = Prefix(o.Name);
                break;

            case AddUniqueConstraintOperation o:
                o.Table = Prefix(o.Table);
                o.Name = Prefix(o.Name);
                break;

            case DropUniqueConstraintOperation o:
                o.Table = Prefix(o.Table);
                o.Name = Prefix(o.Name);
                break;

            case AddCheckConstraintOperation o:
                o.Table = Prefix(o.Table);
                o.Name = Prefix(o.Name);
                break;

            case DropCheckConstraintOperation o:
                o.Table = Prefix(o.Table);
                o.Name = Prefix(o.Name);
                break;

            case InsertDataOperation o:
                o.Table = Prefix(o.Table);
                break;

            case DeleteDataOperation o:
                o.Table = Prefix(o.Table);
                break;

            case UpdateDataOperation o:
                o.Table = Prefix(o.Table);
                break;
        }
    }

    private string Prefix(string? name)
    {
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(_prefix))
        {
            return name ?? string.Empty;
        }

        return name.StartsWith(_prefix, StringComparison.OrdinalIgnoreCase)
            ? name
            : _prefix + name;
    }
}
