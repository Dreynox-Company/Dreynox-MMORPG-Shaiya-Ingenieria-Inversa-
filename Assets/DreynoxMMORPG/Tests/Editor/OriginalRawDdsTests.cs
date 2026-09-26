using System.IO;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.LocalData;
using NUnit.Framework;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class OriginalRawDdsTests
    {
        [TestCase("ctl_hen_01.dds",256,256)]
        [TestCase("hw2018_gravestone.dds",512,512)]
        public void OriginalUncompressedNpcTextureRetainsEveryColorAndAlpha(string name,int width,int height)
        {
            var corpus=CanonicalClientCorpus.FromStoredRoot();
            if(corpus==null)Assert.Ignore("Requires original ps0032 DATA.");
            byte[] bytes=File.ReadAllBytes(corpus.Resolve("DATA_Español/npc/dds/"+name));
            DecodedDds value=LegacyDdsDecoder.Decode(bytes);
            Assert.AreEqual(width,value.Width);Assert.AreEqual(height,value.Height);
            Assert.AreEqual(width*height*4,value.Pixels.Length);
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                int source=128+(y*width+x)*4,destination=((height-1-y)*width+x)*4;
                if(bytes[source+2]!=value.Pixels[destination]||bytes[source+1]!=value.Pixels[destination+1]||
                    bytes[source]!=value.Pixels[destination+2]||bytes[source+3]!=value.Pixels[destination+3])
                    Assert.Fail("Original pixel/alpha mismatch at "+x+","+y);
            }
        }
    }
}
