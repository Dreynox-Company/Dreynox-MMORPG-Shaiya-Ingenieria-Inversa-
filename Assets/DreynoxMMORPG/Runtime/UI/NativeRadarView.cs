using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Dreynox.Mmorpg.UI
{
    /// <summary>Original colored/alpha-masked radar sprites, independently of 3D model appearance.</summary>
    public sealed class NativeRadarView : MonoBehaviour
    {
        public const int MarkerBudget = 512;
        // Native frame is the authored 202x226 area of a 256-square texture.
        private static readonly Vector2 ViewSize = new Vector2(190, 190);
        private NativeRadarSkin skin;
        private RectTransform viewport;
        private RawImage map;
        private Image player;
        private Text positionLabel;
        private readonly List<Image> markers = new List<Image>();
        private Rect uvWindow;
        private Vector2 worldSize = new Vector2(2048, 2048);
        private Vector3 playerPosition;
        private float fraction = 0.22f;
        private int used;
        public int VisibleMarkerCount => used;
        public float ViewFraction => fraction;
        public Rect MapWindow => uvWindow;
        public RectTransform Viewport => viewport;
        public IReadOnlyList<Image> Markers => markers;
        public Image PlayerMarker => player;

        public void Build(RectTransform parent, NativeRadarSkin artwork, Texture2D mapTexture)
        {
            if (artwork == null || mapTexture == null || parent == null) throw new ArgumentException("Native radar content missing.");
            if (viewport != null) throw new InvalidOperationException("Radar already built.");
            skin = artwork;
            viewport = Element("Radar clipped content", parent, new Vector2(6, -13), ViewSize);
            viewport.gameObject.AddComponent<RectMask2D>();
            map = viewport.gameObject.AddComponent<RawImage>();
            map.texture = mapTexture; map.raycastTarget = false;
            player = Marker("Original local-player arrow", skin.Get(NativeRadarKind.Player));
            var frame = Element("Original radar frame", parent, Vector2.zero, new Vector2(202, 226)).gameObject.AddComponent<RawImage>();
            frame.texture = skin.Frame;
            frame.uvRect = TopLeftUv(skin.Frame, new Rect(0, 0, 202, 226));
            frame.raycastTarget = false;
            ZoomButton(parent, "Zoom in", skin.ZoomIn, new Vector2(8, -204), () => Zoom(1));
            ZoomButton(parent, "Zoom out", skin.ZoomOut, new Vector2(25, -204), () => Zoom(-1));
            positionLabel = Element("Radar position", parent, new Vector2(58, -203), new Vector2(135, 20)).gameObject.AddComponent<Text>();
            positionLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            positionLabel.fontSize = 11; positionLabel.color = Color.white; positionLabel.alignment = TextAnchor.MiddleRight;
            positionLabel.raycastTarget = false; positionLabel.supportRichText = false;
        }
        public void BeginFrame(Vector3 position, float heading, Vector2 extent)
        {
            if (map == null) throw new InvalidOperationException("Radar not built.");
            if (extent.x <= 0 || extent.y <= 0 || !Finite(extent.x) || !Finite(extent.y)) throw new ArgumentOutOfRangeException(nameof(extent));
            worldSize = extent; playerPosition = position; used = 0;
            uvWindow = Window(position, extent, fraction);
            map.uvRect = uvWindow;
            if (Project(position, extent, uvWindow, ViewSize, out Vector2 at))
            {
                player.gameObject.SetActive(true); player.rectTransform.anchoredPosition = at;
                player.rectTransform.localRotation = Quaternion.Euler(0, 0, -heading);
            }
            else player.gameObject.SetActive(false);
            positionLabel.text = Mathf.RoundToInt(position.x) + " / " + Mathf.RoundToInt(position.z);
        }
        public bool Add(Vector3 position, NativeRadarKind kind)
        {
            if (used >= MarkerBudget || !Project(position, worldSize, uvWindow, ViewSize, out Vector2 at)) return false;
            Sprite icon = skin.Get(kind);
            if (used == markers.Count) markers.Add(Marker("Original entity icon", icon));
            Image marker = markers[used++];
            marker.gameObject.SetActive(true); marker.sprite = icon;
            // Keep all native transparent padding and pixel proportions. Native monster
            // texture is 16x16 with a small round colored center, not a 5x5 solid quad.
            marker.color = Color.white;
            marker.rectTransform.sizeDelta = icon.rect.size;
            marker.rectTransform.anchoredPosition = at;
            marker.rectTransform.localRotation = Quaternion.identity;
            return true;
        }
        public void EndFrame()
        {
            for (int i = used; i < markers.Count; i++) markers[i].gameObject.SetActive(false);
            player.transform.SetAsLastSibling();
        }
        public void Zoom(int direction)
        {
            fraction = Mathf.Clamp(fraction * (direction > 0 ? 0.8f : 1.25f), 0.055f, 1f);
            uvWindow = Window(playerPosition, worldSize, fraction); map.uvRect = uvWindow;
            // Hide old coordinates until the same world snapshot is projected again.
            foreach (var marker in markers) marker.gameObject.SetActive(false);
            player.gameObject.SetActive(false); used = 0;
        }
        public static Rect Window(Vector3 position, Vector2 extent, float visibleFraction)
        {
            float f = Mathf.Clamp(visibleFraction, 0.001f, 1f);
            return new Rect(Mathf.Clamp(position.x / extent.x - f / 2, 0, 1 - f),
                Mathf.Clamp(position.z / extent.y - f / 2, 0, 1 - f), f, f);
        }
        public static bool Project(Vector3 point, Vector2 extent, Rect window, Vector2 size, out Vector2 result)
        {
            result = Vector2.zero;
            if (!Finite(point.x) || !Finite(point.z) || extent.x <= 0 || extent.y <= 0 || window.width <= 0 || window.height <= 0) return false;
            float x = (point.x / extent.x - window.x) / window.width;
            float y = (point.z / extent.y - window.y) / window.height;
            if (x < 0 || x > 1 || y < 0 || y > 1) return false;
            // Viewport marker anchors are bottom-left; image UV and marker use one transform.
            result = new Vector2(x * size.x, y * size.y); return true;
        }
        public static Rect TopLeftUv(Texture texture, Rect pixels)
        {
            if (texture == null || pixels.x < 0 || pixels.y < 0 || pixels.xMax > texture.width || pixels.yMax > texture.height || pixels.width <= 0 || pixels.height <= 0)
                throw new ArgumentOutOfRangeException(nameof(pixels));
            return new Rect(pixels.x / texture.width, 1f - pixels.yMax / texture.height, pixels.width / texture.width, pixels.height / texture.height);
        }
        private Image Marker(string name, Sprite icon)
        {
            var rect = Element(name, viewport, Vector2.zero, icon.rect.size);
            rect.anchorMin = rect.anchorMax = Vector2.zero; rect.pivot = Vector2.one * .5f;
            Image image = rect.gameObject.AddComponent<Image>(); image.sprite = icon; image.raycastTarget = false;
            image.color = Color.white; image.type = Image.Type.Simple;
            return image;
        }
        private static void ZoomButton(RectTransform parent, string name, Sprite icon, Vector2 at, Action click)
        {
            var root = Element(name, parent, at, icon.rect.size);
            var image = root.gameObject.AddComponent<Image>(); image.sprite = icon;
            var button = root.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            button.onClick.AddListener(() => click());
        }
        private static RectTransform Element(string name, RectTransform parent, Vector2 at, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1); rect.sizeDelta = size; rect.anchoredPosition = at;
            return rect;
        }
        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
