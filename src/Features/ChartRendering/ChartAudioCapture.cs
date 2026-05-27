using System;
using System.IO;
using Unity.Collections;
using UnityEngine;

namespace ADOFAI.EditorTweaks.Features.ChartRendering
{
    internal sealed class ChartAudioCapture : IDisposable
    {
        private readonly string outputPath;
        private readonly int sampleRate;
        private readonly int channels;
        private FileStream? stream;
        private BinaryWriter? writer;
        private long dataBytes;
        private bool started;

        public ChartAudioCapture(string outputPath)
        {
            this.outputPath = outputPath;
            sampleRate = AudioSettings.outputSampleRate;
            channels = GetChannelCount(AudioSettings.speakerMode);
        }

        public void Begin()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            writer = new BinaryWriter(stream);
            WriteHeader(0);
            started = AudioRenderer.Start();
            if (!started)
            {
                throw new InvalidOperationException("Unity AudioRenderer failed to start.");
            }
        }

        public void RenderFrame()
        {
            if (!started || writer == null)
            {
                return;
            }

            int sampleCount = AudioRenderer.GetSampleCountForCaptureFrame();
            if (sampleCount <= 0)
            {
                return;
            }

            NativeArray<float> samples = new NativeArray<float>(sampleCount, Allocator.Temp);
            try
            {
                if (!AudioRenderer.Render(samples))
                {
                    return;
                }

                for (int i = 0; i < samples.Length; i++)
                {
                    float clamped = Mathf.Clamp(samples[i], -1f, 1f);
                    short pcm = (short)Mathf.RoundToInt(clamped * short.MaxValue);
                    writer.Write(pcm);
                    dataBytes += 2;
                }
            }
            finally
            {
                samples.Dispose();
            }
        }

        public void Dispose()
        {
            if (started)
            {
                AudioRenderer.Stop();
                started = false;
            }

            if (writer != null && stream != null)
            {
                stream.Position = 0;
                WriteHeader(dataBytes);
            }

            writer?.Dispose();
            stream?.Dispose();
            writer = null;
            stream = null;
        }

        private void WriteHeader(long dataLength)
        {
            if (writer == null)
            {
                return;
            }

            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write((int)(36 + dataLength));
            writer.Write(new[] { 'W', 'A', 'V', 'E' });
            writer.Write(new[] { 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * channels * 2);
            writer.Write((short)(channels * 2));
            writer.Write((short)16);
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write((int)dataLength);
        }

        private static int GetChannelCount(AudioSpeakerMode mode)
        {
            switch (mode)
            {
                case AudioSpeakerMode.Mono:
                    return 1;
                case AudioSpeakerMode.Quad:
                    return 4;
                case AudioSpeakerMode.Surround:
                    return 5;
                case AudioSpeakerMode.Mode5point1:
                    return 6;
                case AudioSpeakerMode.Mode7point1:
                    return 8;
                default:
                    return 2;
            }
        }
    }
}
