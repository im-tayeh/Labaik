using Labaik.Domain.Profiles;
using Labaik.Domain.Profiles.Enums;

namespace Labaik.Domain.Tests;

public class PilgrimProfileTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

    private static PilgrimProfile New() => PilgrimProfile.Create(Guid.NewGuid()).Value;

    [Fact]
    public void Update_normalizes_values()
    {
        var profile = New();

        Assert.True(profile.Update(Gender.Female, " jo ", " https://cdn.example.com/p.jpg ", " Diabetic ", Now).IsSuccess);

        Assert.Equal("JO", profile.Nationality);
        Assert.Equal("https://cdn.example.com/p.jpg", profile.PhotoUrl);
        Assert.Equal("Diabetic", profile.HealthNotes);
    }

    [Theory]
    [InlineData("JOR", null, "Profile.InvalidNationality")]
    [InlineData("J1", null, "Profile.InvalidNationality")]
    [InlineData(null, "http://insecure.example.com/p.jpg", "Profile.InvalidPhotoUrl")]
    [InlineData(null, "not a url", "Profile.InvalidPhotoUrl")]
    public void Update_rejects_invalid_values_without_changing_anything(string? nationality, string? photoUrl, string expectedError)
    {
        var profile = New();
        profile.Update(Gender.Male, "SA", null, "Asthma", Now);

        var result = profile.Update(Gender.Female, nationality, photoUrl, "Changed", Now);

        Assert.Equal(expectedError, result.Error!.Code);
        Assert.Equal("Asthma", profile.HealthNotes);
        Assert.Equal(Gender.Male, profile.Gender);
    }

    [Fact]
    public void Null_clears_a_field()
    {
        var profile = New();
        profile.Update(Gender.Male, "SA", null, "Asthma", Now);

        profile.Update(Gender.Male, "SA", null, null, Now);

        Assert.Null(profile.HealthNotes);
    }
}
