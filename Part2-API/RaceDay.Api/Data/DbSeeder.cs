using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Data;

/// <summary>Seeds demo data in Development only, so Swagger can be demonstrated straight away.</summary>
public static class DbSeeder
{
    public static void Seed(RaceDayDbContext db, IPasswordHasher hasher)
    {
        if (db.Users.Any()) return;

        var organiser = new AppUser
        {
            FullName = "Thandeka Nkosi",
            Email = "organiser@raceday.test",
            PasswordHash = hasher.Hash("Password123!"),
            Role = UserRole.Organiser,
            PhoneNumber = "0821234567"
        };
        var participant = new AppUser
        {
            FullName = "Malusi Mathonsi",
            Email = "participant@raceday.test",
            PasswordHash = hasher.Hash("Password123!"),
            Role = UserRole.Participant,
            PhoneNumber = "0731112222"
        };
        db.Users.AddRange(organiser, participant);
        db.SaveChanges();

        var ev = new RaceEvent
        {
            OrganiserId = organiser.UserId,
            Name = "Durban Beachfront Marathon",
            Description = "Annual race along the Durban beachfront promenade.",
            EventDate = DateOnly.FromDateTime(DateTime.Today.AddDays(60)),
            Location = "Durban, KZN",
            DistanceKm = 21.1m,
            EventType = EventType.Run,
            Status = "Open"
        };
        ev.Categories.Add(new Category { Name = "10km", DistanceKm = 10m, MaxParticipants = 500, Fee = 150m, StartTime = new TimeOnly(6, 0) });
        ev.Categories.Add(new Category { Name = "21km", DistanceKm = 21.1m, MaxParticipants = 300, Fee = 250m, StartTime = new TimeOnly(5, 30) });
        ev.Categories.Add(new Category { Name = "Under 20", DistanceKm = 5m, MaxParticipants = 200, Fee = 80m, StartTime = new TimeOnly(8, 0) });
        db.Events.Add(ev);
        db.SaveChanges();
    }
}
