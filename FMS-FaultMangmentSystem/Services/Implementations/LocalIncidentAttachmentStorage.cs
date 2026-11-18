using IncidentManagementSystem.Services.Interfaces;

namespace IncidentManagementSystem.Services.Implementations;

public sealed class LocalIncidentAttachmentStorage(IWebHostEnvironment environment) : IIncidentAttachmentStorage
{
    private string RootPath => Path.Combine(environment.ContentRootPath, "App_Data", "incident-attachments");

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken = default)
    {
        var key = $"{Guid.NewGuid():N}{extension}";
        Directory.CreateDirectory(RootPath);
        var path = GetPath(key);
        await using var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
        await content.CopyToAsync(target, cancellationToken);
        return key;
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetPath(storageKey);
        Stream? stream = File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true)
            : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetPath(storageKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string GetPath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey) ||
            !string.Equals(Path.GetFileName(storageKey), storageKey, StringComparison.Ordinal) ||
            storageKey.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("Invalid attachment key.", nameof(storageKey));
        }

        return Path.Combine(RootPath, storageKey);
    }
}
