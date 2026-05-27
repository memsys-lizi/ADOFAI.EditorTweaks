using System;
using UnityEngine;

namespace ADOFAI.EditorTweaks.Features.ChartRendering
{
    internal sealed class ChartFrameCapture : IDisposable
    {
        private readonly int width;
        private readonly int height;
        private readonly RenderTexture target;
        private readonly Texture2D readback;
        private readonly byte[] frameBytes;
        private readonly int rowBytes;

        public ChartFrameCapture(int width, int height)
        {
            this.width = width;
            this.height = height;
            rowBytes = width * 4;
            frameBytes = new byte[rowBytes * height];
            target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false
            };
            target.Create();
            readback = new Texture2D(width, height, TextureFormat.RGBA32, false);
        }

        public byte[] CaptureFrame()
        {
            scrCamera camera = scrCamera.instance;
            if (camera == null)
            {
                throw new InvalidOperationException("scrCamera.instance is not available.");
            }

            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousStatic = camera.Bgcamstatic.targetTexture;
            RenderTexture previousBg = camera.BGcam.targetTexture;
            RenderTexture previousMain = camera.camobj.targetTexture;

            try
            {
                camera.Bgcamstatic.targetTexture = target;
                camera.BGcam.targetTexture = target;
                camera.camobj.targetTexture = target;

                camera.Bgcamstatic.Render();
                camera.BGcam.Render();
                camera.camobj.Render();

                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                readback.Apply(false, false);

                byte[] raw = readback.GetRawTextureData();
                FlipVertically(raw, frameBytes);
                return frameBytes;
            }
            finally
            {
                camera.Bgcamstatic.targetTexture = previousStatic;
                camera.BGcam.targetTexture = previousBg;
                camera.camobj.targetTexture = previousMain;
                RenderTexture.active = previousActive;
            }
        }

        public void Dispose()
        {
            if (target != null)
            {
                target.Release();
                UnityEngine.Object.Destroy(target);
            }

            if (readback != null)
            {
                UnityEngine.Object.Destroy(readback);
            }
        }

        private void FlipVertically(byte[] source, byte[] destination)
        {
            for (int y = 0; y < height; y++)
            {
                int sourceOffset = y * rowBytes;
                int destinationOffset = (height - 1 - y) * rowBytes;
                Buffer.BlockCopy(source, sourceOffset, destination, destinationOffset, rowBytes);
            }
        }
    }
}
