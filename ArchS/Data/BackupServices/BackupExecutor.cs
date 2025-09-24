using System.Collections.Concurrent;
using ArchS.Data.Constants;
using ArchS.Data.NotifierServices;
namespace ArchS.Data.BackupServices;

/// <summary>
/// BackupExecutor uses multithread by running in parallel (up to ParallelismDegree in BackupOptions)
/// by making each item an asynchronous Task that waits for the permission of the semaphore. 
/// 
/// Non-blocking threads calls CopyFileAsync which uses IO operations (ReadAsync returns a Task) and 
/// it does not block the calling thread from ExecuteAsync but before-after it uses threads from the 
/// thread pool. 
/// 
/// The usage of Interlocked to modify itemsCompleted and bytesCopied by many tasks at the same 
/// time also is safe. Task.WhenAll(tasks) makes all the tasks allowed by the semaphore to run concurrently
/// </summary>

public enum BackUpPercentageState { INITIAL, ABOVE_25, ABOVE_50, ABOVE_75, FINAL};
public sealed class BackupExecutor
{
    private readonly BackupOptions _options;
    private readonly IBackupNotifier _notifier;
    private readonly object _notifierProgressLock = new object();

    public BackupExecutor(BackupOptions options, IBackupNotifier notifier)
    {
        _options = options;
        _notifier = notifier;
    }

    private void NotifyProgress(string profileName, ref BackUpPercentageState state, BackupProgress progress)
    {
        lock (_notifierProgressLock) // only one thread can enter here at a time
        {
            var thresholds = new (BackUpPercentageState State, double Percent)[]
            {
                (BackUpPercentageState.ABOVE_25, 25.0),
                (BackUpPercentageState.ABOVE_50, 50.0),
                (BackUpPercentageState.ABOVE_75, 75.0),
                (BackUpPercentageState.FINAL, 100.0)
            };
            if (state == BackUpPercentageState.FINAL) return;

            double percent = progress.PercentItems;
            BackUpPercentageState highestThreshold = state;

            foreach (var threshold in thresholds) // setting highestThreshold to the highest value
            {
                if ((int)threshold.State > (int)state && percent >= threshold.Percent)
                {
                    if ((int)threshold.State > (int)highestThreshold)
                    {
                        highestThreshold = threshold.State;
                    }
                }
            }

            if ((int)highestThreshold > (int)state)
            {
                _notifier.Progress(profileName, progress);
                state = highestThreshold;
            }
        }
    }

    public async Task<List<string>> ExecuteAsync(string profileName, Archive archive)
    {
        var progress = new BackupProgress
        {
            State = BackupProcessConstants.STAGE_STARTING,
            TotalItems = archive.Items.Count,
            TotalBytes = archive.TotalBytes
        };

        BackUpPercentageState state = BackUpPercentageState.INITIAL;
        _notifier.Progress(profileName, progress);

        int itemsCompleted = 0;
        long bytesCopied = 0;

        var errors = new ConcurrentBag<string>();  // thread safe collection to store failed file paths

        foreach (var item in archive.Items)
        {
            var dir = Path.GetDirectoryName(item.TargetPath)!;
            Directory.CreateDirectory(dir); // creating the directories in the target folder, if they exist no exception is thrown
        }

        using var regulator = new SemaphoreSlim(_options.ParallelismDegree);
        var tasks = archive.Items.Select(async item =>
        {
            await regulator.WaitAsync(); // Asynchronously waiting. It can continue once another task call regulator.Release()
            try
            {
                await CopyFileAsync(item.SourcePath, item.TargetPath, _options.FileBufferSize);
                // Interlocked can modify the same variable simultaneously for many tasks, it writes directly in place 
                Interlocked.Add(ref bytesCopied, (long)(item.SizeBytes ?? 0));
            }
            catch (Exception ex)
            {
                errors.Add($"[PATH]: {item.SourcePath} [ERROR]: {ex.Message}");
            }
            Interlocked.Increment(ref itemsCompleted); 
            var progress = new BackupProgress
            {
                State = BackupProcessConstants.STAGE_RUNNING,
                BytesCopied = Interlocked.Read(ref bytesCopied),
                TotalBytes = archive.TotalBytes,
                TotalItems = archive.Items.Count,
                ItemsProcessed = itemsCompleted,
            };
            NotifyProgress(profileName, ref state, progress);
            regulator.Release(); // now a new item can be processed 
        }).ToArray();

        await Task.WhenAll(tasks); // running all items in parallel each on a thread up to max parallel constant 

        var result = new BackupProgress
        {
            State = BackupProcessConstants.STAGE_COMPLETED,
            BytesCopied = bytesCopied,
            TotalBytes = archive.TotalBytes,
            TotalItems = archive.Items.Count,
            ItemsProcessed = itemsCompleted,
        };
        _notifier.Progress(profileName, result);
        var status = errors.IsEmpty
            ? BackupProcessConstants.BACKUP_COMPLETED
            : $"{BackupProcessConstants.BACKUP_COMPLETED_WITH_ERRORS} {errors.Count}";

        _notifier.Notify(profileName, status);
        return errors.ToList();
    }

    // any erros will be catched in ExecuteAsync
    private static async Task CopyFileAsync(string sourceFilePath, string targetFilePath, int bufferBytes)
    {
        if (!File.Exists(sourceFilePath))
            throw new FileNotFoundException();

        var fileInfo = new FileInfo(sourceFilePath);
        // await using will dispose the files after the task is done 
        await using var input = new FileStream(
            sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferBytes, useAsync: true);
        await using var output = new FileStream(
            targetFilePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferBytes, useAsync: true);

        var buffer = new byte[bufferBytes];
        long totalRead = 0;
        int read;

        // buffer.AsMemory creates a slice which is written by ReadAsync and outputs the actual number of bytes written
        while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read));
            totalRead += read;
        }
        await output.FlushAsync(); // this will make sure everything is copied in memory 
    }
}
