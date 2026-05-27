using System;
using System.Diagnostics;
using System.IO;

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
            string args = "-y -f rawvideo -pixel_format rgba "
                + "-video_size " + width + "x" + height + " "
                + "-framerate " + fps + " "
                + "-i - -an -c:v libx264 -preset " + Quote(preset) + " "
                + "-crf " + crf + " -pix_fmt yuv420p "
                + Quote(tempVideoPath);

            process = StartProcess(args, redirectInput: true);
        }

        public void WriteFrame(byte[] frame)
        {
            if (process == null || process.HasExited)
            {
                throw new InvalidOperationException("FFmpeg video process is not running.");
            }

            process.StandardInput.BaseStream.Write(frame, 0, frame.Length);
        }

        public void CompleteVideo()
        {
            if (process == null)
            {
                return;
            }

            process.StandardInput.Close();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException("FFmpeg video encoding failed with exit code " + process.ExitCode + ".");
            }

            process.Dispose();
            process = null;
        }

        public void MuxAudio(string audioPath)
        {
            string args = "-y -i " + Quote(tempVideoPath) + " -i " + Quote(audioPath)
                + " -c:v copy -c:a aac -shortest " + Quote(finalVideoPath);
            using (Process mux = StartProcess(args, redirectInput: false))
            {
                mux.WaitForExit();
                if (mux.ExitCode != 0)
                {
                    throw new InvalidOperationException("FFmpeg audio mux failed with exit code " + mux.ExitCode + ".");
                }
            }
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

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }
}
