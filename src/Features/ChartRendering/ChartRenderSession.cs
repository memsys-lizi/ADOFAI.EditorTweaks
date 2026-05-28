using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using UnityEngine;
using UnityModManagerNet;

namespace ADOFAI.EditorTweaks.Features.ChartRendering
{
    internal sealed class ChartRenderSession
    {
        private const int MaxPendingGpuFrames = 8;
        private const double CompletionFallbackSeconds = 30.0;

        private static readonly FieldInfo? WaitForStartCoCallCountField =
            typeof(scrController).GetField("waitForStartCoCallCount", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly UnityModManager.ModEntry modEntry;
        private readonly Settings settings;
        private readonly Stopwatch stopwatch = new Stopwatch();
        private readonly Queue<ChartFrameCapture.PendingFrame> pendingFrames = new Queue<ChartFrameCapture.PendingFrame>();
        private readonly Queue<byte[]> frameBufferPool = new Queue<byte[]>();
        private readonly object frameBufferPoolLock = new object();

        private bool cancelRequested;
        private int frameIndex;
        private int totalFrames = 1;
        private int frameByteLength;
        private int repeatedFrameCount;
        private double renderDurationSeconds = 1.0;
        private string tempDirectory = string.Empty;
        private string tempVideoPath = string.Empty;
        private string capturedAudioPath = string.Empty;
        private string outputPath = string.Empty;
        private RenderState? savedState;
        private ChartUnityAudioCapture? audioCapture;

        public ChartRenderSession(UnityModManager.ModEntry modEntry, Settings settings)
        {
            this.modEntry = modEntry;
            this.settings = settings;
        }

        public bool IsActive { get; private set; }

        public static bool IsRendering { get; private set; }

        public float Progress => totalFrames <= 0 ? 0f : Mathf.Clamp01((float)frameIndex / totalFrames);

        public string StageText { get; private set; } = Settings.Text("chartRendererRendering");

        public string DetailText { get; private set; } = string.Empty;

        public string EncoderName { get; private set; } = string.Empty;

        public int WrittenFrames => frameIndex;

        public int TotalFrames => totalFrames;

        public int DuplicateFrames => repeatedFrameCount;

        public float DuplicateRatio => frameIndex <= 0 ? 0f : repeatedFrameCount / (float)frameIndex;

        public double ProcessingFps
        {
            get
            {
                double elapsed = Math.Max(0.001, stopwatch.Elapsed.TotalSeconds);
                return frameIndex / elapsed;
            }
        }

        public TimeSpan EstimatedRemaining
        {
            get
            {
                double fps = ProcessingFps;
                double remaining = fps <= 0.001 ? 0.0 : (totalFrames - frameIndex) / fps;
                return TimeSpan.FromSeconds(Math.Max(0, remaining));
            }
        }

        public string SmoothnessText
        {
            get
            {
                float ratio = DuplicateRatio;
                if (ratio <= 0.01f)
                {
                    return "excellent";
                }

                if (ratio <= 0.03f)
                {
                    return "good";
                }

                if (ratio <= 0.05f)
                {
                    return "minor stutter";
                }

                if (ratio <= 0.10f)
                {
                    return "visible stutter";
                }

                return "heavy stutter";
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
            repeatedFrameCount = 0;
            frameByteLength = Math.Max(1, settings.ChartRenderWidth * settings.ChartRenderHeight * 4);
            pendingFrames.Clear();

            ChartFrameCapture? frameCapture = null;
            FfmpegEncoder? encoder = null;
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

            if (!TryStartPlayback(out failure))
            {
                result.Success = false;
                result.Message = failure?.Message ?? "Failed to start level playback.";
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

            BeginForcedVisualClock();
            Time.captureFramerate = Math.Max(1, settings.ChartRenderFps);
            Application.targetFrameRate = Math.Max(1000, settings.ChartRenderFps * 4);
            ChartRenderDiagnostics.LogFrame(0, 0);

            if (!Try(() =>
            {
                renderDurationSeconds = CalculateTotalDuration();
                totalFrames = Math.Max(1, Mathf.CeilToInt((float)(renderDurationSeconds * settings.ChartRenderFps)));
                frameCapture = new ChartFrameCapture(settings.ChartRenderWidth, settings.ChartRenderHeight);
                audioCapture = new ChartUnityAudioCapture(capturedAudioPath);
                audioCapture.Begin();
                encoder = new FfmpegEncoder(ChartRenderPaths.GetFfmpegPath(), tempVideoPath, outputPath, settings.ChartRenderWidth, settings.ChartRenderHeight, settings.ChartRenderFps, settings.ChartRenderCrf, settings.ChartRenderPreset);
                encoder.BeginVideo();
                EncoderName = encoder.EncoderName;
            }, out failure))
            {
                result.Success = false;
                result.Message = failure?.Message ?? "Failed to initialize renderer.";
                Cleanup(frameCapture, encoder, restoreEditor: true, deleteTemp: true);
                Finish(onComplete, result);
                yield break;
            }

            int requestedFrames = 0;
            int completionFrame = -1;
            int fps = Math.Max(1, settings.ChartRenderFps);
            int completionTailFrames = Mathf.Max(0, Mathf.CeilToInt(Mathf.Max(0f, settings.ChartRenderCompletionTailSeconds) * fps));
            int fallbackExtraFrames = Math.Max(completionTailFrames, Mathf.CeilToInt((float)(CompletionFallbackSeconds * fps)));
            int renderFrameLimit = totalFrames + fallbackExtraFrames;
            WriteLog("Render estimate: " + totalFrames + " frames, fallback limit: " + renderFrameLimit + " frames.");
            DetailText = Path.GetFileName(outputPath);
            yield return null;

            while (requestedFrames < renderFrameLimit && !cancelRequested && failure == null)
            {
                ChartRenderDiagnostics.SetFrame(requestedFrames);
                if (!Try(() => WaitForPendingSlot(encoder!), out failure))
                {
                    break;
                }

                if (failure != null || cancelRequested)
                {
                    break;
                }

                yield return new WaitForEndOfFrame();

                if (!Try(() =>
                {
                    audioCapture!.CaptureFrame();
                    pendingFrames.Enqueue(frameCapture!.RequestFrame(requestedFrames));
                    requestedFrames++;
                    SetForcedFrameTime(requestedFrames);
                    ChartRenderDiagnostics.LogFrame(requestedFrames, renderFrameLimit);
                    DrainReadyFrames(encoder!);
                    if (completionFrame < 0 && HasReachedLevelEnd())
                    {
                        completionFrame = requestedFrames;
                        renderFrameLimit = completionFrame + completionTailFrames;
                        totalFrames = renderFrameLimit;
                        WriteLog("Level end detected at frame " + completionFrame + "; rendering tail to frame " + renderFrameLimit + ".");
                    }
                    else if (completionFrame < 0 && requestedFrames >= totalFrames)
                    {
                        totalFrames = Math.Min(renderFrameLimit, requestedFrames + fps * 2);
                    }
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
            audioCapture?.Complete();
            ChartRenderVisualClock.End();
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
            yield return RunBackground(() => encoder!.MuxAudioFile(capturedAudioPath), encoder, result, deleteTempOnCancel: true, deleteTempOnFailure: false, onDone: ok => backgroundOk = ok);
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
            return RunBackground(action, encoder, result, deleteTempOnCancel, deleteTempOnFailure: true, onDone);
        }

        private IEnumerator RunBackground(Action action, FfmpegEncoder? encoder, ChartRenderResult result, bool deleteTempOnCancel, bool deleteTempOnFailure, Action<bool> onDone)
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
                Cleanup(null, encoder, restoreEditor: false, deleteTemp: deleteTempOnFailure);
                onDone(false);
                yield break;
            }

            onDone(true);
        }

        private void Finish(Action<ChartRenderResult> onComplete, ChartRenderResult result)
        {
            IsActive = false;
            IsRendering = false;
            ChartRenderVisualClock.End();
            ChartRenderDiagnostics.End();
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
                    encoder.WriteFrame(buffer, pending.ByteLength, pending.RepeatCount, ReturnFrameBuffer);
                    buffer = null!;
                }
                finally
                {
                    if (buffer != null)
                    {
                        ReturnFrameBuffer(buffer);
                    }
                }

                frameIndex += pending.RepeatCount;
            }
        }

        private void WaitForPendingSlot(FfmpegEncoder encoder)
        {
            while (pendingFrames.Count >= MaxPendingGpuFrames && !cancelRequested)
            {
                int before = pendingFrames.Count;
                DrainReadyFrames(encoder);
                if (pendingFrames.Count < MaxPendingGpuFrames)
                {
                    return;
                }

                if (pendingFrames.Count == before)
                {
                    Thread.Sleep(1);
                }
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
                capturedAudioPath = Path.Combine(tempDirectory, "audio.wav");
                ChartRenderDiagnostics.Begin(Path.Combine(tempDirectory, "render.log"));

                string levelName = GetLevelName();
                string fileName = ChartRenderPaths.MakeSafeFileName(levelName) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".mp4";
                outputPath = Path.Combine(export, fileName);
                result.OutputPath = outputPath;
            }, out failure);
        }

        private bool TryStartPlayback(out Exception? failure)
        {
            return Try(() =>
            {
                savedState = RenderState.Capture();
                Time.captureFramerate = Math.Max(1, settings.ChartRenderFps);
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = Math.Max(1000, settings.ChartRenderFps * 4);

                if (ADOBase.editor != null)
                {
                    StartEditorPlayback();
                    return;
                }

                StartGameScenePlayback();
            }, out failure);
        }

        private static void StartEditorPlayback()
        {
            ChartRenderDiagnostics.Log("Starting official editor playback for render.");
            scnEditor editor = ADOBase.editor;
            if (editor == null || editor.floors == null || editor.floors.Count <= 1)
            {
                throw new InvalidOperationException("The editor has no playable level loaded.");
            }

            editor.SelectFloor(editor.floors[0], cameraJump: false);
            GCS.checkpointNum = 0;
            RDC.auto = false;
            editor.Play();
            RDC.auto = true;
        }

        private static void StartGameScenePlayback()
        {
            scrController controller = ADOBase.controller;
            if (controller == null || !IsPlayableLevelLoaded())
            {
                throw new InvalidOperationException("No playable game level is loaded.");
            }

            ChartRenderDiagnostics.Log("Starting game scene playback for render. scene=" + ADOBase.sceneName
                + " level=" + (controller.levelName ?? string.Empty)
                + " scnGame=" + (ADOBase.customLevel != null)
                + " state=" + controller.state);

            RDC.auto = true;
            if (IsPlaybackScheduled())
            {
                ChartRenderDiagnostics.Log("Game scene playback is already scheduled; capturing current timeline.");
                return;
            }

            GCS.checkpointNum = 0;
            AbortWaitingForStartCoroutine(controller);
            HidePressToStart();
            scrUIController.instance?.txtCountdown?.GetComponent<scrCountdown>()?.ShowGetReady();
            ADOBase.conductor.Rewind();
            ADOBase.conductor.Start();

            int checkpoint = Math.Max(0, GCS.checkpointNum);
            controller.Start_Rewind(checkpoint);

            scnGame? gameLevel = ADOBase.customLevel;
            if (gameLevel != null)
            {
                gameLevel.FinishCustomLevelLoading(checkpoint);
            }
        }

        private static void AbortWaitingForStartCoroutine(scrController controller)
        {
            if (WaitForStartCoCallCountField == null)
            {
                ChartRenderDiagnostics.Log("waitForStartCoCallCount field was not found; continuing without canceling the waiting coroutine.");
                return;
            }

            object raw = WaitForStartCoCallCountField.GetValue(controller);
            int value = raw is int intValue ? intValue : 0;
            WaitForStartCoCallCountField.SetValue(controller, value + 1);
        }

        private static void HidePressToStart()
        {
            try
            {
                scrUIController.instance?.txtPressToStart?.GetComponent<scrPressToStart>()?.HideText();
            }
            catch (Exception ex)
            {
                ChartRenderDiagnostics.Log("Failed to hide press-to-start text: " + ex.Message);
            }
        }

        internal static bool IsPlayableLevelLoaded()
        {
            if (ADOBase.editor != null)
            {
                scnEditor editor = ADOBase.editor;
                scnGame? editorLevel = editor.customLevel;
                return !editor.isLoading
                    && editorLevel != null
                    && editorLevel.levelData != null
                    && editor.floors != null
                    && editor.floors.Count > 1;
            }

            scrController controller = ADOBase.controller;
            List<scrFloor>? floors = ADOBase.lm == null ? null : ADOBase.lm.listFloors;
            if (controller == null || !controller.gameworld || floors == null || floors.Count <= 1)
            {
                return false;
            }

            scnGame gameLevel = ADOBase.customLevel;
            return gameLevel == null || (!gameLevel.isLoading && gameLevel.levelData != null);
        }

        internal static bool HasRenderableAudio()
        {
            return ADOBase.conductor != null
                && ADOBase.conductor.song != null
                && ADOBase.conductor.song.clip != null;
        }

        private double CalculateTotalDuration()
        {
            double duration = 1.0;
            if (ADOBase.lm != null && ADOBase.lm.listFloors != null && ADOBase.lm.listFloors.Count > 1)
            {
                scrFloor last = ADOBase.lm.listFloors[ADOBase.lm.listFloors.Count - 1];
                duration = Math.Max(duration, last.entryTimePitchAdj + 2.0);
            }

            return Math.Max(1.0, duration);
        }

        private static bool IsPlaybackScheduled()
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

        private bool HasReachedLevelEnd()
        {
            scrController controller = ADOBase.controller;
            if (controller == null || ADOBase.conductor == null || !ADOBase.conductor.hasSongStarted)
            {
                return false;
            }

            if (controller.state == States.Won)
            {
                return true;
            }

            List<scrFloor>? floors = ADOBase.lm == null ? null : ADOBase.lm.listFloors;
            if (floors == null || floors.Count <= 1)
            {
                return false;
            }

            int lastSeqId = floors.Count - 1;
            if (controller.currentSeqID >= lastSeqId)
            {
                return true;
            }

            scrFloor currentFloor = controller.currFloor;
            return currentFloor != null && currentFloor.seqID >= lastSeqId;
        }

        private double GetFrameTime(int index)
        {
            return index / (double)Math.Max(1, settings.ChartRenderFps);
        }

        private void SetForcedFrameTime(int index)
        {
            ChartRenderVisualClock.SetFrameTime(GetFrameTime(index), GetRenderPitch());
        }

        private void BeginForcedVisualClock()
        {
            scrConductor conductor = ADOBase.conductor;
            double startSongPosition = conductor == null ? 0.0 : conductor.songposition_minusi;
            ChartRenderVisualClock.Begin(startSongPosition);
            SetForcedFrameTime(0);

            int inputOffsetMs = 0;
            try
            {
                inputOffsetMs = scrConductor.currentPreset.inputOffset;
            }
            catch
            {
            }

            double addOffset = conductor == null ? 0.0 : conductor.addoffset;
            WriteLog("Visual clock anchored at songposition=" + Number(startSongPosition)
                + " pitch=" + Number(GetRenderPitch())
                + " addoffset=" + Number(addOffset)
                + " suppressedInputOffsetMs=" + inputOffsetMs + ".");
        }

        private float GetRenderPitch()
        {
            return ADOBase.conductor == null || ADOBase.conductor.song == null
                ? 1f
                : ADOBase.conductor.song.pitch;
        }

        private void Cleanup(ChartFrameCapture? frameCapture, FfmpegEncoder? encoder, bool restoreEditor, bool deleteTemp)
        {
            frameCapture?.Dispose();
            encoder?.Dispose();
            audioCapture?.Dispose();
            audioCapture = null;
            ChartRenderVisualClock.End();
            ChartRenderDiagnostics.End();

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

            scrController controller = ADOBase.controller;
            if (controller != null)
            {
                if (!string.IsNullOrWhiteSpace(controller.caption))
                {
                    return controller.caption;
                }

                if (!string.IsNullOrWhiteSpace(controller.levelName))
                {
                    return controller.levelName;
                }
            }

            if (!string.IsNullOrWhiteSpace(ADOBase.sceneName))
            {
                return ADOBase.sceneName;
            }

            return "ADOFAI_Render";
        }

        private void WriteLog(string message)
        {
            ChartRenderDiagnostics.Log(message);
        }

        private static string Number(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value)
                ? value.ToString(CultureInfo.InvariantCulture)
                : value.ToString("0.######", CultureInfo.InvariantCulture);
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
