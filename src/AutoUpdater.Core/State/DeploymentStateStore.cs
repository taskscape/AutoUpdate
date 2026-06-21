using System.Text.Json;

namespace AutoUpdater.Core.State;

/// <summary>
/// Reads/writes <see cref="DeploymentState"/> to a machine-wide JSON file under %ProgramData%
/// (spec §6.2). Access is guarded by a cross-process mutex because both the host process and the
/// SYSTEM service may touch it.
/// </summary>
public sealed class DeploymentStateStore
{
    public const int MaxAttempts = 3; // spec §5.3

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly string _mutexName;

    public DeploymentStateStore(string applicationId, string? rootDirectory = null)
    {
        rootDirectory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "AutoUpdater",
            applicationId);

        Directory.CreateDirectory(rootDirectory);
        _filePath = Path.Combine(rootDirectory, "state.json");
        _mutexName = $@"Global\AutoUpdater_State_{applicationId}";
    }

    public string FilePath => _filePath;

    public DeploymentState Load()
    {
        using var guard = AcquireLock();
        return LoadUnlocked();
    }

    /// <summary>Atomically mutates and persists state under the cross-process lock.</summary>
    public DeploymentState Update(Action<DeploymentState> mutate)
    {
        using var guard = AcquireLock();
        var state = LoadUnlocked();
        mutate(state);
        Save(state);
        return state;
    }

    /// <summary>
    /// Increments the attempt count for <paramref name="tag"/> and marks it completed if the
    /// limit is reached. Returns whether another attempt is still permitted (spec §5.3).
    /// </summary>
    public bool RegisterAttempt(string tag, out int attemptNumber)
    {
        var allowed = true;
        var number = 0;
        Update(state =>
        {
            number = state.GetAttempts(tag) + 1;
            state.RetryCounts[tag] = number;
            if (number >= MaxAttempts && !state.IsCompleted(tag))
                state.CompletedTags.Add(tag); // abandon after the 3rd attempt
            allowed = number <= MaxAttempts;
        });
        attemptNumber = number;
        return allowed;
    }

    public void MarkCompleted(string tag, bool setAsCurrent)
    {
        Update(state =>
        {
            if (!state.IsCompleted(tag))
                state.CompletedTags.Add(tag);
            if (setAsCurrent)
                state.CurrentTag = tag;
        });
    }

    private DeploymentState LoadUnlocked()
    {
        if (!File.Exists(_filePath))
            return new DeploymentState();

        var json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<DeploymentState>(json, SerializerOptions) ?? new DeploymentState();
    }

    private void Save(DeploymentState state)
    {
        var json = JsonSerializer.Serialize(state, SerializerOptions);
        var tmp = _filePath + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, _filePath, overwrite: true); // atomic-ish replace
    }

    private MutexGuard AcquireLock() => new(_mutexName);

    private sealed class MutexGuard : IDisposable
    {
        private readonly Mutex _mutex;
        private readonly bool _owned;

        public MutexGuard(string name)
        {
            _mutex = new Mutex(initiallyOwned: false, name);
            try
            {
                _owned = _mutex.WaitOne(TimeSpan.FromSeconds(30));
            }
            catch (AbandonedMutexException)
            {
                _owned = true; // previous owner crashed; we now hold it
            }
        }

        public void Dispose()
        {
            if (_owned)
                _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
    }
}
