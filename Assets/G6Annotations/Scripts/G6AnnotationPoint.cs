using UnityEngine;

namespace G6.Annotations
{
    /// <summary>
    /// One anatomical annotation. This GameObject's local position is the anchor on the model;
    /// the child label's local position is the label offset. Move either in the Inspector/Scene view to fine-tune.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class G6AnnotationPoint : MonoBehaviour
    {
        [Header("Content")]
        public string title = "Region";
        [TextArea(3, 8)] public string description = "";
        public Color color = new Color(1f, 0.75f, 0.2f);

        [Header("Parts")]
        public Transform anchorMarker;
        public G6AnnotationLabel label;
        public LineRenderer leaderLine;

        [Header("Leader line")]
        [Tooltip("Line width in the ModelTarget's local units (scaled with the target).")]
        public float lineWidth = 0.0012f;
        [Tooltip("Stop the line this far short of the label centre, in label-local units.")]
        public float labelGap = 0.0f;

        [Header("Selection")]
        [Tooltip("Anchor marker scale multiplier while this annotation is selected.")]
        public float selectedMarkerScale = 1.6f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        MaterialPropertyBlock _mpb;
        bool _selected;
        Vector3 _markerBaseScale;
        string _lastTitle;
        Color _lastColor;
        Vector3 _lastLabelPos, _lastAnchorPos;
        float _lastWidth = -1f;

        public bool IsSelected => _selected;

        void OnEnable()
        {
            if (anchorMarker != null) _markerBaseScale = anchorMarker.localScale;
            _lastTitle = null;
            _lastWidth = -1f;
            Refresh(true);
        }

        void LateUpdate() => Refresh(false);

        void OnValidate()
        {
            // Colours are safe to push here; text is pushed from LateUpdate (TMP dislikes OnValidate).
            _lastTitle = null;
            ApplyColors();
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;
            if (anchorMarker != null)
                anchorMarker.localScale = _markerBaseScale * (selected ? selectedMarkerScale : 1f);
            if (label != null) label.SetHighlighted(selected);
        }

        void Refresh(bool force)
        {
            if (label != null && (force || _lastTitle != title))
            {
                label.SetText(title);
                _lastTitle = title;
            }

            if (force || _lastColor != color)
            {
                ApplyColors();
                _lastColor = color;
            }

            UpdateLine(force);
        }

        void UpdateLine(bool force)
        {
            if (leaderLine == null || label == null) return;

            Vector3 anchorLocal = anchorMarker != null ? anchorMarker.localPosition : Vector3.zero;
            Vector3 labelLocal = transform.InverseTransformPoint(label.transform.position);
            float width = lineWidth * transform.lossyScale.x;

            if (!force && anchorLocal == _lastAnchorPos && labelLocal == _lastLabelPos && Mathf.Approximately(width, _lastWidth))
                return;

            Vector3 end = labelLocal;
            if (labelGap > 0f)
            {
                Vector3 dir = labelLocal - anchorLocal;
                end = labelLocal - dir.normalized * Mathf.Min(labelGap, dir.magnitude);
            }

            leaderLine.useWorldSpace = false;
            leaderLine.positionCount = 2;
            leaderLine.SetPosition(0, anchorLocal);
            leaderLine.SetPosition(1, end);
            leaderLine.widthMultiplier = width;

            _lastAnchorPos = anchorLocal;
            _lastLabelPos = labelLocal;
            _lastWidth = width;
        }

        void ApplyColors()
        {
            _mpb ??= new MaterialPropertyBlock();
            _mpb.SetColor(BaseColorId, color);
            if (anchorMarker != null && anchorMarker.TryGetComponent(out Renderer markerRenderer))
                markerRenderer.SetPropertyBlock(_mpb);
            if (leaderLine != null)
                leaderLine.SetPropertyBlock(_mpb);
            if (label != null)
                label.SetAccentColor(color);
        }
    }
}
