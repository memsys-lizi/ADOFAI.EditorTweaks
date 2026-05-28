using System;
using System.Collections.Generic;
using System.Threading;

namespace ADOFAI.EditorTweaks.Features.ChartRendering
{
    internal sealed class ChartRenderFramePipeline
    {
        private readonly Queue<ChartFrameCapture.PendingFrame> pendingFrames = new Queue<ChartFrameCapture.PendingFrame>();
        private readonly Queue<byte[]> frameBufferPool = new Queue<byte[]>();
        private readonly object frameBufferPoolLock = new object();
        private readonly int frameByteLength;
        private readonly int maxPendingGpuFrames;

        public ChartRenderFramePipeline(ChartRenderMemoryBudget budget)
        {
            frameByteLength = (int)Math.Max(1L, Math.Min(int.MaxValue, budget.FrameBytes));
            maxPendingGpuFrames = Math.Max(1, budget.MaxPendingGpuFrames);
        }

        public int PendingCount => pendingFrames.Count;

        public int WrittenFrames { get; private set; }

        public int DuplicateFrames { get; private set; }

        public void Reset()
        {
            pendingFrames.Clear();
            WrittenFrames = 0;
            DuplicateFrames = 0;
        }

        public void RequestFrame(ChartFrameCapture frameCapture, int index)
        {
            pendingFrames.Enqueue(frameCapture.RequestFrame(index));
        }

        public void DrainReadyFrames(FfmpegEncoder encoder, Func<bool> isCancelRequested)
        {
            while (pendingFrames.Count > 0 && pendingFrames.Peek().Done)
            {
                ChartFrameCapture.PendingFrame pending = pendingFrames.Dequeue();
                byte[] buffer = RentFrameBuffer();
                try
                {
                    pending.Complete(buffer);
                    encoder.WriteFrame(buffer, pending.ByteLength, pending.RepeatCount, ReturnFrameBuffer, isCancelRequested);
                    buffer = null!;
                }
                finally
                {
                    if (buffer != null)
                    {
                        ReturnFrameBuffer(buffer);
                    }
                }

                WrittenFrames += pending.RepeatCount;
                DuplicateFrames += Math.Max(0, pending.RepeatCount - 1);
            }
        }

        public void WaitForPendingSlot(FfmpegEncoder encoder, Func<bool> isCancelRequested)
        {
            while (pendingFrames.Count >= maxPendingGpuFrames && !isCancelRequested())
            {
                int before = pendingFrames.Count;
                DrainReadyFrames(encoder, isCancelRequested);
                if (pendingFrames.Count < maxPendingGpuFrames)
                {
                    return;
                }

                if (pendingFrames.Count == before)
                {
                    Thread.Sleep(1);
                }
            }
        }

        public void Clear()
        {
            pendingFrames.Clear();
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
    }
}
