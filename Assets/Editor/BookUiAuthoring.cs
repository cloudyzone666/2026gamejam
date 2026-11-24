using System;
using System.IO;
using CardCards.Battle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CardCards.EditorTools
{
    /// <summary>Explicit book UI migration. Existing battle data, portraits and world objects are retained.</summary>
    public static class BookUiAuthoring
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string CardPath = "Assets/Prefabs/Battle/CharacterCard.prefab";
        private static readonly Color Ink = new Color(.25f, .18f, .13f);
        private static readonly Color MutedInk = new Color(.43f, .35f, .25f);
        private static readonly Color Paper = new Color(.96f, .94f, .88f);
        private static Font Font => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        [MenuItem("Card Cards/Authoring/Refresh Book Text Contrast")]
        public static void RefreshBookContrast()
        {
            // This follow-up only changes four prefab label colors; it never rebuilds the authored Canvas.
            GameObject root = PrefabUtility.LoadPrefabContents(CardPath);
            try
            {
                var serialized = new SerializedObject(root.GetComponent<CharacterCardView>());
                string[] fields = { "orderText", "distanceText", "intentText", "statusText" };
                foreach (string field in fields)
                {
                    Text label = serialized.FindProperty(field).objectReferenceValue as Text;
                    if (label == null) throw new InvalidOperationException("Missing card label: " + field);
                    label.color = Ink;
                }
                PrefabUtility.SaveAsPrefabAsset(root, CardPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("Book battlefield label contrast refreshed. Canvas, portraits and battle data retained.");
        }

        [MenuItem("Card Cards/Authoring/Apply Book UI")]
        public static void Apply()
        {
            // Refresh imports the recovered PNGs and creates their Unity-owned .meta files.
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Sprite book = ImportSprite("Assets/Art/UI/BookSurface.png");
            Sprite frame = ImportSprite("Assets/Art/UI/StatBarFrame.png");
            Sprite fill = ImportSprite("Assets/Art/UI/StatBarFill.png");
            if (AssetDatabase.LoadAssetAtPath<CharacterCardView>(CardPath) == null)
                throw new InvalidOperationException("The existing CharacterCard prefab is required; no replacement is generated.");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            BattleController controller = null;
            Canvas canvas = null;
            EventSystem events = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "BattleRoot") controller = root.GetComponent<BattleController>();
                if (root.name == "Canvas") canvas = root.GetComponent<Canvas>();
                if (events == null) events = root.GetComponent<EventSystem>();
            }
            if (controller == null || canvas == null)
                throw new InvalidOperationException("SampleScene must retain its existing BattleRoot and Canvas.");
            var controllerData = new SerializedObject(controller);
            if (controllerData.FindProperty("definition").objectReferenceValue == null)
                throw new InvalidOperationException("The existing battle definition binding is missing.");
            BattleView view = canvas.GetComponent<BattleView>();
            if (view == null) throw new InvalidOperationException("The existing Canvas BattleView is missing.");

            CharacterCardView card = AddShieldBar(frame, fill);
            RectTransform canvasRect = (RectTransform)canvas.transform;
            // Only the old feature UI under Canvas is replaced; no project assets or battle roots are removed.
            for (int i = canvasRect.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(canvasRect.GetChild(i).gameObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = .5f;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            if (canvas.GetComponent<GraphicRaycaster>() == null) canvas.gameObject.AddComponent<GraphicRaycaster>();

            ImageAt("Background", canvasRect, 640, 360, 1280, 720, Paper);
            Image surface = ImageAt("BookSurface", canvasRect, 640, 360, 1280, 720, Color.white);
            surface.sprite = book;
            surface.preserveAspect = true;
            Text turn = TextAt("TurnHeader", canvasRect, "战前整备", 180, 18, 330, 28, 17, Ink, TextAnchor.MiddleLeft);
            Text ap = TextAt("ActionPoints", canvasRect, "整备中", 795, 18, 150, 28, 15, MutedInk);
            Button cancel = SolidButton("CancelButton", canvasRect, "取消选择", 960, 18, 126, 28);
            Button end = SolidButton("EndTurnButton", canvasRect, "开始战斗", 1138, 18, 184, 28);
            TextAt("AllyLabel", canvasRect, "我方", 48, 43, 68, 20, 12, MutedInk);
            TextAt("EnemyLabel", canvasRect, "敌方", 1232, 43, 68, 20, 12, MutedInk);
            ImageAt("CenterDivider", canvasRect, 640, 164, 1, 222, new Color(.43f, .35f, .25f, .18f));
            RectTransform ally = RectAt("AllyRow", canvasRect, 300, 157, 744, 310);
            RectTransform enemy = RectAt("EnemyRow", canvasRect, 980, 157, 744, 310);
            ConfigureRow(ally, true);
            ConfigureRow(enemy, false);
            Text instruction = TextAt("Instruction", canvasRect, "选择角色查看书页；准备好后开始战斗", 640, 294, 1220, 22, 13, Ink);

            // Coordinates use the supplied 1280x720 painting, with screen origin at the top left.
            RectTransform infoRoot = RectAt("BookInfo", canvasRect, 640, 360, 1280, 720);
            BattleInfoPanelView info = infoRoot.gameObject.AddComponent<BattleInfoPanelView>();
            Text unitName = TextAt("UnitName", infoRoot, "无名剑客", 298, 326, 230, 22, 15, Ink);
            Image attackFill = FilledBar("AttackFill", infoRoot, 288, 429, 102, 12, fill, new Color(.32f, .50f, .43f));
            Image healthFill = FilledBar("HealthFill", infoRoot, 286, 460, 112, 14, fill, new Color(.70f, .22f, .30f));
            Text attackValue = TextAt("AttackValue", infoRoot, "12", 288, 429, 106, 19, 13, Ink);
            Text healthValue = TextAt("HealthValue", infoRoot, "40/40", 286, 460, 108, 20, 13, new Color(1f, .98f, .90f));
            // Opaque action panels are intentionally absent: the original four hand-painted frames remain visible.
            Button standby = PaintedButton("StandbyButton", infoRoot, "静置\n0", 370, 372, 54, 49);
            Button rotate = PaintedButton("RotateButton", infoRoot, "旋转\n1", 435, 374, 54, 49);
            Button flip = PaintedButton("FlipButton", infoRoot, "翻面\n2", 387, 441, 51, 56);
            Button adjust = PaintedButton("AdjustButton", infoRoot, "调整\n1", 453, 445, 52, 56);
            InventoryGridView hand = Grid("HandheldGrid", infoRoot, 227, 528, new Vector2(76, 44), Vector2.zero, 1, 1, false);
            InventoryGridView worn = Grid("WornGrid", infoRoot, 331, 530, new Vector2(76, 44), Vector2.zero, 1, 1, false);
            InventoryGridView accessory = Grid("AccessoryGrid", infoRoot, 436, 531, new Vector2(76, 44), Vector2.zero, 1, 1, false);
            TextAt("HandheldLabel", infoRoot, "手持", 227, 573, 90, 20, 13, MutedInk);
            TextAt("WornLabel", infoRoot, "身着", 331, 574, 90, 20, 13, MutedInk);
            TextAt("AccessoryLabel", infoRoot, "佩戴", 436, 575, 90, 20, 13, MutedInk);
            InventoryGridView backpack = Grid("BackpackGrid", canvasRect, 886, 468,
                new Vector2(81f, 84f), Vector2.zero, 5, 3, true);
            TextAt("BackpackHint", canvasRect, "共享背包 · 拖动放置 / 双击穿脱", 886, 620, 408, 22, 13, Ink);

            RectTransform tooltipRoot = RectAt("BookTooltip", canvasRect, 640, 360, 1280, 720);
            BookTooltipView tooltip = tooltipRoot.gameObject.AddComponent<BookTooltipView>();
            RectTransform tooltipPanel = ImageAt("TooltipPanel", tooltipRoot, 640, 360, 340, 230,
                new Color(.96f, .92f, .79f, .99f)).rectTransform;
            var tooltipOutline = tooltipPanel.gameObject.AddComponent<Outline>();
            tooltipOutline.effectColor = new Color(.35f, .25f, .17f, .8f);
            tooltipOutline.effectDistance = new Vector2(2, -2);
            Text tooltipTitle = TextAt("Title", tooltipPanel, "物品信息", 170, 20, 316, 26, 17, Ink, TextAnchor.MiddleLeft);
            tooltipTitle.rectTransform.anchorMin = new Vector2(0, 1);
            tooltipTitle.rectTransform.anchorMax = new Vector2(1, 1);
            tooltipTitle.rectTransform.pivot = new Vector2(.5f, 1);
            tooltipTitle.rectTransform.anchoredPosition = new Vector2(0, -10);
            tooltipTitle.rectTransform.sizeDelta = new Vector2(-24, 28);
            Text tooltipBody = TextAt("Body", tooltipPanel, "", 170, 125, 316, 180, 14, Ink, TextAnchor.UpperLeft);
            tooltipBody.rectTransform.anchorMin = Vector2.zero;
            tooltipBody.rectTransform.anchorMax = Vector2.one;
            tooltipBody.rectTransform.offsetMin = new Vector2(12, 12);
            tooltipBody.rectTransform.offsetMax = new Vector2(-12, -44);
            Bind(tooltip, "panel", tooltipPanel);
            Bind(tooltip, "titleText", tooltipTitle);
            Bind(tooltip, "bodyText", tooltipBody);
            tooltipPanel.gameObject.SetActive(false);
            RectTransform drag = RectAt("DragLayer", canvasRect, 640, 360, 1280, 720);

            RectTransform result = ImageAt("ResultPanel", canvasRect, 640, 360, 1280, 720,
                new Color(.16f, .12f, .09f, .88f), true).rectTransform;
            Text resultText = TextAt("ResultText", result, "战斗胜利", 640, 285, 700, 150, 34, new Color(.98f, .92f, .75f));
            Button restart = SolidButton("RestartButton", result, "重新开始", 640, 440, 240, 48);
            result.gameObject.SetActive(false);

            Bind(info, "unitNameText", unitName);
            Bind(info, "healthValueText", healthValue);
            Bind(info, "attackValueText", attackValue);
            Bind(info, "healthFill", healthFill);
            Bind(info, "attackFill", attackFill);
            Bind(info, "handheldGrid", hand);
            Bind(info, "wornGrid", worn);
            Bind(info, "accessoryGrid", accessory);
            Bind(info, "tooltip", tooltip);
            Bind(view, "characterCardPrefab", card);
            Bind(view, "allyContainer", ally);
            Bind(view, "enemyContainer", enemy);
            Bind(view, "turnText", turn);
            Bind(view, "instructionText", instruction);
            Bind(view, "selectionText", null);
            Bind(view, "actionPointText", ap);
            Bind(view, "logText", null);
            Bind(view, "standbyButton", standby);
            Bind(view, "rotateButton", rotate);
            Bind(view, "flipButton", flip);
            Bind(view, "adjustButton", adjust);
            Bind(view, "cancelButton", cancel);
            Bind(view, "endTurnButton", end);
            Bind(view, "infoPanel", info);
            Bind(view, "backpackGrid", backpack);
            Bind(view, "dragLayer", drag);
            Bind(view, "resultPanel", result.gameObject);
            Bind(view, "resultText", resultText);
            Bind(view, "restartButton", restart);
            Bind(controller, "view", view);
            if (events == null) events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)).GetComponent<EventSystem>();
            if (events.GetComponent<InputSystemUIInputModule>() == null) events.gameObject.AddComponent<InputSystemUIInputModule>();
            events.pixelDragThreshold = 6;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the book UI scene.");
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/BookUI-authoring.json", "{\"success\":true,\"scene\":\"" + ScenePath
                + "\",\"battleDataPreserved\":true,\"portraitPreserved\":true,\"backpackColumns\":5,\"backpackRows\":3}");
            Debug.Log("Book UI saved. Existing battle definition and character portraits retained; equipment slots, tooltip and shield bars bound.");
        }

        private static CharacterCardView AddShieldBar(Sprite frameSprite, Sprite fillSprite)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(CardPath);
            try
            {
                CharacterCardView view = root.GetComponent<CharacterCardView>();
                var serialized = new SerializedObject(view);
                Text armor = serialized.FindProperty("armorText").objectReferenceValue as Text;
                if (armor == null) throw new InvalidOperationException("The existing armor text must be retained in CharacterCard.");
                RectTransform bar = EnsureRect("ShieldBar", root.transform, new Vector2(0, -116), new Vector2(126, 22));
                Image fill = EnsureImage("Fill", bar, fillSprite, new Color(.32f, .58f, .75f));
                Image frame = EnsureImage("Frame", bar, frameSprite, Color.white);
                Stretch(frame.rectTransform, 0, 0);
                Stretch(fill.rectTransform, 4, 4);
                fill.type = UnityEngine.UI.Image.Type.Filled;
                fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
                fill.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;
                fill.fillAmount = 1;
                frame.transform.SetAsFirstSibling();
                armor.transform.SetParent(bar, false);
                Stretch(armor.rectTransform, 2, 0);
                armor.rectTransform.localRotation = Quaternion.identity;
                armor.fontSize = 14;
                armor.color = new Color(.16f, .20f, .23f);
                armor.raycastTarget = false;
                armor.transform.SetAsLastSibling();
                MoveText(serialized, "orderText", -139, 14, 20);
                MoveText(serialized, "distanceText", -157, 12, 20);
                MoveText(serialized, "intentText", 128, 14, 22);
                MoveText(serialized, "statusText", 128, 12, 20);
                Bind(view, "armorTrack", frame);
                Bind(view, "armorFill", fill);
                PrefabUtility.SaveAsPrefabAsset(root, CardPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return AssetDatabase.LoadAssetAtPath<CharacterCardView>(CardPath);
        }

        private static void MoveText(SerializedObject owner, string field, float y, int size, float height)
        {
            Text label = owner.FindProperty(field).objectReferenceValue as Text;
            if (label == null) return;
            label.rectTransform.anchoredPosition = new Vector2(0, y);
            label.rectTransform.sizeDelta = new Vector2(176, height);
            label.fontSize = size;
            label.color = Ink;
        }

        private static Sprite ImportSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new FileNotFoundException("Recovered book UI texture missing.", path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException("Cannot import sprite " + path);
            return sprite;
        }

        private static void ConfigureRow(RectTransform row, bool reverse)
        {
            row.localScale = new Vector3(.78f, .78f, 1);
            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 20;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            layout.reverseArrangement = reverse;
        }

        private static InventoryGridView Grid(string name, Transform parent, float x, float y,
            Vector2 cell, Vector2 gap, int width, int height, bool drawLines)
        {
            RectTransform rect = RectAt(name, parent, x, y,
                width * cell.x + (width - 1) * gap.x, height * cell.y + (height - 1) * gap.y);
            InventoryGridView view = rect.gameObject.AddComponent<InventoryGridView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("cellSize").vector2Value = cell;
            serialized.FindProperty("cellGap").vector2Value = gap;
            serialized.FindProperty("cellColor").colorValue = drawLines ? new Color(.48f, .37f, .21f, .075f) : Color.clear;
            serialized.FindProperty("lineColor").colorValue = drawLines ? new Color(.39f, .29f, .18f, .48f) : Color.clear;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static Button PaintedButton(string name, Transform parent, string label, float x, float y, float width, float height)
        {
            Image image = ImageAt(name, parent, x, y, width, height, Color.clear, true);
            Button button = image.gameObject.AddComponent<Button>();
            Text text = TextAt("Label", image.transform, label, width / 2, height / 2, width, height, 15, Ink);
            // Tint the lettering, so disabled actions remain legible without covering the supplied art.
            button.targetGraphic = text;
            ConfigureButton(button);
            return button;
        }

        private static Button SolidButton(string name, Transform parent, string label, float x, float y, float width, float height)
        {
            Image image = ImageAt(name, parent, x, y, width, height, new Color(.77f, .70f, .54f), true);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            TextAt("Label", image.transform, label, width / 2, height / 2, width - 8, height - 2, height > 34 ? 19 : 15, Ink);
            ConfigureButton(button);
            return button;
        }

        private static void ConfigureButton(Button button)
        {
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.2f, 1.12f, .82f);
            colors.pressedColor = new Color(.7f, .63f, .42f);
            colors.disabledColor = new Color(.63f, .63f, .63f, .48f);
            button.colors = colors;
        }

        private static Image FilledBar(string name, Transform parent, float x, float y,
            float width, float height, Sprite sprite, Color color)
        {
            Image image = ImageAt(name, parent, x, y, width, height, color);
            image.sprite = sprite;
            image.type = UnityEngine.UI.Image.Type.Filled;
            image.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            image.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;
            return image;
        }

        private static RectTransform RectAt(string name, Transform parent, float x, float y, float width, float height)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.gameObject.layer = 5;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private static Image ImageAt(string name, Transform parent, float x, float y,
            float width, float height, Color color, bool raycast = false)
        {
            Image image = RectAt(name, parent, x, y, width, height).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        private static Text TextAt(string name, Transform parent, string value, float x, float y,
            float width, float height, int size, Color color, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            Text text = RectAt(name, parent, x, y, width, height).gameObject.AddComponent<Text>();
            text.font = Font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static RectTransform EnsureRect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            Transform existing = parent.Find(name);
            RectTransform rect = existing == null
                ? new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>() : (RectTransform)existing;
            rect.SetParent(parent, false);
            rect.gameObject.layer = 5;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Image EnsureImage(string name, Transform parent, Sprite sprite, Color color)
        {
            RectTransform rect = EnsureRect(name, parent, Vector2.zero, new Vector2(126, 22));
            Image image = rect.GetComponent<Image>();
            if (image == null) image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void Stretch(RectTransform rect, float horizontalInset, float verticalInset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = new Vector2(horizontalInset, verticalInset);
            rect.offsetMax = new Vector2(-horizontalInset, -verticalInset);
        }

        private static void Bind(UnityEngine.Object owner, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null) throw new InvalidOperationException(owner.GetType().Name + " missing field " + field);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
