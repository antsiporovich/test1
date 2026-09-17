using Bogus;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Domain.Rules;
using PropertyManagement.Infrastructure.Identity;

namespace PropertyManagement.Infrastructure.Data.Seed;

/// <summary>
/// Idempotent startup seeder: roles, lookups, PMs, applicants, properties/units, and
/// applications in every status with their history/lease rows (Assesment.md 2.b.ii).
/// Every block is guarded by existence so re-running on every app start never
/// duplicates data; each "shared" reference value below is fixed so the demo is
/// reproducible across runs and machines.
/// </summary>
public class DbSeeder(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    ILogger<DbSeeder> logger)
{
    public const string DemoPassword = "Demo#12345";

    private static readonly string[] Roles = ["Applicant", "PropertyManager"];

    public async Task SeedAsync()
    {
        await SeedRolesAsync();

        var pmIds = await SeedPropertyManagersAsync();
        var applicantIds = await SeedApplicantsAsync();
        var unitTypes = await SeedUnitTypesAsync();
        var units = await SeedPropertiesAndUnitsAsync(unitTypes);

        await SeedApplicationsAsync(pmIds, applicantIds, units);
    }

    private async Task SeedRolesAsync()
    {
        foreach (var role in Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
                logger.LogInformation("Seeded role {Role}.", role);
            }
        }
    }

    private async Task<ApplicationUser> GetOrCreateUserAsync(string email, string displayName, string role)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            return existing;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = displayName,
        };

        var result = await userManager.CreateAsync(user, DemoPassword);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to seed user {email}: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }

        await userManager.AddToRoleAsync(user, role);
        logger.LogInformation("Seeded {Role} user {Email}.", role, email);
        return user;
    }

    private async Task<List<string>> SeedPropertyManagersAsync()
    {
        var pm1 = await GetOrCreateUserAsync("pm1@demo.local", "Jordan Blake", "PropertyManager");
        var pm2 = await GetOrCreateUserAsync("pm2@demo.local", "Casey Nguyen", "PropertyManager");
        return [pm1.Id, pm2.Id];
    }

    private async Task<List<string>> SeedApplicantsAsync()
    {
        (string Email, string Name)[] definitions =
        [
            ("applicant1@demo.local", "Alex Morgan"),
            ("applicant2@demo.local", "Taylor Reed"),
            ("applicant3@demo.local", "Jamie Chen"),
            ("applicant4@demo.local", "Morgan Lee"),
        ];

        var ids = new List<string>();
        foreach (var (email, name) in definitions)
        {
            var user = await GetOrCreateUserAsync(email, name, "Applicant");
            ids.Add(user.Id);
        }

        return ids;
    }

    private async Task<Dictionary<string, UnitType>> SeedUnitTypesAsync()
    {
        (string Name, bool IsActive)[] definitions =
        [
            ("Studio", true),
            ("One Bedroom", true),
            ("Two Bedroom", true),
            ("Three Bedroom", true),
            ("Loft", false), // inactive: still valid on units already using it, not selectable for others
        ];

        var result = new Dictionary<string, UnitType>();
        var anyNew = false;

        foreach (var (name, isActive) in definitions)
        {
            var unitType = await db.UnitTypes.FirstOrDefaultAsync(t => t.Name == name);
            if (unitType is null)
            {
                unitType = new UnitType { Name = name, IsActive = isActive };
                db.UnitTypes.Add(unitType);
                anyNew = true;
            }

            result[name] = unitType;
        }

        if (anyNew)
        {
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded unit type lookups.");
        }

        return result;
    }

    private record UnitDefinition(string UnitNumber, int Bedrooms, int Bathrooms, decimal MonthlyRent, string UnitTypeName, bool IsActive = true);

    private async Task<List<Unit>> SeedPropertiesAndUnitsAsync(Dictionary<string, UnitType> unitTypes)
    {
        var faker = new Faker { Random = new Randomizer(20260101) };

        (string Name, UnitDefinition[] Units)[] definitions =
        [
            ("Maple Grove Apartments",
            [
                new("101", 0, 1, 950m, "Studio"),
                new("102", 1, 1, 1200m, "One Bedroom"),
                new("103", 1, 1, 1250m, "Loft"), // grandfathered onto the now-inactive "Loft" type
                new("104", 2, 1, 1500m, "Two Bedroom", IsActive: false), // soft-deleted demo unit
            ]),
            ("Riverside Commons",
            [
                new("201", 2, 1, 1550m, "Two Bedroom"),
                new("202", 1, 1, 1225m, "One Bedroom"),
                new("203", 3, 2, 1850m, "Three Bedroom"),
                new("204", 0, 1, 975m, "Studio"),
            ]),
            ("Downtown Lofts",
            [
                new("301", 1, 1, 1600m, "Loft"),
                new("302", 2, 1, 1700m, "Two Bedroom"),
                new("303", 0, 1, 1100m, "Studio"),
                new("304", 1, 1, 1350m, "One Bedroom"),
            ]),
            ("Cedar Hill Residences",
            [
                new("401", 3, 2, 1950m, "Three Bedroom"),
                new("402", 2, 1, 1600m, "Two Bedroom"),
                new("403", 1, 1, 1275m, "One Bedroom"),
                new("404", 0, 1, 999m, "Studio"),
            ]),
            ("Willow Park Flats",
            [
                new("501", 1, 1, 1300m, "One Bedroom"),
                new("502", 2, 1, 1625m, "Two Bedroom"),
                new("503", 3, 2, 1900m, "Three Bedroom"),
                new("504", 1, 1, 1225m, "One Bedroom"),
            ]),
        ];

        var units = new List<Unit>();
        var anyNew = false;

        foreach (var (propertyName, unitDefs) in definitions)
        {
            var property = await db.Properties.FirstOrDefaultAsync(p => p.Name == propertyName);
            if (property is null)
            {
                property = new Property
                {
                    Name = propertyName,
                    AddressLine1 = faker.Address.StreetAddress(),
                    City = faker.Address.City(),
                    State = faker.Address.StateAbbr(),
                    ZipCode = faker.Address.ZipCode(),
                    IsActive = true,
                };
                db.Properties.Add(property);
                await db.SaveChangesAsync(); // need property.Id for the unit lookups below
                anyNew = true;
            }

            foreach (var def in unitDefs)
            {
                var unit = await db.Units.FirstOrDefaultAsync(u => u.PropertyId == property.Id && u.UnitNumber == def.UnitNumber);
                if (unit is null)
                {
                    unit = new Unit
                    {
                        PropertyId = property.Id,
                        UnitNumber = def.UnitNumber,
                        Bedrooms = def.Bedrooms,
                        Bathrooms = def.Bathrooms,
                        MonthlyRent = def.MonthlyRent,
                        UnitTypeId = unitTypes[def.UnitTypeName].Id,
                        IsActive = def.IsActive,
                    };
                    db.Units.Add(unit);
                    anyNew = true;
                }

                units.Add(unit);
            }
        }

        if (anyNew)
        {
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded {Count} properties/units.", definitions.Length);
        }

        return units;
    }

    /// <summary>
    /// Builds one application graph (Applicant Info, Applicants, optional Residences)
    /// via navigation properties only — FKs are fixed up by EF on SaveChanges, so the
    /// whole batch in <see cref="SeedApplicationsAsync"/> commits (or fails) atomically
    /// in a single call.
    /// </summary>
    private Application BuildApplication(Unit unit, Faker faker, IEnumerable<string> applicantUserIds, DateTimeOffset createdAt)
    {
        var application = new Application
        {
            Unit = unit,
            Status = ApplicationStatus.Draft,
            CreatedAtUtc = createdAt,
            ApplicantInfo = new ApplicantInfo
            {
                FullName = faker.Name.FullName(),
                Phone = faker.Phone.PhoneNumber("###-###-####"),
                Email = faker.Internet.Email(),
                AddressLine1 = faker.Address.StreetAddress(),
                City = faker.Address.City(),
                State = faker.Address.StateAbbr(),
                ZipCode = faker.Address.ZipCode(),
                DateOfBirth = DateOnly.FromDateTime(faker.Date.Past(40, DateTime.UtcNow.AddYears(-21))),
                Employment = faker.Name.JobTitle(),
                AnnualIncome = faker.Finance.Amount(35000, 120000, 0),
                DesiredMoveInDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(faker.Random.Int(1, 4))),
                UpdatedAtUtc = createdAt,
            },
        };

        foreach (var userId in applicantUserIds)
        {
            application.Applicants.Add(new ApplicationApplicant { UserId = userId, AddedAtUtc = createdAt });
        }

        db.Applications.Add(application);
        return application;
    }

    private static void AddResidences(Application application, Faker faker, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var moveIn = faker.Date.PastOffset(5).UtcDateTime;
            var isCurrent = i == 0; // most recent entry is the current residence (no move-out yet)

            application.Residences.Add(new Residence
            {
                AddressLine1 = faker.Address.StreetAddress(),
                City = faker.Address.City(),
                State = faker.Address.StateAbbr(),
                ZipCode = faker.Address.ZipCode(),
                LandlordName = faker.Name.FullName(),
                LandlordPhone = faker.Phone.PhoneNumber("###-###-####"),
                MoveInDate = DateOnly.FromDateTime(moveIn),
                MoveOutDate = isCurrent ? null : DateOnly.FromDateTime(moveIn.AddMonths(10)),
                MonthlyRent = faker.Finance.Amount(800, 2000, 0),
                ReasonForLeaving = isCurrent ? null : faker.PickRandom("Relocating for work", "Need more space", "Lease ended"),
            });
        }
    }

    private static void TransitionStatus(Application application, ApplicationStatus toStatus, string actorUserId, DateTimeOffset when, string? comment = null)
    {
        application.StatusHistory.Add(new ApplicationStatusHistory
        {
            FromStatus = application.Status,
            ToStatus = toStatus,
            ActorUserId = actorUserId,
            Timestamp = when,
            Comment = comment,
        });
        application.Status = toStatus;
    }

    /// <summary>
    /// One application per status (Draft ×2, Submitted ×5, Under Review ×3, Returned,
    /// Approved ×3 covering the today/past/future lease cases, Denied, Withdrawn),
    /// plus a multi-applicant application (Bonus 5) and a PM note (Bonus 3). The extra
    /// Submitted/Under Review rows beyond the first of each just give the Bonus 2
    /// review queue and Bonus 1 grid enough depth to demo claim/release and paging.
    /// Guarded as a single all-or-nothing block: everything here is added to one
    /// change tracker and saved in one <c>SaveChangesAsync</c> call, so a failure
    /// partway through leaves nothing behind for the "any applications exist" check
    /// below to misinterpret as already-seeded, and — critically — nothing here ever
    /// runs again nor touches a row it didn't just create, so a real applicant's own
    /// Draft can never be silently rewritten or force-submitted by a later restart.
    /// </summary>
    private async Task SeedApplicationsAsync(List<string> pmIds, List<string> applicantIds, List<Unit> units)
    {
        if (await db.Applications.AnyAsync())
        {
            return;
        }

        var faker = new Faker { Random = new Randomizer(20260103) };
        var now = DateTimeOffset.UtcNow;
        var pm1 = pmIds[0];
        var pm2 = pmIds[1];

        // 1) Draft — only Applicant Information saved so far; Residence History not yet visited.
        BuildApplication(units[0], faker, [applicantIds[0]], now.AddDays(-2));

        // 2) Draft — both sections saved (incl. zero-is-valid-but-here-non-zero residences), not yet submitted.
        var draft2 = BuildApplication(units[1], faker, [applicantIds[1]], now.AddDays(-3));
        AddResidences(draft2, faker, count: 1);
        draft2.ResidenceHistoryConfirmedAtUtc = now.AddDays(-3);

        // 3) Submitted — zero residences (demonstrates the "zero is valid" rule), two applicants (Bonus 5).
        var submitted = BuildApplication(units[2], faker, [applicantIds[0], applicantIds[1]], now.AddDays(-5));
        submitted.ResidenceHistoryConfirmedAtUtc = now.AddDays(-5);
        TransitionStatus(submitted, ApplicationStatus.Submitted, applicantIds[0], now.AddDays(-4));
        submitted.SubmittedAtUtc = now.AddDays(-4);

        // 4) Under Review (Bonus 2) — claimed by pm1; carries a PM-only note (Bonus 3).
        var underReview = BuildApplication(units[4], faker, [applicantIds[2]], now.AddDays(-6));
        AddResidences(underReview, faker, count: 2);
        underReview.ResidenceHistoryConfirmedAtUtc = now.AddDays(-6);
        TransitionStatus(underReview, ApplicationStatus.Submitted, applicantIds[2], now.AddDays(-5));
        underReview.SubmittedAtUtc = now.AddDays(-5);
        TransitionStatus(underReview, ApplicationStatus.UnderReview, pm1, now.AddDays(-4));
        underReview.ClaimedByUserId = pm1;
        underReview.ClaimedAtUtc = now.AddDays(-4);
        underReview.Notes.Add(new ApplicationNote
        {
            AuthorUserId = pm1,
            Body = "Verifying employment letter with applicant's employer before deciding.",
            CreatedAt = now.AddDays(-4),
        });

        // 5) Returned — with the PM's required comment; editable again by the applicant.
        var returned = BuildApplication(units[5], faker, [applicantIds[3]], now.AddDays(-10));
        AddResidences(returned, faker, count: 1);
        returned.ResidenceHistoryConfirmedAtUtc = now.AddDays(-10);
        TransitionStatus(returned, ApplicationStatus.Submitted, applicantIds[3], now.AddDays(-9));
        returned.SubmittedAtUtc = now.AddDays(-9);
        TransitionStatus(returned, ApplicationStatus.Returned, pm2, now.AddDays(-8),
            "Please add your two most recent residences with landlord phone numbers.");

        // 6) Approved — lease covers today, so its unit is currently unavailable.
        var approvedCurrent = BuildApplication(units[6], faker, [applicantIds[0]], now.AddDays(-40));
        AddResidences(approvedCurrent, faker, count: 2);
        approvedCurrent.ResidenceHistoryConfirmedAtUtc = now.AddDays(-40);
        TransitionStatus(approvedCurrent, ApplicationStatus.Submitted, applicantIds[0], now.AddDays(-35));
        approvedCurrent.SubmittedAtUtc = now.AddDays(-35);
        TransitionStatus(approvedCurrent, ApplicationStatus.Approved, pm1, now.AddDays(-30), "Approved — strong rental history.");
        var currentStart = DateOnly.FromDateTime(now.AddDays(-15).UtcDateTime);
        approvedCurrent.Lease = new Lease
        {
            Unit = units[6],
            StartDate = currentStart,
            EndDate = currentStart.AddMonths(12),
            CreatedAtUtc = now.AddDays(-30),
        };

        // 7) Approved — lease fully in the past, so its unit is available again.
        var approvedPast = BuildApplication(units[7], faker, [applicantIds[1]], now.AddMonths(-15));
        AddResidences(approvedPast, faker, count: 1);
        approvedPast.ResidenceHistoryConfirmedAtUtc = now.AddMonths(-15);
        TransitionStatus(approvedPast, ApplicationStatus.Submitted, applicantIds[1], now.AddMonths(-15).AddDays(3));
        approvedPast.SubmittedAtUtc = now.AddMonths(-15).AddDays(3);
        TransitionStatus(approvedPast, ApplicationStatus.Approved, pm2, now.AddMonths(-15).AddDays(5), "Approved.");
        var pastStart = DateOnly.FromDateTime(now.AddMonths(-14).UtcDateTime);
        approvedPast.Lease = new Lease
        {
            Unit = units[7],
            StartDate = pastStart,
            EndDate = pastStart.AddMonths(12),
            CreatedAtUtc = now.AddMonths(-15).AddDays(5),
        };

        // 8) Approved — lease starts in the future, so its unit is still available now.
        var approvedFuture = BuildApplication(units[8], faker, [applicantIds[2]], now.AddDays(-20));
        AddResidences(approvedFuture, faker, count: 1);
        approvedFuture.ResidenceHistoryConfirmedAtUtc = now.AddDays(-20);
        TransitionStatus(approvedFuture, ApplicationStatus.Submitted, applicantIds[2], now.AddDays(-18));
        approvedFuture.SubmittedAtUtc = now.AddDays(-18);
        TransitionStatus(approvedFuture, ApplicationStatus.Approved, pm1, now.AddDays(-15), "Approved.");
        var futureStart = DateOnly.FromDateTime(now.AddDays(20).UtcDateTime);
        approvedFuture.Lease = new Lease
        {
            Unit = units[8],
            StartDate = futureStart,
            EndDate = futureStart.AddMonths(12),
            CreatedAtUtc = now.AddDays(-15),
        };

        // 9) Denied — terminal, with the required comment.
        var denied = BuildApplication(units[9], faker, [applicantIds[3]], now.AddDays(-25));
        AddResidences(denied, faker, count: 2);
        denied.ResidenceHistoryConfirmedAtUtc = now.AddDays(-25);
        TransitionStatus(denied, ApplicationStatus.Submitted, applicantIds[3], now.AddDays(-22));
        denied.SubmittedAtUtc = now.AddDays(-22);
        TransitionStatus(denied, ApplicationStatus.Denied, pm2, now.AddDays(-20), "Insufficient verifiable rental history.");

        // 10) Withdrawn — terminal; applicant changed their mind after submitting.
        var withdrawn = BuildApplication(units[10], faker, [applicantIds[0]], now.AddDays(-12));
        AddResidences(withdrawn, faker, count: 1);
        withdrawn.ResidenceHistoryConfirmedAtUtc = now.AddDays(-12);
        TransitionStatus(withdrawn, ApplicationStatus.Submitted, applicantIds[0], now.AddDays(-11));
        withdrawn.SubmittedAtUtc = now.AddDays(-11);
        TransitionStatus(withdrawn, ApplicationStatus.Withdrawn, applicantIds[0], now.AddDays(-9));

        // 11-13) Extra Submitted — Bonus 2 review queue / Bonus 1 grid paging depth.
        var submitted2 = BuildApplication(units[11], faker, [applicantIds[1]], now.AddDays(-7));
        submitted2.ResidenceHistoryConfirmedAtUtc = now.AddDays(-7);
        TransitionStatus(submitted2, ApplicationStatus.Submitted, applicantIds[1], now.AddDays(-6));
        submitted2.SubmittedAtUtc = now.AddDays(-6);

        var submitted3 = BuildApplication(units[12], faker, [applicantIds[2]], now.AddDays(-8));
        submitted3.ResidenceHistoryConfirmedAtUtc = now.AddDays(-8);
        TransitionStatus(submitted3, ApplicationStatus.Submitted, applicantIds[2], now.AddDays(-7));
        submitted3.SubmittedAtUtc = now.AddDays(-7);

        var submitted4 = BuildApplication(units[13], faker, [applicantIds[3]], now.AddDays(-9));
        submitted4.ResidenceHistoryConfirmedAtUtc = now.AddDays(-9);
        TransitionStatus(submitted4, ApplicationStatus.Submitted, applicantIds[3], now.AddDays(-8));
        submitted4.SubmittedAtUtc = now.AddDays(-8);

        // 14-15) Extra Under Review — one per PM, so both claim/release paths demo immediately.
        var underReview2 = BuildApplication(units[14], faker, [applicantIds[0]], now.AddDays(-11));
        underReview2.ResidenceHistoryConfirmedAtUtc = now.AddDays(-11);
        TransitionStatus(underReview2, ApplicationStatus.Submitted, applicantIds[0], now.AddDays(-10));
        underReview2.SubmittedAtUtc = now.AddDays(-10);
        TransitionStatus(underReview2, ApplicationStatus.UnderReview, pm2, now.AddDays(-9));
        underReview2.ClaimedByUserId = pm2;
        underReview2.ClaimedAtUtc = now.AddDays(-9);

        var underReview3 = BuildApplication(units[15], faker, [applicantIds[1]], now.AddDays(-13));
        underReview3.ResidenceHistoryConfirmedAtUtc = now.AddDays(-13);
        TransitionStatus(underReview3, ApplicationStatus.Submitted, applicantIds[1], now.AddDays(-12));
        underReview3.SubmittedAtUtc = now.AddDays(-12);
        TransitionStatus(underReview3, ApplicationStatus.UnderReview, pm1, now.AddDays(-11));
        underReview3.ClaimedByUserId = pm1;
        underReview3.ClaimedAtUtc = now.AddDays(-11);

        // 16) One more Submitted, rounding the queue out to 8 rows total.
        var submitted5 = BuildApplication(units[16], faker, [applicantIds[2]], now.AddDays(-14));
        submitted5.ResidenceHistoryConfirmedAtUtc = now.AddDays(-14);
        TransitionStatus(submitted5, ApplicationStatus.Submitted, applicantIds[2], now.AddDays(-13));
        submitted5.SubmittedAtUtc = now.AddDays(-13);

        await db.SaveChangesAsync();
        logger.LogInformation("Seeded demo applications covering every status.");
    }
}
