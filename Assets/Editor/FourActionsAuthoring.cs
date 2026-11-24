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
    // Explicit authoring command, never executed on import or during normal play.
    public static class FourActionsAuthoring
    {
        private static readonly Color Ink = new Color(.035f, .046f, .067f);
        private static readonly Color Panel = new Color(.068f, .085f, .116f);
        private static readonly Color Gold = new Color(.86f, .70f, .37f);
        private static readonly Color Muted = new Color(.60f, .66f, .73f);
        private static Font Font => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        [MenuItem("Card Cards/Authoring/Apply Four Actions Scene")]
        public static void Apply()
        {
            // Keep the old entry point safe after the book migration: it must not reset battle data or portraits.
            BookUiAuthoring.Apply();
        }

        // Retained as historical authoring code, with no automatic or menu entry point.
        private static void CreateLegacyBattleScene()
        {
            Directory.CreateDirectory("Assets/Data/Equipment");
            AssetDatabase.Refresh();
            EquipmentDefinition sword = Gear("IronSword", "精铁剑", EquipmentSlotKind.Handheld, 2, 1,
                End(2, 0, 0, Damage(.5f, 3)), End(1, 0, 0, null, Damage(.5f, 2)), "剑锋与剑脊，适合不同的姿态。");
            EquipmentDefinition leather = Gear("LeatherArmor", "皮甲", EquipmentSlotKind.Worn, 2, 1,
                End(0, 4, 2), End(0, 8, 1), "轻便护具。转身时改变防护重心。");
            EquipmentDefinition dagger = Gear("Dagger", "短匕", EquipmentSlotKind.Handheld, 1, 1,
                End(1, 0, 0, Damage(.5f, 1)), End(2, 0, 0, null, Damage(.5f, 1)), "小巧的副手武器，可与另一件小型武器并排携带。");
            EquipmentDefinition scales = Gear("ScaleArmor", "铁鳞甲", EquipmentSlotKind.Worn, 2, 1,
                End(0, 2, 4), End(0, 4, 3), "铁鳞交叠，提供更坚实的防护。");
            EquipmentDefinition charm = Gear("Charm", "护符", EquipmentSlotKind.Accessory, 1, 1,
                End(0, 3, 0, Support(EffectKind.Heal, 2)), End(0, 2, 1, null, Support(EffectKind.Heal, 1)), "静置或旋转时，微弱的镜光修复持有者的伤口。");
            EquipmentDefinition mark = Gear("WarMark", "战纹", EquipmentSlotKind.Accessory, 2, 1,
                End(1, 0, 1, Support(EffectKind.GainArmor, 2)), End(2, 0, 0, null, Support(EffectKind.GainArmor, 3)), "随姿态改变的战纹，行动时凝聚护甲。");

            const string battlePath = "Assets/Data/Battle/FourVsFour.asset";
            BattleDefinition battle = AssetDatabase.LoadAssetAtPath<BattleDefinition>(battlePath);
            if (battle == null)
            {
                battle = ScriptableObject.CreateInstance<BattleDefinition>();
                AssetDatabase.CreateAsset(battle, battlePath);
            }
            battle.allies = new[]
            {
                Ally("无名剑客", 40, 6, 2, sword, leather),
                Ally("无名剑客·影", 44, 4, 3, sword, leather),
                Ally("无名剑客·三", 40, 6, 4, sword, leather),
                Ally("无名剑客·四", 44, 4, 5, sword, leather)
            };
            battle.enemies = new[]
            {
                Enemy("腐化史莱姆 A"), Enemy("腐化史莱姆 B"),
                Enemy("腐化史莱姆 C"), Enemy("腐化史莱姆 D")
            };
            battle.backpackEquipment = new[] { dagger, scales, charm, mark };
            EditorUtility.SetDirty(battle);

            CharacterCardView prefab = AuthorCard();
            AuthorScene(battle, prefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (File.Exists("Assets/Art/Characters/NamelessSwordsman.png") && File.Exists("Assets/Art/Characters/SkullSpider.png"))
                CharacterArtAuthoring.Apply();
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/FourActions-authoring.json", "{\"success\":true,\"scene\":\"Assets/Scenes/SampleScene.unity\",\"battle\":\"Assets/Data/Battle/FourVsFour.asset\",\"equipmentCount\":6}");
            Debug.Log("Four-actions scene, 4v4 definition, equipment and Inspector bindings saved.");
        }

        private static AllyDefinition Ally(string name, int health, int armor, int range,
            EquipmentDefinition sword, EquipmentDefinition leather)
        {
            return new AllyDefinition { name = name, baseHealth = health, baseArmor = armor,
                attackRange = range, frontTop = new FormDefinition(), frontBottom = FormDefinition.Guard(),
                back = FormDefinition.Mirror(), startingEquipment = new[] { sword, leather } };
        }

        private static EnemyDefinition Enemy(string name)
        {
            return new EnemyDefinition { name = name, maxHealth = 30, maxArmor = 2, attackRange = 4, intentDamage = new[] { 6, 6, 9 } };
        }

        private static EffectDefinition Damage(float scaling, int amount)
        {
            return new EffectDefinition { kind = EffectKind.Damage, target = EffectTarget.EnemySingle, scaling = scaling, amount = amount };
        }

        private static EffectDefinition Support(EffectKind kind, int amount)
        {
            return new EffectDefinition { kind = kind, target = EffectTarget.Self, scaling = 0f, amount = amount };
        }

        private static EquipmentEndDefinition End(int attack, int health, int armor,
            EffectDefinition standby = null, EffectDefinition rotation = null)
        {
            return new EquipmentEndDefinition { attack = attack, health = health, armor = armor,
                standby = standby == null ? Array.Empty<EffectDefinition>() : new[] { standby },
                rotation = rotation == null ? Array.Empty<EffectDefinition>() : new[] { rotation } };
        }

        private static EquipmentDefinition Gear(string id, string name, EquipmentSlotKind kind,
            int width, int height, EquipmentEndDefinition top, EquipmentEndDefinition bottom, string description)
        {
            string path = "Assets/Data/Equipment/" + id + ".asset";
            EquipmentDefinition gear = AssetDatabase.LoadAssetAtPath<EquipmentDefinition>(path);
            if (gear == null)
            {
                gear = ScriptableObject.CreateInstance<EquipmentDefinition>();
                AssetDatabase.CreateAsset(gear, path);
            }
            gear.displayName = name; gear.kind = kind; gear.width = width; gear.height = height;
            gear.canRotate = true; gear.top = top; gear.bottom = bottom; gear.description = description;
            EditorUtility.SetDirty(gear);
            return gear;
        }

        private static CharacterCardView AuthorCard()
        {
            RectTransform root = Rect("CharacterCard", null, 0, 0, 134, 274);
            Image hit = root.gameObject.AddComponent<Image>(); hit.color = Color.clear;
            Button button = ButtonComponent(root, hit);
            CharacterCardView view = root.gameObject.AddComponent<CharacterCardView>();
            RectTransform visual = Rect("CardVisual", root, 0, 0, 134, 214);
            Image border = Image("SelectionBorder", visual, 0, 0, 138, 218, Gold);
            Image body = Image("CardBackground", visual, 0, 0, 132, 212, new Color(.12f, .21f, .30f));
            Image("PortraitFrame", visual, 0, 0, 94, 70, new Color(.15f, .20f, .26f));
            Image("Blade", visual, 0, 0, 5, 44, new Color(.55f, .61f, .66f));
            Image("Hilt", visual, 0, -13, 25, 4, Gold);
            Text top = Text("TopProfession", visual, "剑士", 0, 83, 126, 26, 18, Gold);
            Text bottom = Text("BottomProfession", visual, "守卫", 0, -83, 126, 26, 18, Gold);
            bottom.rectTransform.localEulerAngles = new Vector3(0, 0, 180);
            Text name = Text("Name", visual, "无名剑客", 0, 53, 126, 26, 15);
            Text mirroredName = Text("MirroredName", visual, "无名剑客", 0, -53, 126, 26, 15);
            mirroredName.rectTransform.localEulerAngles = new Vector3(0, 0, 180);
            Text order = Text("Order", root, "前排 1", 0, -122, 134, 22, 14, Gold);
            Text distance = Text("Distance", root, "射程 2", 0, -144, 148, 20, 12, Muted);
            Text intent = Text("Intent", root, "攻击 6", 0, 126, 150, 26, 15, new Color(.95f, .65f, .44f));
            Text health = Text("Health", visual, "生命 30 / 30", 0, 16, 126, 24, 15);
            Text armor = Text("Armor", visual, "护甲 2 / 2", 0, -12, 126, 22, 14, Muted);
            Text status = Text("Status", root, "", 0, 150, 150, 20, 12, Muted);
            Image("HealthTrack", visual, 0, -42, 112, 6, Ink);
            Image fill = Image("HealthFill", visual, 0, -42, 112, 6, new Color(.78f, .25f, .27f));
            fill.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/White.png");
            fill.type = UnityEngine.UI.Image.Type.Filled; fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            Bind(view, "cardBackground", body); Bind(view, "borderImage", border); Bind(view, "cardButton", button);
            Bind(view, "nameText", name); Bind(view, "mirroredNameText", mirroredName); Bind(view, "orderText", order);
            Bind(view, "healthText", health); Bind(view, "armorText", armor); Bind(view, "statusText", status);
            Bind(view, "intentText", intent); Bind(view, "distanceText", distance); Bind(view, "healthFill", fill);
            Bind(view, "visualRoot", visual); Bind(view, "topProfessionText", top); Bind(view, "bottomProfessionText", bottom);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root.gameObject, "Assets/Prefabs/Battle/CharacterCard.prefab");
            UnityEngine.Object.DestroyImmediate(root.gameObject);
            return saved.GetComponent<CharacterCardView>();
        }

        private static void AuthorScene(BattleDefinition definition, CharacterCardView prefab)
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
            // Replace only this feature's authored hierarchy; camera and 2D lighting remain intact.
            foreach (GameObject old in scene.GetRootGameObjects())
                if (old.name == "Canvas" || old.name == "BattleRoot" || old.name == "EventSystem")
                    UnityEngine.Object.DestroyImmediate(old);
            RectTransform canvasRect = Rect("Canvas", null, 0, 0, 1280, 720);
            Canvas canvas = canvasRect.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            BattleView view = canvas.gameObject.AddComponent<BattleView>();
            Image("Background", canvasRect, 0, 0, 1280, 720, Ink);
            Image("TopBar", canvasRect, 0, 332, 1280, 56, Panel);
            Text turn = Text("TurnHeader", canvasRect, "第 1 战 · 战前整备", -410, 332, 420, 36, 24, Gold, TextAnchor.MiddleLeft);
            Text ap = Text("ActionPoints", canvasRect, "整备中", 95, 332, 170, 34, 20, Gold);
            Button end = Button("EndTurnButton", canvasRect, "开始战斗", 515, 332, 216, 38);
            Image("Battlefield", canvasRect, 0, 132, 1280, 344, new Color(.047f, .061f, .084f));
            Image("CenterDivider", canvasRect, 0, 134, 1, 254, new Color(.23f, .25f, .28f));
            Text("AllyLabel", canvasRect, "我方  /  前排靠中央", -318, 285, 596, 22, 13, Muted);
            Text("EnemyLabel", canvasRect, "敌方  /  前排靠中央", 318, 285, 596, 22, 13, Muted);
            RectTransform ally = Rect("AllyRow", canvasRect, -318, 128, 596, 274);
            RectTransform enemy = Rect("EnemyRow", canvasRect, 318, 128, 596, 274);
            RowLayout(ally, true);
            RowLayout(enemy, false);
            Text selection = Text("Selection", canvasRect, "选中角色查看详情", 0, -28, 1220, 24, 14, Muted);

            RectTransform actions = Image("ActionPanel", canvasRect, -526, -194, 204, 284, Panel).rectTransform;
            Button standby = Button("StandbyButton", actions, "静置  0", 0, 113, 180, 30);
            Button rotate = Button("RotateButton", actions, "旋转  1", 0, 77, 180, 30);
            Button flip = Button("FlipButton", actions, "翻面  2", 0, 41, 180, 30);
            Button adjust = Button("AdjustButton", actions, "调整  1", 0, 5, 180, 30);
            Button cancel = Button("CancelButton", actions, "取消选择", 0, -29, 180, 26);
            Text log = Text("BattleLog", actions, "", 0, -96, 180, 94, 12, Muted, TextAnchor.UpperLeft);

            RectTransform infoRoot = Image("InformationPanel", canvasRect, -96, -194, 632, 284, Panel).rectTransform;
            BattleInfoPanelView info = infoRoot.gameObject.AddComponent<BattleInfoPanelView>();
            Button characterTab = Button("CharacterTab", infoRoot, "角色", -205, 127, 180, 30);
            Button itemTab = Button("ItemTab", infoRoot, "物品", 0, 127, 180, 30);
            Button enemyTab = Button("EnemyTab", infoRoot, "敌人", 205, 127, 180, 30);
            RectTransform characterPage = Rect("CharacterPage", infoRoot, 0, -18, 608, 250);
            RectTransform itemPage = Rect("ItemPage", infoRoot, 0, -18, 608, 250);
            RectTransform enemyPage = Rect("EnemyPage", infoRoot, 0, -18, 608, 250);
            Text characterDetails = Text("CharacterDetails", characterPage, "选择我方角色", 0, 52, 596, 140, 14, null, TextAnchor.UpperLeft);
            Text itemDetails = Text("ItemDetails", itemPage, "选择物品查看详情", 0, 0, 596, 242, 14, null, TextAnchor.UpperLeft);
            Text enemyDetails = Text("EnemyDetails", enemyPage, "选择敌人查看详情", 0, 0, 596, 242, 17, null, TextAnchor.UpperLeft);
            Text("HandheldLabel", characterPage, "手持  2×1", -205, -30, 150, 22, 13, Muted);
            Text("WornLabel", characterPage, "身着  2×1", -55, -30, 150, 22, 13, Muted);
            Text("AccessoryLabel", characterPage, "佩戴  2×2", 115, -30, 150, 22, 13, Muted);
            InventoryGridView hand = Grid("HandheldGrid", characterPage, -205, -70, 85, 40);
            InventoryGridView worn = Grid("WornGrid", characterPage, -55, -70, 85, 40);
            InventoryGridView accessory = Grid("AccessoryGrid", characterPage, 115, -82, 85, 85);
            Bind(info, "characterTabButton", characterTab); Bind(info, "itemTabButton", itemTab); Bind(info, "enemyTabButton", enemyTab);
            Bind(info, "characterPage", characterPage.gameObject); Bind(info, "itemPage", itemPage.gameObject); Bind(info, "enemyPage", enemyPage.gameObject);
            Bind(info, "characterDetailsText", characterDetails); Bind(info, "itemDetailsText", itemDetails); Bind(info, "enemyDetailsText", enemyDetails);
            Bind(info, "handheldGrid", hand); Bind(info, "wornGrid", worn); Bind(info, "accessoryGrid", accessory);
            itemPage.gameObject.SetActive(false); enemyPage.gameObject.SetActive(false);

            RectTransform backpackPanel = Image("BackpackPanel", canvasRect, 430, -194, 396, 284, Panel).rectTransform;
            Text("BackpackTitle", backpackPanel, "共享背包", 0, 118, 355, 28, 20, Gold);
            Text("BackpackHint", backpackPanel, "单击查看 · 拖动放置 · 双击穿脱", 0, 84, 368, 26, 13, Muted);
            InventoryGridView backpack = Grid("BackpackGrid", backpackPanel, 0, -8, 355, 130);
            Text("BackpackControls", backpackPanel, "右键 / R：转向\n战斗中需进入整顿才能换装", 0, -108, 368, 52, 13, Muted);
            Text instruction = Text("Instruction", canvasRect, "", 0, -346, 1220, 22, 13, Gold);
            // A dedicated non-raycasting overlay keeps drag previews above every panel.
            RectTransform drag = Rect("DragLayer", canvasRect, 0, 0, 1280, 720);
            RectTransform result = Image("ResultPanel", canvasRect, 0, 0, 1280, 720, new Color(.015f, .02f, .035f, .96f), true).rectTransform;
            Text resultText = Text("ResultText", result, "战斗胜利", 0, 55, 700, 140, 34, Gold);
            Button restart = Button("RestartButton", result, "重新开始", 0, -85, 240, 48);
            result.gameObject.SetActive(false);

            Bind(view, "characterCardPrefab", prefab); Bind(view, "allyContainer", ally); Bind(view, "enemyContainer", enemy);
            Bind(view, "turnText", turn); Bind(view, "instructionText", instruction); Bind(view, "selectionText", selection);
            Bind(view, "actionPointText", ap); Bind(view, "logText", log); Bind(view, "standbyButton", standby);
            Bind(view, "rotateButton", rotate); Bind(view, "flipButton", flip); Bind(view, "adjustButton", adjust);
            Bind(view, "cancelButton", cancel); Bind(view, "endTurnButton", end); Bind(view, "resultPanel", result.gameObject);
            Bind(view, "resultText", resultText); Bind(view, "restartButton", restart); Bind(view, "infoPanel", info);
            Bind(view, "backpackGrid", backpack); Bind(view, "dragLayer", drag);
            BattleController controller = new GameObject("BattleRoot").AddComponent<BattleController>();
            Bind(controller, "definition", definition); Bind(controller, "view", view);
            var controllerData = new SerializedObject(controller); controllerData.FindProperty("enemyActionDelay").floatValue = .45f;
            controllerData.ApplyModifiedPropertiesWithoutUndo();
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.GetComponent<EventSystem>().pixelDragThreshold = 6;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static InventoryGridView Grid(string name, Transform parent, float x, float y, float width, float height)
        {
            RectTransform rect = Rect(name, parent, x, y, width, height);
            return rect.gameObject.AddComponent<InventoryGridView>();
        }

        private static void RowLayout(RectTransform row, bool reverse)
        {
            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 20;
            layout.childControlWidth = false; layout.childControlHeight = false;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
            layout.reverseArrangement = reverse;
        }

        private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            if (parent != null) rect.SetParent(parent, false);
            rect.gameObject.layer = 5;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private static Image Image(string name, Transform parent, float x, float y, float width, float height, Color color, bool raycast = false)
        {
            Image image = Rect(name, parent, x, y, width, height).gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = raycast; return image;
        }

        private static Text Text(string name, Transform parent, string value, float x, float y, float width, float height,
            int size, Color? color = null, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            Text text = Rect(name, parent, x, y, width, height).gameObject.AddComponent<Text>();
            text.font = Font; text.text = value; text.fontSize = size; text.color = color ?? new Color(.89f, .90f, .88f);
            text.alignment = alignment; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Button Button(string name, Transform parent, string label, float x, float y, float width, float height)
        {
            Image image = Image(name, parent, x, y, width, height, new Color(.15f, .19f, .25f), true);
            Button button = ButtonComponent(image.rectTransform, image);
            Text("Label", image.transform, label, 0, 0, width - 8, height - 2, height > 34 ? 18 : 15, Gold);
            return button;
        }

        private static Button ButtonComponent(RectTransform rect, Image image)
        {
            Button button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            ColorBlock colors = button.colors; colors.highlightedColor = new Color(1.2f, 1.15f, 1f);
            colors.pressedColor = new Color(.7f, .7f, .7f); colors.disabledColor = new Color(.45f, .45f, .45f, .7f);
            button.colors = colors; return button;
        }

        private static void Bind(UnityEngine.Object owner, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null) throw new InvalidOperationException(owner.GetType().Name + " missing field " + field);
            property.objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
