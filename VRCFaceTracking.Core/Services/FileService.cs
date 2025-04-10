using System.Text;
using System.Threading;
using Newtonsoft.Json;
using VRCFaceTracking.Core.Contracts.Services;

namespace VRCFaceTracking.Core.Services;

public class FileService : IFileService
{
    private static readonly SemaphoreSlim _saveLock = new SemaphoreSlim(1, 1);

    public T Read<T>(string folderPath, string fileName)
    {
        var path = Path.Combine(folderPath, fileName);
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<T>(json);
        }

        return default;
    }

    public async Task Save<T>(string folderPath, string fileName, T content)
    {
        // Acquire the lock before performing file operations
        await _saveLock.WaitAsync();
        try
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            var fileContent = JsonConvert.SerializeObject(content);
            await File.WriteAllTextAsync(Path.Combine(folderPath, fileName), fileContent, Encoding.UTF8);
        }
        finally
        {
            // Always release the lock, even if an exception occurs
            _saveLock.Release();
        }
    }

    public void Delete(string folderPath, string fileName)
    {
        if (fileName != null && File.Exists(Path.Combine(folderPath, fileName)))
        {
            File.Delete(Path.Combine(folderPath, fileName));
        }
    }
}
