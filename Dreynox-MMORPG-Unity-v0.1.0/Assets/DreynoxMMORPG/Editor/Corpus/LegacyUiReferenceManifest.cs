using System.Collections.Generic;

namespace Dreynox.Mmorpg.Editor.Corpus
{
    public static class LegacyUiReferenceManifest
    {
        public static readonly IReadOnlyDictionary<string, string> Paths =
            new Dictionary<string, string>
            {
                ["login.world"] =
                    "DATA_Español/world/Login.wld",
                ["login.background"] =
                    "DATA_Español/interface/Login/BG.tga",
                ["login.logo"] =
                    "DATA_Español/interface/Login/shaiyalogo01_new.TGA",
                ["login.check"] =
                    "DATA_Español/interface/Login/LoginCheck.tga",

                ["character.select.background"] =
                    "DATA_Español/interface/CharacterSelect/selectBG.tga",
                ["character.select.start"] =
                    "DATA_Español/interface/CharacterSelect/button/select_start.tga",

                ["character.make.tab"] =
                    "DATA_Español/interface/CharacterMake/button/create_tab_button.tga",
                ["character.make.nav.left"] =
                    "DATA_Español/interface/CharacterMake/button/navi_left.tga",
                ["character.make.nav.right"] =
                    "DATA_Español/interface/CharacterMake/button/navi_right.tga",
                ["character.make.nav.play"] =
                    "DATA_Español/interface/CharacterMake/button/navi_play.tga",
                ["character.make.nav.stop"] =
                    "DATA_Español/interface/CharacterMake/button/navi_stop.tga",
                ["character.make.nav.zoomIn"] =
                    "DATA_Español/interface/CharacterMake/button/navi_zoomin.tga",
                ["character.make.nav.zoomOut"] =
                    "DATA_Español/interface/CharacterMake/button/navi_zoomout.tga",

                ["wing.position"] =
                    "DATA_Español/excelxml/wingposition.xml",
                ["wing.mon"] =
                    "DATA_Español/character/wing/wing.mon",
            };
    }
}
