using Dreynox.Mmorpg.Editor.ReverseEngineering.Executable;
using NUnit.Framework;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class LegacyGameExeBaselineTests
    {
        [Test]
        public void Match_DistinguishesKnownClientVariants()
        {
            var ps0032 = new PortableExecutableInfo
            {
                fileBytes = 5_352_488,
                sha256 = "509c4a8fbe4d5292961fdfb6d1045795a7bb5970fcf2560fd1070aee18273c2d"
            };
            var later = new PortableExecutableInfo
            {
                fileBytes = 8_297_632,
                sha256 = "4768f225250838787db5496ecf304753fb44a6c161b5290f06e836b83e0dd1e8"
            };

            Assert.That(LegacyGameExeBaseline.Match(ps0032)?.id, Is.EqualTo("ps0032-x86-3.3.2.10"));
            Assert.That(LegacyGameExeBaseline.Match(later)?.id, Is.EqualTo("alternate-client-2026-09-20"));
        }

        [Test]
        public void Match_DoesNotSilentlyAcceptUnknownHash()
        {
            var unknown = new PortableExecutableInfo { fileBytes = 5_352_488, sha256 = new string('0', 64) };
            Assert.That(LegacyGameExeBaseline.Match(unknown), Is.Null);
        }
    }
}
