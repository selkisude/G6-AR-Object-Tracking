using TMPro;
using UnityEngine;

namespace G6.Annotations
{
    /// <summary>
    /// World-space TextMeshPro label with a background plate that keeps itself sized to the text
    /// and, in Play mode, turns to face the AR camera.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class G6AnnotationLabel : MonoBehaviour
    {
        public TextMeshPro text;
        public Renderer background;
        [Tooltip("Thin coloured strip under the text, tinted with the annotation colour.")]
        public Renderer accentBar;
        [Tooltip("Collider used for click/tap picking; resized with the background.")]
        public BoxCollider pickCollider;

        [Header("Billboard")]
        [Tooltip("Camera to face. Falls back to Camera.main (the ARCamera is tagged MainCamera).")]
        public Camera targetCamera;
        public bool faceCamera = true;

        [Header("Look")]
        public Vector2 padding = new Vector2(0.25f, 0.12f);
        public Color backgroundColor = new Color(0.08f, 0.09f, 0.11f, 1f);
        public Color highlightBackgroundColor = new Color(0.18f, 0.2f, 0.26f, 1f);
        public float accentHeight = 0.06f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        MaterialPropertyBlock _mpb;
        bool _highlighted;
        string _sizedFor;

        void OnEnable()
        {
            _sizedFor = null;
            ApplyBackgroundColor();
        }

        void LateUpdate()
        {
            if (text != null && _sizedFor != text.text) FitToText();

            if (!Application.isPlaying || !faceCamera) return;
            var cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null) return;
            // Parallel billboard: matches the camera's orientation so text never skews or mirrors.
            transform.rotation = cam.transform.rotation;
        }

        public void SetText(string value)
        {
            if (text == null) return;
            text.text = value;
            FitToText();
        }

        public void SetHighlighted(bool on)
        {
            _highlighted = on;
            ApplyBackgroundColor();
        }

        public void SetAccentColor(Color color)
        {
            if (accentBar == null) return;
            _mpb ??= new MaterialPropertyBlock();
            _mpb.SetColor(BaseColorId, color);
            accentBar.SetPropertyBlock(_mpb);
        }

        void ApplyBackgroundColor()
        {
            if (background == null) return;
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, _highlighted ? highlightBackgroundColor : backgroundColor);
            background.SetPropertyBlock(block);
        }

        void FitToText()
        {
            if (text == null) return;
            text.ForceMeshUpdate();
            Vector2 size = text.GetRenderedValues(false);
            if (float.IsNaN(size.x) || size.x <= 0f) return;
            _sizedFor = text.text;

            float w = size.x + padding.x * 2f;
            float h = size.y + padding.y * 2f;
            Vector3 center = text.transform.localPosition;

            if (background != null)
            {
                var bt = background.transform;
                bt.localPosition = new Vector3(center.x, center.y, 0.02f); // behind the text (text faces -Z)
                bt.localScale = new Vector3(w, h, 1f);
            }

            if (accentBar != null)
            {
                var at = accentBar.transform;
                at.localPosition = new Vector3(center.x, center.y - h * 0.5f + accentHeight * 0.5f, 0.01f);
                at.localScale = new Vector3(w, accentHeight, 1f);
            }

            if (pickCollider != null)
            {
                pickCollider.center = new Vector3(center.x, center.y, 0.02f);
                pickCollider.size = new Vector3(w, h, 0.05f);
            }
        }
    }
}
