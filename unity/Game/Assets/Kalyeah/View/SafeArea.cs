using UnityEngine;

namespace Kalyeah.Game
{
    /// <summary>Keeps its RectTransform inside Screen.safeArea (notch, rounded corners, home bar), in either landscape way round.</summary>
    public sealed class SafeArea : MonoBehaviour
    {
        Rect _applied;

        void Update() => Apply();

        public void Apply()
        {
            Rect safe = Screen.safeArea;
            if (safe == _applied || Screen.width <= 0 || Screen.height <= 0) return;
            _applied = safe;
            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            rect.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
