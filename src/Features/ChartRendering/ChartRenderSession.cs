using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;

namespace ADOFAI.EditorTweaks.Features.ChartRendering
{
    internal sealed class ChartRenderSession
    {
        private const int MaxPendingGpuFrames = 8;

        private readonly UnityModManager.ModEntry modEntry;
        private readonly Settings settings;
        private readonly Stopwatch stopwatch = new Stopwatch();
        private readonly Queue<ChartFrameCapture.PendingFrame> pendingFrames = new Queue<ChartFrameCapture.PendingFrame>();
        private readonly Queue<byte[]> frameBufferPool = new Queue<byte[]>();
        private readonly object frameBufferPoolLock = new object();
        private static readonly AccessTools.FieldRef<scrConductor, double> DspTimeSong =
            AccessTools.FieldRefAccess<scrConductor, double>("dspTimeSong");

        private bool cancelRequested;
        private int frameIndex;
        private int totalFrames = 1;
        private int frameByteLength;
        private double renderDurationSeconds = 1.0;
        private string tempDirectory = string.Empty;
        private string tempVideoPath = string.Empty;
        private string songAudioPath = string.Empty;
        private string outputPath = string.Empty;
        private RenderState? savedState;
        private ChartRenderAudioRecorder? audioRecorder;

        public ChartRenderSession(UnityModManager.ModEntry modEntry, Settings settings)
        {
            this.modEntry = modEntry;
            this.settings = settings;
        }

        public bool IsActive { get; private set; }

        public static bool IsRendering { get; private set; }

        public float Progress => totalFrames <= 0 ? 0f : (float)frameIndex / totalFrames;

        public string StageText { get; private set; } = Settings.Text("chartRendererRendering");

        public string DetailText { get; private set; } = string.Empty;

        public string TimingText
        {
            get
            {
                double elapsed = Math.Max(0.001, stopwatch.Elapsed.TotalSeconds);
                double fps = frameIndex / elapsed;
                double remaining = fps <= 0.001 ? 0.0 : (totalFrames - frameIndex) / fps;
                return $"{frameIndex}/{totalFrames}  |  {fps:0.0} fps  |  ETA {TimeSpan.FromSeconds(Math.Max(0, remaining)):hh\\:mm\\:ss}";
            }
        }

        public void Cancel()
        {
            cancelRequested = true;
        }

        public IEnumerator Run(Action<ChartRenderResult> onComplete)
        {
            IsActive = true;
            IsRendering = true;
            stopwatch.Restart();
            frameIndex = 0;
            frameByteLength = Math.Max(1, settings.ChartRenderWidth * settings.ChartRenderHeight * 3);
            pendingFrames.Clear();

            ChartFrameCapture? frameCapture = null;
            FfmpegEncoder? encoder = null;
            ChartRenderAudioMixPlan? audioMixPlan = null;
            ChartRenderResult result = new ChartRenderResult();
            Exception? failure;

            if (!TryPrepare(result, out failure))
            {
                result.Success = false;
                result.Message = failure?.Message ?? result.Message;
                Finish(onComplete, result);
                yield break;
            }

            yield return null;

            if (!TryStartOfficialPlayback(out failure))
            {
                result.Success = false;
                result.Message = failure?.Message ?? "Failed to start editor playback.";
                Cleanup(frameCapture, encoder, restoreEditor: true, deleteTemp: true);
                Finish(onComplete, result);
                yield break;
            }

            int waitFrames = 0;
            while (!cancelRequested && waitFrames < settings.ChartRenderFps * 10)
            {
                if (ADOBase.conductor != null && IsPlaybackScheduled())
                {
                    break;
                }

                waitFrames++;
                DetailText = "Waiting for level playback...";
                yield return null;
            }

            if (cancelRequested)
            {
                result.Success = false;
                result.Message = "Canceled.";
                Cleanup(frameCapture, encoder, restoreEditor: true, deleteTemp: true);
                Finish(onComplete, result);
                yield break;
            }

            if (ADOBase.conductor == null || !IsPlaybackScheduled())
            {
                result.Success = false;
                result.Message = "Timed out while waiting for level playback.";
                Cleanup(frameCapture, encoder, restoreEditor: true, deleteTemp: true);
                Finish(onComplete, result);
                yield break;
            }

            if (!Try(() =>
            {
                renderDurationSeconds = CalculateTotalDuration();
                totalFrames = Math.Max(1, Mathf.CeilToInt((float)(renderDurationSeconds * settings.ChartRenderFps)));
                frameCapture = new ChartFrameCapture(settings.ChartRenderWidth, settings.ChartRenderHeight);
                encoder = new FfmpegEncoder(ChartRenderPaths.GetFfmpegPath(), tempVideoPath, outputPath, settings.ChartRenderWidth, settings.ChartRenderHeight, settings.ChartRenderFps, settings.ChartRenderCrf, settings.ChartRenderPreset);
                encoder.BeginVideo();
            }, out failure))
            {
                result.Success = false;
                result.Message = failure?.Message ?? "Failed to initialize renderer.";
                Cleanup(frameCapture, encoder, restoreEditor: true, deleteTemp: true);
                Finish(onComplete, result);
                yield break;
            }

            int requestedFrames = 0;
            while (requestedFrames < totalFrames && !cancelRequested && failure == null)
            {
                while (pendingFrames.Count >= MaxPendingGpuFrames && !cancelRequested)
                {
                    if (!Try(() => DrainReadyFrames(encoder!), out failure))
                    {
                        break;
                    }

                    yield return null;
                }

                if (failure != null || cancelRequested)
                {
                    break;
                }

                yield return new WaitForEndOfFrame();

                double captureUntil = GetCurrentCaptureTime();
                if (captureUntil < 0.0)
                {
                    DetailText = Path.GetFileName(outputPath);
                    continue;
                }

                if (IsPlaybackFinished())
                {
                    captureUntil = double.PositiveInfinity;
                }

                if (!Try(() =>
                {
                    while (requestedFrames < totalFrames
                        && pendingFrames.Count < MaxPendingGpuFrames
                        && GetFrameTime(requestedFrames) <= captureUntil)
                    {
                        pendingFrames.Enqueue(frameCapture!.RequestFrame(requestedFrames));
                        requestedFrames++;
                    }

                    DrainReadyFrames(encoder!);
                }, out failure))
                {
                    break;
                }

                DetailText = Path.GetFileName(outputPath);
            }

            while (pendingFrames.Count > 0 && !cancelRequested && failure == null)
            {
                if (!Try(() => DrainReadyFrames(encoder!), out failure))
                {
                    break;
                }

                yield return null;
            }

            frameCapture?.Dispose();
            frameCapture = null;
            RestoreState();

            if (cancelRequested)
            {
                result.Success = false;
                result.Message = "Canceled.";
                Cleanup(frameCapture, encoder, restoreEditor: false, deleteTemp: true);
                Finish(onComplete, result);
                yield break;
            }

            if (failure != null)
            {
                result.Success = false;
                result.Message = failure.Message;
                Cleanup(frameCapture, encoder, restoreEditor: false, deleteTemp: true);
                Finish(onComplete, result);
                yield break;
            }

            if (!Try(() =>
            {
                audioMixPlan = audioRecorder!.CreateMixPlan(songAudioPath, tempDirectory, CreateAudioTiming());
                audioRecorder.Dispose();
                audioRecorder = null;
            }, out failure))
            {
                result.Success = false;
                result.Message = failure?.Message ?? "Failed to prepare audio mix.";
                Cleanup(frameCapture, encoder, restoreEditor: false, deleteTemp: true);
                Finish(onComplete, result);
                yield break;
            }

            StageText = "Finalizing video";
            DetailText = Path.GetFileName(tempVideoPath);
            bool backgroundOk = false;
            yield return RunBackground(() => encoder!.CompleteVideo(), encoder, result, deleteTempOnCancel: true, ok => backgroundOk = ok);
            if (!backgroundOk)
            {
                Finish(onComplete, result);
                yield break;
            }

            StageText = Settings.Text("chartRendererMuxing");
            DetailText = Path.GetFileName(outputPath);
            backgroundOk = false;
            yield return RunBackground(() => encoder!.MuxAudio(audioMixPlan!), encoder, result, deleteTempOnCancel: true, ok => backgroundOk = ok);
            if (!backgroundOk)
            {
                Finish(onComplete, result);
                yield break;
            }

            Cleanup(frameCapture, encoder, restoreEditor: false, deleteTemp: false);
            result.Success = true;
            result.OutputPath = outputPath;
            result.Message = "OK";
            WriteLog("Render complete: " + outputPath);
            Finish(onComplete, result);
        }

        private IEnumerator RunBackground(Action action, FfmpegEncoder? encoder, ChartRenderResult result, bool deleteTempOnCancel, Action<bool> onDone)
        {
            Exception? backgroundFailure = null;
            ManualResetEventSlim done = new ManualResetEventSlim(false);
            Thread thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    backgroundFailure = ex;
                }
                finally
                {
                    done.Set();
                }
            })
            {
                IsBackground = true,
                Name = "ADOFAI.EditorTweaks.ChartRenderWorker"
            };

            thread.Start();
            while (!done.IsSet)
            {
                if (cancelRequested)
                {
                    encoder?.Dispose();
                    result.Success = false;
                    result.Message = "Canceled.";
                    if (deleteTempOnCancel)
                    {
                        DeleteTempDirectory();
                    }

                    done.Dispose();
                    onDone(false);
                    yield break;
                }

                yield return null;
            }

            done.Dispose();
            if (backgroundFailure != null)
            {
                result.Success = false;
                result.Message = backgroundFailure.Message;
                Cleanup(null, encoder, restoreEditor: false, deleteTemp: true);
                onDone(false);
                yield break;
            }

            onDone(true);
        }

        private void Finish(Action<ChartRenderResult> onComplete, ChartRenderResult result)
        {
            IsActive = false;
            IsRendering = false;
            pendingFrames.Clear();
            onComplete(result);
        }

        private void DrainReadyFrames(FfmpegEncoder encoder)
        {
            while (pendingFrames.Count > 0 && pendingFrames.Peek().Done)
            {
                ChartFrameCapture.PendingFrame pending = pendingFrames.Dequeue();
                byte[] buffer = RentFrameBuffer();
                try
                {
                    pending.Complete(buffer);
                    encoder.WriteFrame(buffer, pending.ByteLength, ReturnFrameBuffer);
                    buffer = null!;
                }
                finally
                {
                    if (buffer != null)
                    {
                        ReturnFrameBuffer(buffer);
                    }
                }

                frameIndex++;
            }
        }

        private byte[] RentFrameBuffer()
        {
            lock (frameBufferPoolLock)
            {
                if (frameBufferPool.Count > 0)
                {
                    return frameBufferPool.Dequeue();
                }
            }

            return new byte[frameByteLength];
        }

        private void ReturnFrameBuffer(byte[] buffer)
        {
            if (buffer.Length < frameByteLength)
            {
                return;
            }

            lock (frameBufferPoolLock)
            {
                frameBufferPool.Enqueue(buffer);
            }
        }

        private bool TryPrepare(ChartRenderResult result, out Exception? failure)
        {
            failure = null;
            return Try(() =>
            {
                settings.EnsureDefaults(modEntry);
                string workspace = ChartRenderPaths.GetWorkspaceDirectory(settings);
                string export = ChartRenderPaths.GetExportDirectory(settings);
                Directory.CreateDirectory(workspace);
                Directory.CreateDirectory(export);

                tempDirectory = Path.Combine(workspace, "CurrentRender");
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, recursive: true);
                }

                Directory.CreateDirectory(tempDirectory);
                tempVideoPath = Path.Combine(tempDirectory, "temp_video.mp4");
                songAudioPath = GetSongAudioPath();

                string levelName = GetLevelName();
                string fileName = ChartRenderPaths.MakeSafeFileName(levelName) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".mp4";
                outputPath = Path.Combine(export, fileName);
                result.OutputPath = outputPath;
            }, out failure);
        }

        private bool TryStartOfficialPlayback(out Exception? failure)
        {
            return Try(() =>
            {
                savedState = RenderState.Capture();
                Time.captureFramerate = 0;
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = Math.Max(240, settings.ChartRenderFps);
                GCS.checkpointNum = 0;
                audioRecorder = ChartRenderAudioRecorder.Begin();

                scnEditor editor = ADOBase.editor;
                if (editor == null || editor.floors == null || editor.floors.Count <= 1)
                {
                    throw new InvalidOperationException("The editor has no playable level loaded.");
                }

                editor.SelectFloor(editor.floors[0], cameraJump: false);
                RDC.auto = false;
                editor.Play();
                RDC.auto = true;
            }, out failure);
        }

        private double CalculateTotalDuration()
        {
            double duration = 1.0;
            if (ADOBase.lm != null && ADOBase.lm.listFloors != null && ADOBase.lm.listFloors.Count > 1)
            {
                scrFloor last = ADOBase.lm.listFloors[ADOBase.lm.listFloors.Count - 1];
                duration = Math.Max(duration, last.entryTimePitchAdj + 2.0);
            }

            if (ADOBase.conductor != null && ADOBase.conductor.song != null && ADOBase.conductor.song.clip != null)
            {
                float pitch = Mathf.Max(0.01f, ADOBase.conductor.song.pitch);
                double songEnd = GetSongDelaySeconds() + ADOBase.conductor.song.clip.length / pitch;
                duration = Math.Max(duration, songEnd);
            }

            return Math.Max(1.0, duration);
        }

        private double GetCurrentCaptureTime()
        {
            if (ADOBase.conductor == null)
            {
                return 0.0;
            }

            float pitch = ADOBase.conductor.song == null ? 1f : Mathf.Max(0.01f, ADOBase.conductor.song.pitch);
            return ADOBase.conductor.songposition_minusi / pitch;
        }

        private bool IsPlaybackFinished()
        {
            return ADOBase.conductor != null
                && ADOBase.conductor.hasSongStarted
                && ADOBase.conductor.song != null
                && !ADOBase.conductor.song.isPlaying
                && GetCurrentCaptureTime() > 0.5;
        }

        private bool IsPlaybackScheduled()
        {
            if (ADOBase.conductor == null)
            {
                return false;
            }

            if (ADOBase.conductor.hasSongStarted)
            {
                return true;
            }

            return ADOBase.controller != null
                && (ADOBase.controller.state == States.Countdown || ADOBase.controller.state == States.PlayerControl);
        }

        private ChartRenderAudioTiming CreateAudioTiming()
        {
            if (ADOBase.conductor == null || ADOBase.conductor.song == null)
            {
                throw new InvalidOperationException("Conductor is not available.");
            }

            float pitch = Mathf.Max(0.01f, ADOBase.conductor.song.pitch);
            double outputZeroDsp = DspTimeSong(ADOBase.conductor) + scrConductor.calibration_i + ADOBase.conductor.addoffset / pitch;
            return new ChartRenderAudioTiming(
                outputZeroDsp,
                GetSongDelaySeconds(),
                pitch,
                ADOBase.conductor.song.volume,
                renderDurationSeconds);
        }

        private double GetSongDelaySeconds()
        {
            if (ADOBase.conductor == null || ADOBase.conductor.song == null)
            {
                return 0.0;
            }

            float pitch = Mathf.Max(0.01f, ADOBase.conductor.song.pitch);
            double countdown = ADOBase.conductor.separateCountdownTime
                ? ADOBase.conductor.crotchetAtStart * ADOBase.conductor.adjustedCountdownTicks / pitch
                : 0.0;
            return countdown - ADOBase.conductor.addoffset / pitch - scrConductor.calibration_i;
        }

        private double GetFrameTime(int index)
        {
            return index / (double)Math.Max(1, settings.ChartRenderFps);
        }

        private void Cleanup(ChartFrameCapture? frameCapture, FfmpegEncoder? encoder, bool restoreEditor, bool deleteTemp)
        {
            frameCapture?.Dispose();
            encoder?.Dispose();
            audioRecorder?.Dispose();
            audioRecorder = null;

            if (restoreEditor)
            {
                RestoreState();
            }

            if (deleteTemp)
            {
                DeleteTempDirectory();
            }
        }

        private void DeleteTempDirectory()
        {
            if (string.IsNullOrEmpty(tempDirectory) || !Directory.Exists(tempDirectory))
            {
                return;
            }

            try
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
            catch
            {
            }
        }

        private void RestoreState()
        {
            if (savedState == null)
            {
                return;
            }

            try
            {
                if (ADOBase.editor != null && !ADOBase.editor.inStrictlyEditingMode)
                {
                    ADOBase.editor.SwitchToEditMode();
                }
            }
            catch (Exception ex)
            {
                Main.Log("Failed to switch back to edit mode: " + ex.Message);
            }

            savedState.Restore();
        }

        private string GetSongAudioPath()
        {
            scnGame level = ADOBase.editor != null ? ADOBase.editor.customLevel : ADOBase.customLevel;
            if (level == null || level.levelData == null)
            {
                throw new InvalidOperationException("No custom level is loaded.");
            }

            string songFilename = level.levelData.songFilename;
            if (string.IsNullOrWhiteSpace(songFilename))
            {
                throw new InvalidOperationException("The loaded level does not have a song filename.");
            }

            string levelDirectory = string.IsNullOrWhiteSpace(level.levelPath)
                ? string.Empty
                : Path.GetDirectoryName(level.levelPath) ?? string.Empty;
            string path = Path.IsPathRooted(songFilename)
                ? songFilename
                : Path.Combine(levelDirectory, songFilename);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Song file was not found.", path);
            }

            return path;
        }

        private string GetLevelName()
        {
            scnGame level = ADOBase.editor != null ? ADOBase.editor.customLevel : ADOBase.customLevel;
            if (level != null && level.levelData != null)
            {
                string title = level.levelData.artist + " - " + level.levelData.song;
                if (!string.IsNullOrWhiteSpace(title.Trim(' ', '-')))
                {
                    return title;
                }

                if (!string.IsNullOrWhiteSpace(level.levelPath))
                {
                    return Path.GetFileNameWithoutExtension(level.levelPath);
                }
            }

            return "ADOFAI_Render";
        }

        private void WriteLog(string message)
        {
            try
            {
                File.AppendAllText(Path.Combine(tempDirectory, "render.log"), DateTime.Now.ToString("O") + " " + message + Environment.NewLine);
            }
            catch
            {
            }
        }

        private static bool Try(Action action, out Exception? failure)
        {
            try
            {
                action();
                failure = null;
                return true;
            }
            catch (Exception ex)
            {
                Main.Log("Chart render error: " + ex);
                failure = ex;
                return false;
            }
        }

        private sealed class RenderState
        {
            private readonly bool auto;
            private readonly int checkpoint;
            private readonly int captureFramerate;
            private readonly int targetFrameRate;
            private readonly int vSyncCount;
            private readonly List<int> selectedFloors;

            private RenderState(bool auto, int checkpoint, int captureFramerate, int targetFrameRate, int vSyncCount, List<int> selectedFloors)
            {
                this.auto = auto;
                this.checkpoint = checkpoint;
                this.captureFramerate = captureFramerate;
                this.targetFrameRate = targetFrameRate;
                this.vSyncCount = vSyncCount;
                this.selectedFloors = selectedFloors;
            }

            public static RenderState Capture()
            {
                List<int> floors = new List<int>();
                if (ADOBase.editor != null && ADOBase.editor.selectedFloors != null)
                {
                    foreach (scrFloor floor in ADOBase.editor.selectedFloors)
                    {
                        if (floor != null)
                        {
                            floors.Add(floor.seqID);
                        }
                    }
                }

                return new RenderState(RDC.auto, GCS.checkpointNum, Time.captureFramerate, Application.targetFrameRate, QualitySettings.vSyncCount, floors);
            }

            public void Restore()
            {
                RDC.auto = auto;
                GCS.checkpointNum = checkpoint;
                Time.captureFramerate = captureFramerate;
                Application.targetFrameRate = targetFrameRate;
                QualitySettings.vSyncCount = vSyncCount;

                if (ADOBase.editor == null || ADOBase.editor.floors == null || selectedFloors.Count == 0)
                {
                    return;
                }

                int seqId = Mathf.Clamp(selectedFloors[0], 0, ADOBase.editor.floors.Count - 1);
                ADOBase.editor.SelectFloor(ADOBase.editor.floors[seqId], cameraJump: false);
            }
        }
    }
}
