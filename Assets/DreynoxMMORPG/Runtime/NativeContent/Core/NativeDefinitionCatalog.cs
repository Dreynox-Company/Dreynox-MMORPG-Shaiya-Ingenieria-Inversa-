using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;

namespace Dreynox.Mmorpg.NativeContent
{
    public readonly struct NativeDefinitionKey : IEquatable<NativeDefinitionKey>,IComparable<NativeDefinitionKey>
    {
        public readonly int Id, Variant;
        public NativeDefinitionKey(int id,int variant)
        {
            if(id<1||id>65535||variant<1||variant>255)throw new InvalidDataException("Native definition key out of range.");
            Id=id;Variant=variant;
        }
        public bool Equals(NativeDefinitionKey other)=>Id==other.Id&&Variant==other.Variant;
        public override bool Equals(object obj)=>obj is NativeDefinitionKey other&&Equals(other);
        public override int GetHashCode()=>Id*397^Variant;
        public int CompareTo(NativeDefinitionKey other){int id=Id.CompareTo(other.Id);return id!=0?id:Variant.CompareTo(other.Variant);}
        public override string ToString()=>Id+"/"+Variant;
        public int ItemKey
        {
            get {if(Id>255)throw new InvalidOperationException("A skill key is not an item key.");return Id<<8|Variant;}
        }
    }
    public sealed class NativeDefinition
    {
        private readonly NativeDataTable data;
        private readonly int row;
        public NativeDefinitionKey Key {get;}
        public string Name {get;}
        public string DisplayName=>String.IsNullOrWhiteSpace(Name)?"[Sin nombre] "+Key:Name;
        public string OriginalDescription {get;}
        public string Description=>NativeDescription.Plain(OriginalDescription);
        public IReadOnlyList<string> Fields=>data.Columns;
        public long Value(string field)=>data.Number(row,field);
        internal NativeDefinition(NativeDefinitionKey key,string name,string text,NativeDataTable table,int index)
        {Key=key;Name=name;OriginalDescription=text;data=table;row=index;}
    }
    /// <summary>All rows and all numeric fields. Catalog membership never grants ownership or learns a skill.</summary>
    public sealed class NativeDefinitionCatalog
    {
        private readonly Dictionary<NativeDefinitionKey,NativeDefinition> entries;
        private readonly NativeDefinition[] sorted;
        public IReadOnlyList<NativeDefinition> Entries {get;}
        public bool IsSkills {get;}
        public int Count=>sorted.Length;
        public int NumericFieldCount {get;}
        public int TextFieldCount {get;}
        public NativeDefinitionCatalog(NativeDataTable numbers,NativeDataTable text,bool skills)
        {
            if(numbers==null||text==null||numbers.IsText||!text.IsText)throw new ArgumentException("Both original numeric and text tables are required.");
            IsSkills=skills;NumericFieldCount=numbers.Columns.Count;TextFieldCount=text.Columns.Count;
            string id=skills?"id":"itemtype",variant=skills?"skilllevel":"itemtypeid",name=skills?"name":"itemname";
            numbers.Column(id);numbers.Column(variant);numbers.Column("image");numbers.Column("level");
            if(!skills)numbers.Column("icon");
            text.Column(id);text.Column(variant);text.Column(name);text.Column("text");
            var labels=new Dictionary<NativeDefinitionKey,int>();
            for(int row=0;row<text.RowCount;row++)
            {
                var key=Key(text,row,id,variant,skills);
                if(labels.ContainsKey(key))throw new InvalidDataException("Duplicate native text key: "+key);
                labels.Add(key,row);
            }
            entries=new Dictionary<NativeDefinitionKey,NativeDefinition>();sorted=new NativeDefinition[numbers.RowCount];
            for(int row=0;row<numbers.RowCount;row++)
            {
                var key=Key(numbers,row,id,variant,skills);
                if(entries.ContainsKey(key))throw new InvalidDataException("Duplicate native numeric key: "+key);
                if(!labels.TryGetValue(key,out int labelRow))throw new InvalidDataException("No original text for native key: "+key);
                var value=new NativeDefinition(key,text.Text(labelRow,name),text.Text(labelRow,"text"),numbers,row);
                entries.Add(key,value);sorted[row]=value;
            }
            if(labels.Count!=entries.Count)throw new InvalidDataException("Orphan original text records remain; catalog join is incomplete.");
            Array.Sort(sorted,(a,b)=>a.Key.CompareTo(b.Key));Entries=new ReadOnlyCollection<NativeDefinition>(sorted);
        }
        private static NativeDefinitionKey Key(NativeDataTable table,int row,string id,string variant,bool skills)
        {
            long first=table.Number(row,id),second=table.Number(row,variant);
            if(first<1||first>(skills?65535:255)||second<1||second>255)throw new InvalidDataException("Native key exceeds its wire identity range.");
            return new NativeDefinitionKey((int)first,(int)second);
        }
        public bool TryGet(int id,int variant,out NativeDefinition result)
        {
            result=null;if(id<1||id>(IsSkills?65535:255)||variant<1||variant>255)return false;
            return entries.TryGetValue(new NativeDefinitionKey(id,variant),out result);
        }
        public bool TryItem(int key,out NativeDefinition value)
        {
            value=null;return !IsSkills&&key>0&&key<=65535&&TryGet(key>>8,key&255,out value);
        }
        public string ItemName(int key)=>TryItem(key,out var item)?item.DisplayName:"Objeto "+(key>>8)+"/"+(key&255);
        /// <summary>Bounded result page. Search is read-only, accent-insensitive, with deterministic native-key order.</summary>
        public IReadOnlyList<NativeDefinition> Search(string query,int offset,int pageSize,out int total)
        {
            if(offset<0||pageSize<1||pageSize>100||query!=null&&query.Length>128)throw new ArgumentOutOfRangeException(nameof(pageSize));
            string term=(query??"").Trim();var result=new List<NativeDefinition>(pageSize);total=0;
            var comparer=CultureInfo.InvariantCulture.CompareInfo;
            foreach(var entry in sorted)
            {
                if(term.Length>0&&comparer.IndexOf(entry.Name,term,CompareOptions.IgnoreCase|CompareOptions.IgnoreNonSpace)<0&&
                    entry.Key.ToString().IndexOf(term,StringComparison.OrdinalIgnoreCase)<0)continue;
                if(total>=offset&&result.Count<pageSize)result.Add(entry);total++;
            }
            return result.AsReadOnly();
        }
    }
    public static class NativeDescription
    {
        /// <summary>Remove ONLY recognized native color controls. Never interpret user/native text as Unity markup.</summary>
        public static string Plain(string value)
        {
            if(value==null)return "";var result=new StringBuilder(value.Length);
            for(int i=0;i<value.Length;i++)
            {
                if(value[i]=='\\'&&i+1<value.Length&&value[i+1]=='n'){result.Append('\n');i++;continue;}
                if(value[i]=='$'&&i+1<value.Length&&value[i+1]=='n'){result.Append('\n');i++;continue;}
                if(value[i]=='{')
                {
                    int end=value.IndexOf('}',i+1);
                    if(end>i&&end-i<=12)
                    {
                        string token=value.Substring(i+1,end-i-1);bool color=token=="/c";
                        if(token.Length>=2&&token[0]=='c')
                        {
                            color=true;for(int j=1;j<token.Length;j++)if(token[j]<'0'||token[j]>'9')color=false;
                        }
                        if(color){i=end;continue;}
                    }
                }
                // Keep tabs/newlines; suppress embedded null/control bytes only in the VIEW.
                char c=value[i];if(c=='\t'||c=='\n'||!Char.IsControl(c))result.Append(c);
            }
            return result.ToString();
        }
    }
}
