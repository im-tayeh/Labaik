using Labaik.Domain.Announcements;
using Labaik.Domain.Groups;
using Labaik.Domain.Guides;
using Labaik.Domain.Guides.Enums;
using Labaik.Domain.Journeys.Enums;
using Labaik.Domain.Notifications;
using Labaik.Domain.Notifications.Enums;
using Labaik.Domain.Rituals;
using Labaik.Domain.Rituals.Enums;

namespace Labaik.Domain.Tests;

public class ContentAndMessagingTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RitualStep_validates_and_trims_content()
    {
        Assert.Equal("Ritual.InvalidOrder",
            RitualStep.Create(PilgrimageType.Umrah, 0, RitualPhase.Ihram, RitualStepKind.Pillar, "T", "B", null, true).Error!.Code);
        Assert.Equal("Ritual.BodyRequired",
            RitualStep.Create(PilgrimageType.Umrah, 1, RitualPhase.Ihram, RitualStepKind.Pillar, "T", " ", null, true).Error!.Code);

        var step = RitualStep.Create(PilgrimageType.Umrah, 1, RitualPhase.Ihram, RitualStepKind.Pillar, " T ", " B ", "  ", true).Value;
        Assert.Equal("T", step.Title);
        Assert.Null(step.Reference);

        step.Unpublish();
        Assert.False(step.IsPublished);
    }

    [Fact]
    public void GuideArticle_validates_content()
    {
        Assert.Equal("Guide.TitleRequired",
            GuideArticle.Create(GuideCategory.IhramPreparation, 1, "", "B", null, true).Error!.Code);
        Assert.True(GuideArticle.Create(GuideCategory.IhramPreparation, 1, "T", "B", "Ref", true).IsSuccess);
    }

    [Fact]
    public void Announcement_is_read_by_author_and_marking_read_is_idempotent()
    {
        var leaderId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var group = Group.Create("G", "CODE", leaderId, Now).Value;
        group.Join(memberId, Now);
        var announcement = group.PostAnnouncement(leaderId, new AnnouncementContent("T", "B"), Now).Value;

        Assert.True(announcement.IsReadBy(leaderId));
        Assert.False(announcement.IsReadBy(memberId));

        announcement.MarkReadBy(memberId, Now);
        announcement.MarkReadBy(memberId, Now.AddMinutes(1));

        Assert.True(announcement.IsReadBy(memberId));
        Assert.Equal(2, announcement.Reads.Count);
    }

    [Fact]
    public void Notification_truncates_preview_and_marks_read_once()
    {
        var n = Notification.Create(Guid.NewGuid(), NotificationType.AnnouncementPosted, Guid.NewGuid(), Guid.NewGuid(),
            new string('x', 300), Now).Value;

        Assert.Equal(Notification.PreviewMaxLength, n.Preview!.Length);
        Assert.False(n.IsRead);

        n.MarkRead(Now);
        n.MarkRead(Now.AddHours(1));

        Assert.Equal(Now, n.ReadAtUtc);
    }

    [Fact]
    public void Notification_requires_recipient()
    {
        Assert.True(Notification.Create(Guid.Empty, NotificationType.MemberJoinedGroup, null, null, null, Now).IsFailure);
    }
}
