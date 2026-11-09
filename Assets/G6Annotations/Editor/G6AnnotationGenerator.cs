using G6.Annotations;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Vuforia;
using Image = UnityEngine.UI.Image;

namespace G6.Annotations.EditorTools
{
    /// <summary>
    /// Builds the AR_Annotations hierarchy under the existing Vuforia ModelTarget plus the info-panel UI.
    /// Additive only: never modifies the ModelTarget's own components, the ARCamera or Vuforia configuration,
    /// and does nothing if AR_Annotations already exists.
    /// </summary>
    public static class G6AnnotationGenerator
    {
        const string RootName = "AR_Annotations";
        const string UiName = "AR_AnnotationUI";
        const string MaterialFolder = "Assets/G6Annotations/Materials";
        const string MaterialPath = MaterialFolder + "/G6_AnnotationUnlit.mat";

        // Sizes in ModelTarget-local units (the target is ~0.16 tall in this space).
        const float LabelScale = 0.045f;
        const float MarkerDiameter = 0.0025f;
        const float LineWidth = 0.0004f;
        static readonly Vector2 LabelPadding = new Vector2(0.08f, 0.04f);
        const float AccentHeight = 0.035f;

        struct Region
        {
            public string Name;
            public Vector3 Anchor;   // ModelTarget-local
            public Vector3 Label;    // ModelTarget-local
            public Color Color;
            public string Description;
        }

        // Anchor points sampled from the Dinosaur_3D_Scan mesh (ModelTarget-local space:
        // +X = snout/belly, -X = back/spikes, Y = up, ±Z = sides). Fine-tune in the Scene view afterwards.
        static readonly Region[] Regions =
        {
            new Region
            {
                Name = "Head", Anchor = new Vector3(-0.009f, 0.155f, -0.020f), Label = new Vector3(-0.005f, 0.180f, -0.020f),
                Color = new Color(0.98f, 0.62f, 0.20f),
                Description = "The skull houses the brain and the jaws. In theropod dinosaurs the skull was lightened by large openings (fenestrae), saving weight while leaving room for powerful jaw muscles."
            },
            new Region
            {
                Name = "Eye", Anchor = new Vector3(-0.002f, 0.112f, -0.052f), Label = new Vector3(0.012f, 0.128f, -0.080f),
                Color = new Color(0.30f, 0.80f, 0.95f),
                Description = "Many dinosaurs had large eyes supported by a ring of bones called the sclerotic ring. Eye size and position hint at whether an animal was active by day or night and how well it judged distance."
            },
            new Region
            {
                Name = "Back Spikes", Anchor = new Vector3(-0.044f, 0.113f, -0.021f), Label = new Vector3(-0.080f, 0.128f, -0.025f),
                Color = new Color(0.62f, 0.45f, 0.95f),
                Description = "A row of bony spikes runs along the back. Structures like these may have been used for display, recognising members of the same species, defence, or helping to regulate body temperature."
            },
            new Region
            {
                Name = "Body", Anchor = new Vector3(0.036f, 0.050f, 0.014f), Label = new Vector3(0.075f, 0.062f, 0.022f),
                Color = new Color(0.40f, 0.85f, 0.45f),
                Description = "The torso held the heart, lungs and digestive organs. Many dinosaurs had a bird-like breathing system with air sacs, which supplied oxygen efficiently for an active lifestyle."
            },
            new Region
            {
                Name = "Legs", Anchor = new Vector3(0.003f, 0.024f, 0.052f), Label = new Vector3(0.020f, 0.012f, 0.082f),
                Color = new Color(0.95f, 0.40f, 0.45f),
                Description = "Dinosaurs held their legs directly beneath the body instead of sprawling them to the side. This upright posture made walking and running far more efficient."
            },
            new Region
            {
                Name = "Tail", Anchor = new Vector3(-0.055f, 0.021f, 0.000f), Label = new Vector3(-0.085f, 0.010f, 0.022f),
                Color = new Color(0.95f, 0.85f, 0.30f),
                Description = "The muscular tail counterbalanced the head and body over the hips. In many species it also anchored the large caudofemoralis muscle that pulled the legs backwards when walking."
            },
        };

        [MenuItem("G6/Build AR Annotations")]
        public static void BuildMenu() => Debug.Log("[G6Annotations] " + Build());

        public static string Build()
        {
            var observer = FindModelTarget();
            if (observer == null) return "No ModelTargetBehaviour found in the open scene. Nothing created.";

            var mt = observer.transform;
            if (mt.Find(RootName) != null) return $"'{RootName}' already exists under '{mt.name}'. Nothing changed.";

            var cam = FindArCamera();
            var material = GetOrCreateMaterial();
            var font = TMP_Settings.defaultFontAsset;

            Undo.SetCurrentGroupName("Build AR Annotations");
            int undoGroup = Undo.GetCurrentGroup();

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create " + RootName);
            root.transform.SetParent(mt, false);

            var controller = root.AddComponent<G6AnnotationController>();
            controller.observer = observer;
            controller.arCamera = cam;
            controller.visibleWhen = ReadObserverFilter(observer);

            foreach (var region in Regions)
                controller.points.Add(CreatePoint(root.transform, region, material, font, cam));

            controller.infoPanel = CreateUi(font);
            EnsureEventSystem();

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(mt.gameObject.scene);
            Selection.activeGameObject = root;
            return $"Created '{RootName}' with {Regions.Length} annotations under '{mt.name}', plus '{UiName}'. Scene is NOT saved.";
        }

        static ObserverBehaviour FindModelTarget()
        {
            foreach (var mtb in Object.FindObjectsByType<ModelTargetBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                return mtb;
            return null;
        }

        static Camera FindArCamera()
        {
            var vb = Object.FindFirstObjectByType<VuforiaBehaviour>(FindObjectsInactive.Include);
            if (vb != null && vb.TryGetComponent(out Camera c)) return c;
            return Camera.main;
        }

        static G6AnnotationController.VisibilityFilter ReadObserverFilter(ObserverBehaviour observer)
        {
            if (!observer.TryGetComponent(out DefaultObserverEventHandler handler))
                return G6AnnotationController.VisibilityFilter.Tracked_ExtendedTracked;
            switch (handler.StatusFilter)
            {
                case DefaultObserverEventHandler.TrackingStatusFilter.Tracked:
                    return G6AnnotationController.VisibilityFilter.Tracked;
                case DefaultObserverEventHandler.TrackingStatusFilter.Tracked_ExtendedTracked:
                    return G6AnnotationController.VisibilityFilter.Tracked_ExtendedTracked;
                default:
                    return G6AnnotationController.VisibilityFilter.Tracked_ExtendedTracked_Limited;
            }
        }

        static Material GetOrCreateMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat != null) return mat;

            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder("Assets/G6Annotations", "Materials");

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            mat = new Material(shader) { name = "G6_AnnotationUnlit", enableInstancing = true };
            mat.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(mat, MaterialPath);
            return mat;
        }

        static G6AnnotationPoint CreatePoint(Transform parent, Region region, Material material, TMP_FontAsset font, Camera cam)
        {
            var go = new GameObject("Annotation_" + region.Name.Replace(" ", ""));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = region.Anchor;

            // Anchor marker (sphere, clickable)
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "Anchor";
            marker.transform.SetParent(go.transform, false);
            marker.transform.localScale = Vector3.one * MarkerDiameter;
            ConfigureRenderer(marker.GetComponent<MeshRenderer>(), material);

            // Leader line
            var lineGo = new GameObject("LeaderLine");
            lineGo.transform.SetParent(go.transform, false);
            var line = lineGo.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.numCapVertices = 2;
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;

            // Label
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            labelGo.transform.localPosition = region.Label - region.Anchor;
            labelGo.transform.localScale = Vector3.one * LabelScale;
            var label = labelGo.AddComponent<G6AnnotationLabel>();
            label.targetCamera = cam;
            label.padding = LabelPadding;
            label.accentHeight = AccentHeight;
            label.pickCollider = labelGo.AddComponent<BoxCollider>();

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(labelGo.transform, false);
            var tmp = textGo.AddComponent<TextMeshPro>();
            if (font != null) tmp.font = font;
            tmp.text = region.Name;
            tmp.fontSize = 1f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.color = Color.white;
            tmp.rectTransform.sizeDelta = new Vector2(4f, 1f);
            label.text = tmp;

            label.background = CreateQuad("Background", labelGo.transform, material);
            label.accentBar = CreateQuad("Accent", labelGo.transform, material);

            var point = go.AddComponent<G6AnnotationPoint>();
            point.title = region.Name;
            point.description = region.Description;
            point.color = region.Color;
            point.anchorMarker = marker.transform;
            point.label = label;
            point.leaderLine = line;
            point.lineWidth = LineWidth;
            return point;
        }

        static Renderer CreateQuad(string name, Transform parent, Material material)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Object.DestroyImmediate(quad.GetComponent<Collider>()); // label's BoxCollider handles picking
            quad.transform.SetParent(parent, false);
            quad.transform.localRotation = Quaternion.identity; // quad faces -Z, same as TMP text
            var r = quad.GetComponent<MeshRenderer>();
            ConfigureRenderer(r, material);
            return r;
        }

        static void ConfigureRenderer(Renderer r, Material material)
        {
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        static G6AnnotationInfoPanel CreateUi(TMP_FontAsset font)
        {
            var existing = GameObject.Find(UiName);
            if (existing != null && existing.TryGetComponent(out G6AnnotationInfoPanel existingPanel)) return existingPanel;

            var ui = new GameObject(UiName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(ui, "Create " + UiName);
            var canvas = ui.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = ui.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            ui.AddComponent<GraphicRaycaster>();
            var panelComp = ui.AddComponent<G6AnnotationInfoPanel>();

            var panel = NewUi("InfoPanel", ui.transform);
            var prt = panel.GetComponent<RectTransform>();
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(1f, 0f);
            prt.anchoredPosition = new Vector2(-40f, 40f);
            prt.sizeDelta = new Vector2(620f, 380f);
            panel.AddComponent<Image>().color = new Color(0.08f, 0.09f, 0.11f, 0.92f);

            var accent = NewUi("Accent", panel.transform);
            var art = accent.GetComponent<RectTransform>();
            art.anchorMin = new Vector2(0f, 1f); art.anchorMax = new Vector2(1f, 1f); art.pivot = new Vector2(0.5f, 1f);
            art.anchoredPosition = Vector2.zero; art.sizeDelta = new Vector2(0f, 10f);
            var accentImg = accent.AddComponent<Image>();

            var title = NewText("Title", panel.transform, font, 42f, FontStyles.Bold);
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(0f, 1f);
            trt.offsetMin = new Vector2(32f, -90f); trt.offsetMax = new Vector2(-96f, -24f);
            title.alignment = TextAlignmentOptions.MidlineLeft;

            var body = NewText("Body", panel.transform, font, 28f, FontStyles.Normal);
            var brt = body.rectTransform;
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(32f, 28f); brt.offsetMax = new Vector2(-32f, -104f);
            body.alignment = TextAlignmentOptions.TopLeft;
            body.textWrappingMode = TextWrappingModes.Normal;
            body.color = new Color(0.85f, 0.87f, 0.9f);

            var close = NewUi("CloseButton", panel.transform);
            var crt = close.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(1f, 1f);
            crt.anchoredPosition = new Vector2(-20f, -26f); crt.sizeDelta = new Vector2(60f, 60f);
            var closeImg = close.AddComponent<Image>();
            closeImg.color = new Color(1f, 1f, 1f, 0.12f);
            var button = close.AddComponent<Button>();
            button.targetGraphic = closeImg;
            var x = NewText("X", close.transform, font, 34f, FontStyles.Bold);
            x.text = "X";
            x.alignment = TextAlignmentOptions.Center;
            x.rectTransform.anchorMin = Vector2.zero; x.rectTransform.anchorMax = Vector2.one;
            x.rectTransform.offsetMin = x.rectTransform.offsetMax = Vector2.zero;

            panelComp.panelRoot = panel;
            panelComp.titleText = title;
            panelComp.bodyText = body;
            panelComp.accent = accentImg;
            panelComp.closeButton = button;
            panel.SetActive(false);
            return panelComp;
        }

        static GameObject NewUi(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        static TextMeshProUGUI NewText(string name, Transform parent, TMP_FontAsset font, float size, FontStyles style)
        {
            var t = NewUi(name, parent).AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = Color.white;
            t.raycastTarget = false;
            return t;
        }

        static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}
