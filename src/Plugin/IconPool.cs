using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SpellBrigadeMinimap
{
    /// <summary>Reusable Images under one parent. Later placements draw on top of earlier ones.</summary>
    internal sealed class IconPool
    {
        readonly RectTransform parent;
        readonly List<Image> images = new List<Image>();
        int used;

        public IconPool(RectTransform parent)
        {
            this.parent = parent;
        }

        public void Begin() => used = 0;

        public void Place(Sprite sprite, Color color, Vector2 position, float size, float rotation)
            => Place(sprite, color, position, new Vector2(size, size), rotation);

        public void Place(Sprite sprite, Color color, Vector2 position, Vector2 size, float rotation)
        {
            Image img;
            if (used < images.Count) img = images[used];
            else
            {
                img = UiBuilder.NewImage("Icon", parent);
                images.Add(img);
            }
            used++;

            if (img.sprite != sprite) img.sprite = sprite;
            if (img.color != color) img.color = color;
            var rt = img.rectTransform;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            rt.localEulerAngles = new Vector3(0f, 0f, rotation);
            if (!img.enabled) img.enabled = true;
        }

        /// <summary>Hides every image not placed since <see cref="Begin"/>.</summary>
        public void End()
        {
            for (int i = used; i < images.Count; i++)
                if (images[i].enabled) images[i].enabled = false;
        }
    }
}
