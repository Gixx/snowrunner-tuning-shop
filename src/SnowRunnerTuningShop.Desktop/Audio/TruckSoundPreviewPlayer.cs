using NAudio.Wave;

namespace SnowRunnerTuningShop.Desktop.Audio;

/// <summary>
/// Plays RIFF WAVE bytes from shared_sound.pak (.pcm extension). Windows-only (ACM ADPCM → PCM).
/// </summary>
public sealed class TruckSoundPreviewPlayer : IDisposable
{
    private IWavePlayer? _waveOut;
    private WaveStream? _reader;
    private WaveStream? _pcm;
    private MemoryStream? _ownedMemory;
    private string? _playingKey;
    private bool _disposed;

    public event EventHandler? PlayingChanged;

    public string? PlayingKey => _playingKey;

    public static bool IsSupported => OperatingSystem.IsWindows();

    public bool IsPlaying(string key) =>
        string.Equals(_playingKey, key, StringComparison.Ordinal);

    public void Toggle(string key, byte[] wavBytes)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(wavBytes);

        if (!IsSupported)
        {
            return;
        }

        if (IsPlaying(key))
        {
            Stop();
            return;
        }

        Stop();
        _ownedMemory = new MemoryStream(wavBytes, writable: false);
        _reader = new WaveFileReader(_ownedMemory);
        _pcm = WaveFormatConversionStream.CreatePcmStream(_reader);
        _waveOut = new WaveOutEvent();
        _waveOut.PlaybackStopped += (_, _) =>
        {
            CleanupStreams();
            _playingKey = null;
            PlayingChanged?.Invoke(this, EventArgs.Empty);
        };
        _waveOut.Init(_pcm);
        _waveOut.Play();
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
            _waveOut?.Stop();
        }
        catch
        {
            // ignore
        }

        CleanupStreams();
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
    }

    private void CleanupStreams()
    {
        _waveOut?.Dispose();
        _waveOut = null;
        _pcm?.Dispose();
        _pcm = null;
        _reader?.Dispose();
        _reader = null;
        _ownedMemory?.Dispose();
        _ownedMemory = null;
    }
}
