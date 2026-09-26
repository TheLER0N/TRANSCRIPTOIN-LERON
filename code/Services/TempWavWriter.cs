using System;
using System.IO;

namespace Leron.Audio.Services;

public sealed class TempWavWriter : IDisposable
{
    private readonly string _path;
    private readonly FileStream _stream;
    private readonly BinaryWriter _writer;
    private readonly int _sampleRate;
    private readonly short _channels;
    private readonly short _bits;
    private int _dataLength;
    private bool _disposed;

    public string Path => _path;

    public TempWavWriter(string path, int sampleRate, int bitsPerSample, int channels)
    {
        _path = path;
        _sampleRate = sampleRate;
        _bits = (short)bitsPerSample;
        _channels = (short)channels;

        var dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        _stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        _writer = new BinaryWriter(_stream);
        WriteHeader();
    }

    public void WriteSamples(byte[] buffer, int offset, int count)
    {
        if (_disposed) return;
        _writer.Write(buffer, offset, count);
        _dataLength += count;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _writer.Flush();
        _stream.Seek(0, SeekOrigin.Begin);
        WriteHeader();
        _writer.Flush();
        _writer.Dispose();
        _stream.Dispose();
    }

    private void WriteHeader()
    {
        int byteRate = _sampleRate * _channels * _bits / 8;
        short blockAlign = (short)(_channels * _bits / 8);

        _writer.Write(0x46464952u);      // "RIFF"
        _writer.Write(36 + _dataLength); // chunk size
        _writer.Write(0x45564157u);      // "WAVE"
        _writer.Write(0x20746D66u);      // "fmt "
        _writer.Write(16);               // fmt chunk size
        _writer.Write((short)1);         // PCM
        _writer.Write(_channels);
        _writer.Write(_sampleRate);
        _writer.Write(byteRate);
        _writer.Write(blockAlign);
        _writer.Write(_bits);
        _writer.Write(0x61746164u);      // "data"
        _writer.Write(_dataLength);
    }
}