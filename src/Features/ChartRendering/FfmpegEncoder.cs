using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace ADOFAI.EditorTweaks.Features.ChartRendering
{
    internal sealed class FfmpegEncoder : IDisposable
    {
        private readonly string ffmpegPath;
        private readonly string tempVideoPath;
        private readonly string finalVideoPath;
        private readonly int width;
        private readonly int height;
        private readonly int fps;
        private readonly int crf;
        private readonly string preset;
        private Process? process;
        private BlockingCollection<QueuedFrame>? frameQueue;
        private Thread? writerThread;
        private Exception? writerException;

        public FfmpegEncoder(string ffmpegPath, string tempVideoPath, string finalVideoPath, int width, int height, int fps, int crf, string preset)
        {
            this.ffmpegPath = ffmpegPath;
            this.tempVideoPath = tempVideoPath;
            this.finalVideoPath = finalVideoPath;
            this.width = width;
            this.height = height;
            this.fps = fps;
            this.crf = crf;
            this.preset = string.IsNullOrWhiteSpace(preset) ? "veryfast" : preset.Trim();
        }

        public void BeginVideo()
        {
            string args = "-y -f rawvideo -pixel_format rgb24 "
                + "-video_size " + width + "x" + height + " "
                + "-framerate " + fps + " "
                + "-i - -an -c:v libx264 -preset " + Quote(GetRealtimePreset()) + " "
                + "-crf " + crf + " -vf vflip -pix_fmt yuv420p "
                + Quote(tempVideoPath);

            process = StartProcess(args, redirectInput: true);
            frameQueue = new BlockingCollection<QueuedFrame>(boundedCapacity: Math.Max(90, fps));
            writerException = null;
            writerThread = new Thread(WriteLoop)
            {
                IsBackground = true,
                Name = "ADOFAI.EditorTweaks.FFmpegVideoWriter"
            };
            writerThread.Start();
        }

        public void WriteFrame(byte[] frame, int length, Action<byte[]>? release)
        {
            if (process == null || process.HasExited)
            {
                throw new InvalidOperationException("FFmpeg video process is not running.");
            }

            if (writerException != null)
            {
                throw new InvalidOperationException("FFmpeg writer failed.", writerException);
            }

            frameQueue!.Add(new QueuedFrame(frame, length, release));
        }

        public void CompleteVideo()
        {
            if (process == null)
            {
                return;
            }

            frameQueue?.CompleteAdding();
            writerThread?.Join();
            if (writerException != null)
            {
                throw new InvalidOperationException("FFmpeg writer failed.", writerException);
            }

            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException("FFmpeg video encoding failed with exit code " + process.ExitCode + ".");
            }

            process.Dispose();
            process = null;
            frameQueue?.Dispose();
            frameQueue = null;
            writerThread = null;
        }

        public void MuxAudio(ChartRenderAudioMixPlan audioPlan)
        {
            string scriptPath = Path.Combine(Path.GetDirectoryName(tempVideoPath) ?? string.Empty, "audio_mix.ffscript");
            List<ChartRenderAudioClip> clips = audioPlan.Events
                .Select(e => e.Clip)
                .GroupBy(c => c.Name)
                .Select(g => g.First())
                .ToList();
            File.WriteAllText(scriptPath, BuildAudioFilterScript(audioPlan, clips), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            StringBuilder inputs = new StringBuilder();
            inputs.Append("-y -i ").Append(Quote(tempVideoPath)).Append(' ');
            inputs.Append("-i ").Append(Quote(audioPlan.SongPath)).Append(' ');
            foreach (ChartRenderAudioClip clip in clips)
            {
                inputs.Append("-i ").Append(Quote(clip.Path)).Append(' ');
            }

            string args = inputs
                + "-filter_complex_script " + Quote(scriptPath) + " "
                + "-map 0:v:0 -map " + Quote("[mix]") + " -c:v copy -c:a aac -b:a 320k -movflags +faststart "
                + Quote(finalVideoPath);
            Process mux = StartProcess(args, redirectInput: false);
            process = mux;
            try
            {
                using (mux)
                {
                    mux.WaitForExit();
                    if (mux.ExitCode != 0)
                    {
                        throw new InvalidOperationException("FFmpeg audio mux failed with exit code " + mux.ExitCode + ".");
                    }
                }
            }
            finally
            {
                process = null;
            }
        }

        private static string BuildAudioFilterScript(ChartRenderAudioMixPlan plan, IReadOnlyList<ChartRenderAudioClip> clips)
        {
            StringBuilder script = new StringBuilder();
            List<string> mixLabels = new List<string>();

            List<string> baseFilters = new List<string>();
            double trimStart = Math.Max(0.0, -plan.SongDelaySeconds * Math.Max(0.01, plan.SongPitch));
            if (trimStart > 0.000001)
            {
                baseFilters.Add("atrim=start=" + ChartRenderAudioFormat.Number(trimStart));
                baseFilters.Add("asetpts=PTS-STARTPTS");
            }

            baseFilters.AddRange(BuildAtempoFilters(plan.SongPitch));
            if (Math.Abs(plan.SongVolume - 1f) > 0.0001f)
            {
                baseFilters.Add("volume=" + ChartRenderAudioFormat.Number(plan.SongVolume));
            }

            double songDelay = Math.Max(0.0, plan.SongDelaySeconds);
            if (songDelay > 0.000001)
            {
                int delayMs = SecondsToMilliseconds(songDelay);
                baseFilters.Add("adelay=" + delayMs + "|" + delayMs);
            }

            baseFilters.Add("aformat=sample_fmts=fltp:channel_layouts=stereo");
            script.Append("[1:a]").Append(string.Join(",", baseFilters)).Append("[base];").AppendLine();
            mixLabels.Add("[base]");

            Dictionary<string, int> clipIndexes = new Dictionary<string, int>();
            for (int i = 0; i < clips.Count; i++)
            {
                clipIndexes[clips[i].Name] = i;
            }

            List<IGrouping<string, ChartRenderAudioEvent>> groups = plan.Events
                .Where(e => e.Volume > 0.0001f)
                .GroupBy(e => e.Clip.Name)
                .ToList();
            int soundIndex = 0;
            foreach (IGrouping<string, ChartRenderAudioEvent> group in groups)
            {
                int clipInputIndex = 2 + clipIndexes[group.Key];
                ChartRenderAudioEvent[] events = group.ToArray();
                string[] splitLabels = new string[events.Length];
                for (int i = 0; i < events.Length; i++)
                {
                    splitLabels[i] = "[c" + clipIndexes[group.Key] + "_" + i + "]";
                }

                script.Append("[").Append(clipInputIndex).Append(":a]aformat=sample_fmts=fltp:channel_layouts=stereo");
                if (events.Length == 1)
                {
                    script.Append(splitLabels[0]).Append(";").AppendLine();
                }
                else
                {
                    script.Append(",asplit=").Append(events.Length).Append(string.Concat(splitLabels)).Append(";").AppendLine();
                }

                for (int i = 0; i < events.Length; i++)
                {
                    ChartRenderAudioEvent evnt = events[i];
                    double outputStart = Math.Max(0.0, evnt.StartSeconds);
                    double trim = Math.Max(0.0, -evnt.StartSeconds);
                    double duration = evnt.Loops
                        ? Math.Max(0.001, evnt.EndSeconds - outputStart)
                        : -1.0;

                    List<string> filters = new List<string>();
                    if (evnt.Loops)
                    {
                        filters.Add("aloop=loop=-1:size=" + Math.Max(1, evnt.Clip.Samples));
                    }

                    if (trim > 0.000001 || duration > 0.0)
                    {
                        string atrim = "atrim";
                        if (trim > 0.000001)
                        {
                            atrim += "=start=" + ChartRenderAudioFormat.Number(trim);
                            if (duration > 0.0)
                            {
                                atrim += ":duration=" + ChartRenderAudioFormat.Number(duration);
                            }
                        }
                        else if (duration > 0.0)
                        {
                            atrim += "=duration=" + ChartRenderAudioFormat.Number(duration);
                        }

                        filters.Add(atrim);
                    }

                    filters.Add("asetpts=PTS-STARTPTS");
                    if (Math.Abs(evnt.Volume - 1f) > 0.0001f)
                    {
                        filters.Add("volume=" + ChartRenderAudioFormat.Number(evnt.Volume));
                    }

                    int delay = SecondsToMilliseconds(outputStart);
                    if (delay > 0)
                    {
                        filters.Add("adelay=" + delay + "|" + delay);
                    }

                    string label = "[s" + soundIndex++ + "]";
                    script.Append(splitLabels[i]).Append(string.Join(",", filters)).Append(label).Append(";").AppendLine();
                    mixLabels.Add(label);
                }
            }

            if (mixLabels.Count == 1)
            {
                script.Append(mixLabels[0]).Append("atrim=duration=").Append(ChartRenderAudioFormat.Number(plan.DurationSeconds)).Append("[mix];").AppendLine();
            }
            else
            {
                script.Append(string.Concat(mixLabels))
                    .Append("amix=inputs=").Append(mixLabels.Count)
                    .Append(":duration=longest:dropout_transition=0,atrim=duration=")
                    .Append(ChartRenderAudioFormat.Number(plan.DurationSeconds))
                    .Append("[mix];")
                    .AppendLine();
            }

            return script.ToString();
        }

        private static IEnumerable<string> BuildAtempoFilters(double tempo)
        {
            tempo = Math.Max(0.01, tempo);
            while (tempo > 2.0)
            {
                yield return "atempo=2";
                tempo /= 2.0;
            }

            while (tempo < 0.5)
            {
                yield return "atempo=0.5";
                tempo /= 0.5;
            }

            if (Math.Abs(tempo - 1.0) > 0.0001)
            {
                yield return "atempo=" + ChartRenderAudioFormat.Number(tempo);
            }
        }

        private static int SecondsToMilliseconds(double seconds)
        {
            return Math.Max(0, (int)Math.Round(seconds * 1000.0));
        }

        public void Dispose()
        {
            if (process != null)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }
                }
                catch
                {
                }

                process.Dispose();
                process = null;
            }

            if (frameQueue != null)
            {
                if (!frameQueue.IsAddingCompleted)
                {
                    frameQueue.CompleteAdding();
                }

                frameQueue.Dispose();
                frameQueue = null;
            }
        }

        private void WriteLoop()
        {
            try
            {
                foreach (QueuedFrame frame in frameQueue!.GetConsumingEnumerable())
                {
                    try
                    {
                        process!.StandardInput.BaseStream.Write(frame.Buffer, 0, frame.Length);
                    }
                    finally
                    {
                        frame.Release?.Invoke(frame.Buffer);
                    }
                }

                process!.StandardInput.Close();
            }
            catch (Exception ex)
            {
                writerException = ex;
            }
        }

        private string GetRealtimePreset()
        {
            return string.IsNullOrWhiteSpace(preset) || preset == "veryfast" ? "ultrafast" : preset;
        }

        private sealed class QueuedFrame
        {
            public QueuedFrame(byte[] buffer, int length, Action<byte[]>? release)
            {
                Buffer = buffer;
                Length = length;
                Release = release;
            }

            public byte[] Buffer { get; }

            public int Length { get; }

            public Action<byte[]>? Release { get; }
        }

        private Process StartProcess(string arguments, bool redirectInput)
        {
            ProcessStartInfo info = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = "-hide_banner -loglevel warning " + arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = redirectInput
            };

            Process? started = Process.Start(info);
            if (started == null)
            {
                throw new InvalidOperationException("Failed to start FFmpeg.");
            }

            try
            {
                started.PriorityClass = ProcessPriorityClass.BelowNormal;
            }
            catch
            {
            }

            return started;
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }
}
