using System.Net.Http.Json;

namespace BootLens.Community.Client;

public sealed record CommunityObservationPreview(string ItemHash, string Mechanism, string Action, string? PublisherCategory);

public sealed class CommunityClient(HttpClient httpClient)
{
    public bool Enabled => false;

    public async Task<bool> SubmitAsync(CommunityObservationPreview observation, CancellationToken cancellationToken = default)
    {
        if (!Enabled || httpClient.BaseAddress is null) return false;
        using var response = await httpClient.PostAsJsonAsync("api/v1/observations", observation, cancellationToken);
        return response.IsSuccessStatusCode;
    }
}
