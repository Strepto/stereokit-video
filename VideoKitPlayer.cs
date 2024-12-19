#nullable enable
using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using LibVLCSharp.Shared;
using StereoKit;

namespace VideoKit;

public class VideoKitPlayer : IDisposable
{
    // Output Resolution (Higher res may work fine, did not experiment.)
    private const uint Width = 960;
    private const uint Height = 540;

    /// <summary>
    /// - "S16N" for signed 16-bit PCM
    /// - "S32N" for signed 32-bit PCM
    /// - "FL32" for single precision IEEE 754
    /// All supported formats use the native endianness. If there are more than one channel, samples are interleaved.
    /// </summary>
    private const string AudioFormat = "S16N"; // Signed 16-bit PCM

    /// <summary>
    /// Channels are interleaved in the output buffer.
    /// I just used one channel for now, but more should be possible
    /// </summary>
    private const uint AudioChannels = 1;

    /// <summary>
    /// StereoKit prefers 48kHz audio.
    /// </summary>
    private const uint AudioSampleRate = 48000;

    private const string Chroma = "RGBA";

    /// <summary>
    /// RGBA is used in <see cref="Chroma"/>, so 4 byte per pixel, or 32 bits.
    /// </summary>
    private const uint BytePerPixel = 4;

    /// <summary>
    /// the number of bytes per "line"
    /// For performance reasons inside the core of VLC, it must be aligned to multiples of 32.
    /// </summary>
    private static readonly uint Pitch = AlignToNearestMultipleOf32VlcQuirk(Width * BytePerPixel);

    /// <summary>
    /// The number of lines in the buffer.
    /// For performance reasons inside the core of VLC, it must be aligned to multiples of 32.
    /// </summary>
    private static readonly uint Lines = AlignToNearestMultipleOf32VlcQuirk(Height);
    private static long _videoFrameCounter = 0;

    private Tex? VideoTexture { get; set; }
    private byte[]? _videoTextureData;
    private Sound _activeSoundStream = Sound.CreateStream(4); // 4 seconds buffer (arbitrary number)
    private SoundInst _activeSoundInst;
    private MediaPlayer? _mediaPlayer;
    private MemoryMappedFile? _mappedFileForVideo;
    private MemoryMappedViewAccessor? _currentMappedViewAccessor;
    private readonly float[] _tempAudioBuffer = new float[4096 * 16];

    private static uint AlignToNearestMultipleOf32VlcQuirk(uint size)
    {
        // Align on the next multiple of 32 because that is what VLC wants for best performance.
        return (size % 32 == 0) ? size : (size / 32 + 1) * 32;
    }

    public Tex InitializeSoundAndTexture()
    {
        var videoFormat = TexFormat.Rgba32;
        VideoTexture = new Tex(TexType.Dynamic | TexType.ImageNomips, videoFormat)
        {
            Anisoptropy = 4,
            Id = "VideoTextureCool",
            AddressMode = TexAddress.Clamp,
        };
        _videoTextureData = new byte[Pitch * Lines];
        VideoTexture.SetSize((int)Pitch, (int)Lines);
        return VideoTexture;
    }

    /// <summary>
    /// </summary>
    /// <param name="videoURI">Best if the URL allows "Range processing" to support skipping in the video.
    /// On android the URL must be https (or hack to support http in the manifest. Google it.)</param>
    public async void PlayVideoAsync(Uri videoURI)
    {
        // Run Core.Initialize on windows as its ran in the MainActivity in android
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            Core.Initialize();

        // If android, RUN Core.Initialize() first in MainActivity.cs outside stereokit! (for some reason)

        // Create a new libvlc instance
        using var libVlc = new LibVLC();
        using var mediaPlayer = _mediaPlayer = new MediaPlayer(libVlc);

        // Not sure if EnableHardwareDecoding makes performance better or not. Did not test.
        // mediaPlayer.EnableHardwareDecoding = true;

        // TODO: Implement better events listening
        var processingCancellationTokenSource = new CancellationTokenSource();
        mediaPlayer.Stopped += (_, _) => processingCancellationTokenSource.CancelAfter(1);

        // TODO: This can be any url.
        using var media = new Media(libVlc, videoURI);

        // Set the audio format and sample rate.
        mediaPlayer.SetAudioFormat(AudioFormat, AudioSampleRate, AudioChannels);
        // Subscribe to audio callback events
        mediaPlayer.SetAudioCallbacks(PlayAudio, PauseAudio, ResumeAudio, FlushAudio, DrainAudio);

        // Set the video format and sizes
        mediaPlayer.SetVideoFormat(Chroma, Width, Height, Pitch);
        mediaPlayer.SetVideoCallbacks(Lock, null, Display);
        mediaPlayer.Play(media);
        // TODO: Move the sound-stream around a bit
        _activeSoundInst = _activeSoundStream.Play(Vec3.Zero);
        mediaPlayer.Time = 0;

        try
        {
            while (!processingCancellationTokenSource.Token.IsCancellationRequested)
            {
                // TODO: Actually implement some playback handling
                await Task.Delay(100, processingCancellationTokenSource.Token);
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Video processing stopped.");
        }

        _mediaPlayer = null;
        Console.WriteLine("Video processing finished.");
    }

    public void Play() => _mediaPlayer?.Play();

    public void Pause() => _mediaPlayer?.Pause();

    public long Time
    {
        get => _mediaPlayer?.Time ?? 0;
        set
        {
            if (_mediaPlayer == null) return;
            long clamped = Math.Clamp(value, 0, _mediaPlayer.Length);
            // The media player doesn't like being set to the time it's already
            // at.
            if (clamped == _mediaPlayer.Time) return;
            _mediaPlayer.Time = clamped;
        }
    }

    public long Length => _mediaPlayer?.Length ?? 0;

    public Vec3 SoundPosition { get => _activeSoundInst.Position; set => _activeSoundInst.Position = value; }

    /// <summary>
    /// Stepper to update the video texture and draw the UI
    /// </summary>
    public void Step()
    {
        if (_mediaPlayer == null)
            return;
        if (_videoFrameCounter == _lastRenderedFrame)
            return;
        if (_videoFrameCounter == 1)
            _activeSoundStream.Play(Vec3.Zero);
        VideoTexture?.SetColors((int)(Pitch / BytePerPixel), (int)Lines, _videoTextureData);
        _lastRenderedFrame = _videoFrameCounter;
    }

    #region Video Callbacks

    private long _lastRenderedFrame = _videoFrameCounter;

    /// <summary>
    /// Copy the video frame data to our managed memory.
    /// </summary>
    /// <param name="opaque">No idea</param>
    /// <param name="planes">The data array for all the planes (Length: Pitch * Lines)</param>
    /// <returns>No idea</returns>
    private IntPtr Lock(IntPtr opaque, IntPtr planes)
    {
        _mappedFileForVideo ??= MemoryMappedFile.CreateNew(null, Pitch * Lines);
        _currentMappedViewAccessor ??= _mappedFileForVideo.CreateViewAccessor();
        Marshal.WriteIntPtr(planes, _currentMappedViewAccessor.SafeMemoryMappedViewHandle.DangerousGetHandle());
        return IntPtr.Zero;
    }

    private void Display(IntPtr opaque, IntPtr picture)
    {
        // _mappedFileForVideo has been created in the Lock callback
        using var sourceStream = _mappedFileForVideo!.CreateViewStream();
        var read = sourceStream.Read(_videoTextureData);
        if (read != _videoTextureData!.Length)
        {
            Console.WriteLine("Error reading from memory mapped file.");
            return;
        }

        _videoFrameCounter++;
    }

    #endregion Video Callbacks

    #region Audio Callbacks

    /// <summary>
    /// Hacky audio state thing. We don't have an _activeSoundStream.IsPlaying so we need to keep track of it ourselves.
    /// </summary>
    private bool _isPlayingAudio = false;

    private unsafe void PlayAudio(IntPtr data, IntPtr samples, uint count, long pts)
    {
        var bytes = new ReadOnlySpan<byte>(samples.ToPointer(), (int)(count * sizeof(short)));

        var shortSpan = MemoryMarshal.Cast<byte, short>(bytes);
        for (var index = 0; index < shortSpan.Length; index++)
        {
            // Normalize the short to a float between -1 and 1 for SK
            _tempAudioBuffer[index] = shortSpan[index] / ((float)short.MaxValue);
        }

        _activeSoundStream.WriteSamples(_tempAudioBuffer, (int)count);

        if (_isPlayingAudio)
            return;
        // Restart the audio if its stopped
        _activeSoundStream.Play(Vec3.Zero);
        _isPlayingAudio = true;
    }

    private void DrainAudio(IntPtr data)
    {
        // No more audio will come in but we should play what's remaining in the buffer as far as I understand.
        Console.WriteLine(nameof(DrainAudio));
        _isPlayingAudio = false;
    }

    private void FlushAudio(IntPtr data, long pts)
    {
        // FlushAudio means that we should remove all audio from the buffer. Currently not supported in SK afaik, so we just create a new stream. Smarter solutions are welcome.
        var soundInstance = _activeSoundStream.Play(Vec3.Zero);
        soundInstance.Stop();
        _activeSoundStream = Sound.CreateStream(4);
        Console.WriteLine(nameof(FlushAudio));
        _isPlayingAudio = false;
    }

    private void ResumeAudio(IntPtr data, long pts)
    {
        Console.WriteLine(nameof(ResumeAudio));
        _activeSoundStream.Play(Vec3.Zero);
    }

    private void PauseAudio(IntPtr data, long pts)
    {
        var stream = _activeSoundStream.Play(Vec3.Zero);
        stream.Stop();
        Console.WriteLine(nameof(PauseAudio));
        _isPlayingAudio = false;
    }

    #endregion Audio Callbacks

    public void Dispose()
    {
        // This class probably misses a lot required disposes. Check before going anywhere near production.
        _currentMappedViewAccessor?.Dispose();
        _mappedFileForVideo?.Dispose();
    }
}
