using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WildGoose.Application.Permission.Internal.V10.Queries;
using WildGoose.Domain.Entity;
using WildGoose.Infrastructure;

namespace WildGoose.Application.Permission.Internal.V10;

public class PermissionService(
    WildGooseDbContext dbContext,
    HttpSession session,
    IOptions<DbOptions> dbOptions,
    IMemoryCache memoryCache,
    ILogger<PermissionService> logger)
    : BaseService(dbContext, session, dbOptions, logger)
{
    private static readonly Random Random = new();

    public async Task<bool> EnforceAsync(EnforceQuery query)
    {
        var roles = Session.Roles;
        if (roles == null || roles.Count == 0)
        {
            return false;
        }

        var effect = Effect.Allow.Equals(query.PolicyEffect, StringComparison.OrdinalIgnoreCase)
            ? Effect.Allow
            : Effect.Deny.Equals(query.PolicyEffect, StringComparison.OrdinalIgnoreCase)
                ? Effect.Deny
                : null;
        if (string.IsNullOrEmpty(effect))
        {
            Logger.LogError("PolicyEffect {PolicyEffect} is invalid", effect);
            return false;
        }

        var policies = new List<List<Statement>>();
        foreach (var role in Session.Roles)
        {
            var statements = await memoryCache.GetOrCreateAsync($"PermissionService.Role.{role}",
                async entry =>
                {
                    var json = await DbContext
                        .Set<WildGoose.Domain.Entity.Role>()
                        .AsNoTracking()
                        .Where(x => x.NormalizedName == role.ToUpperInvariant())
                        .Select(x => x.Statement)
                        .FirstOrDefaultAsync();
                    var statements = !string.IsNullOrEmpty(json)
                        ? JsonSerializer.Deserialize<List<Statement>>(json)
                        : new List<Statement>();

                    entry.SetValue(statements);
                    entry.SetAbsoluteExpiration(TimeSpan.FromSeconds(Random.Next(60, 120)));
                    return statements;
                });
            // 若授权模式是所有角色都需要有授权， 因此即使是空也应该要添加到列表中
            policies.Add(statements ?? new List<Statement>());
        }

        return Assert(policies, effect, query.Action, query.Resource);
    }

    private bool Assert(List<List<Statement>> policies, string effect, string action, string resource)
    {
        var containTrue = false;
        var containFalse = false;
        foreach (var statements in policies)
        {
            var assert = Enforce(statements, effect, action, resource);
            if (!assert.HasValue)
            {
                continue;
            }

            if (assert.Value)
            {
                containTrue = true;
            }
            else
            {
                containFalse = true;
            }
        }

        return !containFalse && containTrue;
    }

    private bool? Enforce(List<Statement> statements, string policyEffect, string action, string resource)
    {
        if (!statements.Any())
        {
            return null;
        }

        var anyAllow = false;
        var anyDeny = false;
        var emptyEffect = 0;

        foreach (var effect in statements.Select(s => s.Assert(action, resource)))
        {
            if (Equals(effect, Effect.Allow))
            {
                if (!anyAllow)
                {
                    anyAllow = true;
                }
            }

            else if (Equals(effect, Effect.Deny))
            {
                if (!anyDeny)
                {
                    anyDeny = true;
                }
            }
            else
            {
                emptyEffect++;
            }
        }

        if (emptyEffect == statements.Count)
        {
            return null;
        }

        if (Equals(policyEffect, Effect.Allow))
        {
            return anyAllow;
        }

        if (Equals(policyEffect, Effect.Deny))
        {
            return anyDeny;
        }

        return anyAllow && !anyDeny;
    }
}