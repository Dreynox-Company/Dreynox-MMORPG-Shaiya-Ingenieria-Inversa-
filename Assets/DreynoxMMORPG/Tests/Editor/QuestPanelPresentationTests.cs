using System.Reflection;
using Dreynox.Mmorpg.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class QuestPanelPresentationTests
    {
        [Test]
        public void UnopenedOrUninitializedPanelCannotAcceptOrSelectHiddenQuests()
        {
            var root=new GameObject("Quest action fixture");
            try
            {
                var panel=root.AddComponent<QuestWorldPanel>();
                Assert.IsFalse(panel.SelectVisibleQuest(3400));
                Assert.IsFalse(panel.SubmitSelectedQuest());
                Assert.IsNotEmpty(panel.ActionFailure);
            }
            finally {Object.DestroyImmediate(root);}
        }
        [Test]
        public void ModalSortsAbovePooledWorldLabelsAndOwnsItsRaycaster()
        {
            var root=new GameObject("Quest canvas fixture",typeof(RectTransform),typeof(Canvas));
            try
            {
                var canvas=root.GetComponent<Canvas>();canvas.sortingOrder=0;
                var panel=root.AddComponent<QuestWorldPanel>();
                const BindingFlags fields=BindingFlags.Instance|BindingFlags.NonPublic;
                typeof(QuestWorldPanel).GetField("canvasRoot",fields).SetValue(panel,root.GetComponent<RectTransform>());
                typeof(QuestWorldPanel).GetField("font",fields).SetValue(panel,Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
                typeof(QuestWorldPanel).GetMethod("BuildDialog",fields).Invoke(panel,null);
                var modal=(RectTransform)typeof(QuestWorldPanel).GetField("modal",fields).GetValue(panel);
                var overlay=modal.GetComponent<Canvas>();
                Assert.IsNotNull(overlay);Assert.IsTrue(overlay.overrideSorting);
                Assert.Greater(overlay.sortingOrder,canvas.sortingOrder);
                Assert.IsNotNull(modal.GetComponent<GraphicRaycaster>());
                Assert.IsFalse(modal.gameObject.activeSelf,"A dialog does not open by itself.");
                Assert.IsFalse(panel.SelectVisibleQuest(3400),"Generated controls do not bypass visible NPC eligibility.");
            }
            finally {Object.DestroyImmediate(root);}
        }
    }
}
