using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.UI;

[InitializeOnLoad]
public static class ComfortMenuSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string ControllerName = "Comfort Menu Controller";
    private const string SessionKey = "Demo4.ComfortMenuSceneBuilder.v4";
    private const string RayName = "Comfort Menu UI Ray";

    static ComfortMenuSceneBuilder()
    {
        EditorApplication.delayCall += BuildOnce;
    }

    [MenuItem("Tools/Demo 4/Rebuild Comfort Menu")]
    public static void RebuildFromMenu()
    {
        Build(true);
    }

    private static void BuildOnce()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(SessionKey, false))
            return;

        SessionState.SetBool(SessionKey, true);
        Build(false);
    }

    private static void Build(bool forceRebuild)
    {
        Scene originalActiveScene = SceneManager.GetActiveScene();
        Scene targetScene = SceneManager.GetSceneByPath(ScenePath);
        bool openedAdditively = !targetScene.IsValid() || !targetScene.isLoaded;

        if (openedAdditively)
            targetScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        SceneManager.SetActiveScene(targetScene);

        GameObject existing = FindRoot(targetScene, ControllerName);
        bool removedDuplicate = RemoveDuplicateMenus(targetScene, existing);
        XRMovement movement = FindInScene<XRMovement>(targetScene);
        Camera camera = FindMainCamera(targetScene);
        if (existing != null && !forceRebuild)
        {
            WorldComfortMenu existingMenu = existing.GetComponent<WorldComfortMenu>();
            XRRayInteractor existingRay = EnsureMenuRay(movement);
            if (existingMenu != null && existingRay != null)
                SetSerializedReference(existingMenu, "menuRayInteractor", existingRay);

            SerializedObject serializedMenu = existingMenu != null ? new SerializedObject(existingMenu) : null;
            bool needsHeightSlider = serializedMenu == null ||
                serializedMenu.FindProperty("seatedHeightSlider")?.objectReferenceValue == null;
            if (!needsHeightSlider)
            {
                if (removedDuplicate || existingRay != null)
                {
                    EditorSceneManager.MarkSceneDirty(targetScene);
                    EditorSceneManager.SaveScene(targetScene);
                }
                RestoreActiveScene(originalActiveScene, targetScene, openedAdditively);
                return;
            }
        }
        if (existing != null)
            Object.DestroyImmediate(existing);

        if (movement == null || camera == null)
        {
            Debug.LogError("Comfort menu setup could not find XRMovement and the Main Camera in SampleScene.");
            RestoreActiveScene(originalActiveScene, targetScene, openedAdditively);
            return;
        }

        EnsureEventSystem(targetScene);

        GameObject controller = new GameObject(ControllerName);
        SceneManager.MoveGameObjectToScene(controller, targetScene);
        WorldComfortMenu menu = controller.AddComponent<WorldComfortMenu>();

        GameObject canvasObject = new GameObject("Comfort Menu Canvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(TrackedDeviceGraphicRaycaster));
        canvasObject.transform.SetParent(controller.transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = camera;
        canvas.sortingOrder = 50;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(800f, 1080f);
        canvasRect.localScale = Vector3.one * 0.0015f;

        TrackedDeviceGraphicRaycaster raycaster = canvasObject.GetComponent<TrackedDeviceGraphicRaycaster>();
        raycaster.ignoreReversedGraphics = false;

        GameObject panel = CreateUIObject("Panel", canvasRect);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        Stretch(panelRect);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.035f, 0.055f, 0.085f, 0.97f);

        CreateText("Title", panelRect, "COMFORT SETTINGS", 42f, FontStyles.Bold,
            new Vector2(0f, 475f), new Vector2(700f, 65f));

        CreateText("Movement Heading", panelRect, "LOCOMOTION", 25f, FontStyles.Bold,
            new Vector2(-260f, 400f), new Vector2(250f, 45f), TextAlignmentOptions.Left);
        Toggle teleport = CreateToggle("Teleport Toggle", panelRect, "Teleport", new Vector2(-190f, 343f));
        Toggle smoothMove = CreateToggle("Smooth Movement Toggle", panelRect, "Smooth", new Vector2(190f, 343f));

        CreateText("Turning Heading", panelRect, "TURNING", 25f, FontStyles.Bold,
            new Vector2(-260f, 265f), new Vector2(250f, 45f), TextAlignmentOptions.Left);
        Toggle snap = CreateToggle("Snap Turn Toggle", panelRect, "Snap", new Vector2(-190f, 208f));
        Toggle smoothTurn = CreateToggle("Smooth Turn Toggle", panelRect, "Smooth", new Vector2(190f, 208f));

        TMP_Text speedLabel = CreateText("Movement Speed Label", panelRect, "Movement Speed", 24f,
            FontStyles.Normal, new Vector2(0f, 135f), new Vector2(650f, 45f));
        Slider speed = CreateSlider("Movement Speed Slider", panelRect, new Vector2(0f, 85f));

        TMP_Text vignetteLabel = CreateText("Vignette Label", panelRect, "Vignette", 24f,
            FontStyles.Normal, new Vector2(0f, 5f), new Vector2(650f, 45f));
        Slider vignette = CreateSlider("Vignette Slider", panelRect, new Vector2(0f, -45f));

        CreateText("Height Heading", panelRect, "PLAY HEIGHT", 25f, FontStyles.Bold,
            new Vector2(-260f, -120f), new Vector2(250f, 45f), TextAlignmentOptions.Left);
        Toggle seated = CreateToggle("Seated Toggle", panelRect, "Seated", new Vector2(-190f, -177f));
        Toggle standing = CreateToggle("Standing Toggle", panelRect, "Standing", new Vector2(190f, -177f));

        TMP_Text seatedHeightLabel = CreateText("Seated Height Label", panelRect, "Seated Height: 1.65 m", 24f,
            FontStyles.Normal, new Vector2(0f, -250f), new Vector2(650f, 45f));
        Slider seatedHeight = CreateSlider("Seated Height Slider", panelRect, new Vector2(0f, -300f));
        seatedHeight.minValue = 1.2f;
        seatedHeight.maxValue = 2.1f;
        seatedHeight.value = 1.65f;

        Button reset = CreateButton("Reset View Button", panelRect, "RESET VIEW", new Vector2(0f, -395f),
            new Vector2(610f, 62f), new Color(0.10f, 0.42f, 0.62f, 1f));
        Button close = CreateButton("Close Button", panelRect, "CLOSE", new Vector2(0f, -470f),
            new Vector2(610f, 56f), new Color(0.22f, 0.27f, 0.34f, 1f));

        SetSerializedReference(menu, "movement", movement);
        SetSerializedReference(menu, "head", camera.transform);
        SetSerializedReference(menu, "menuRoot", canvasObject);
        SetSerializedReference(menu, "menuRayInteractor", EnsureMenuRay(movement));
        SetSerializedReference(menu, "teleportToggle", teleport);
        SetSerializedReference(menu, "smoothMovementToggle", smoothMove);
        SetSerializedReference(menu, "snapTurnToggle", snap);
        SetSerializedReference(menu, "smoothTurnToggle", smoothTurn);
        SetSerializedReference(menu, "movementSpeedSlider", speed);
        SetSerializedReference(menu, "movementSpeedLabel", speedLabel);
        SetSerializedReference(menu, "vignetteSlider", vignette);
        SetSerializedReference(menu, "vignetteLabel", vignetteLabel);
        SetSerializedReference(menu, "seatedToggle", seated);
        SetSerializedReference(menu, "standingToggle", standing);
        SetSerializedReference(menu, "seatedHeightSlider", seatedHeight);
        SetSerializedReference(menu, "seatedHeightLabel", seatedHeightLabel);
        SetSerializedReference(menu, "resetViewButton", reset);
        SetSerializedReference(menu, "closeButton", close);

        EditorSceneManager.MarkSceneDirty(targetScene);
        EditorSceneManager.SaveScene(targetScene);
        Debug.Log("Built and wired the Demo 4 comfort menu in SampleScene.", controller);

        RestoreActiveScene(originalActiveScene, targetScene, openedAdditively);
    }

    private static void EnsureEventSystem(Scene scene)
    {
        EventSystem eventSystem = FindInScene<EventSystem>(scene);
        if (eventSystem == null)
        {
            GameObject eventObject = new GameObject("XR UI EventSystem", typeof(EventSystem), typeof(XRUIInputModule));
            SceneManager.MoveGameObjectToScene(eventObject, scene);
            return;
        }

        if (eventSystem.GetComponent<XRUIInputModule>() == null)
        {
            BaseInputModule oldModule = eventSystem.GetComponent<BaseInputModule>();
            if (oldModule != null)
                Object.DestroyImmediate(oldModule);
            eventSystem.gameObject.AddComponent<XRUIInputModule>();
        }
    }

    private static XRRayInteractor EnsureMenuRay(XRMovement movement)
    {
        if (movement == null || movement.rightController == null)
            return null;

        Transform controller = movement.rightController.transform;
        Transform existing = controller.Find(RayName);
        if (existing != null)
        {
            XRRayInteractor existingRay = existing.GetComponent<XRRayInteractor>();
            if (existingRay != null)
                return existingRay;
            Object.DestroyImmediate(existing.gameObject);
        }

        GameObject rayObject = new GameObject(RayName, typeof(LineRenderer), typeof(XRRayInteractor),
            typeof(XRInteractorLineVisual));
        rayObject.transform.SetParent(controller, false);

        LineRenderer line = rayObject.GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.startWidth = 0.004f;
        line.endWidth = 0.002f;
        line.startColor = new Color(0.20f, 0.78f, 1f, 0.9f);
        line.endColor = new Color(0.20f, 0.78f, 1f, 0.35f);
        line.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Line.mat");

        XRRayInteractor ray = rayObject.GetComponent<XRRayInteractor>();
        ray.enableUIInteraction = true;
        ray.maxRaycastDistance = 5f;
        ray.selectInput.inputSourceMode = XRInputButtonReader.InputSourceMode.Unused;
        ray.activateInput.inputSourceMode = XRInputButtonReader.InputSourceMode.Unused;

        XRInputButtonReader uiPress = new XRInputButtonReader(
            "Menu UI Press", "Menu UI Press Value", false, XRInputButtonReader.InputSourceMode.InputAction);
        uiPress.inputActionPerformed.AddBinding("<XRController>{RightHand}/triggerPressed");
        uiPress.inputActionValue.AddBinding("<XRController>{RightHand}/trigger");
        ray.uiPressInput = uiPress;
        return ray;
    }

    private static Toggle CreateToggle(string name, Transform parent, string label, Vector2 position)
    {
        GameObject root = CreateUIObject(name, parent);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(310f, 60f);
        rect.anchoredPosition = position;

        Toggle toggle = root.AddComponent<Toggle>();
        GameObject background = CreateUIObject("Background", rect);
        RectTransform bgRect = background.GetComponent<RectTransform>();
        bgRect.anchorMin = bgRect.anchorMax = new Vector2(0f, 0.5f);
        bgRect.pivot = new Vector2(0f, 0.5f);
        bgRect.anchoredPosition = Vector2.zero;
        bgRect.sizeDelta = new Vector2(48f, 48f);
        Image backgroundImage = background.AddComponent<Image>();
        backgroundImage.color = new Color(0.13f, 0.17f, 0.23f, 1f);

        GameObject checkmark = CreateUIObject("Checkmark", bgRect);
        RectTransform checkRect = checkmark.GetComponent<RectTransform>();
        checkRect.anchorMin = new Vector2(0.18f, 0.18f);
        checkRect.anchorMax = new Vector2(0.82f, 0.82f);
        checkRect.offsetMin = checkRect.offsetMax = Vector2.zero;
        Image checkImage = checkmark.AddComponent<Image>();
        checkImage.color = new Color(0.20f, 0.78f, 1f, 1f);

        CreateText("Label", rect, label, 25f, FontStyles.Normal,
            new Vector2(83f, 0f), new Vector2(210f, 52f), TextAlignmentOptions.Left);
        toggle.targetGraphic = backgroundImage;
        toggle.graphic = checkImage;
        return toggle;
    }

    private static Slider CreateSlider(string name, Transform parent, Vector2 position)
    {
        GameObject root = CreateUIObject(name, parent);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(620f, 52f);
        rect.anchoredPosition = position;
        Slider slider = root.AddComponent<Slider>();

        GameObject background = CreateUIObject("Background", rect);
        RectTransform bg = background.GetComponent<RectTransform>();
        bg.anchorMin = new Vector2(0f, 0.35f);
        bg.anchorMax = new Vector2(1f, 0.65f);
        bg.offsetMin = bg.offsetMax = Vector2.zero;
        Image bgImage = background.AddComponent<Image>();
        bgImage.color = new Color(0.13f, 0.17f, 0.23f, 1f);

        GameObject fillArea = CreateUIObject("Fill Area", rect);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        Stretch(fillAreaRect, 12f, 12f, 15f, 15f);
        GameObject fill = CreateUIObject("Fill", fillAreaRect);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        Stretch(fillRect);
        Image fillImage = fill.AddComponent<Image>();
        fillImage.color = new Color(0.20f, 0.65f, 0.94f, 1f);

        GameObject handleArea = CreateUIObject("Handle Slide Area", rect);
        RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
        Stretch(handleAreaRect, 20f, 20f, 0f, 0f);
        GameObject handle = CreateUIObject("Handle", handleAreaRect);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(38f, 48f);
        Image handleImage = handle.AddComponent<Image>();
        handleImage.color = Color.white;

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    private static Button CreateButton(string name, Transform parent, string label, Vector2 position,
        Vector2 size, Color color)
    {
        GameObject root = CreateUIObject(name, parent);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        Image image = root.AddComponent<Image>();
        image.color = color;
        Button button = root.AddComponent<Button>();
        button.targetGraphic = image;
        RectTransform textRect = CreateText("Label", rect, label, 25f, FontStyles.Bold,
            Vector2.zero, size).rectTransform;
        Stretch(textRect);
        return button;
    }

    private static TMP_Text CreateText(string name, Transform parent, string value, float size,
        FontStyles style, Vector2 position, Vector2 dimensions,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        GameObject root = CreateUIObject(name, parent);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = dimensions;
        rect.anchoredPosition = position;
        TextMeshProUGUI text = root.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = new Color(0.93f, 0.96f, 1f, 1f);
        text.raycastTarget = false;
        return text;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static void Stretch(RectTransform rect, float left = 0f, float right = 0f,
        float bottom = 0f, float top = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void SetSerializedReference(Object target, string propertyName, Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(propertyName).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T result = root.GetComponentInChildren<T>(true);
            if (result != null)
                return result;
        }
        return null;
    }

    private static Camera FindMainCamera(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                if (camera.CompareTag("MainCamera"))
                    return camera;
        }
        return FindInScene<Camera>(scene);
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == name)
                return root;
        return null;
    }

    private static bool RemoveDuplicateMenus(Scene scene, GameObject keep)
    {
        bool removed = false;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            WorldComfortMenu[] menus = root.GetComponentsInChildren<WorldComfortMenu>(true);
            foreach (WorldComfortMenu menu in menus)
            {
                if (menu.gameObject == keep)
                    continue;
                Object.DestroyImmediate(menu.gameObject);
                removed = true;
            }
        }
        return removed;
    }

    private static void RestoreActiveScene(Scene original, Scene target, bool closeTarget)
    {
        if (original.IsValid() && original.isLoaded)
            SceneManager.SetActiveScene(original);
        if (closeTarget)
            EditorSceneManager.CloseScene(target, true);
    }
}
