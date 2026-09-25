using System;
using System.IO;
using System.Text;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using Dreynox.Mmorpg.Editor.ReverseEngineering.Binary;
using NUnit.Framework;
using UnityEngine;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class LegacyItemVisualTests
    {
        [TestCase("ITM", 0)] [TestCase("IT2", 16)] [TestCase("pandaIT2", 24)]
        public void AuthoredTablesRetainExtendedFieldsAndTransformSlots(string signature, int slots)
        {
            byte[] data = Descriptor(signature, out int start);
            var result = LegacyItemVisualParser.ParseItm(data);
            Assert.AreEqual(signature, result.Signature); Assert.AreEqual(slots, result.ArchetypeCount);
            Assert.AreEqual(1, result.Records.Count); Assert.AreEqual("fixture.3do", result.MeshNames[0]);
            var row = result.Records[0];
            Assert.AreEqual(-1, row.BlendMode); Assert.AreEqual(22, row.Unknown1); Assert.AreEqual(0xff123456u, row.Rgba);
            Assert.AreEqual(0.75f, row.Scale); Assert.AreEqual(slots, row.Primary.Length);
            for (int i = 0; i < slots; i++)
            {
                Assert.AreEqual(i + 1, row.Primary[i].Bone);
                Assert.AreEqual(new Vector3(i, 2, 3), row.Primary[i].Position);
                Assert.AreEqual(new Quaternion(0, 0, 0, 1.0000001f), row.Primary[i].Rotation);
                Assert.AreEqual(0, row.Secondary[i].Bone);
            }
        }
        [Test]
        public void TruncationBadIndexUnknownFormatAndInvalidRotationAreRejected()
        {
            byte[] source = Descriptor("IT2", out int record);
            byte[] truncated = new byte[source.Length - 1]; Array.Copy(source, truncated, truncated.Length);
            Assert.Throws<EndOfStreamException>(() => LegacyItemVisualParser.ParseItm(truncated));
            byte[] index = (byte[])source.Clone(); Word(index, record, 1);
            Assert.Throws<InvalidDataException>(() => LegacyItemVisualParser.ParseItm(index));
            byte[] format = (byte[])source.Clone(); Word(format, record + 16, 2);
            Assert.Throws<InvalidDataException>(() => LegacyItemVisualParser.ParseItm(format));
            byte[] rotation = (byte[])source.Clone(); Array.Clear(rotation, record + 40 + 16, 16);
            Assert.Throws<InvalidDataException>(() => LegacyItemVisualParser.ParseItm(rotation));
            byte[] nonfinite = (byte[])source.Clone(); Word(nonfinite, record + 40 + 4, 0x7fc00000);
            Assert.Throws<InvalidDataException>(() => LegacyItemVisualParser.ParseItm(nonfinite));
            byte[] tail = new byte[source.Length + 1]; Array.Copy(source, tail, source.Length);
            Assert.Throws<InvalidDataException>(() => LegacyItemVisualParser.ParseItm(tail));
        }
        [Test]
        public void ExcessiveNamesAndPathsCannotEscapeTheResourceDirectory()
        {
            byte[] count = Descriptor("IT2", out _); Word(count, 3, int.MaxValue);
            Assert.Throws<InvalidDataException>(() => LegacyItemVisualParser.ParseItm(count));
            byte[] path = Descriptor("IT2", out _); path[11] = (byte)'/';
            Assert.Throws<InvalidDataException>(() => LegacyItemVisualParser.ParseItm(path));
        }
        [Test]
        public void SimpleRecordsDoNotInventAbsentExtendedFields()
        {
            byte[] full = Descriptor("ITM", out int record);
            byte[] simple = new byte[full.Length - 16]; Array.Copy(full, simple, simple.Length); Word(simple, record + 16, 0);
            var value = LegacyItemVisualParser.ParseItm(simple).Records[0];
            Assert.AreEqual(0, value.RecordFormat); Assert.AreEqual(0, value.Scale); Assert.AreEqual(0u, value.Rgba);
        }
        [TestCase("01.itm",80,16)] [TestCase("02.itm",84,16)] [TestCase("03.itm",80,16)]
        [TestCase("04.itm",80,16)] [TestCase("05.itm",161,16)] [TestCase("06.itm",152,16)]
        [TestCase("07.itm",156,16)] [TestCase("08.itm",141,16)] [TestCase("09.itm",89,16)]
        [TestCase("10.itm",87,16)] [TestCase("11.itm",79,16)] [TestCase("12.itm",165,16)]
        [TestCase("13.itm",152,16)] [TestCase("14.itm",77,16)] [TestCase("15.itm",83,16)]
        [TestCase("19.itm",72,16)] [TestCase("34.itm",71,16)] [TestCase("05_01.itm",181,24)]
        public void EveryCanonicalWeaponDescriptorIsConsumedWithoutGuessingTransformLayout(string name, int records, int slots)
        {
            var corpus = CanonicalClientCorpus.FromStoredRoot();
            if (corpus == null) Assert.Ignore("Requires original ps0032 DATA.");
            string path = corpus.Resolve("DATA_Español/item/" + name);
            string before = FileFingerprint.Sha256(path);
            var value = LegacyItemVisualParser.ParseItm(path);
            Assert.AreEqual(records, value.Records.Count); Assert.AreEqual(slots, value.ArchetypeCount);
            Assert.AreEqual(before, FileFingerprint.Sha256(path));
        }
        [Test]
        public void OriginalSwordRecordAndMeshArePreservedButDoNotClaimInventoryOrNativeCombat()
        {
            var corpus = CanonicalClientCorpus.FromStoredRoot();
            if (corpus == null) Assert.Ignore("Requires original ps0032 DATA.");
            string itm = corpus.Resolve("DATA_Español/item/01.itm");
            Assert.AreEqual("14e17ddc4bf65deb2b2b1758ae7f3b62ce573546a42b745c0cce48124098f9b3", FileFingerprint.Sha256(itm));
            int image = LegacyItemDefinitionParser.ResolveVisualIndex(corpus.Resolve("DATA_Español/binarysdata/dbitemdata.sdata"), 1, 1);
            Assert.AreEqual(0, image);
            var table = LegacyItemVisualParser.ParseItm(itm); var row = table.Records[image];
            Assert.AreEqual("01001.3DO", table.MeshNames[row.MeshIndex]);
            Assert.AreEqual("01001.dds", table.TextureNames[row.TextureIndex]);
            Assert.AreEqual(21, row.Primary[0].Bone);
            Assert.AreEqual(new Vector3(0.099f,0.004f,0.006f), row.Primary[0].Position);
            Assert.AreEqual(new Quaternion(-0.024678f,-0.706676f,-0.024678f,0.706676f),row.Primary[0].Rotation);
            string mesh = corpus.Resolve("DATA_Español/item/3do/01001.3do");
            Assert.AreEqual("96eebdb03e2df03944b10726e0ade62ee01b4f9b2f84a95ecc926bb0d3dda90d",FileFingerprint.Sha256(mesh));
            var value = LegacyItemVisualParser.Parse3do(mesh);
            Assert.AreEqual(169,value.Vertices.Length); Assert.AreEqual(152,value.Faces.Length);
            Assert.AreEqual("",value.EmbeddedTexture);
        }
        [Test]
        public void MeshBoundsTruncationNonfiniteAndForeignTrailerAreValidated()
        {
            byte[] source = MeshFixture();
            Assert.AreEqual(3,LegacyItemVisualParser.Parse3do(source).Vertices.Length);
            byte[] truncated = new byte[source.Length-1];Array.Copy(source,truncated,truncated.Length);
            Assert.Throws<EndOfStreamException>(() => LegacyItemVisualParser.Parse3do(truncated));
            byte[] face = (byte[])source.Clone(); face[108]=3;
            Assert.Throws<InvalidDataException>(() => LegacyItemVisualParser.Parse3do(face));
            byte[] nan = (byte[])source.Clone(); Word(nan,8,0x7fc00000);
            Assert.Throws<InvalidDataException>(() => LegacyItemVisualParser.Parse3do(nan));
            byte[] trailer = new byte[source.Length+1];Array.Copy(source,trailer,source.Length);trailer[trailer.Length-1]=1;
            Assert.Throws<InvalidDataException>(() => LegacyItemVisualParser.Parse3do(trailer));
        }
        private static byte[] Descriptor(string signature, out int record)
        {
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.ASCII))
            {
                writer.Write(Encoding.ASCII.GetBytes(signature));
                void Name(string value) { byte[] b=Encoding.ASCII.GetBytes(value+"\0");writer.Write(b.Length);writer.Write(b); }
                writer.Write(1);Name("fixture.3do");writer.Write(1);Name("fixture.dds");writer.Write(1);
                record=(int)stream.Position;
                foreach(int v in new[]{0,0,-1,22,1,0})writer.Write(v);
                writer.Write(0xff123456u);writer.Write(0.1f);writer.Write(0.75f);writer.Write(1);
                int count=signature=="ITM"?0:signature=="IT2"?16:24;
                for(int i=0;i<count;i++)
                {
                    writer.Write(i+1);writer.Write((float)i);writer.Write(2f);writer.Write(3f);
                    writer.Write(0f);writer.Write(0f);writer.Write(0f);writer.Write(1.0000001f);
                    writer.Write(0);for(int j=0;j<6;j++)writer.Write(0f);writer.Write(1f);
                }
                return stream.ToArray();
            }
        }
        private static byte[] MeshFixture()
        {
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {
                writer.Write(0);writer.Write(3);
                for(int i=0;i<3;i++)foreach(float f in new[]{(float)i,0,0,0,1,0,0,0})writer.Write(f);
                writer.Write(1);writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)2);
                return stream.ToArray();
            }
        }
        private static void Word(byte[] bytes,int offset,int value) { Array.Copy(BitConverter.GetBytes(value),0,bytes,offset,4); }
    }
}
