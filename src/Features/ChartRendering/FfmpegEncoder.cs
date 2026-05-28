using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace ADOFAI.EditorTweaks.Features.ChartRendering
{
    internal sealed class FfmpegEncoder : IDisposable
    {
        private static readonly object EncoderProbeLock = new object();
        private static bool? nvencAvailable;

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

        public string EncoderName { get; private set; } = "unknown";

        public void BeginVideo()
        {
            string args = "-y -f rawvideo -pixel_format rgba "
                + "-video_size " + width + "x" + height + " "
                + "-framerate " + fps + " "
                + "-i - -an -vf vflip "
                + GetVideoEncoderArguments() + " "
                + "-pix_fmt yuv420p "
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

        public void WriteFrame(byte[] frame, int length, int repeatCount, Action<byte[]>? release)
        {
            if (process == null || process.HasExited)
            {
                throw new InvalidOperationException("FFmpeg video process is not running.");
            }

            if (writerException != null)
            {
                throw new InvalidOperationException("FFmpeg writer failed.", writerException);
            }

            frameQueue!.Add(new QueuedFrame(frame, length, Math.Max(1, repeatCount), release));
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

        public void MuxAudioFile(string audioPath)
        {
            if (!File.Exists(audioPath))
            {
                throw new InvalidOperationException("Captured audio file does not exist: " + audioPath);
            }

            long videoBytes = File.Exists(tempVideoPath) ? new FileInfo(tempVideoPath).Length : 0L;
            long audioBytes = new FileInfo(audioPath).Length;
            ChartRenderDiagnostics.Log("Muxing captured audio. videoBytes=" + videoBytes + " audioBytes=" + audioBytes);

            string args = "-y -i " + Quote(tempVideoPath) + " "
                + "-i " + Quote(audioPath) + " "
                + "-map 0:v:0 -map 1:a:0 -c:v copy -c:a aac -b:a 320k -ac 2 -movflags +faststart "
                + Quote(finalVideoPath);
            RunMuxProcess(args);
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
                        for (int i = 0; i < frame.RepeatCount; i++)
                        {
                            process!.StandardInput.BaseStream.Write(frame.Buffer, 0, frame.Length);
                        }
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

        private string GetVideoEncoderArguments()
        {
            if (!ForcesCpuEncoder() && IsNvencAvailable())
            {
                EncoderName = "h264_nvenc";
                Main.Log("Chart renderer using h264_nvenc.");
                return "-c:v h264_nvenc -preset p1 -tune ll -rc constqp -qp " + Math.Max(0, Math.Min(51, crf)) + " -g " + Math.Max(1, fps * 2);
            }

            EncoderName = "libx264";
            Main.Log("Chart renderer using libx264.");
            return "-c:v libx264 -preset " + Quote(GetRealtimePreset()) + " -crf " + crf + " -g " + Math.Max(1, fps * 2);
        }

        private bool ForcesCpuEncoder()
        {
            string lowered = preset.ToLowerInvariant();
            return lowered == "cpu" || lowered == "x264" || lowered.StartsWith("x264:", StringComparison.Ordinal);
        }

        private bool IsNvencAvailable()
        {
            lock (EncoderProbeLock)
            {
                if (nvencAvailable.HasValue)
                {
                    return nvencAvailable.Value;
                }

                nvencAvailable = ProbeEncoder("h264_nvenc");
                return nvencAvailable.Value;
            }
        }

        private bool ProbeEncoder(string encoder)
        {
            try
            {
                ProcessStartInfo info = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = "-hide_banner -loglevel error -y -f lavfi -i color=size=256x256:rate=1:duration=0.1 -c:v " + encoder + " -frames:v 1 -f null -",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };

                using (Process? probe = Process.Start(info))
                {
                    if (probe == null)
                    {
                        return false;
                    }

                    if (!probe.WaitForExit(5000))
                    {
                        try
                        {
                            probe.Kill();
                        }
                        catch
                        {
                        }

                        return false;
                    }

                    return probe.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        private sealed class QueuedFrame
        {
            public QueuedFrame(byte[] buffer, int length, int repeatCount, Action<byte[]>? release)
            {
                Buffer = buffer;
                Length = length;
                RepeatCount = repeatCount;
                Release = release;
            }

            public byte[] Buffer { get; }

            public int Length { get; }

            public int RepeatCount { get; }

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

            return started;
        }

        private void RunMuxProcess(string arguments)
        {
            ProcessStartInfo info = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = "-hide_banner -loglevel warning " + arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            StringBuilder stderr = new StringBuilder();
            StringBuilder stdout = new StringBuilder();
            ChartRenderDiagnostics.Log("FFmpeg mux args: " + arguments);

            Process? mux = Process.Start(info);
            if (mux == null)
            {
                throw new InvalidOperationException("Failed to start FFmpeg.");
            }

            process = mux;
            try
            {
                using (mux)
                {
                    mux.ErrorDataReceived += (_, e) =>
                    {
                        if (e.Data != null)
                        {
                            stderr.AppendLine(e.Data);
                        }
                    };
                    mux.OutputDataReceived += (_, e) =>
                    {
                        if (e.Data != null)
                        {
                            stdout.AppendLine(e.Data);
                        }
                    };
                    mux.BeginErrorReadLine();
                    mux.BeginOutputReadLine();
                    mux.WaitForExit();

                    if (mux.ExitCode != 0)
                    {
                        string detail = BuildProcessFailureDetail(stdout.ToString(), stderr.ToString());
                        ChartRenderDiagnostics.Log("FFmpeg audio mux failed with exit code " + mux.ExitCode + "." + detail);
                        throw new InvalidOperationException("FFmpeg audio mux failed with exit code " + mux.ExitCode + "." + detail);
                    }
                }
            }
            finally
            {
                process = null;
            }
        }

        private static string BuildProcessFailureDetail(string stdout, string stderr)
        {
            StringBuilder detail = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(stderr))
            {
                detail.AppendLine().Append("stderr: ").Append(stderr.Trim());
            }

            if (!string.IsNullOrWhiteSpace(stdout))
            {
                detail.AppendLine().Append("stdout: ").Append(stdout.Trim());
            }

            return detail.ToString();
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }
}
