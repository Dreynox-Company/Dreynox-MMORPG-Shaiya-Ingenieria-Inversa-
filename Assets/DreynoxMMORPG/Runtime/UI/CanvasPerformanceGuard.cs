using UnityEngine;
using UnityEngine.UI;

namespace Dreynox.Mmorpg.UI
{
    [RequireComponent(typeof(Canvas))]
    public sealed class CanvasPerformanceGuard : MonoBehaviour
    {
        [SerializeField] private bool disablePixelPerfect = true;
        [SerializeField] private bool stripExtraShaderChannels = true;
        private void Awake()
        {
            Canvas canvas = GetComponent<Canvas>();
            if (disablePixelPerfect) canvas.pixelPerfect = false;
            if (stripExtraShaderChannels) canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.None;
            GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster != null) raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;
        }
    }
}
