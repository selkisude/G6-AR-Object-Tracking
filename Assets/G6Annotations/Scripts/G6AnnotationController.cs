using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Vuforia;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace G6.Annotations
{
    /// <summary>
    /// Shows the annotations while the parent Vuforia target is tracked, and opens the info panel
    /// when an annotation (label or anchor marker) is clicked with the mouse or tapped on a touchscreen.
    /// Lives on the AR_Annotations object under the ModelTarget.
    /// </summary>
    [DisallowMultipleComponent]
    public class G6AnnotationController : MonoBehaviour
    {
        public enum VisibilityFilter
        {
            Tracked,
            Tracked_ExtendedTracked,
            Tracked_ExtendedTracked_Limited
        }

        [Header("Tracking")]
        [Tooltip("Vuforia target whose status drives visibility. Auto-found in parents if empty.")]
        public ObserverBehaviour observer;
        [Tooltip("Keep in sync with the DefaultObserverEventHandler's Status Filter on the ModelTarget.")]
        public VisibilityFilter visibleWhen = VisibilityFilter.Tracked_ExtendedTracked;

        [Header("Interaction")]
        [Tooltip("Camera used for click/tap raycasts. Falls back to Camera.main.")]
        public Camera arCamera;
        public G6AnnotationInfoPanel infoPanel;
        public LayerMask pickMask = ~0;
        public float maxPickDistance = 100f;
        [Tooltip("Clicking the selected annotation again closes the panel.")]
        public bool toggleOnSecondClick = true;

        [Header("Annotations (auto-filled from children if empty)")]
        public List<G6AnnotationPoint> points = new List<G6AnnotationPoint>();

        G6AnnotationPoint _selected;
        bool _visible = true;
        readonly List<RaycastResult> _uiHits = new List<RaycastResult>();

        public G6AnnotationPoint Selected => _selected;

        void Awake()
        {
            if (observer == null) observer = GetComponentInParent<ObserverBehaviour>();
            if (points.Count == 0) GetComponentsInChildren(true, points);
        }

        void OnEnable()
        {
            if (infoPanel != null) infoPanel.Closed += OnPanelClosed;
        }

        void OnDisable()
        {
            if (infoPanel != null) infoPanel.Closed -= OnPanelClosed;
        }

        void Start()
        {
            if (observer != null)
            {
                observer.OnTargetStatusChanged += OnTargetStatusChanged;
                OnTargetStatusChanged(observer, observer.TargetStatus);
            }
            else
            {
                Debug.LogWarning($"[G6Annotations] No Vuforia ObserverBehaviour found above '{name}'; annotations stay visible.", this);
                SetVisible(true);
            }
        }

        void OnDestroy()
        {
            if (observer != null) observer.OnTargetStatusChanged -= OnTargetStatusChanged;
        }

        void OnTargetStatusChanged(ObserverBehaviour behaviour, TargetStatus status)
        {
            SetVisible(PassesFilter(status.Status));
        }

        bool PassesFilter(Status status)
        {
            switch (visibleWhen)
            {
                case VisibilityFilter.Tracked:
                    return status == Status.TRACKED;
                case VisibilityFilter.Tracked_ExtendedTracked:
                    return status == Status.TRACKED || status == Status.EXTENDED_TRACKED;
                default:
                    return status == Status.TRACKED || status == Status.EXTENDED_TRACKED || status == Status.LIMITED;
            }
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            foreach (var p in points)
                if (p != null) p.gameObject.SetActive(visible);

            if (!visible)
            {
                Select(null);
                if (infoPanel != null) infoPanel.Hide();
            }
        }

        void Update()
        {
            if (!_visible) return;
            if (!TryGetPointerDown(out Vector2 screenPos)) return;
            if (IsOverUI(screenPos)) return;

            var cam = arCamera != null ? arCamera : Camera.main;
            if (cam == null) return;

            var hit = Pick(cam.ScreenPointToRay(screenPos));
            if (hit == null) return;

            if (hit == _selected && toggleOnSecondClick)
            {
                Select(null);
                if (infoPanel != null) infoPanel.Hide();
                return;
            }

            Select(hit);
            if (infoPanel != null) infoPanel.Show(hit);
        }

        G6AnnotationPoint Pick(Ray ray)
        {
            var hits = Physics.RaycastAll(ray, maxPickDistance, pickMask, QueryTriggerInteraction.Collide);
            G6AnnotationPoint best = null;
            float bestDist = float.MaxValue;
            foreach (var h in hits)
            {
                var p = h.collider.GetComponentInParent<G6AnnotationPoint>();
                if (p == null || !points.Contains(p) || h.distance >= bestDist) continue;
                best = p;
                bestDist = h.distance;
            }
            return best;
        }

        public void Select(G6AnnotationPoint point)
        {
            if (_selected != null) _selected.SetSelected(false);
            _selected = point;
            if (_selected != null) _selected.SetSelected(true);
        }

        void OnPanelClosed() => Select(null);

        bool IsOverUI(Vector2 screenPos)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            var data = new PointerEventData(es) { position = screenPos };
            _uiHits.Clear();
            es.RaycastAll(data, _uiHits);
            return _uiHits.Count > 0;
        }

        static bool TryGetPointerDown(out Vector2 position)
        {
#if ENABLE_INPUT_SYSTEM
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
            {
                position = touch.primaryTouch.position.ReadValue();
                return true;
            }
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                position = mouse.position.ReadValue();
                return true;
            }
#else
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            {
                position = Input.GetTouch(0).position;
                return true;
            }
            if (Input.GetMouseButtonDown(0))
            {
                position = Input.mousePosition;
                return true;
            }
#endif
            position = default;
            return false;
        }
    }
}
