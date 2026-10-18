using Labaik.Domain.Common.Results;
using Labaik.Domain.HelpCenter;
using Labaik.Domain.HelpCenter.Events;

namespace Labaik.Domain.Tests;

public class LostChildReportTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

    private static Result<LostChildReport> Report(int age = 6, string description = "Red shirt", string? photo = null) =>
        LostChildReport.Create(Guid.NewGuid(), age, description, photo, 21.42, 39.82, null, true, Now);

    [Fact]
    public void Create_validates_and_raises_event()
    {
        Assert.IsType<LostChildReported>(Assert.Single(Report().Value.DomainEvents));
        Assert.Equal("LostChild.InvalidAge", Report(age: 18).Error!.Code);
        Assert.Equal("LostChild.DescriptionRequired", Report(description: " ").Error!.Code);
        Assert.Equal("LostChild.InvalidPhotoUrl", Report(photo: "http://insecure.example.com/a.jpg").Error!.Code);
    }

    [Fact]
    public void Only_the_reporter_marks_found()
    {
        var report = Report().Value;

        Assert.Equal("LostChild.NotReporter", report.MarkFound(Guid.NewGuid(), Now).Error!.Code);
        Assert.True(report.MarkFound(report.ReporterId, Now).IsSuccess);
        Assert.True(report.MarkFound(report.ReporterId, Now.AddHours(1)).IsSuccess);
        Assert.Equal(LostChildReportStatus.Found, report.Status);
        Assert.Equal(Now, report.FoundAtUtc);
    }
}
