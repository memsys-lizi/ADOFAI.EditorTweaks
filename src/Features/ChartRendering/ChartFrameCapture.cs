using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace ADOFAI.EditorTweaks.Features.ChartRendering
{
    internal sealed class ChartFrameCapture : IDisposable
    {
        private readonly int width;
        private readonly int height;
        private readonly RenderTexture scaleTarget;
        private readonly int rowBytes;
        private static readonly AccessTools.FieldRef<scrCamera, RenderTexture> CamRt =
            AccessTools.FieldRefAccess<scrCamera, RenderTexture>("camRT");

        public ChartFrameCapture(int width, int height)
        {
            this.width = width;
            this.height = height;
            rowBytes = width * 3;
            scaleTarget = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false
            };
            scaleTarget.Create();
        }

        public PendingFrame RequestFrame(int index)
        {
            scrCamera camera = scrCamera.instance;
            if (camera == null)
            {
                throw new InvalidOperationException("scrCamera.instance is not available.");
            }

            RenderTexture source = CamRt(camera);
            if (source == null)
            {
                throw new InvalidOperationException("scrCamera.camRT is not available.");
            }

            if (source.width == width && source.height == height)
            {
                return new PendingFrame(index, AsyncGPUReadback.Request(source, 0, TextureFormat.RGB24), rowBytes, height);
            }

            Graphics.Blit(source, scaleTarget);
            return new PendingFrame(index, AsyncGPUReadback.Request(scaleTarget, 0, TextureFormat.RGB24), rowBytes, height);
        }

        public void Dispose()
        {
            if (scaleTarget != null)
            {
                scaleTarget.Release();
                UnityEngine.Object.Destroy(scaleTarget);
            }
        }

        public sealed class PendingFrame
        {
            private readonly int byteLength;
            private readonly AsyncGPUReadbackRequest request;

            public PendingFrame(int index, AsyncGPUReadbackRequest request, int rowBytes, int height)
            {
                Index = index;
                this.request = request;
                byteLength = rowBytes * height;
            }

            public int Index { get; }

            public int ByteLength => byteLength;

            public bool Done => request.done;

            public void Complete(byte[] destination)
            {
                if (request.hasError)
                {
                    throw new InvalidOperationException("AsyncGPUReadback failed for frame " + Index + ".");
                }

                Unity.Collections.NativeArray<byte> source = request.GetData<byte>();
                if (destination.Length < source.Length)
                {
                    throw new InvalidOperationException("Frame buffer is too small.");
                }

                source.CopyTo(destination);
            }
        }
    }
}
