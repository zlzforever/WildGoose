using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using WildGoose.Domain.Entity;

namespace WildGoose.Infrastructure;

public static class EfExtensions
{
    public static PropertyBuilder<DateTimeOffset?> UseUnixTime(this PropertyBuilder<DateTimeOffset?> builder,
        bool milliseconds = false)
    {
        builder.Metadata.SetValueConverter(new NullableDateTimeOffsetToLongConverter(milliseconds));
        builder.HasColumnType("bigint");
        builder.IsRequired(false);
        return builder;
    }

    public static PropertyBuilder<DateTimeOffset> UseUnixTime(this PropertyBuilder<DateTimeOffset> builder,
        bool milliseconds = false)
    {
        builder.IsRequired();
        builder.HasColumnType("bigint");
        builder.HasDefaultValue(DateTimeOffset.UnixEpoch);
        builder.Metadata.SetValueConverter(new DateTimeOffsetToLongConverter(milliseconds));
        return builder;
    }
    
    // public static void AddSoftDeleteQueryFilter(
    //     this IMutableEntityType entityData)
    // {
    //     // ReSharper disable once PossibleNullReferenceException
    //     var methodToCall = typeof(EfExtensions)
    //         .GetMethod(nameof(GetSoftDeleteFilter),
    //             BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(entityData.ClrType);
    //     var filter = methodToCall.Invoke(null, Array.Empty<object>());
    //     entityData.SetQueryFilter((LambdaExpression)filter);
    // }

    private static LambdaExpression GetSoftDeleteFilter<TEntity>()
        where TEntity : class, IDeletion
    {
        Expression<Func<TEntity, bool>> filter = x => !x.IsDeleted;
        return filter;
    }
}