using System;
using System.IO;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.UI;
using UnityEditor;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.Importing
{
    public static class NativeHudSkinImporter
    {
        private const string Root="Assets/DreynoxMMORPG/LocalLegacyGenerated/NativeHud/PixelSkin";
        public static NativeHudSkin Import(CanonicalClientCorpus corpus)
        {
            if(corpus==null)throw new ArgumentNullException(nameof(corpus));Directory.CreateDirectory(Root);
            string path=Root+"/NativeHudSkin.asset";
            var skin=AssetDatabase.LoadAssetAtPath<NativeHudSkin>(path);
            if(skin==null){skin=ScriptableObject.CreateInstance<NativeHudSkin>();AssetDatabase.CreateAsset(skin,path);}
            skin.horizontalBar=Texture(corpus,"slot/main_slot_1.tga");skin.verticalBar=Texture(corpus,"slot/main_slot_2.tga");
            var fills=Texture(corpus,"statusminibar/player_bar.tga");
            skin.health=Sprite(fills,new Rect(0,0,150,8),"health");skin.mana=Sprite(fills,new Rect(0,8,150,8),"mana");
            skin.stamina=Sprite(fills,new Rect(0,16,150,8),"stamina");
            skin.levelFrame=Sprite(Texture(corpus,"statusminibar/player_bar_level_bg.tga"),new Rect(0,0,21,18),"level");
            // Original basic-action atlas cell 4 visually matches the native attack control.
            // This does not make the local diagnostic combat formula native.
            skin.attackIcon=Sprite(Texture(corpus,"icon/icon_sub.tga"),new Rect(0,32,32,32),"basic-attack");
            skin.previous=Strip(corpus,"slot/button/main_slot_btn_up.tga",16,8,"page-up");
            skin.next=Strip(corpus,"slot/button/main_slot_btn_down.tga",16,8,"page-down");
            skin.previousVertical=Strip(corpus,"slot/button/main_slot_btn_left.tga",8,16,"page-left");
            skin.nextVertical=Strip(corpus,"slot/button/main_slot_btn_right.tga",8,16,"page-right");
            skin.expand=Strip(corpus,"slot/button/main_slot_btn_plus.tga",16,16,"expand");
            skin.collapse=Strip(corpus,"slot/button/main_slot_btn_minus.tga",16,16,"collapse");
            skin.rotate=Strip(corpus,"slot/button/main_slot_btn_rotation.tga",16,16,"rotate");
            skin.command=Strip(corpus,"button/design0.tga",32,32,"command",new Vector4(3,3,3,3));
            var scroll=Texture(corpus,"common/scrollbar.tga");
            skin.scrollUp=new Sprite[4];skin.scrollDown=new Sprite[4];
            for(int i=0;i<4;i++)
            {
                int column=Math.Min(i,2)*16;
                skin.scrollUp[i]=Sprite(scroll,new Rect(column,0,13,19),"scroll-up-"+i);
                skin.scrollDown[i]=Sprite(scroll,new Rect(column,32,13,19),"scroll-down-"+i);
            }
            skin.scrollTop=Sprite(scroll,new Rect(50,0,9,4),"scroll-top");
            skin.scrollMiddle=Sprite(scroll,new Rect(50,32,9,1),"scroll-middle");
            skin.scrollBottom=Sprite(scroll,new Rect(66,0,9,4),"scroll-bottom");
            skin.Validate();EditorUtility.SetDirty(skin);AssetDatabase.SaveAssets();return skin;
        }
        private static Texture2D Texture(CanonicalClientCorpus corpus,string relative)
        {
            string destination=Root+"/"+relative.Replace('/','_');
            return NativeRadarSkinImporter.ImportTexture(corpus.Resolve("DATA_Español/interface/"+relative),destination,false);
        }
        private static Sprite[] Strip(CanonicalClientCorpus corpus,string relative,int width,int height,string name,Vector4 border=default)
        {
            var texture=Texture(corpus,relative);var result=new Sprite[4];
            for(int i=0;i<4;i++)result[i]=Sprite(texture,new Rect(i*width,0,width,height),name+"-"+i,border);
            return result;
        }
        private static Sprite Sprite(Texture2D texture,Rect topLeft,string name,Vector4 border=default)
        {
            NativeRadarView.TopLeftUv(texture,topLeft);
            var value=UnityEngine.Sprite.Create(texture,new Rect(topLeft.x,texture.height-topLeft.yMax,topLeft.width,topLeft.height),
                Vector2.one*.5f,100,0,SpriteMeshType.FullRect,border);
            value.name=name;string path=Root+"/"+name+".asset";
            var previous=AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(path);
            if(previous!=null&&previous.texture==texture&&previous.rect==value.rect&&previous.border==border)
            {UnityEngine.Object.DestroyImmediate(value);return previous;}
            if(previous!=null)AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(value,path);return value;
        }
    }
}
