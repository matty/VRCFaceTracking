using System.Text;
using System.Threading;
using System.Collections.Concurrent;
using Newtonsoft.Json;
using VRCFaceTracking.Core.Contracts.Services;

namespace VRCFaceTracking.Core.Services;

public class FileService : IFileService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new ConcurrentDictionary<string, SemaphoreSlim>();
    private const int MaxRetryAttempts = 3;
    private const int RetryDelayMs = 100;

    private SemaphoreSlim GetLockForFile(string filePath)
    {
        return _fileLocks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));
    }

    private async Task<T> ReadAsync<T>(string folderPath, string fileName)
    {
        var path = Path.Combine(folderPath, fileName);
        var fileLock = GetLockForFile(path);

        await fileLock.WaitAsync();
        try
        {
            if (File.Exists(path))
            {
                for (var attempt = 0; attempt < MaxRetryAttempts; attempt++)
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(path);
                        return JsonConvert.DeserializeObject<T>(json);
                    }
                    catch (IOException) when (attempt < MaxRetryAttempts - 1)
                    {
                        await Task.Delay(RetryDelayMs * (attempt + 1));
                    }
                }
            }
            return default;
        }
        finally
        {
            fileLock.Release();
        }
    }

    // Keep synchronous Read for backward compatibility
    public T Read<T>(string folderPath, string fileName)
    {
        return ReadAsync<T>(folderPath, fileName).GetAwaiter().GetResult();
    }

    public async Task Save<T>(string folderPath, string fileName, T content)
    {
        var path = Path.Combine(folderPath, fileName);
        var fileLock = GetLockForFile(path);

        await fileLock.WaitAsync();
        try
        {
            for (var attempt = 0; attempt < MaxRetryAttempts; attempt++)
            {
                try
                {
                    if (!Directory.Exists(folderPath))
                    {
                        Directory.CreateDirectory(folderPath);
                    }

                    var fileContent = JsonConvert.SerializeObject(content);

                    // Write to a temporary file first, then move it to ensure atomic write
                    var tempPath = path + ".tmp";
                    await File.WriteAllTextAsync(tempPath, fileContent, Encoding.UTF8);

                    // If the target file exists, delete it
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }

                    // Rename the temp file to the target file
                    File.Move(tempPath, path);

                    break; // Success, exit retry loop
                }
                catch (IOException) when (attempt < MaxRetryAttempts - 1)
                {
                    await Task.Delay(RetryDelayMs * (attempt + 1));
                }
            }
        }
        finally
        {
            fileLock.Release();
        }
    }

    private async Task DeleteAsync(string folderPath, string fileName)
    {
        if (fileName == null) return;

        var path = Path.Combine(folderPath, fileName);
        var fileLock = GetLockForFile(path);

        await fileLock.WaitAsync();
        try
        {
            if (File.Exists(path))
            {
                for (var attempt = 0; attempt < MaxRetryAttempts; attempt++)
                {
                    try
                    {
                        File.Delete(path);
                        break; // Success, exit retry loop
                    }
                    catch (IOException) when (attempt < MaxRetryAttempts - 1)
                    {
                        await Task.Delay(RetryDelayMs * (attempt + 1));
                    }
                }
            }
        }
        finally
        {
            fileLock.Release();
        }
    }

    // Keep synchronous Delete for backward compatibility
    public void Delete(string folderPath, string fileName)
    {
        DeleteAsync(folderPath, fileName).GetAwaiter().GetResult();
    }
}
