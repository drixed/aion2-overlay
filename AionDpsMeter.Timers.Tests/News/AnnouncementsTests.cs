using AionDpsMeter.Timers.News;

namespace AionDpsMeter.Timers.Tests.News;

public class AnnouncementsTests
{
    // The shape of NCSOFT's public board list (api-global-community.plaync.com/aion2_global/board/notice_en/article), trimmed.
    private const string List = """
    { "contentList": [
        { "id": "a1", "title": "[Notice] Scheduled Maintenance | Nov 20 (Completed)", "summary": "&nbsp;Hey, Daevas,&nbsp;the servers&amp;more",
          "timestamps": { "postedAt": "2026-11-19T22:00:25.293Z" },
          "categoryBoard": { "boardUrlPattern": "https://aion2.plaync.com/en-us/board/notice/view?articleId={articleId}" } },
        { "id": "a2", "title": "AION 2 Twitch Drops: War for Atreia", "summary": "Watch and earn", "timestamps": { "postedAt": "2026-11-18T10:00:00Z" } },
        { "id": "a3", "title": "Compensation for the Server Outage", "summary": "", "timestamps": { "postedAt": "2026-11-17T10:00:00Z" } },
        { "id": "a4", "title": "Account Restrictions | Nov 19 (14:00 PST)", "summary": "", "timestamps": { "postedAt": "2026-11-16T10:00:00Z" } },
        { "id": "a5", "title": "Patch Notes 1.2", "summary": "", "timestamps": { "postedAt": "not a date" } },
        { "title": "no id" }
      ], "hasMore": true }
    """;

    [Fact]
    public void The_board_list_reads_newest_first_with_a_kind_and_a_link()
    {
        var items = Announcements.Parse(List);
        Assert.Equal(["a1", "a2", "a3", "a4"], items.Select(i => i.Id)); // no date or no id: left out
        var first = items[0];
        Assert.Equal("Scheduled Maintenance | Nov 20", first.Title);
        Assert.Equal(NewsKind.Maintenance, first.Kind);
        Assert.True(first.Done);
        Assert.Equal("Hey, Daevas, the servers&more", first.Summary);
        Assert.Equal(new DateTimeOffset(2026, 11, 19, 22, 0, 25, 293, TimeSpan.Zero), first.Posted);
        Assert.Equal("https://aion2.plaync.com/en-us/board/notice/view?articleId=a1", first.Url);
        Assert.Equal("https://aion2.plaync.com/en-us/board/notice/view?articleId=a2", items[1].Url); // the pattern is known anyway
        Assert.Equal(NewsKind.TwitchDrops, items[1].Kind);
        Assert.Equal(NewsKind.Compensation, items[2].Kind); // "compensation for the outage" is a compensation
        Assert.Equal(NewsKind.Account, items[3].Kind);
        Assert.False(items[3].Done);
    }

    [Fact]
    public void Broken_text_reads_as_nothing()
    {
        Assert.Empty(Announcements.Parse("not json"));
        Assert.Empty(Announcements.Parse("{}"));
    }

    [Fact]
    public void Every_kind_has_a_russian_label()
    {
        Assert.All(Enum.GetValues<NewsKind>(), k => Assert.False(string.IsNullOrEmpty(Announcements.Label(k))));
        Assert.Equal("Тех. работы", Announcements.Label(NewsKind.Maintenance));
        Assert.Equal("Прочее", Announcements.Label(NewsKind.Other));
    }

    [Fact]
    public void The_archive_keeps_older_notices_and_takes_the_newer_text_of_the_same_one()
    {
        var at = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var kept = new[] { new Announcement("a", "Maintenance", "", at, NewsKind.Maintenance, false, "u"), new Announcement("old", "Old", "", at.AddDays(-30), NewsKind.Other, false, "u") };
        var fresh = new[] { new Announcement("a", "Maintenance", "", at, NewsKind.Maintenance, true, "u"), new Announcement("b", "New", "", at.AddDays(1), NewsKind.Other, false, "u") };
        var merged = Announcements.Merge(kept, fresh);
        Assert.Equal(["b", "a", "old"], merged.Select(i => i.Id));
        Assert.True(merged[1].Done);
    }

    [Fact]
    public void Older_pages_and_bodies_have_their_addresses()
    {
        Assert.EndsWith("/article/search/moreArticle?isVote=true&moreSize=18&moreDirection=BEFORE&previousArticleId=x1", Announcements.MoreUrl("x1"));
        Assert.EndsWith("/article/x1", Announcements.ArticleUrl("x1"));
        Assert.Equal("<p>hi</p>", Announcements.ArticleHtml("""{"article":{"content":{"content":"<p>hi</p>"}}}"""));
        Assert.Equal("", Announcements.ArticleHtml("nope"));
        Assert.True(Announcements.HasMore("""{"contentList":[],"hasMore":true}"""));
        Assert.False(Announcements.HasMore("{}"));
    }
}
