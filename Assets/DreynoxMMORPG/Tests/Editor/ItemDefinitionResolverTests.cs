using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using NUnit.Framework;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class ItemDefinitionResolverTests
    {
        [Test]
        public void ReorderedColumnsResolveAnExplicitTypeIdNotTheRowOrdinal()
        {
            var data = Table(new[] { "image", "itemtypeid", "extra", "itemtype" },
                new[] { new long[] { 9, 2, 88, 1 }, new long[] { 7, 1, 99, 1 } });
            Assert.AreEqual(7, LegacyItemDefinitionParser.ResolveVisualIndexPlain(data, 1, 1));
            Assert.AreEqual(9, LegacyItemDefinitionParser.ResolveVisualIndexPlain(data, 1, 2));
            Assert.Throws<KeyNotFoundException>(() => LegacyItemDefinitionParser.ResolveVisualIndexPlain(data, 2, 1));
        }
        [Test]
        public void DuplicateRequestedIdentityAndInvalidImageIndicesAreRejected()
        {
            var names = new[] { "itemtype", "itemtypeid", "image" };
            var duplicate = Table(names, new[] { new long[] { 1, 1, 0 }, new long[] { 1, 1, 2 } });
            Assert.Throws<InvalidDataException>(() => LegacyItemDefinitionParser.ResolveVisualIndexPlain(duplicate, 1, 1));
            foreach (long image in new[] { -1L, (long)int.MaxValue + 1 })
            {
                var invalid = Table(names, new[] { new long[] { 1, 1, image } });
                Assert.Throws<InvalidDataException>(() => LegacyItemDefinitionParser.ResolveVisualIndexPlain(invalid, 1, 1));
            }
        }
        [Test]
        public void MissingDuplicateColumnsTruncatedRowsAndUnknownTrailerCannotShiftRecords()
        {
            var names = new[] { "itemtype", "itemtypeid", "image" };
            byte[] valid = Table(names, new[] { new long[] { 1, 1, 0 } });
            var shortRow = new byte[valid.Length - 1]; Array.Copy(valid, shortRow, shortRow.Length);
            Assert.Throws<EndOfStreamException>(() => LegacyItemDefinitionParser.ResolveVisualIndexPlain(shortRow, 1, 1));
            byte[] tail = new byte[valid.Length + 1]; Array.Copy(valid, tail, valid.Length); tail[tail.Length - 1] = 1;
            Assert.Throws<InvalidDataException>(() => LegacyItemDefinitionParser.ResolveVisualIndexPlain(tail, 1, 1));
            var missing = Table(new[] { "itemtype", "itemtypeid", "unknown" }, new long[0][]);
            Assert.Throws<InvalidDataException>(() => LegacyItemDefinitionParser.ResolveVisualIndexPlain(missing, 1, 1));
            var duplicate = Table(new[] { "itemtype", "itemtypeid", "IMAGE", "image" }, new long[0][]);
            Assert.Throws<InvalidDataException>(() => LegacyItemDefinitionParser.ResolveVisualIndexPlain(duplicate, 1, 1));
        }
        private static byte[] Table(string[] columns, long[][] rows)
        {
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream, Encoding.Unicode))
            {
                writer.Write(new byte[128]); writer.Write(columns.Length);
                foreach (string field in columns) { writer.Write((byte)field.Length); writer.Write(Encoding.Unicode.GetBytes(field)); }
                writer.Write(rows.Length);
                foreach (var row in rows) foreach (long cell in row) writer.Write(cell);
                return stream.ToArray();
            }
        }
    }
}
