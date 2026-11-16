using UnityEngine;
using UnityEngine.UI;

namespace SpellBrigadeMinimap
{
    internal static class UiBuilder
    {
        const int UiLayer = 5;

        /// <summary>A RectTransform centered in its parent (anchors and pivot at 0.5).</summary>
        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.layer = UiLayer;
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            return rt;
        }

        public static Image NewImage(string name, Transform parent)
        {
            var img = NewRect(name, parent).gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            return img;
        }

        public static RawImage NewRawImage(string name, Transform parent)
        {
            var img = NewRect(name, parent).gameObject.AddComponent<RawImage>();
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Fills the parent, inset by <paramref name="inset"/> on every side.</summary>
        public static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
