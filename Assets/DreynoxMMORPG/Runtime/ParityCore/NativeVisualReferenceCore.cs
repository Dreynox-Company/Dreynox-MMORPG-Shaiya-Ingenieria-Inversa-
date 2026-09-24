using System;
using System.Collections.Generic;

namespace Dreynox.Mmorpg.ParityCore
{
    public readonly struct NativeVisualReference
    {
        public readonly string ScenarioId;
        public readonly string FileName;
        public readonly int Width;
        public readonly int Height;
        public readonly string Sha256;

        // Native screenshots were captured externally and include the
        // Windows non-client frame. These values describe the game client
        // content rectangle inside the verified 1024x768 PNG.
        public readonly int NativeCropX;
        public readonly int NativeCropY;
        public readonly int NativeCropWidth;
        public readonly int NativeCropHeight;

        public NativeVisualReference(
            string scenarioId,
            string fileName,
            int width,
            int height,
            string sha256,
            int nativeCropX,
            int nativeCropY,
            int nativeCropWidth,
            int nativeCropHeight)
        {
            ScenarioId = scenarioId ?? string.Empty;
            FileName = fileName ?? string.Empty;
            Width = width;
            Height = height;
            Sha256 = sha256 ?? string.Empty;
            NativeCropX = nativeCropX;
            NativeCropY = nativeCropY;
            NativeCropWidth = nativeCropWidth;
            NativeCropHeight = nativeCropHeight;
        }
    }

    public static class NativeVisualReferenceCore
    {
        public const string OriginalClientSha256 =
            "509c4a8fbe4d5292961fdfb6d1045795a7bb5970fcf2560fd1070aee18273c2d";

        public const string DiagnosticCaptureClientSha256 =
            "32232a8e3e176c32ac75ad357d1f70ccf8ccadc7f223384866afdd2c9e6282df";

        public const string DiagnosticPatchOffset =
            "0x10A084";

        // Initial Windows 11 native-capture calibration. The full PNG is
        // 1024x768 and contains the title bar. The crop is intentionally
        // explicit so future captures from another window style can use a
        // different verified rectangle instead of silently changing metrics.
        public const int NativeCropX = 3;
        public const int NativeCropY = 26;
        public const int NativeCropWidth = 1021;
        public const int NativeCropHeight = 739;

        private static readonly NativeVisualReference[] Items =
        {
            R(
                "server-list",
                "01-server-list.png",
                "533f6cd8bf684ff18d46cc188c9bfbd981e684e0946cd2730839ab0970fda72d"),
            R(
                "after-faction",
                "04-after-faction.png",
                "b40064d95f1c60ab2e5ce8a6340a6679c6bec2712dba35eaf9a512092f6b33aa"),
            R(
                "character-editor",
                "05-character-editor.png",
                "2fd2807d305f5ae589f30232ac31c52a12f9caef66ed1b674ca6405a607f5549"),
            R(
                "mode-selected",
                "06c-mode-selected.png",
                "e8ddd8f935f05e6dc101a1b5dafdbffaf233312a747fd5aecffd05152f47df0a"),
            R(
                "character-selection",
                "08-character-selection.png",
                "64d80b94bcfd41aef6b3c44971275c4b166bde416e5946ba6d9b4f0e26ae74fd"),
            R(
                "world-entry",
                "09-world-entry.png",
                "7759bca4d47ba7cbe22ed2636b0a39535d359eca40dc786d076dba928e87842b"),
            R(
                "world-loaded",
                "10-world-loaded.png",
                "c19cb5f06154bf6eacb029b08be7f6762bd9a002c044ff170363a9dfeadee6a0"),
            R(
                "after-movement",
                "11-after-movement.png",
                "8e6592c871047643c6377498194fab98d9de7e46b0f4e5117885f9859fc32757")
        };

        private static readonly Dictionary<string, NativeVisualReference> ById =
            BuildIndex();

        public static IReadOnlyList<NativeVisualReference> All =>
            Items;

        public static bool TryGet(
            string scenarioId,
            out NativeVisualReference reference)
        {
            if (string.IsNullOrWhiteSpace(scenarioId))
            {
                reference = default;
                return false;
            }

            return ById.TryGetValue(
                scenarioId.Trim(),
                out reference);
        }

        public static NativeVisualReference Get(
            string scenarioId)
        {
            NativeVisualReference reference;

            if (!TryGet(
                    scenarioId,
                    out reference))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(scenarioId),
                    "Unknown native parity scenario: " +
                    scenarioId);
            }

            return reference;
        }

        private static NativeVisualReference R(
            string id,
            string fileName,
            string sha256)
        {
            return new NativeVisualReference(
                id,
                fileName,
                1024,
                768,
                sha256,
                NativeCropX,
                NativeCropY,
                NativeCropWidth,
                NativeCropHeight);
        }

        private static Dictionary<string, NativeVisualReference> BuildIndex()
        {
            var result =
                new Dictionary<string, NativeVisualReference>(
                    StringComparer.OrdinalIgnoreCase);

            for (int i = 0;
                 i < Items.Length;
                 i++)
            {
                NativeVisualReference item =
                    Items[i];

                if (string.IsNullOrWhiteSpace(item.ScenarioId) ||
                    string.IsNullOrWhiteSpace(item.FileName) ||
                    item.Width <= 0 ||
                    item.Height <= 0 ||
                    item.Sha256.Length != 64 ||
                    item.NativeCropX < 0 ||
                    item.NativeCropY < 0 ||
                    item.NativeCropWidth <= 0 ||
                    item.NativeCropHeight <= 0 ||
                    item.NativeCropX + item.NativeCropWidth > item.Width ||
                    item.NativeCropY + item.NativeCropHeight > item.Height)
                {
                    throw new InvalidOperationException(
                        "Invalid native parity reference at index " +
                        i + ".");
                }

                if (result.ContainsKey(item.ScenarioId))
                {
                    throw new InvalidOperationException(
                        "Duplicate native parity scenario '" +
                        item.ScenarioId + "'.");
                }

                result.Add(
                    item.ScenarioId,
                    item);
            }

            return result;
        }
    }
}
