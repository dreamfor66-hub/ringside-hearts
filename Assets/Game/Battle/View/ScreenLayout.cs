using UnityEngine;
using UnityEngine.UI;

namespace RingsideHearts.Battle.View
{
    /// <summary>
    /// Portrait layout (design 10.2): a 1080-wide column split into Top (250), Ring (1080 square)
    /// and Bottom (the rest). The zones never overlap, so nothing covers the ring or its apron.
    /// </summary>
    public sealed class ScreenLayout : MonoBehaviour
    {
        public const float ReferenceWidth = 1080f;
        public const float ReferenceHeight = 1920f;
        public const float TopHeight = 250f;
        public const float RingSize = 1080f;

        public Canvas Canvas { get; private set; }
        public RectTransform Safe { get; private set; }
        public RectTransform Top { get; private set; }
        public RectTransform Ring { get; private set; }
        public RectTransform Bottom { get; private set; }

        Rect _appliedSafeArea;
        Vector2Int _appliedScreen;

        public static ScreenLayout Create()
        {
            var go = new GameObject("BattleCanvas");
            var layout = go.AddComponent<ScreenLayout>();
            layout.Build();
            return layout;
        }

        void Build()
        {
            Canvas = gameObject.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            gameObject.AddComponent<GraphicRaycaster>();

            Image background = UiKit.MakeImage(transform, "Background", UiKit.Square, UiKit.Background);
            UiKit.Stretch(background.rectTransform);

            Safe = UiKit.MakeRect(transform, "SafeArea");
            UiKit.Stretch(Safe);

            RectTransform column = UiKit.MakeRect(Safe, "Column");
            column.anchorMin = new Vector2(0.5f, 0f);
            column.anchorMax = new Vector2(0.5f, 1f);
            column.pivot = new Vector2(0.5f, 0.5f);
            column.sizeDelta = new Vector2(ReferenceWidth, 0f);
            column.anchoredPosition = Vector2.zero;

            Top = MakeZone(column, "Top");
            Top.anchorMin = new Vector2(0f, 1f);
            Top.anchorMax = new Vector2(1f, 1f);
            Top.pivot = new Vector2(0.5f, 1f);
            Top.sizeDelta = new Vector2(0f, TopHeight);
            Top.anchoredPosition = Vector2.zero;

            Ring = UiKit.MakeRect(column, "Ring");
            Ring.anchorMin = Ring.anchorMax = new Vector2(0.5f, 1f);
            Ring.pivot = new Vector2(0.5f, 1f);
            Ring.sizeDelta = new Vector2(RingSize, RingSize);
            Ring.anchoredPosition = new Vector2(0f, -TopHeight);

            Bottom = MakeZone(column, "Bottom");
            UiKit.Stretch(Bottom, 0f, 0f, 0f, TopHeight + RingSize);

            ApplySafeArea();
        }

        static RectTransform MakeZone(RectTransform parent, string name)
        {
            Image image = UiKit.MakeImage(parent, name, UiKit.Square, UiKit.Zone);
            return image.rectTransform;
        }

        void Update()
        {
            if (_appliedSafeArea != Screen.safeArea || _appliedScreen.x != Screen.width || _appliedScreen.y != Screen.height)
                ApplySafeArea();
        }

        void ApplySafeArea()
        {
            Rect area = Screen.safeArea;
            _appliedSafeArea = area;
            _appliedScreen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0 || Safe == null) return;
            Safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            Safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            Safe.offsetMin = Safe.offsetMax = Vector2.zero;
        }
    }
}
