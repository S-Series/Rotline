using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// One-time additive authoring. An existing Run HUD is left untouched.
public static class RunUISceneAuthoring
{
    private static readonly Color Ink = new(0.9f, 0.94f, 1f);
    private static Font font;
    private static Sprite barSprite;

    [MenuItem("ROTLINE/Run/Author Run UI Once")]
    public static void Author()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Gameplay.unity");
        var run = Object.FindFirstObjectByType<SurvivalRunController>();
        if (run == null) throw new System.InvalidOperationException("Survival Run is missing.");
        bool hasHud = run.transform.Find("Run HUD") != null;
        bool hasPages = run.transform.Find("Alpha System Pages") != null;
        if (hasHud && hasPages)
        {
            Debug.Log("[RunUI] Authored UI already exists; existing hierarchy was left unchanged.");
            return;
        }
        if (hasHud || hasPages) throw new System.InvalidOperationException("Run UI is only partly authored; inspect the scene before continuing.");
        font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/NanumGothic-Regular.ttf");
        barSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        if (font == null || barSprite == null) throw new System.InvalidOperationException("Run UI font or bar sprite is missing.");

        AuthorHud(run);
        AuthorSystemPages(run);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[RunUI] Saved Run HUD and Alpha System Pages in Gameplay.unity.");
    }

    private static void AuthorHud(SurvivalRunController run)
    {
        var canvas = NewCanvas("Run HUD", run.transform, 100, 1f);
        var panel = Panel(canvas.transform, "Status", new Vector2(18, -18), new Vector2(420, 162), new Color(0.025f, 0.035f, 0.055f, 0.95f));
        var status = Label(panel, "Status Text", "", new Vector2(14, -8), new Vector2(395, 78), 17);
        var health = Bar(panel, "Health Bar", new Vector2(14, -92), new Color(0.95f, 0.3f, 0.35f));
        var experience = Bar(panel, "Experience Bar", new Vector2(14, -115), new Color(0.2f, 0.8f, 0.95f));
        var resource = Bar(panel, "Resource Bar", new Vector2(14, -138), new Color(0.85f, 0.65f, 0.3f));
        var inventory = Label(canvas.transform, "Recent Rewards", "", new Vector2(18, -190), new Vector2(390, 320), 16);
        var objective = Label(canvas.transform, "Objective", "", new Vector2(470, -18), new Vector2(780, 170), 20);
        var hints = Label(canvas.transform, "Control Hints", "WASD 이동 · 마우스 조준 · 자동 공격 · Shift 회피 · Q/E 모드 · R 능력 · ESC 일시정지",
            new Vector2(18, -680), new Vector2(1100, 28), 17);
        var overlay = Panel(canvas.transform, "Choice Overlay", Vector2.zero, new Vector2(1280, 720), new Color(0.015f, 0.025f, 0.05f, 0.97f));
        Stretch(overlay);
        var heading = Label(overlay, "Heading", "", new Vector2(100, -100), new Vector2(1050, 60), 36);
        var details = Label(overlay, "Details", "", new Vector2(100, -175), new Vector2(1050, 85), 20);
        var choices = new Button[3];
        for (int i = 0; i < choices.Length; i++)
            choices[i] = Button(overlay, "Reward " + i, "", new Vector2(100 + 365 * i, -300), new Vector2(340, 220));
        var resume = Button(overlay, "Resume", "계속하기", new Vector2(100, -560), new Vector2(280, 64));
        var restart = Button(overlay, "Restart", "다시 시작", new Vector2(400, -560), new Vector2(280, 64));
        var mainMenu = Button(overlay, "Main Menu", "메뉴로 돌아가기", new Vector2(700, -560), new Vector2(280, 64));
        var equipment = Button(overlay, "Equipment", "장비 · 능력치", new Vector2(100, -400), new Vector2(280, 64));
        var settings = Button(overlay, "Settings", "설정 · 조작법", new Vector2(400, -400), new Vector2(280, 64));

        var hud = run.GetComponent<SurvivalRunHUD>();
        var data = new SerializedObject(hud);
        Set(data, "canvas", canvas);
        Set(data, "statusPanel", panel.gameObject);
        Set(data, "status", status);
        Set(data, "inventory", inventory);
        Set(data, "objective", objective);
        Set(data, "hints", hints.gameObject);
        Set(data, "healthFill", health);
        Set(data, "experienceFill", experience);
        Set(data, "resourceFill", resource);
        Set(data, "overlay", overlay.gameObject);
        Set(data, "heading", heading);
        Set(data, "details", details);
        SetArray(data, "options", choices);
        Set(data, "resume", resume);
        Set(data, "restart", restart);
        Set(data, "mainMenu", mainMenu);
        Set(data, "equipment", equipment);
        Set(data, "settings", settings);
        data.ApplyModifiedPropertiesWithoutUndo();
        overlay.gameObject.SetActive(false);
        objective.gameObject.SetActive(false);
    }

    private static void AuthorSystemPages(SurvivalRunController run)
    {
        var canvas = NewCanvas("Alpha System Pages", run.transform, 300, 0.8f);
        var backdrop = Panel(canvas.transform, "Backdrop", Vector2.zero, new Vector2(1280, 720), new Color(0.015f, 0.03f, 0.045f, 0.98f));
        Stretch(backdrop);
        var heading = Label(canvas.transform, "Heading", "", new Vector2(76, -68), new Vector2(1100, 70), 42);
        var back = Button(canvas.transform, "Back", "돌아가기", new Vector2(76, -615), new Vector2(300, 64));
        var shop = Group(canvas.transform, "Shop");
        var coins = Label(shop, "Coins", "", new Vector2(76, -160), new Vector2(1100, 45), 22);
        var offers = new[]
        {
            Button(shop, "Heal", "체력 30 회복\n동전 8", new Vector2(76, -265), new Vector2(330, 180)),
            Button(shop, "Power", "숫돌 · 공격력 +2\n동전 12", new Vector2(445, -265), new Vector2(330, 180)),
            Button(shop, "Speed", "가벼운 장갑 · 공격속도 +20%\n동전 12", new Vector2(814, -265), new Vector2(330, 180))
        };
        Label(shop, "Shop Hint", "구입하지 않고 나가도 다음 문이 열립니다.", new Vector2(76, -490), new Vector2(1100, 45), 22);

        var settings = Group(canvas.transform, "Settings");
        var volume = Button(settings, "Volume", "", new Vector2(76, -240), new Vector2(430, 70));
        var volumeLabel = volume.GetComponentInChildren<Text>();
        Label(settings, "Controls", "WASD 이동 · 자동 공격 · 좌클릭 차지 · Shift 회피\nQ/E 전투·정화 · R 능력 · F 상호작용\n붉은 탄막을 피하고, 파란 경험치 조각을 모으세요.\n아이템 효과는 같은 종류도 중첩됩니다.",
            new Vector2(76, -345), new Vector2(1100, 190), 26);

        var inventory = Group(canvas.transform, "Equipment");
        var stats = Label(inventory, "Stats", "", new Vector2(76, -170), new Vector2(1100, 80), 25);
        var itemRows = new Text[5];
        for (int i = 0; i < itemRows.Length; i++)
            itemRows[i] = Label(inventory, "Item Row " + i, "", new Vector2(76, -285 - i * 42), new Vector2(520, 40), 22);
        var attackRows = new Text[4];
        for (int i = 0; i < attackRows.Length; i++)
            attackRows[i] = Label(inventory, "Attack Row " + i, "", new Vector2(660, -285 - i * 42), new Vector2(480, 40), 22);

        var view = canvas.gameObject.AddComponent<AlphaSystemView>();
        var data = new SerializedObject(view);
        Set(data, "canvas", canvas);
        Set(data, "shopPage", shop.gameObject);
        Set(data, "settingsPage", settings.gameObject);
        Set(data, "inventoryPage", inventory.gameObject);
        Set(data, "heading", heading);
        Set(data, "coins", coins);
        SetArray(data, "shopOffers", offers);
        Set(data, "volumeButton", volume);
        Set(data, "volumeLabel", volumeLabel);
        Set(data, "statsLabel", stats);
        SetArray(data, "itemRows", itemRows);
        SetArray(data, "attackRows", attackRows);
        Set(data, "backButton", back);
        data.ApplyModifiedPropertiesWithoutUndo();
        var controller = new SerializedObject(run.GetComponent<AlphaRunController>());
        Set(controller, "ui", view);
        controller.ApplyModifiedPropertiesWithoutUndo();
        canvas.gameObject.SetActive(false);
    }

    private static Canvas NewCanvas(string name, Transform parent, int order, float distance)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        obj.layer = 5;
        obj.transform.SetParent(parent, false);
        var canvas = obj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = Camera.main;
        canvas.planeDistance = distance;
        canvas.sortingLayerName = "Front UI";
        canvas.sortingOrder = order;
        var scaler = obj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        return canvas;
    }

    private static RectTransform Group(Transform parent, string name)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.layer = 5;
        obj.transform.SetParent(parent, false);
        var rect = (RectTransform)obj.transform;
        Stretch(rect);
        return rect;
    }

    private static RectTransform Panel(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.layer = 5;
        obj.transform.SetParent(parent, false);
        var rect = (RectTransform)obj.transform;
        Place(rect, position, size);
        obj.GetComponent<Image>().color = color;
        return rect;
    }

    private static Text Label(Transform parent, string name, string value, Vector2 position, Vector2 size, int fontSize)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Text));
        obj.layer = 5;
        obj.transform.SetParent(parent, false);
        var label = obj.GetComponent<Text>();
        Place(label.rectTransform, position, size);
        label.font = font;
        label.fontSize = fontSize;
        label.color = Ink;
        label.text = value;
        label.raycastTarget = false;
        return label;
    }

    private static Button Button(Transform parent, string name, string caption, Vector2 position, Vector2 size)
    {
        var panel = Panel(parent, name, position, size, new Color(0.09f, 0.2f, 0.22f));
        var button = panel.gameObject.AddComponent<Button>();
        button.targetGraphic = panel.GetComponent<Image>();
        var label = Label(panel, "Label", caption, new Vector2(16, -10), size - new Vector2(32, 20), 21);
        label.alignment = TextAnchor.MiddleCenter;
        return button;
    }

    private static Image Bar(Transform parent, string name, Vector2 position, Color color)
    {
        var background = Panel(parent, name, position, new Vector2(392, 12), new Color(0.15f, 0.19f, 0.25f));
        var fill = Panel(background, "Fill", Vector2.zero, new Vector2(392, 12), color).GetComponent<Image>();
        fill.sprite = barSprite;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        return fill;
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void Set(SerializedObject owner, string property, Object value) => owner.FindProperty(property).objectReferenceValue = value;
    private static void SetArray<T>(SerializedObject owner, string property, T[] values) where T : Object
    {
        var array = owner.FindProperty(property);
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }
}
