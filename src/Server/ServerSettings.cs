namespace FileUpdaterServer;

public class ServerSettings
{
    // Server section
    public int Port { get; set; } = 8080;
    public string Hostname { get; set; } = string.Empty;
    public int MaxConcurrentDownloads { get; set; } = 50;

    // Files section
    public string FilesDirectory { get; set; } = "./files/";
    public string CacheFileName { get; set; } = "jsoncache.json";
    public int CacheRegenerationInterval { get; set; } = 3600;

    // Security section
    public string CorsAllowedOrigins { get; set; } = "*";
    public bool EnablePathTraversalProtection { get; set; } = true;
    public long MaxFileSize { get; set; } = 0; // 0 = unlimited

    // Logging section
    public string LogLevel { get; set; } = "Information";
    public string LogFilePath { get; set; } = string.Empty; // empty = console only
    public bool EnableRequestLogging { get; set; } = true;

    // Performance section
    public int StreamBufferSize { get; set; } = 81920;
    public bool EnableCompression { get; set; } = true;
}
