using System;
using System.Collections;
using System.IO;
using Dreynox.Mmorpg.Interaction;
using Dreynox.Mmorpg.Quests;
using Dreynox.Mmorpg.UI;
using UnityEngine;

namespace Dreynox.Mmorpg.NativeContent
{
    /// <summary>Sequential opt-in Player proof, invoked by the existing real-world qualification.</summary>
    public static class NativeContentQualification
    {
        [Serializable] private sealed class Evidence
        {
            public string scope="actual-converted-content-and-local-inventory-ui-not-native-bag-or-learned-skills",failure="";
            public bool passed,ownershipUnchanged,inputReleased,scriptedUiActions=true;
            public int itemRows,skillRanks,itemFields,skillFields,ownedTypesBefore,ownedTypesAfter;
            public long goldBefore,goldAfter;
            public Vector2 inventoryPixels,catalogPixels;
            public string itemNumericHash,itemTextHash,skillNumericHash,skillTextHash;
        }
        public static IEnumerator Run(NativeWorldHud hud,QuestJournalRuntime journal,string output,Func<string,IEnumerator> capture)
        {
            var report=new Evidence();
            var panel=journal.GetComponent<NativeContentPanel>();
            try
            {
                if(panel==null||!panel.Ready)throw new InvalidOperationException("Original catalog interface did not initialize.");
                report.itemRows=panel.Items.Count;report.skillRanks=panel.Skills.Count;
                report.itemFields=panel.Items.FieldCount;report.skillFields=panel.Skills.FieldCount;
                if(report.itemRows!=28142||report.skillRanks!=12060||report.itemFields!=70||report.skillFields!=101)
                    throw new InvalidOperationException("Converted original catalog is incomplete in the actual Player.");
                report.itemNumericHash=panel.Items.NumericSourceHash;report.itemTextHash=panel.Items.TextSourceHash;
                report.skillNumericHash=panel.Skills.NumericSourceHash;report.skillTextHash=panel.Skills.TextSourceHash;
                string before=JsonUtility.ToJson(journal.Journal.Snapshot());
                report.ownedTypesBefore=journal.Journal.Inventory.Count;report.goldBefore=journal.Journal.Gold;
                if(!panel.OpenInventory())throw new InvalidOperationException("Inventory cannot open through its real UI controller.");
                report.inventoryPixels=hud.CanvasRoot.Find("Native inventory").GetComponent<RectTransform>().rect.size;
                if(report.inventoryPixels!=new Vector2(284,564)||panel.VisibleInventoryTypes!=report.ownedTypesBefore)
                    throw new InvalidOperationException("Inventory view does not match its live local source and original-art dimensions.");
                yield return capture("01-native-inventory-local");
                if(!panel.OpenCatalog(false)||panel.CatalogMatches!=report.itemRows)
                    throw new InvalidOperationException("The complete original item catalog is not reachable.");
                report.catalogPixels=hud.CanvasRoot.Find("Native development catalog").GetComponent<RectTransform>().rect.size;
                if(report.catalogPixels!=new Vector2(500,626))throw new InvalidOperationException("Original catalog frame was stretched.");
                if(!panel.SetCatalogQuery("Espada Larga")||panel.CatalogMatches<1||!panel.InspectCatalogRow(0)||panel.InspectedKey!=257)
                    throw new InvalidOperationException("Original starter sword cannot be found and inspected by its Spanish name.");
                yield return capture("01-original-sword-catalog");
                if(!panel.OpenCatalog(true)||panel.CatalogMatches!=report.skillRanks)
                    throw new InvalidOperationException("The complete original skill-rank catalog is not reachable.");
                yield return capture("01-original-skills-catalog");
                if(!panel.SetCatalogQuery("Musculatura")||panel.CatalogMatches<1||!panel.InspectCatalogRow(0)||panel.InspectedKey!=257)
                    throw new InvalidOperationException("Original skill name/key does not match the converted table.");
                yield return capture("01-original-skill-description");
                if(!panel.SetCatalogQuery("__no_native_definition_expected__")||panel.CatalogMatches!=0||panel.InspectCatalogRow(0))
                    throw new InvalidOperationException("An empty catalog result exposes a stale definition.");
                report.ownershipUnchanged=before==JsonUtility.ToJson(journal.Journal.Snapshot());
                report.ownedTypesAfter=journal.Journal.Inventory.Count;report.goldAfter=journal.Journal.Gold;
                panel.Close();report.inputReleased=!WorldInputGate.IsBlocked;
                if(!report.ownershipUnchanged||!report.inputReleased)
                    throw new InvalidOperationException("Read-only catalog changed the gameplay journal or left world input blocked.");
                report.passed=true;
            }
            finally
            {
                if(panel!=null)panel.Close();
                if(!report.passed)report.failure="Catalog qualification interrupted; see the parent Player report for the original exception.";
                File.WriteAllText(Path.Combine(output,"native-content-qualification.json"),JsonUtility.ToJson(report,true));
            }
        }
    }
}
