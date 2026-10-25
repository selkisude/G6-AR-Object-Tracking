using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace G6.Annotations
{
    /// <summary>
    /// Screen-space information panel shown when an annotation is clicked or tapped.
    /// </summary>
    [DisallowMultipleComponent]
    public class G6AnnotationInfoPanel : MonoBehaviour
    {
        [Tooltip("Root object of the panel that is shown/hidden.")]
        public GameObject panelRoot;
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI bodyText;
        [Tooltip("Coloured strip tinted with the selected annotation's colour.")]
        public Image accent;
        public Button closeButton;

        public event Action Closed;

        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        void OnDestroy()
        {
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        }

        public void Show(G6AnnotationPoint point)
        {
            if (point == null || panelRoot == null) return;
            if (titleText != null) titleText.text = point.title;
            if (bodyText != null) bodyText.text = point.description;
            if (accent != null) accent.color = point.color;
            panelRoot.SetActive(true);
        }

        /// <summary>Hides the panel without raising <see cref="Closed"/> (used when tracking is lost).</summary>
        public void Hide()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        public void Close()
        {
            Hide();
            Closed?.Invoke();
        }
    }
}
