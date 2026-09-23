using System;
using System.IO;

namespace Dreynox.Mmorpg.Editor.ReverseEngineering.Data
{
    public enum ShaiyaAssetKind { Unknown, Spk, Sah, Saf, Svmap, CharacterMesh3DC, ObjectMesh3DO, AnimationANI, Texture, SData, Effect, Sound, Config }
    public static class ShaiyaAssetClassifier
    {
        public static ShaiyaAssetKind Classify(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            switch (ext)
            {
                case ".spk": return ShaiyaAssetKind.Spk;
                case ".sah": return ShaiyaAssetKind.Sah;
                case ".saf": return ShaiyaAssetKind.Saf;
                case ".svmap": return ShaiyaAssetKind.Svmap;
                case ".3dc": return ShaiyaAssetKind.CharacterMesh3DC;
                case ".3do": return ShaiyaAssetKind.ObjectMesh3DO;
                case ".ani": return ShaiyaAssetKind.AnimationANI;
                case ".dds": case ".tga": case ".png": case ".jpg": case ".jpeg": case ".bmp": return ShaiyaAssetKind.Texture;
                case ".sdata": return ShaiyaAssetKind.SData;
                case ".eft": case ".3de": return ShaiyaAssetKind.Effect;
                case ".wav": case ".mp3": case ".ogg": return ShaiyaAssetKind.Sound;
                case ".ini": case ".cfg": case ".xml": case ".txt": case ".csv": return ShaiyaAssetKind.Config;
                default: return ShaiyaAssetKind.Unknown;
            }
        }
    }
}
