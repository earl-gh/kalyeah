using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityObject = UnityEngine.Object;

namespace Kalyeah.Game
{
    /// <summary>
    /// Screen fit (docs/SPEC.md §8): a menu canvas is 720 units tall, and a screen narrower than 16:9 keeps 1280 units of width and
    /// gains height; match HUDs always match the height.
    /// </summary>
    public sealed class MenuCanvasScalingTests
    {
        GameObject _root;

        [SetUp]
        public void SetUp() => _root = new GameObject("ScalingRoot");

        [TearDown]
        public void TearDown()
        {
            ScreenFitMetrics.ClearSimulatedViewport();
            if (_root != null) UnityObject.DestroyImmediate(_root);
        }

        static Vector2 CanvasUnits(Canvas canvas)
        {
            Canvas.ForceUpdateCanvases();
            return ((RectTransform)canvas.transform).rect.size;
        }

        [TestCase(1024, 768, 0f, TestName = "Tablet_4by3_KeepsTheWidth")]
        [TestCase(1280, 720, 1f, TestName = "Sixteen_by_nine_MatchesTheHeight")]
        [TestCase(844, 390, 1f, TestName = "WideFlatPhone_MatchesTheHeight")]
        public void MenuCanvas_MatchesWidthBelowWide_AndHeightAbove(int width, int height, float expectedMatch)
        {
            ScreenFitMetrics.SetSimulatedViewport(new Vector2Int(width, height), new Rect(0f, 0f, width, height));
            var camera = new GameObject("Cam", typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetParent(_root.transform, false);
            Canvas canvas = Ui.MakeCanvas("MenuCanvas", _root.transform, camera);
            Assert.AreEqual(expectedMatch, canvas.GetComponent<CanvasScaler>().matchWidthOrHeight);
        }

        [Test]
        public void MatchHud_AlwaysMatchesTheHeight()
        {
            ScreenFitMetrics.SetSimulatedViewport(new Vector2Int(1024, 768), new Rect(0f, 0f, 1024, 768));
            Canvas hud = Ui.MakeCanvas("Hud", _root.transform, null, keepWidthBelowWide: false);
            Assert.AreEqual(1f, hud.GetComponent<CanvasScaler>().matchWidthOrHeight);
            Assert.IsNull(hud.GetComponent<Ui.KeepWidthBelowWide>());
        }
    }
}
