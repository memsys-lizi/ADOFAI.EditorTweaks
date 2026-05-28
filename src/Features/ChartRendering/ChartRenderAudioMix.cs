using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace ADOFAI.EditorTweaks.Features.ChartRendering
{
    internal sealed class ChartRenderAudioRecorder : IDisposable
    {
        private readonly object sync = new object();
        private readonly List<RecordedSound> sounds = new List<RecordedSound>();
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private bool seededConductorSounds;

        private static readonly BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly FieldInfo? HitSoundsDataField = typeof(scrConductor).GetField("hitSoundsData", InstanceFields);
        private static readonly FieldInfo? HoldSoundsDataField = typeof(scrConductor).GetField("holdSoundsData", InstanceFields);
        private static readonly MethodInfo? AudioClipGetDataMethod = typeof(AudioClip).GetMethod("GetData", new[] { typeof(float[]), typeof(int) });

        private ChartRenderAudioRecorder()
        {
        }

        public static ChartRenderAudioRecorder? Active { get; private set; }

        public static ChartRenderAudioRecorder Begin()
        {
            ChartRenderAudioRecorder recorder = new ChartRenderAudioRecorder();
            Active = recorder;
            return recorder;
        }

        public static void RecordScheduledSound(string clipName, double dspTime, float volume, double endDspTime = -1.0)
        {
            Active?.Record(clipName, dspTime, volume, endDspTime);
        }

        public void SeedConductorScheduledSounds(scrConductor conductor)
        {
            if (seededConductorSounds || conductor == null)
            {
                return;
            }

            seededConductorSounds = true;
            try
            {
                RecordHitSounds(HitSoundsDataField?.GetValue(conductor) as IEnumerable);
                RecordHoldSounds(HoldSoundsDataField?.GetValue(conductor) as IEnumerable);
                RecordExtraTicks(conductor.extraTicksCountdown, conductor.hitSoundVolume);
            }
            catch (Exception ex)
            {
                Main.Log("Failed to seed conductor sounds: " + ex.Message);
            }
        }

        public ChartRenderAudioMixPlan CreateMixPlan(string songPath, string tempDirectory, ChartRenderAudioTiming timing)
        {
            string clipDirectory = Path.Combine(tempDirectory, "AudioClips");
            Directory.CreateDirectory(clipDirectory);

            Dictionary<string, ChartRenderAudioClip> exportedClips = new Dictionary<string, ChartRenderAudioClip>();
            List<ChartRenderAudioEvent> exportedEvents = new List<ChartRenderAudioEvent>();

            foreach (RecordedSound sound in SnapshotSounds())
            {
                AudioClip? clip = GetClip(sound.ClipName);
                if (clip == null)
                {
                    continue;
                }

                if (!exportedClips.TryGetValue(sound.ClipName, out ChartRenderAudioClip exportedClip))
                {
                    string clipPath = Path.Combine(clipDirectory, MakeSafeFileName(sound.ClipName) + ".wav");
                    if (!TryWriteWav(clip, clipPath))
                    {
                        continue;
                    }

                    exportedClip = new ChartRenderAudioClip(sound.ClipName, clipPath, clip.samples, clip.frequency, clip.channels, clip.length);
                    exportedClips.Add(sound.ClipName, exportedClip);
                }

                double start = sound.DspTime - timing.OutputZeroDsp;
                bool loops = sound.EndDspTime > sound.DspTime;
                double end = loops ? sound.EndDspTime - timing.OutputZeroDsp : -1.0;
                if (end > 0.0 && end <= start)
                {
                    continue;
                }

                if (loops && end <= 0.0)
                {
                    continue;
                }

                if (end < 0.0 && start + exportedClip.Length <= 0.0)
                {
                    continue;
                }

                if (start > timing.DurationSeconds + 1.0)
                {
                    continue;
                }

                exportedEvents.Add(new ChartRenderAudioEvent(exportedClip, start, end, Math.Max(0f, sound.Volume)));
            }

            exportedEvents.Sort((a, b) =>
            {
                int byTime = a.StartSeconds.CompareTo(b.StartSeconds);
                return byTime != 0 ? byTime : string.CompareOrdinal(a.Clip.Name, b.Clip.Name);
            });
            exportedEvents = RemoveDuplicates(exportedEvents);

            return new ChartRenderAudioMixPlan(
                songPath,
                timing.SongDelaySeconds,
                timing.SongPitch,
                timing.SongVolume,
                timing.DurationSeconds,
                exportedEvents);
        }

        public void Dispose()
        {
            if (ReferenceEquals(Active, this))
            {
                Active = null;
            }
        }

        private void Record(string clipName, double dspTime, float volume, double endDspTime)
        {
            if (string.IsNullOrWhiteSpace(clipName) || double.IsNaN(dspTime) || double.IsInfinity(dspTime))
            {
                return;
            }

            AudioClip? clip = null;
            try
            {
                clip = AudioManager.Instance.FindOrLoadAudioClip(clipName);
            }
            catch
            {
            }

            lock (sync)
            {
                sounds.Add(new RecordedSound(clipName, dspTime, volume, endDspTime));
                if (clip != null && !clips.ContainsKey(clipName))
                {
                    clips.Add(clipName, clip);
                }
            }
        }

        private void RecordHitSounds(IEnumerable? hitSounds)
        {
            if (hitSounds == null)
            {
                return;
            }

            foreach (object item in hitSounds)
            {
                Type type = item.GetType();
                object? hitSound = type.GetField("hitSound", InstanceFields)?.GetValue(item);
                double time = Convert.ToDouble(type.GetField("time", InstanceFields)?.GetValue(item), CultureInfo.InvariantCulture);
                float volume = Convert.ToSingle(type.GetField("volume", InstanceFields)?.GetValue(item), CultureInfo.InvariantCulture);
                if (hitSound != null)
                {
                    Record("snd" + hitSound, time, volume, -1.0);
                }
            }
        }

        private void RecordHoldSounds(IEnumerable? holdSounds)
        {
            if (holdSounds == null)
            {
                return;
            }

            foreach (object item in holdSounds)
            {
                Type type = item.GetType();
                string? name = type.GetField("name", InstanceFields)?.GetValue(item) as string;
                double time = Convert.ToDouble(type.GetField("time", InstanceFields)?.GetValue(item), CultureInfo.InvariantCulture);
                double endTime = Convert.ToDouble(type.GetField("endTime", InstanceFields)?.GetValue(item), CultureInfo.InvariantCulture);
                float volume = Convert.ToSingle(type.GetField("volume", InstanceFields)?.GetValue(item), CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    Record(name!, time, volume, endTime);
                }
            }
        }

        private void RecordExtraTicks(IEnumerable<scrConductor.ExtraTickData>? extraTicks, float volume)
        {
            if (extraTicks == null)
            {
                return;
            }

            foreach (scrConductor.ExtraTickData tick in extraTicks)
            {
                Record("sndHat", tick.time, volume, -1.0);
            }
        }

        private AudioClip? GetClip(string clipName)
        {
            lock (sync)
            {
                if (clips.TryGetValue(clipName, out AudioClip clip))
                {
                    return clip;
                }
            }

            try
            {
                return AudioManager.Instance.FindOrLoadAudioClip(clipName);
            }
            catch
            {
                return null;
            }
        }

        private List<RecordedSound> SnapshotSounds()
        {
            lock (sync)
            {
                return new List<RecordedSound>(sounds);
            }
        }

        private static bool TryWriteWav(AudioClip clip, string path)
        {
            try
            {
                float[] samples = new float[clip.samples * clip.channels];
                if (!TryGetAudioClipData(clip, samples))
                {
                    return false;
                }

                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream, Encoding.ASCII))
                {
                    int dataLength = samples.Length * 2;
                    writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                    writer.Write(36 + dataLength);
                    writer.Write(Encoding.ASCII.GetBytes("WAVE"));
                    writer.Write(Encoding.ASCII.GetBytes("fmt "));
                    writer.Write(16);
                    writer.Write((short)1);
                    writer.Write((short)clip.channels);
                    writer.Write(clip.frequency);
                    writer.Write(clip.frequency * clip.channels * 2);
                    writer.Write((short)(clip.channels * 2));
                    writer.Write((short)16);
                    writer.Write(Encoding.ASCII.GetBytes("data"));
                    writer.Write(dataLength);

                    for (int i = 0; i < samples.Length; i++)
                    {
                        float clamped = Mathf.Clamp(samples[i], -1f, 1f);
                        writer.Write((short)Mathf.RoundToInt(clamped * 32767f));
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Main.Log("Failed to export audio clip " + clip.name + ": " + ex.Message);
                return false;
            }
        }

        private static bool TryGetAudioClipData(AudioClip clip, float[] samples)
        {
            object? result = AudioClipGetDataMethod?.Invoke(clip, new object[] { samples, 0 });
            return result is bool success && success;
        }

        private static string MakeSafeFileName(string raw)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                raw = raw.Replace(c, '_');
            }

            return raw;
        }

        private static List<ChartRenderAudioEvent> RemoveDuplicates(List<ChartRenderAudioEvent> events)
        {
            List<ChartRenderAudioEvent> result = new List<ChartRenderAudioEvent>();
            HashSet<string> seen = new HashSet<string>();
            foreach (ChartRenderAudioEvent evnt in events)
            {
                string key = evnt.Clip.Name
                    + "|"
                    + Math.Round(evnt.StartSeconds, 3).ToString(CultureInfo.InvariantCulture)
                    + "|"
                    + Math.Round(evnt.EndSeconds, 3).ToString(CultureInfo.InvariantCulture)
                    + "|"
                    + Math.Round(evnt.Volume, 3).ToString(CultureInfo.InvariantCulture);
                if (seen.Add(key))
                {
                    result.Add(evnt);
                }
            }

            return result;
        }

        private sealed class RecordedSound
        {
            public RecordedSound(string clipName, double dspTime, float volume, double endDspTime)
            {
                ClipName = clipName;
                DspTime = dspTime;
                Volume = volume;
                EndDspTime = endDspTime;
            }

            public string ClipName { get; }

            public double DspTime { get; }

            public float Volume { get; }

            public double EndDspTime { get; }
        }
    }

    internal sealed class ChartRenderAudioTiming
    {
        public ChartRenderAudioTiming(double outputZeroDsp, double songDelaySeconds, float songPitch, float songVolume, double durationSeconds)
        {
            OutputZeroDsp = outputZeroDsp;
            SongDelaySeconds = songDelaySeconds;
            SongPitch = songPitch;
            SongVolume = songVolume;
            DurationSeconds = durationSeconds;
        }

        public double OutputZeroDsp { get; }

        public double SongDelaySeconds { get; }

        public float SongPitch { get; }

        public float SongVolume { get; }

        public double DurationSeconds { get; }
    }

    internal sealed class ChartRenderAudioMixPlan
    {
        public ChartRenderAudioMixPlan(string songPath, double songDelaySeconds, float songPitch, float songVolume, double durationSeconds, IReadOnlyList<ChartRenderAudioEvent> events)
        {
            SongPath = songPath;
            SongDelaySeconds = songDelaySeconds;
            SongPitch = songPitch;
            SongVolume = songVolume;
            DurationSeconds = durationSeconds;
            Events = events;
        }

        public string SongPath { get; }

        public double SongDelaySeconds { get; }

        public float SongPitch { get; }

        public float SongVolume { get; }

        public double DurationSeconds { get; }

        public IReadOnlyList<ChartRenderAudioEvent> Events { get; }
    }

    internal sealed class ChartRenderAudioClip
    {
        public ChartRenderAudioClip(string name, string path, int samples, int frequency, int channels, float length)
        {
            Name = name;
            Path = path;
            Samples = samples;
            Frequency = frequency;
            Channels = channels;
            Length = length;
        }

        public string Name { get; }

        public string Path { get; }

        public int Samples { get; }

        public int Frequency { get; }

        public int Channels { get; }

        public float Length { get; }
    }

    internal sealed class ChartRenderAudioEvent
    {
        public ChartRenderAudioEvent(ChartRenderAudioClip clip, double startSeconds, double endSeconds, float volume)
        {
            Clip = clip;
            StartSeconds = startSeconds;
            EndSeconds = endSeconds;
            Volume = volume;
        }

        public ChartRenderAudioClip Clip { get; }

        public double StartSeconds { get; }

        public double EndSeconds { get; }

        public float Volume { get; }

        public bool Loops => EndSeconds > StartSeconds;
    }

    internal static class ChartRenderAudioFormat
    {
        public static string Number(double value)
        {
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }
    }
}
