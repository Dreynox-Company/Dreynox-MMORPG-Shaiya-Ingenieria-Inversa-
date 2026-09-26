using System;
using System.IO;
using System.Text;

namespace Dreynox.Mmorpg.UI.Core
{
    /// <summary>Only this remake's local UI preferences; never reads/writes native config.ini.</summary>
    public sealed class NativeUiLocalStore
    {
        private readonly string root;
        public const int MaximumBytes=65536;
        public NativeUiLocalStore(string directory){root=Path.GetFullPath(directory??throw new ArgumentNullException(nameof(directory)));}
        private string PathFor(string name)
        {
            if(String.IsNullOrWhiteSpace(name)||name.Length>80||name.Contains(".."))throw new ArgumentException("Invalid UI settings name.");
            foreach(char c in name)if(!(c>='a'&&c<='z'||c>='0'&&c<='9'||c=='-'||c=='.'))throw new ArgumentException("Invalid UI settings name.");
            return Path.Combine(root,name);
        }
        public string Read(string name)
        {
            string path=PathFor(name);if(!File.Exists(path))return null;
            RejectLinks(path);
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(stream.Length>MaximumBytes)throw new InvalidDataException("UI settings exceed their size budget.");
                var bytes=new byte[(int)stream.Length];int at=0;
                while(at<bytes.Length){int read=stream.Read(bytes,at,bytes.Length-at);if(read<=0)throw new EndOfStreamException();at+=read;}
                return new UTF8Encoding(false,true).GetString(bytes);
            }
        }
        public void Write(string name,string value)
        {
            string path=PathFor(name);byte[] bytes=new UTF8Encoding(false,true).GetBytes(value??throw new ArgumentNullException(nameof(value)));
            if(bytes.Length>MaximumBytes)throw new InvalidDataException("UI settings exceed their size budget.");
            RejectLinks(root);Directory.CreateDirectory(root);RejectLinks(path);
            string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
            }
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
        private static void RejectLinks(string path)
        {
            for(string current=Path.GetFullPath(path);!String.IsNullOrEmpty(current);current=Path.GetDirectoryName(current))
                if((Directory.Exists(current)||File.Exists(current))&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)
                    throw new IOException("Linked UI settings paths are not supported.");
        }
    }
}
