using System.IO;
using System.Windows.Media;

namespace SnowRunnerTuningShop.Audio;

/// <summary>Plays RIFF WAVE bytes from shared_sound.pak (.pcm = ADPCM/PCM WAV) via WPF MediaPlayer.</summary>
public sealed class TruckSoundPreviewPlayer : IDisposable
{
    private readonly MediaPlayer _player = new();
    private string? _tempPath;
    private string? _playingKey;
    private bool _disposed;

    public TruckSoundPreviewPlayer()
    {
        _player.MediaEnded += (_, _) =>
        {
            CleanupTemp();
            _playingKey = null;
            PlayingChanged?.Invoke(this, EventArgs.Empty);
        };
        _player.MediaFailed += (_, _) =>
        {
            CleanupTemp();
            _playingKey = null;
            PlayingChanged?.Invoke(this, EventArgs.Empty);
        };
    }

    public event EventHandler? PlayingChanged;

    public string? PlayingKey => _playingKey;

    public bool IsPlaying(string key) =>
        string.Equals(_playingKey, key, StringComparison.Ordinal);

    public void Toggle(string key, byte[] wavBytes)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(wavBytes);

        if (IsPlaying(key))
        {
            Stop();
            return;
        }

        Stop();
        _tempPath = Path.Combine(Path.GetTempPath(), $"srts-sound-{Guid.NewGuid():N}.wav");
        File.WriteAllBytes(_tempPath, wavBytes);
        _player.Open(new Uri(_tempPath));
        _player.Play();
        _playingKey = key;
        PlayingChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _player.Stop();
            _player.Close();
        }
        catch
        {
            // ignore teardown races
        }

        CleanupTemp();
        if (_playingKey is not null)
        {
            _playingKey = null;
            PlayingChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
        // MediaPlayer has no Dispose; Close is enough.
    }

    private void CleanupTemp()
    {
        if (_tempPath is null)
        {
            return;
        }

        try
        {
            if (File.Exists(_tempPath))
            {
                File.Delete(_tempPath);
            }
        }
        catch
        {
            // best-effort
        }

        _tempPath = null;
    }
}
