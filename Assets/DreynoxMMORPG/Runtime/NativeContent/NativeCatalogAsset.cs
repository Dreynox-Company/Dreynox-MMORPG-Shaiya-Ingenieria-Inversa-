using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Dreynox.Mmorpg.NativeContent
{
    [Serializable]
    public sealed class NativeCatalogEntry
    {
        [SerializeField] private int id,variant,atlasIndex=-1,iconX,iconY;
        [SerializeField] private string originalName,originalDescription,iconIssue;
        [SerializeField] private long[] numbers;
        public int Id=>id;
        public int Variant=>variant;
        public int Key=>checked(id*256+variant);
        public string Name=>String.IsNullOrWhiteSpace(originalName)?"[Sin nombre] "+id+"/"+variant:originalName;
        public string OriginalName=>originalName;
        public string OriginalDescription=>originalDescription;
        public string Description=>NativeDescription.Plain(originalDescription);
        public string IconIssue=>iconIssue??"";
        internal int NumberCount=>numbers==null?-1:numbers.Length;
        internal long Number(int column)=>numbers[column];
        internal int AtlasIndex=>atlasIndex;
        internal Rect IconPixels=>new Rect(iconX,iconY,32,32);
        public NativeCatalogEntry(NativeDefinition definition,IReadOnlyList<string> columns,int atlas,NativeIconRegion region,string issue)
        {
            if(definition==null||columns==null)throw new ArgumentNullException(nameof(definition));
            id=definition.Key.Id;variant=definition.Key.Variant;
            originalName=definition.Name;originalDescription=definition.OriginalDescription;
            numbers=new long[columns.Count];
            for(int i=0;i<numbers.Length;i++)numbers[i]=definition.Value(columns[i]);
            atlasIndex=atlas;iconX=region.X;iconY=region.Y;iconIssue=issue??"";
        }
    }

    /// <summary>
    /// Editor-converted native data, serialized by Unity. The Player does not
    /// open SData, decrypt files, parse native table bytes, or write to DATA.
    /// A catalog entry is not an item instance, bag slot or learned ability.
    /// </summary>
    public sealed class NativeCatalogAsset : ScriptableObject
    {
        [SerializeField] private int schema=1;
        [SerializeField] private bool skills;
        [SerializeField] private string numericSourceHash,textSourceHash;
        [SerializeField] private string[] fields=Array.Empty<string>();
        [SerializeField] private NativeCatalogEntry[] entries=Array.Empty<NativeCatalogEntry>();
        [SerializeField] private Texture2D[] atlases=Array.Empty<Texture2D>();
        private Dictionary<string,int> columnIndex;
        public bool IsSkills=>skills;
        public int Count=>entries.Length;
        public int FieldCount=>fields.Length;
        public int AtlasCount=>atlases.Length;
        public string NumericSourceHash=>numericSourceHash;
        public string TextSourceHash=>textSourceHash;
        public string FieldName(int column)=>fields[column];
        public NativeCatalogEntry Entry(int index)=>entries[index];
        public void Configure(bool isSkills,string[] columns,NativeCatalogEntry[] rows,Texture2D[] textures,string numericHash,string textHash)
        {
            if(columns==null||rows==null||textures==null)throw new ArgumentNullException(nameof(rows));
            ValidateData(isSkills,columns,rows,textures,numericHash,textHash);
            skills=isSkills;fields=(string[])columns.Clone();entries=(NativeCatalogEntry[])rows.Clone();
            atlases=(Texture2D[])textures.Clone();numericSourceHash=numericHash;textSourceHash=textHash;schema=1;columnIndex=null;
        }
        public void Validate()
        {
            if(schema!=1)throw new InvalidDataException("Unsupported converted catalog schema.");
            ValidateData(skills,fields,entries,atlases,numericSourceHash,textSourceHash);
        }
        private static void ValidateData(bool isSkills,string[] columns,NativeCatalogEntry[] rows,Texture2D[] textures,string numericHash,string textHash)
        {
            if(columns==null||columns.Length<4||columns.Length>NativeDataTable.MaximumColumns||rows==null||rows.Length>NativeDataTable.MaximumRows||
                textures==null||textures.Length>256||!Hash(numericHash)||!Hash(textHash))throw new InvalidDataException("Invalid converted native catalog.");
            var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(string field in columns)if(String.IsNullOrWhiteSpace(field)||!names.Add(field))throw new InvalidDataException("Invalid converted field names.");
            if(!names.Contains(isSkills?"id":"itemtype")||!names.Contains(isSkills?"skilllevel":"itemtypeid")||!names.Contains("level"))
                throw new InvalidDataException("Converted identity fields are absent.");
            int previous=0;
            foreach(var row in rows)
            {
                if(row==null||row.Id<1||row.Id>(isSkills?65535:255)||row.Variant<1||row.Variant>255||row.Key<=previous||row.NumberCount!=columns.Length)
                    throw new InvalidDataException("Invalid/unsorted converted native record.");
                previous=row.Key;
                if(row.AtlasIndex<-1||row.AtlasIndex>=textures.Length)throw new InvalidDataException("Converted icon index is outside atlas set.");
                if(row.AtlasIndex>=0)
                {
                    var texture=textures[row.AtlasIndex];var rect=row.IconPixels;
                    if(texture==null||rect.x<0||rect.y<0||rect.xMax>texture.width||rect.yMax>texture.height)
                        throw new InvalidDataException("Converted icon crop is outside the actual source texture.");
                }
                else if(String.IsNullOrWhiteSpace(row.IconIssue))throw new InvalidDataException("Missing icons need an explicit source reason.");
            }
        }
        private static bool Hash(string value)
        {
            if(value==null||value.Length!=64)return false;
            foreach(char c in value)if(!(c>='0'&&c<='9'||c>='a'&&c<='f'))return false;
            return true;
        }
        private void OnEnable(){columnIndex=null;}
        public bool TryGet(int id,int variant,out NativeCatalogEntry entry)
        {
            entry=null;if(id<1||id>(skills?65535:255)||variant<1||variant>255)return false;
            int key=id*256+variant,low=0,high=entries.Length-1;
            while(low<=high)
            {
                int middle=low+(high-low)/2,value=entries[middle].Key;
                if(value==key){entry=entries[middle];return true;}
                if(value<key)low=middle+1;else high=middle-1;
            }
            return false;
        }
        public bool TryItem(int key,out NativeCatalogEntry entry)
        {entry=null;return !skills&&key>0&&key<=65535&&TryGet(key>>8,key&255,out entry);}
        public string ItemName(int key)=>TryItem(key,out var item)?item.Name:"Objeto "+(key>>8)+"/"+(key&255);
        public long Value(NativeCatalogEntry entry,string field)
        {
            if(entry==null||!TryGet(entry.Id,entry.Variant,out var actual)||!ReferenceEquals(entry,actual))
                throw new ArgumentException("Entry belongs to another catalog.");
            if(columnIndex==null)
            {
                columnIndex=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
                for(int i=0;i<fields.Length;i++)columnIndex.Add(fields[i],i);
            }
            if(!columnIndex.TryGetValue(field,out int column))throw new InvalidDataException("Native field absent: "+field);
            return entry.Number(column);
        }
        public bool TryIcon(NativeCatalogEntry entry,out Texture2D texture,out Rect pixels)
        {
            texture=null;pixels=default;
            if(entry==null||!TryGet(entry.Id,entry.Variant,out var actual)||!ReferenceEquals(entry,actual)||entry.AtlasIndex<0)return false;
            texture=atlases[entry.AtlasIndex];pixels=entry.IconPixels;return texture!=null;
        }
        public List<NativeCatalogEntry> Search(string query,int offset,int limit,out int total)
        {
            if(offset<0||limit<1||limit>100||query!=null&&query.Length>128)throw new ArgumentOutOfRangeException(nameof(limit));
            var result=new List<NativeCatalogEntry>(limit);total=0;string term=(query??"").Trim();
            var comparer=CultureInfo.InvariantCulture.CompareInfo;
            foreach(var entry in entries)
            {
                if(term.Length>0&&comparer.IndexOf(entry.Name,term,CompareOptions.IgnoreCase|CompareOptions.IgnoreNonSpace)<0&&
                    (entry.Id+"/"+entry.Variant).IndexOf(term,StringComparison.OrdinalIgnoreCase)<0)continue;
                if(total>=offset&&result.Count<limit)result.Add(entry);total++;
            }
            return result;
        }
    }
}
