namespace ADOFAI.EditorTweaks.Features.ChartRendering
{
    internal static class ChartRenderOptionValues
    {
        public const string EncoderAutoBalanced = "auto-balanced";
        public const string EncoderFastest = "fastest";
        public const string EncoderBalanced = "balanced";
        public const string EncoderQuality = "quality";
        public const string EncoderCpuCompatibility = "cpu-compatibility";
        public const string EncoderCustom = "custom";

        public const string CaptureRgba = "rgba";
        public const string CaptureBgra = "bgra";

        public const string PreviewFull = "full";
        public const string PreviewDim = "dim";
        public const string PreviewMinimal = "minimal";

        public static readonly string[] EncoderModes =
        {
            EncoderAutoBalanced,
            EncoderFastest,
            EncoderBalanced,
            EncoderQuality,
            EncoderCpuCompatibility,
            EncoderCustom
        };

        public static readonly string[] CaptureFormats =
        {
            CaptureRgba,
            CaptureBgra
        };

        public static readonly string[] PreviewModes =
        {
            PreviewFull,
            PreviewDim,
            PreviewMinimal
        };

        public static string NormalizeEncoderMode(string? value)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case EncoderFastest:
                    return EncoderFastest;
                case EncoderBalanced:
                    return EncoderBalanced;
                case EncoderQuality:
                    return EncoderQuality;
                case EncoderCpuCompatibility:
                    return EncoderCpuCompatibility;
                case EncoderCustom:
                    return EncoderCustom;
                default:
                    return EncoderAutoBalanced;
            }
        }

        public static string NormalizeCaptureFormat(string? value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant() == CaptureBgra
                ? CaptureBgra
                : CaptureRgba;
        }

        public static string NormalizePreviewMode(string? value)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case PreviewDim:
                    return PreviewDim;
                case PreviewMinimal:
                    return PreviewMinimal;
                default:
                    return PreviewFull;
            }
        }
    }
}
