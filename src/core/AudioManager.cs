using System;
using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NAudio.Vorbis;

namespace VoxelChaos.Core;

public static class AudioManager
{
    private static IWavePlayer? _musicPlayer;
    private static IWavePlayer? _sfxPlayer;
    private static MixingSampleProvider? _mixer;
    
    private static string[]? _musicTracks;
    private static VorbisWaveReader? _currentMusic;
    private static readonly Random _rng = new();

    public static void Initialize()
    {
        // 1. Настраиваем микшер для звуковых эффектов (SFX)
        _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));
        _mixer.ReadFully = true; // Не останавливать микшер, если нет звуков
        
        _sfxPlayer = new WaveOutEvent();
        _sfxPlayer.Init(_mixer);
        _sfxPlayer.Play();

        // 2. Настраиваем музыку
        _musicPlayer = new WaveOutEvent();
        _musicPlayer.PlaybackStopped += OnMusicStopped;
        _musicPlayer.Volume = 0.3f; // Музыка играет тише звуков

        LoadMusicTracks();
        PlayNextMusic();
    }

    private static void LoadMusicTracks()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "assets", "music");
        if (Directory.Exists(path))
        {
            _musicTracks = Directory.GetFiles(path, "*.ogg");
            if (_musicTracks.Length == 0) _musicTracks = null;
        }
    }

    private static void PlayNextMusic()
    {
        if (_musicTracks == null || _musicTracks.Length == 0) return;

        // Выбираем случайный трек
        string track = _musicTracks[_rng.Next(_musicTracks.Length)];
        
        _currentMusic = new VorbisWaveReader(track);
        _musicPlayer?.Init(_currentMusic);
        _musicPlayer?.Play();
    }

    private static void OnMusicStopped(object? sender, StoppedEventArgs e)
    {
        // Когда трек заканчивается — включаем следующий
        _currentMusic?.Dispose();
        PlayNextMusic();
    }

    public static void PlaySound(string fileName, float volume = 1.0f)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "assets", "sounds", fileName);
        if (!File.Exists(path) || _mixer == null) return;

        var reader = new VorbisWaveReader(path);
        ISampleProvider sampleProvider = reader.ToSampleProvider();

        // Конвертируем частоту дискретизации (если файл не 44100 Hz)
        if (sampleProvider.WaveFormat.SampleRate != 44100)
            sampleProvider = new WdlResamplingSampleProvider(sampleProvider, 44100);

        // Конвертируем моно в стерео (микшер требует стерео)
        if (sampleProvider.WaveFormat.Channels == 1)
            sampleProvider = new MonoToStereoSampleProvider(sampleProvider);

        var volumeProvider = new VolumeSampleProvider(sampleProvider) { Volume = volume };
        
        // Добавляем в микшер с авто-очисткой памяти (чтобы не забивать ОЗУ)
        _mixer.AddMixerInput(new AutoDisposeSampleProvider(volumeProvider, reader));
    }

    public static void Dispose()
    {
        _musicPlayer?.Stop();
        _musicPlayer?.Dispose();
        _sfxPlayer?.Stop();
        _sfxPlayer?.Dispose();
        _currentMusic?.Dispose();
    }

    // Обертка, которая автоматически освобождает файл после завершения проигрывания
    private class AutoDisposeSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly IDisposable _disposable;

        public AutoDisposeSampleProvider(ISampleProvider source, IDisposable disposable)
        {
            _source = source;
            _disposable = disposable;
        }

        public WaveFormat WaveFormat => _source.WaveFormat;

        // ⚠️ Новая сигнатура для NAudio 3.0+
        public int Read(Span<float> buffer)
        {
            int read = _source.Read(buffer);
            if (read == 0) _disposable?.Dispose(); // Конец файла — освобождаем
            return read;
        }
    }
}