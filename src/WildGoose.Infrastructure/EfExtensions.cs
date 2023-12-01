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
        builder.HasConversion(new ValueConverter<DateTimeOffset?, long?>(
            v => v.HasValue
                ? milliseconds ? v.Value.ToUnixTimeMilliseconds() : v.Value.ToUnixTimeSeconds()
                : default,
            v => v.HasValue
                ? milliseconds
                    ? DateTimeOffset.FromUnixTimeMilliseconds(v.Value).ToLocalTime()
                    : DateTimeOffset.FromUnixTimeSeconds(v.Value).ToLocalTime()
                : default));
        return builder;
    }

    public static PropertyBuilder<DateTimeOffset> UseUnixTime(this PropertyBuilder<DateTimeOffset> builder,
        bool milliseconds = false)
    {
        builder.HasConversion(new ValueConverter<DateTimeOffset, long>(
            v => milliseconds ? v.ToUnixTimeMilliseconds() : v.ToUnixTimeSeconds(),
            v => milliseconds
                ? DateTimeOffset.FromUnixTimeMilliseconds(v)
                : DateTimeOffset.FromUnixTimeSeconds(v).ToLocalTime()));
        builder.IsRequired();
        builder.HasDefaultValue(DateTimeOffset.UnixEpoch);
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