using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using Dreynox.Mmorpg.LocalData;
using Dreynox.Mmorpg.World;
using NUnit.Framework;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class Map1EntityTexturePreflightTests
    {
        [TestCase(LegacyMonCatalogKind.Monster)]
        [TestCase(LegacyMonCatalogKind.Npc)]
        public void EveryPlacedModelUsesDecodableOriginalColorTextures(LegacyMonCatalogKind kind)
        {
            var corpus = CanonicalClientCorpus.FromStoredRoot();
            if (corpus == null) Assert.Ignore("Requires original ps0032 DATA.");
            var map = LegacySvmapParser.Parse(corpus.Resolve("DATA_Español/world/1.svmap"));
            var ids = new SortedSet<int>();
            string category;
            if (kind == LegacyMonCatalogKind.Monster)
            {
                category = "monster";
                var definitions = LegacyDbMonsterDataParser.ParseData(
                    corpus.Resolve("DATA_Español/binarysdata/DBMonsterData.SData"));
                foreach (var spawn in map.MonsterAreas.SelectMany(a => a.Monsters).Where(s => s.Count > 0))
                {
                    Assert.IsTrue(definitions.TryGet(spawn.MobId, out var row), "Missing original monster definition.");
                    ids.Add(checked((int)row.Image));
                }
                Assert.AreEqual(30, ids.Count);
            }
            else
            {
                category = "npc";
                var definitions = LegacyNpcQuestHeaderParser.ParseEncrypted(
                    corpus.Resolve("DATA_Español/npc/NpcQuest.SData"));
                int unresolved = 0;
                foreach (var spawn in map.Npcs)
                {
                    if (definitions.TryGet(spawn.NpcType, spawn.NpcId, out var row)) ids.Add(row.Model);
                    else
                    {
                        // Preserve the already reported source gap. Do not invent a substitute NPC.
                        Assert.IsTrue(spawn.NpcType == 8 && spawn.NpcId == 169,
                            "An additional original NPC definition is unresolved.");
                        unresolved += spawn.Positions.Count;
                    }
                }
                Assert.AreEqual(4, unresolved);
                Assert.AreEqual(54, ids.Count);
            }
            var catalog = LegacyMonParser.Parse(corpus.Resolve("DATA_Español/" + category + "/" + category + ".mon"));
            var checkedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var errors = new List<string>();
            long decodedBytes = 0;
            foreach (int id in ids)
            {
                Assert.That(id, Is.InRange(0, catalog.Records.Count - 1));
                var model = catalog.Records[id];
                foreach (var part in model.Objects)
                {
                    string relative = "DATA_Español/" + category + "/dds/" + part.TextureName;
                    if (!checkedPaths.Add(relative)) continue;
                    try
                    {
                        string path = corpus.Resolve(relative);
                        if (!File.Exists(path)) throw new FileNotFoundException("Original texture is absent.", path);
                        if (!string.Equals(Path.GetExtension(path), ".dds", StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("Unexpected MON color format; qualify it explicitly.");
                        long size = new FileInfo(path).Length;
                        if (size > 64L * 1024 * 1024) throw new InvalidDataException("Original texture exceeds the per-file preflight budget.");
                        // Exactly the decoder used by the real skinned material importer.
                        // One texture at a time; do not retain all decoded image buffers.
                        DecodedDds texture = LegacyDdsDecoder.Decode(File.ReadAllBytes(path));
                        if (texture.Pixels.Length != checked(texture.Width * texture.Height * 4))
                            throw new InvalidDataException("Decoded image size mismatch.");
                        decodedBytes += texture.Pixels.Length;
                    }
                    catch (Exception ex)
                    {
                        if (!(ex is IOException) && !(ex is NotSupportedException) && !(ex is ArgumentException)) throw;
                        errors.Add(kind + "/" + id + " '" + model.Name + "' " + relative + ": " + ex.GetType().Name + ": " + ex.Message);
                    }
                }
            }
            TestContext.WriteLine(kind + " textures checked=" + checkedPaths.Count + " decodedBytes=" + decodedBytes);
            Assert.Greater(checkedPaths.Count, 0);
            Assert.IsEmpty(errors, "Original entity texture preflight failed before any scene import:\n" + string.Join("\n", errors));
        }
    }
}
