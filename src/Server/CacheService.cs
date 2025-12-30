using System.Security.Cryptography;
using System.Text.Json;

namespace FileUpdaterServer;

public class CacheService : BackgroundService
{
    private readonly ServerSettings _settings;
    private readonly ILogger<CacheService> _logger;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    public CacheService(ServerSettings settings, ILogger<CacheService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Cache service starting...");

        // Generate cache immediately on startup
        await RegenerateCacheAsync(stoppingToken);

        // Schedule periodic regeneration if interval > 0
        if (_settings.CacheRegenerationInterval > 0)
        {
            _logger.LogInformation("Cache will regenerate every {Interval} seconds", _settings.CacheRegenerationInterval);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(_settings.CacheRegenerationInterval), stoppingToken);
                    await RegenerateCacheAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected when stopping
                    break;
                }
            }
        }
        else
        {
            _logger.LogInformation("Automatic cache regeneration disabled (interval = 0)");
        }
    }

    private async Task RegenerateCacheAsync(CancellationToken cancellationToken)
    {
        // Ensure only one regeneration runs at a time
        await _cacheLock.WaitAsync(cancellationToken);

        try
        {
            _logger.LogInformation("Regenerating cache from {Directory}...", _settings.FilesDirectory);

            var fileEntries = new List<FileEntry>();

            // Ensure files directory exists
            if (!Directory.Exists(_settings.FilesDirectory))
            {
                _logger.LogWarning("Files directory does not exist: {Directory}", _settings.FilesDirectory);
                Directory.CreateDirectory(_settings.FilesDirectory);
                _logger.LogInformation("Created files directory: {Directory}", _settings.FilesDirectory);
            }

            // Get all files recursively
            var files = Directory.EnumerateFiles(_settings.FilesDirectory, "*", SearchOption.AllDirectories);

            foreach (var filePath in files)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    // Compute MD5 by streaming the file
                    string md5Hash;
                    using (var md5 = MD5.Create())
                    using (var stream = File.OpenRead(filePath))
                    {
                        var hash = await md5.ComputeHashAsync(stream, cancellationToken);
                        md5Hash = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    }

                    // Get relative path from files directory
                    var relativePath = Path.GetRelativePath(_settings.FilesDirectory, filePath);

                    // Normalize path separators to forward slashes (like Python version)
                    relativePath = relativePath.Replace(Path.DirectorySeparatorChar, '/');

                    fileEntries.Add(new FileEntry
                    {
                        name = relativePath,
                        md5 = md5Hash
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing file {FilePath}", filePath);
                }
            }

            // Serialize to JSON with lowercase property names
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var json = JsonSerializer.Serialize(fileEntries, options);

            // Write to temp file then rename for atomic operation
            var tempFile = _settings.CacheFileName + ".tmp";
            await File.WriteAllTextAsync(tempFile, json, cancellationToken);
            File.Move(tempFile, _settings.CacheFileName, overwrite: true);

            _logger.LogInformation("Cache regenerated successfully with {Count} files", fileEntries.Count);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Cache regeneration cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error regenerating cache");
        }
        finally
        {
            _cacheLock.Release();
        }
    }
}

public class FileEntry
{
    public string name { get; set; } = string.Empty;
    public string md5 { get; set; } = string.Empty;
}
