using NpiManagement.Domain;
using NpiManagement.Domain.Common;
using NpiManagement.Domain.ValueObjects;
using NpiManagement.Infrastructure.Identity;
using NpiManagement.Infrastructure.Persistence;

namespace NpiManagement.IntegrationTests;

public static class TestDataFactory
{
    public static readonly IBusinessCalendar Calendar = new MexicoBusinessCalendar();

    public static ApplicationUser NewUser(string emailPrefix, AreaType area)
    {
        var id = Guid.NewGuid();
        var email = $"{emailPrefix}-{id:N}@test.local";
        return new ApplicationUser
        {
            Id = id,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            FullName = emailPrefix,
            Area = area,
            Status = UserStatus.Active,
            RegisteredAt = DateTimeOffset.UtcNow,
        };
    }

    public static async Task<ApplicationUser> AddUserAsync(NpiManagementDbContext db, string emailPrefix, AreaType area)
    {
        var user = NewUser(emailPrefix, area);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public static Npi NewDraft(Actor actor, IClock clock) =>
        Npi.CreateDraft(Guid.NewGuid(), actor, clock, NpiCaptureData.Empty with { Cliente = "Cliente Integración", NumeroCotizacion = "1001" }, Priority.Media);

    public static Actor ActorFor(ApplicationUser user, string role) => new(user.Id, role, user.FullName);
}
