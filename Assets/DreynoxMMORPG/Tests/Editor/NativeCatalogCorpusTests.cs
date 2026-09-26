using System;
using System.IO;
using System.Linq;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.NativeContent;
using Dreynox.Mmorpg.NativeContent;
using NUnit.Framework;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class NativeCatalogCorpusTests
    {
        private CanonicalClientCorpus corpus;
        private NativeDataTable[] tables;
        private NativeDefinitionCatalog items,skills;
        [OneTimeSetUp] public void Setup()
        {
            corpus=CanonicalClientCorpus.FromStoredRoot();
            if(corpus==null||!File.Exists(corpus.Resolve("DATA_Español/binarysdata/dbitemdata.sdata")))
                Assert.Ignore("Original DATA not supplied to this test run; synthetic tests are separate.");
            tables=NativeCatalogImporter.ReadVerifiedTables(corpus);
            items=new NativeDefinitionCatalog(tables[0],tables[1],false);skills=new NativeDefinitionCatalog(tables[2],tables[3],true);
        }
        [Test] public void AllFourSourceTablesPassExactHashAndLosslessRoundtripBeforeJoining()
        {
            Assert.AreEqual(28142,items.Count);Assert.AreEqual(12060,skills.Count);
            Assert.AreEqual(70,tables[0].Columns.Count);Assert.AreEqual(101,tables[2].Columns.Count);
            Assert.AreEqual(4,tables[1].Columns.Count);Assert.AreEqual(4,tables[3].Columns.Count);
            Assert.AreEqual(2,tables[0].Footer.Length);Assert.AreEqual(13,tables[2].Footer.Length);
            Assert.IsTrue(items.TryItem(257,out var sword));Assert.AreEqual("Espada Larga",sword.Name);
            Assert.IsTrue(skills.TryGet(1,1,out var passive));StringAssert.Contains("Musculatura",passive.Name);
        }
        [Test] public void EverySkillRankUsesAnExistingOriginalAtlasAndAnInBoundsSourceCell()
        {
            var dimensions=new System.Collections.Generic.Dictionary<string,(int width,int height)>(StringComparer.OrdinalIgnoreCase);
            foreach(var skill in skills.Entries)
            {
                Assert.IsTrue(NativeSkillIcon.TryResolve(skill.Value("image"),out var icon),skill.Key.ToString());
                if(!dimensions.TryGetValue(icon.File,out var size))
                {
                    string path=corpus.Resolve("DATA_Español/interface/icon/"+icon.File);
                    Assert.IsTrue(File.Exists(path),path);byte[] data=File.ReadAllBytes(path);
                    size=Path.GetExtension(path).Equals(".dds",StringComparison.OrdinalIgnoreCase)?
                        (BitConverter.ToInt32(data,16),BitConverter.ToInt32(data,12)):
                        (BitConverter.ToUInt16(data,12),BitConverter.ToUInt16(data,14));
                    dimensions.Add(icon.File,size);
                }
                Assert.IsTrue(icon.Fits(size.width,size.height),skill.Key+" "+icon.File);
            }
            Assert.AreEqual(3,dimensions.Count);
        }
        [Test] public void UnnamedAndZeroIconItemRowsAreRetainedInsteadOfDiscarded()
        {
            Assert.AreEqual(6144,items.Entries.Count(x=>x.Value("icon")==0));
            Assert.AreEqual(28142,items.Entries.Select(x=>x.Key).Distinct().Count());
            Assert.AreEqual(804,skills.Entries.Select(x=>x.Key.Id).Distinct().Count());
            Assert.AreEqual(15,skills.Entries.Max(x=>x.Key.Variant));
        }
        [OneTimeTearDown] public void Cleanup(){tables=null;items=null;skills=null;corpus=null;}
    }
}
