using UnityEngine;
using UnityEngine.UI;

namespace RingsideHearts.Battle.View
{
    /// <summary>Short-lived text that rises and fades (rope counts, hits, damage later).</summary>
    public sealed class FloatingText : MonoBehaviour
    {
        const float Lifetime = 0.9f;
        const float Rise = 80f;

        Text _text;
        Color _color;
        Vector2 _origin;
        float _age;

        public static void Spawn(RectTransform layer, Vector2 localPosition, string message, Color color)
        {
            Text text = UiKit.MakeText(layer, "Float", 40, TextAnchor.MiddleCenter, color);
            UiKit.Center(text.rectTransform, new Vector2(300f, 60f), localPosition);
            text.text = message;
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);
            var floating = text.gameObject.AddComponent<FloatingText>();
            floating._text = text;
            floating._color = color;
            floating._origin = localPosition;
        }

        void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / Lifetime);
            _text.rectTransform.anchoredPosition = _origin + new Vector2(0f, Rise * t);
            _text.color = new Color(_color.r, _color.g, _color.b, 1f - t * t);
            if (_age >= Lifetime) Destroy(gameObject);
        }
    }
}
