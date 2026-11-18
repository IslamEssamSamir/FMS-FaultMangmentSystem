using IncidentManagementSystem.Services.Interfaces;

namespace IncidentManagementSystem.Tests;

internal sealed class TestIncidentAttachmentStorage : IIncidentAttachmentStorage
{
    private readonly Dictionary<string, byte[]> files = new(StringComparer.Ordinal);

    public int SaveCount { get; private set; }
    public int DeleteCount { get; private set; }
    public bool FailNextDelete { get; set; }
    public IReadOnlyCollection<string> Keys => files.Keys;

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken = default)
    {
        var key = $"{Guid.NewGuid():N}{extension}";
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        files.Add(key, buffer.ToArray());
        SaveCount++;
        return key;
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<Stream?>(files.TryGetValue(storageKey, out var bytes)
            ? new MemoryStream(bytes, writable: false)
            : null);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DeleteCount++;
        if (FailNextDelete)
        {
            FailNextDelete = false;
            throw new IOException("Simulated attachment deletion failure.");
        }

        files.Remove(storageKey);
        return Task.CompletedTask;
    }
}
