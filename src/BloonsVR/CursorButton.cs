using Il2CppInterop.Runtime;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BloonsVR
{
    /// <summary>
    /// An on-screen button that unlocks the mouse cursor so BTD6's own shop, upgrade and hero menus can be
    /// clicked while the first-person view stays live.
    ///
    /// Why this exists in this shape:
    ///   - The cursor has to be locked for mouse look, so the player cannot click BTD6's UI while playing.
    ///   - A normal UGUI button cannot save them either: a locked cursor cannot be pointed at anything, and
    ///     the button would have to be clicked with a cursor that does not exist yet.
    ///   - So the hit test is ours. While the cursor is locked the "pointer" is the centre of the screen, so
    ///     this button is clickable by simply pressing the left mouse button while looking at it.
    ///
    /// The canvas has no GraphicRaycaster and there is no EventSystem, so it cannot intercept or compete
    /// with BTD6's own UI. It is purely a picture, and clicks are resolved by the rect test below.
    /// </summary>
    internal static class CursorButton
    {
        private const float Margin = 18f;
        private const float PanelWidth = 300f;
        private const float PanelHeight = 62f;
        private const int TextScale = 2;

        private static readonly Color32 PanelLocked = new Color32(28, 24, 16, 216);
        private static readonly Color32 PanelFree = new Color32(16, 34, 20, 216);
        private static readonly Color32 TextLocked = new Color32(255, 196, 96, 255);
        private static readonly Color32 TextFree = new Color32(140, 240, 160, 255);

        private static GameObject _canvasObject;
        private static UnityEngine.UI.Image _panelImage;
        private static UnityEngine.UI.RawImage _label;
        private static Texture2D _labelTexture;
        private static bool _built;
        private static bool _failed;
        private static bool _shownState;

        /// <summary>Fired when the player clicks the button or presses the toggle key.</summary>
        internal static System.Action OnToggleRequested;

        /// <summary>Screen-space rect of the button, for the hit test.</summary>
        private static Rect HitRect => new Rect(Margin, Screen.height - Margin - PanelHeight, PanelWidth, PanelHeight);

        /// <summary>
        /// Rebuild the panel and process a click. <paramref name="cursorLocked"/> decides both the label
        /// and where the pointer is.
        /// </summary>
        internal static void Tick(bool cursorLocked, bool rigActive)
        {
            if (_failed)
                return;

            try
            {
                Ensure();
                if (!_built)
                    return;

                var canvas = _canvasObject.GetComponent<Canvas>();
                if (canvas != null)
                    canvas.enabled = rigActive;

                if (!rigActive)
                {
                    _shownState = false;
                    return;
                }

                if (!_shownState || _shownState != cursorLocked)
                {
                    _shownState = cursorLocked;
                    UpdatePanel(cursorLocked);
                }

                if (Clicked(cursorLocked) && OnToggleRequested != null)
                    OnToggleRequested();
            }
            catch (System.Exception e)
            {
                _failed = true;
                MelonLogger.Warning($"[BloonsVR] cursor button disabled: {e.Message}");
            }
        }

        private static void Ensure()
        {
            if (_built || _failed)
                return;

            _canvasObject = new GameObject("BloonsVR_CursorButton");
            _canvasObject.transform.SetParent(null, false);

            var canvas = _canvasObject.AddComponent(Il2CppType.Of<Canvas>()).TryCast<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;

            var panelObject = new GameObject("Panel");
            panelObject.transform.SetParent(_canvasObject.transform, false);

            var panelTransform = panelObject.AddComponent(Il2CppType.Of<RectTransform>());
            var panel = panelTransform.TryCast<RectTransform>();
            panel.anchorMin = new Vector2(0f, 1f);
            panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = new Vector2(Margin, -Margin);
            panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);

            _panelImage = panelObject.AddComponent(Il2CppType.Of<UnityEngine.UI.Image>())
                .TryCast<UnityEngine.UI.Image>();
            _panelImage.color = PanelLocked;

            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(panelObject.transform, false);

            var labelTransform = labelObject.AddComponent(Il2CppType.Of<RectTransform>());
            var label = labelTransform.TryCast<RectTransform>();
            label.anchorMin = new Vector2(0f, 1f);
            label.anchorMax = new Vector2(0f, 1f);
            label.pivot = new Vector2(0f, 1f);
            label.anchoredPosition = new Vector2(10f, -8f);

            _label = labelObject.AddComponent(Il2CppType.Of<UnityEngine.UI.RawImage>())
                .TryCast<UnityEngine.UI.RawImage>();
            _label.color = new Color(1f, 1f, 1f, 1f);

            _built = true;
            MelonLogger.Msg("[BloonsVR] cursor button ready (click it, or press Tab)");
        }

        private static void UpdatePanel(bool cursorLocked)
        {
            if (!_built)
                return;

            _panelImage.color = cursorLocked ? PanelLocked : PanelFree;

            string top = cursorLocked ? "CURSOR LOCKED" : "CURSOR FREE";
            string bottom = cursorLocked ? "TAB OR CLICK TO UNLOCK" : "CLICK BTD6 MENUS - TAB TO RELOCK";

            var colour = cursorLocked ? TextLocked : TextFree;
            var previous = _labelTexture;

            _labelTexture = PixelFont.Render(top, TextScale, colour, new Color32(0, 0, 0, 0));
            var second = PixelFont.Render(bottom, TextScale, colour, new Color32(0, 0, 0, 0));

            // Stack the two lines into one texture so there is only a single RawImage to size.
            if (_labelTexture != null && second != null)
            {
                var combined = Combine(_labelTexture, second);
                UnityEngine.Object.Destroy(second);
                if (previous != null)
                    UnityEngine.Object.Destroy(previous);
                _labelTexture = combined;
            }
            else if (previous != null)
            {
                UnityEngine.Object.Destroy(previous);
            }

            if (_labelTexture == null)
                return;

            _label.texture = _labelTexture;
            _label.SetNativeSize();
        }

        private static Texture2D Combine(Texture2D top, Texture2D bottom)
        {
            int width = Mathf.Max(top.width, bottom.width);
            int height = top.height + 6 + bottom.height;

            var merged = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];

            // Texture rows run bottom-up, so the first line goes in the upper half.
            Blit(pixels, width, top, 0, top.height);
            Blit(pixels, width, bottom, 0, 0);

            merged.SetPixels32(new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Color32>(pixels));
            merged.Apply();
            return merged;
        }

        private static void Blit(Color32[] destination, int destinationWidth, Texture2D source, int x, int y)
        {
            var sourcePixels = source.GetPixels32();
            for (int row = 0; row < source.height; row++)
            {
                for (int col = 0; col < source.width; col++)
                {
                    int dx = x + col;
                    int dy = y + row;
                    if (dx < 0 || dy < 0 || dx >= destinationWidth || dy >= destination.Length / destinationWidth)
                        continue;

                    destination[(dy * destinationWidth) + dx] = sourcePixels[(row * source.width) + col];
                }
            }
        }

        private static bool Clicked(bool cursorLocked)
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
                return false;

            Vector2 pointer;
            if (cursorLocked)
            {
                pointer = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }
            else
            {
                pointer = mouse.position.ReadValue();
            }

            return HitRect.Contains(pointer);
        }

        internal static void Destroy()
        {
            if (_canvasObject != null)
                UnityEngine.Object.Destroy(_canvasObject);

            _canvasObject = null;
            _panelImage = null;
            _label = null;
            _labelTexture = null;
            _built = false;
            _shownState = false;
            _failed = false;
        }
    }
}