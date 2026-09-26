using System;
using System.IO;
using Dreynox.Mmorpg.Editor.Corpus;
using NUnit.Framework;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class CanonicalCorpusPathsTests
    {
        private string root;
        [SetUp] public void Setup() { root = Path.Combine(Path.GetTempPath(), "dreynox-alias-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); }
        [TearDown] public void Cleanup() { Directory.Delete(root, true); }
        [TestCase("DATA")] [TestCase("DATA_Español")] [TestCase("DATA_Espanol")]
        public void SameLogicalResourceWorksFromGameAndDirectDataRoot(string alias)
        {
            string data = Path.Combine(root, alias);
            Directory.CreateDirectory(Path.Combine(data, "world"));
            string actual = Path.Combine(data, "world", "login.wld"); File.WriteAllText(actual, "fixture");
            Assert.AreEqual(actual, CanonicalCorpusPaths.Resolve(root, "DATA_Español/World/Login.wld"));
            Assert.AreEqual(actual, CanonicalCorpusPaths.Resolve(data, "DATA_Español/World/Login.wld"));
            Assert.AreEqual(actual, CanonicalCorpusPaths.Resolve(data, "World/Login.wld"));
        }
        [TestCase("../outside")] [TestCase("DATA/../outside")] [TestCase("C:/outside")] [TestCase("DATA//world")]
        public void EscapePathsAreRejected(string path) => Assert.Throws<ArgumentException>(() => CanonicalCorpusPaths.Resolve(root, path));
        [Test] public void TwoAliasesRequireExplicitSelection()
        {
            Directory.CreateDirectory(Path.Combine(root, "DATA")); Directory.CreateDirectory(Path.Combine(root, "DATA_Espanol"));
            Assert.Throws<IOException>(() => CanonicalCorpusPaths.FindDataRoot(root));
            Assert.AreEqual(Path.Combine(root,"DATA"), CanonicalCorpusPaths.FindDataRoot(Path.Combine(root,"DATA")));
        }
        [Test] public void MissingSuffixIsPreservedForCallersCheckingOptionalResources()
        {
            Directory.CreateDirectory(Path.Combine(root,"DATA"));
            Assert.AreEqual(Path.Combine(root,"DATA","missing","child.dds"), CanonicalCorpusPaths.Resolve(root,"DATA_Español/missing/child.dds"));
        }
    }
}
