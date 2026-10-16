using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Labaik.Api.IntegrationTests.Infrastructure;

namespace Labaik.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ContentTests(LabaikApiFactory factory)
{
    private static object Complete(int order) => new { pilgrimageType = 1, stepOrder = order };

    [Fact]
    public async Task Seeded_umrah_steps_are_listed_in_order()
    {
        var user = await TestUsers.CreateAsync(factory);

        var steps = await user.Client.GetFromJsonAsync<JsonElement>("/api/v1/rituals/steps?pilgrimageType=Umrah");

        var orders = steps.EnumerateArray().Select(s => s.GetProperty("order").GetInt32()).ToList();
        Assert.Equal(10, orders.Count);
        Assert.Equal(orders.Order(), orders);
    }

    [Fact]
    public async Task Ritual_progress_advances_in_order_until_completed_and_can_reset()
    {
        var user = await TestUsers.CreateAsync(factory);

        var start = await user.Client.GetFromJsonAsync<JsonElement>("/api/v1/rituals/progress");
        Assert.Equal(1, start.GetProperty("currentOrder").GetInt32());

        var outOfOrder = await user.Client.PostAsJsonAsync("/api/v1/rituals/progress/complete", Complete(3));
        Assert.Equal(HttpStatusCode.Conflict, outOfOrder.StatusCode);

        JsonElement progress = default;
        for (var order = 1; order <= 10; order++)
        {
            var response = await user.Client.PostAsJsonAsync("/api/v1/rituals/progress/complete", Complete(order));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            progress = await response.Content.ReadFromJsonAsync<JsonElement>();
        }

        Assert.True(progress.GetProperty("isCompleted").GetBoolean());
        Assert.Equal(JsonValueKind.Null, progress.GetProperty("currentStep").ValueKind);
        Assert.Equal(HttpStatusCode.Conflict,
            (await user.Client.PostAsJsonAsync("/api/v1/rituals/progress/complete", Complete(10))).StatusCode);

        var reset = await user.Client.PostAsJsonAsync("/api/v1/rituals/progress/reset", new { pilgrimageType = 1 });
        var afterReset = await reset.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, afterReset.GetProperty("completedCount").GetInt32());
        Assert.Equal(HttpStatusCode.OK,
            (await user.Client.PostAsJsonAsync("/api/v1/rituals/progress/complete", Complete(1))).StatusCode);
    }

    [Fact]
    public async Task Ihram_guide_articles_are_seeded()
    {
        var user = await TestUsers.CreateAsync(factory);

        var articles = await user.Client.GetFromJsonAsync<JsonElement>("/api/v1/guides");
        var first = articles.EnumerateArray().First();
        var single = await user.Client.GetFromJsonAsync<JsonElement>($"/api/v1/guides/{first.GetProperty("id").GetGuid()}");

        Assert.Equal(8, articles.GetArrayLength());
        Assert.Equal(first.GetProperty("title").GetString(), single.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Home_shows_the_first_step_without_creating_progress()
    {
        var user = await TestUsers.CreateAsync(factory);

        var home = await user.Client.GetFromJsonAsync<JsonElement>("/api/v1/home");

        Assert.Equal(JsonValueKind.Null, home.GetProperty("journey").ValueKind);
        Assert.Equal(JsonValueKind.Null, home.GetProperty("group").ValueKind);
        var ritual = home.GetProperty("ritual");
        Assert.False(ritual.GetProperty("isStarted").GetBoolean());
        Assert.Equal(1, ritual.GetProperty("nextStep").GetProperty("order").GetInt32());
    }
}
