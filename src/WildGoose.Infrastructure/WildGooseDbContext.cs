using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Options;
using WildGoose.Domain;
using WildGoose.Domain.Entity;
using WildGoose.Domain.Extensions;

namespace WildGoose.Infrastructure;

// 20231207124420_Init
public class WildGooseDbContext(DbContextOptions<WildGooseDbContext> options)
    : IdentityDbContext<User, Role, string>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        var options = this.GetService<IOptions<DbOptions>>().Value;

        builder.Entity<User>(b =>
        {
            b.ToTable(GetName(options, "user"));

            b.Property(x => x.Id).HasMaxLength(36).ValueGeneratedNever();
            b.Property(x => x.Name).HasMaxLength(256);
            b.Property(x => x.UserName).HasMaxLength(256);
            b.Property(x => x.GivenName).HasMaxLength(256);
            b.Property(x => x.FamilyName).HasMaxLength(256);
            b.Property(x => x.MiddleName).HasMaxLength(256);
            b.Property(x => x.NickName).HasMaxLength(256);
            b.Property(x => x.Picture).HasMaxLength(256);
            b.Property(x => x.Code).HasMaxLength(256);
            b.Property(x => x.Address).HasMaxLength(256);

            b.Property(x => x.CreationTime).UseUnixTime();
            b.Property(x => x.CreatorId).HasMaxLength(36);
            b.Property(x => x.CreatorName).HasMaxLength(256);
            b.Property(x => x.LastModificationTime).UseUnixTime();
            b.Property(x => x.LastModifierId).HasMaxLength(36);
            b.Property(x => x.LastModifierName).HasMaxLength(256);
            b.Property(x => x.IsDeleted).HasDefaultValue(false);
            b.Property(x => x.DeletionTime).UseUnixTime();
            b.Property(x => x.DeleterId).HasMaxLength(36);
            b.Property(x => x.DeleterName).HasMaxLength(256);

            b.HasQueryFilter(x => !x.IsDeleted);

            var userNameIndex = b.Metadata.GetIndexes().FirstOrDefault(x => x.GetDatabaseName() == "UserNameIndex");
            if (userNameIndex != null)
            {
                userNameIndex.IsUnique = false;
            }
        });

        builder.Entity<IdentityUserClaim<string>>(b =>
        {
            b.ToTable(GetName(options, "user_claim"));
            b.Property(x => x.UserId).HasMaxLength(36);
            b.Property(x => x.ClaimType).HasMaxLength(256);
            b.Property(x => x.ClaimValue).HasMaxLength(256);
        });
        builder.Entity<IdentityUserRole<string>>(b =>
        {
            b.ToTable(GetName(options, "user_role"));
            b.Property(x => x.UserId).HasMaxLength(36);
            b.Property(x => x.RoleId).HasMaxLength(36);
        });
        builder.Entity<IdentityUserLogin<string>>(b =>
        {
            b.ToTable(GetName(options, "user_login"));
            b.Property(x => x.LoginProvider).HasMaxLength(256);
            b.Property(x => x.ProviderKey).HasMaxLength(256);
            b.Property(x => x.ProviderDisplayName).HasMaxLength(256);
            b.Property(x => x.UserId).HasMaxLength(36);
        });
        builder.Entity<IdentityUserToken<string>>(b =>
        {
            b.ToTable(GetName(options, "user_token"));
            b.Property(x => x.UserId).HasMaxLength(36);
            b.Property(x => x.LoginProvider).HasMaxLength(256);
            b.Property(x => x.Name).HasMaxLength(256);
            b.Property(x => x.Value).HasMaxLength(256);
        });
        builder.Entity<Role>(b =>
        {
            b.ToTable(GetName(options, "role"));
            b.Property(x => x.Id).HasMaxLength(36).ValueGeneratedNever();
            b.Property(x => x.Name).HasMaxLength(256);
            b.Property(x => x.NormalizedName).HasMaxLength(256);
            b.Property(x => x.Description).HasMaxLength(256);
            b.Property(x => x.Version);
            b.Property(x => x.Statement).HasMaxLength(6000);

            b.Property(x => x.CreationTime).UseUnixTime();
            b.Property(x => x.CreatorId).HasMaxLength(36);
            b.Property(x => x.CreatorName).HasMaxLength(256);
            b.Property(x => x.LastModificationTime).UseUnixTime();
            b.Property(x => x.LastModifierId).HasMaxLength(36);
            b.Property(x => x.LastModifierName).HasMaxLength(256);
        });
        builder.Entity<IdentityRoleClaim<string>>(b =>
        {
            b.ToTable(GetName(options, "role_claim"));
            b.Property(x => x.RoleId).HasMaxLength(36);
            b.Property(x => x.ClaimType).HasMaxLength(256);
            b.Property(x => x.ClaimValue).HasMaxLength(256);
        });

        builder.Entity<UserExtension>(b =>
        {
            b.ToTable(GetName(options, "user_extension"));
            b.Property(x => x.Id).HasMaxLength(36).ValueGeneratedNever();
            b.Property(x => x.Title).HasMaxLength(256);
            b.Property(x => x.DepartureTime).UseUnixTime();
            b.Property(x => x.PasswordContainsDigit);
            b.Property(x => x.PasswordContainsLowercase);
            b.Property(x => x.PasswordContainsUppercase);
            b.Property(x => x.PasswordContainsNonAlphanumeric);
            b.Property(x => x.PasswordLength);
            b.Property(x => x.ResetPasswordFlag);
            b.Property(x => x.HiddenSensitiveData);
        });

        builder.Entity<RoleAssignableRole>(b =>
        {
            b.ToTable(GetName(options, "role_assignable_role"));
            b.Property(x => x.RoleId).HasMaxLength(36);
            b.Property(x => x.AssignableId).HasMaxLength(36);

            b.HasKey(x => new
            {
                x.RoleId,
                x.AssignableId
            });
        });

        builder.Entity<Organization>(b =>
        {
            b.ToTable(GetName(options, "organization"));
            b.Property(x => x.Id).HasMaxLength(36).ValueGeneratedNever();
            b.Property(x => x.Name).HasMaxLength(256);
            b.Property(x => x.Code).HasMaxLength(256);
            b.Property(x => x.Address).HasMaxLength(256);
            b.Property(x => x.Description).HasMaxLength(256);
            b.Property(x => x.Metadata).HasMaxLength(2000);
            b.Property(x => x.NId).ValueGeneratedOnAdd();
            // b.Property(x => x.Order);

            b.HasQueryFilter(x => !x.IsDeleted);

            b.Property(x => x.CreationTime).UseUnixTime();
            b.Property(x => x.CreatorId).HasMaxLength(36);
            b.Property(x => x.CreatorName).HasMaxLength(256);
            b.Property(x => x.LastModificationTime).UseUnixTime();
            b.Property(x => x.LastModifierId).HasMaxLength(36);
            b.Property(x => x.LastModifierName).HasMaxLength(256);
            b.Property(x => x.IsDeleted).HasDefaultValue(false);
            b.Property(x => x.DeletionTime).UseUnixTime();
            b.Property(x => x.DeleterId).HasMaxLength(36);
            b.Property(x => x.DeleterName).HasMaxLength(256);

            b.HasIndex(x => x.NId).IsUnique();
        });

        builder.Entity<OrganizationUser>(b =>
        {
            b.ToTable(GetName(options, "organization_user"));
            b.Property(x => x.OrganizationId).HasMaxLength(36);
            b.Property(x => x.UserId).HasMaxLength(36);

            b.HasKey(x => new
            {
                x.OrganizationId,
                x.UserId
            });
        });

        builder.Entity<OrganizationScope>(b =>
        {
            b.ToTable(GetName(options, "organization_scope"));
            b.Property(x => x.OrganizationId).HasMaxLength(36);
            b.Property(x => x.Scope).HasMaxLength(256);

            b.HasKey(x => new
            {
                x.OrganizationId,
                x.Scope
            });
        });

        builder.Entity<OrganizationAdministrator>(b =>
        {
            b.ToTable(GetName(options, "organization_administrator"));
            b.Property(x => x.OrganizationId).HasMaxLength(36);
            b.Property(x => x.UserId).HasMaxLength(36);

            b.HasKey(x => new
            {
                x.OrganizationId,
                x.UserId
            });
        });

        var entryName = Assembly.GetEntryAssembly()?.GetName().Name;
        if (!"ef".Equals(entryName, StringComparison.OrdinalIgnoreCase))
        {
            builder.Entity<OrganizationDetail>().ToTable($"{options.TablePrefix}organization_detail");
        }

        if (options.UseUnderScoreCase)
        {
            foreach (var entityType in builder.Model.GetEntityTypes())
            {
                var properties = entityType.GetProperties();
                foreach (var property in properties)
                {
                    var storeObjectIdentifier = StoreObjectIdentifier.Create(entityType, StoreObjectType.Table);
                    var propertyName = property.GetColumnName(storeObjectIdentifier.GetValueOrDefault());
                    if (string.IsNullOrEmpty(propertyName))
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(propertyName) && propertyName.StartsWith("_"))
                    {
                        propertyName = propertyName.Substring(1, propertyName.Length - 1);
                    }

                    property.SetColumnName(propertyName.ToSnakeCase());
                }
            }
        }
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = new())
    {
        ApplyConcepts();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyConcepts();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    private void ApplyConcepts()
    {
        var scope = this.GetService<ScopeServiceProvider>();
        var session = scope.GetService<ISession>();
        string userId;
        string userDisplayName;
        if (session != null)
        {
            userId = session.UserId;
            userDisplayName = session.UserDisplayName;
        }
        else
        {
            userId = null;
            userDisplayName = null;
        }


        foreach (var entry in ChangeTracker.Entries())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    ApplyConceptsForAddedEntity(entry, userId, userDisplayName);
                    break;
                case EntityState.Modified:
                    ApplyConceptsForModifiedEntity(entry, userId, userDisplayName);
                    break;
                case EntityState.Deleted:
                    ApplyConceptsForDeletedEntity(entry, userId, userDisplayName);
                    break;
                case EntityState.Detached:
                case EntityState.Unchanged:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    protected virtual void ApplyConceptsForAddedEntity(EntityEntry entry, string userId, string userName)
    {
        if (entry.Entity is ICreation entity)
        {
            entity.SetCreation(userId, userName);
        }
    }

    protected virtual void ApplyConceptsForModifiedEntity(EntityEntry entry, string userId, string userName)
    {
        if (entry.Entity is IModification entity)
        {
            entity.SetModification(userId, userName);
        }
    }

    protected virtual void ApplyConceptsForDeletedEntity(EntityEntry entry, string userId, string userName)
    {
        if (entry.Entity is not IDeletion entity)
        {
            return;
        }

        entry.Reload();
        entry.State = EntityState.Modified;
        entity.SetDeletion(userId, userName);
    }

    private string GetName(DbOptions dbOptions, string name)
    {
        var v = dbOptions.TableMapper == null ? name : dbOptions.TableMapper.GetValueOrDefault(name, name);
        return $"{dbOptions.TablePrefix}{v}";
    }
}