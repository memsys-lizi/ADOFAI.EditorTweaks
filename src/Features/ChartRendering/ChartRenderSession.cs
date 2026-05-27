using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityModManagerNet;

namespace ADOFAI.EditorTweaks.Features.ChartRendering
{
    internal sealed class ChartRenderSession
    {
        private readonly UnityModManager.ModEntry modEntry;
        private readonly Settings settings;
        private readonly Stopwatch stopwatch = new Stopwatch();
        private bool cancelRequested;
        private int frameIndex;
        private int totalFrames = 1;
        private string tempDirectory = string.Empty;
        private string tempVideoPath = string.Empty;
        private string audioPath = string.Empty;
        private string outputPath = string.Empty;
        private RenderState? savedState;

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
            ChartFrameCapture? frameCapture = null;
            ChartAudioCapture? audioCapture = null;
            FfmpegEncoder? encoder = null;
            ChartRenderResult result = new ChartRenderResult();
            Exception? failure = null;

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
                Cleanup(frameCapture, audioCapture, encoder, restoreEditor: true, deleteTemp: true);
                Finish(onComplete, result);
                yield break;
            }

            int waitFrames = 0;
            while (!cancelRequested && waitFrames < settings.ChartRenderFps * 10)
            {
                if (ADOBase.conductor != null && ADOBase.conductor.hasSongStarted)
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
                Cleanup(frameCapture, audioCapture, encoder, restoreEditor: true, deleteTemp: true);
                Finish(onComplete, result);
                yield break;
            }

            if (ADOBase.conductor == null || !ADOBase.conductor.hasSongStarted)
            {
                result.Success = false;
                result.Message = "Timed out while waiting for song start.";
                Cleanup(frameCapture, audioCapture, encoder, restoreEditor: true, deleteTemp: true);
                Finish(onComplete, result);
                yield break;
            }

            failure = null;
            if (!Try(() =>
            {
                totalFrames = CalculateTotalFrames();
                frameCapture = new ChartFrameCapture(settings.ChartRenderWidth, settings.ChartRenderHeight);
                audioCapture = new ChartAudioCapture(audioPath);
                encoder = new FfmpegEncoder(ChartRenderPaths.GetFfmpegPath(), tempVideoPath, outputPath, settings.ChartRenderWidth, settings.ChartRenderHeight, settings.ChartRenderFps, settings.ChartRenderCrf, settings.ChartRenderPreset);
                audioCapture.Begin();
                encoder.BeginVideo();
            }, out failure))
            {
                result.Success = false;
                result.Message = failure?.Message ?? "Failed to initialize renderer.";
                Cleanup(frameCapture, audioCapture, encoder, restoreEditor: true, deleteTemp: true);
                Finish(onComplete, result);
                yield break;
            }

            for (frameIndex = 0; frameIndex < totalFrames && !cancelRequested; frameIndex++)
            {
                yield return new WaitForEndOfFrame();

                if (!Try(() =>
                {
                    byte[] frame = frameCapture!.CaptureFrame();
                    encoder!.WriteFrame(frame);
                    audioCapture!.RenderFrame();
                }, out failure))
                {
                    break;
                }

                DetailText = Path.GetFileName(outputPath);
            }

            if (cancelRequested)
            {
                result.Success = false;
                result.Message = "Canceled.";
                Cleanup(frameCapture, audioCapture, encoder, restoreEditor: true, deleteTemp: true);
                Finish(onComplete, result);
                yield break;
            }

            if (failure != null)
            {
                result.Success = false;
                result.Message = failure.Message;
                Cleanup(frameCapture, audioCapture, encoder, restoreEditor: true, deleteTemp: true);
                Finish(onComplete, result);
                yield break;
            }

            if (!Try(() =>
            {
                StageText = Settings.Text("chartRendererMuxing");
                DetailText = Path.GetFileName(outputPath);
                audioCapture!.Dispose();
                audioCapture = null;
                encoder!.CompleteVideo();
                encoder.MuxAudio(audioPath);
            }, out failure))
            {
                result.Success = false;
                result.Message = failure?.Message ?? "Failed to finish video.";
                Cleanup(frameCapture, audioCapture, encoder, restoreEditor: true, deleteTemp: true);
                Finish(onComplete, result);
                yield break;
            }

            Cleanup(frameCapture, audioCapture, encoder, restoreEditor: true, deleteTemp: false);
            result.Success = true;
            result.OutputPath = outputPath;
            result.Message = "OK";
            WriteLog("Render complete: " + outputPath);
            Finish(onComplete, result);
        }

        private void Finish(Action<ChartRenderResult> onComplete, ChartRenderResult result)
        {
            IsActive = false;
            IsRendering = false;
            onComplete(result);
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
                audioPath = Path.Combine(tempDirectory, "audio.wav");

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
                Time.captureFramerate = settings.ChartRenderFps;
                Application.targetFrameRate = settings.ChartRenderFps;
                RDC.auto = true;
                GCS.checkpointNum = 0;

                scnEditor editor = ADOBase.editor;
                if (editor == null || editor.floors == null || editor.floors.Count <= 1)
                {
                    throw new InvalidOperationException("The editor has no playable level loaded.");
                }

                editor.SelectFloor(editor.floors[0], cameraJump: false);
                editor.Play();
            }, out failure);
        }

        private int CalculateTotalFrames()
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
                duration = Math.Max(duration, ADOBase.conductor.song.clip.length / pitch);
            }

            return Math.Max(1, Mathf.CeilToInt((float)(duration * settings.ChartRenderFps)));
        }

        private void Cleanup(ChartFrameCapture? frameCapture, ChartAudioCapture? audioCapture, FfmpegEncoder? encoder, bool restoreEditor, bool deleteTemp)
        {
            frameCapture?.Dispose();
            audioCapture?.Dispose();
            encoder?.Dispose();

            if (restoreEditor)
            {
                RestoreState();
            }

            if (deleteTemp && !string.IsNullOrEmpty(tempDirectory) && Directory.Exists(tempDirectory))
            {
                try
                {
                    Directory.Delete(tempDirectory, recursive: true);
                }
                catch
                {
                }
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
            private readonly List<int> selectedFloors;

            private RenderState(bool auto, int checkpoint, int captureFramerate, int targetFrameRate, List<int> selectedFloors)
            {
                this.auto = auto;
                this.checkpoint = checkpoint;
                this.captureFramerate = captureFramerate;
                this.targetFrameRate = targetFrameRate;
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

                return new RenderState(RDC.auto, GCS.checkpointNum, Time.captureFramerate, Application.targetFrameRate, floors);
            }

            public void Restore()
            {
                RDC.auto = auto;
                GCS.checkpointNum = checkpoint;
                Time.captureFramerate = captureFramerate;
                Application.targetFrameRate = targetFrameRate;

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
