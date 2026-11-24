using CardCards.Battle;
using UnityEditor;
using UnityEngine;

namespace CardCards.EditorTools
{
    /// <summary>Edits an asset outside Play Mode and a disposable runtime copy during Play Mode.</summary>
    public sealed class CardFormationWindow : EditorWindow
    {
        private CardFormationSettings asset;
        private CardFormationSettings source;
        private SerializedObject serialized;
        private Vector2 scroll;
        private bool linked = true;

        [MenuItem("Card Cards/UI/卡面布局调节")]
        public static void Open() { GetWindow<CardFormationWindow>("卡面布局调节"); }

        private void OnEnable()
        {
            minSize = new Vector2(390f, 430f);
            asset = AssetDatabase.LoadAssetAtPath<CardFormationSettings>(CardFormationSettings.DefaultAssetPath);
            SceneView.duringSceneGui += DrawSceneDebug;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawSceneDebug;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            source = null;
            serialized = null;
            Repaint();
        }

        private void OnInspectorUpdate() { Repaint(); }

        private CardFormationSettings FindSource()
        {
            if (!EditorApplication.isPlaying) return asset;
            BattleView battle = FindObjectOfType<BattleView>();
            return battle != null ? battle.PresentationSettings : null;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            asset = (CardFormationSettings)EditorGUILayout.ObjectField("保存到配置", asset, typeof(CardFormationSettings), false);
            if (asset == null)
            {
                EditorGUILayout.HelpBox("创建一次默认配置，再在战斗场景的 BattleView 上绑定。", MessageType.Info);
                if (GUILayout.Button("创建或载入默认配置")) asset = GetOrCreateAsset();
            }
            CardFormationSettings active = FindSource();
            if (active == null)
            {
                EditorGUILayout.HelpBox(EditorApplication.isPlaying
                    ? "尚未找到运行中的 BattleView。请打开战斗场景并等待初始化完成。"
                    : "请先选择或创建配置资源。", MessageType.Info);
                return;
            }
            if (source != active || serialized == null)
            {
                source = active;
                serialized = new SerializedObject(source);
            }
            EditorGUILayout.HelpBox(EditorApplication.isPlaying
                ? "正在调整运行时副本，Game 视图立即预览。点击“保存参数”写入配置，退出运行仍可保留；未保存的调整会丢弃。"
                : "正在编辑配置资源。进入 Play Mode 可查看完整卡面、重叠和浮动效果。", MessageType.Info);
            linked = EditorGUILayout.ToggleLeft("双方联动（位置独立，其余卡面参数同步）", linked);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            serialized.Update();
            EditorGUILayout.LabelField("全局透视", EditorStyles.boldLabel);
            Property("perspectiveCenter", "透视中心（左上角坐标）");
            Property("perspectiveDistance", "透视距离");
            EditorGUILayout.LabelField("坐标基于参考 Canvas：1280 × 720，Y 向下。", EditorStyles.miniLabel);
            Property("pauseFloat", "暂停浮动，便于对齐");
            Property("showDebug", "Scene 视图显示编号与命中轮廓");
            EditorGUILayout.Space();
            if (linked)
            {
                Property("ally.position", "我方位置");
                Property("enemy.position", "敌方位置");
                EditorGUI.BeginChangeCheck();
                DrawSide("ally", "双方卡面", false);
                bool copyToEnemy = EditorGUI.EndChangeCheck();
                serialized.ApplyModifiedProperties();
                if (copyToEnemy)
                {
                    Undo.RecordObject(source, "联动调整卡面参数");
                    Vector2 enemyPosition = source.enemy.position;
                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source.ally), source.enemy);
                    source.enemy.position = enemyPosition;
                    source.ValidateValues();
                    EditorUtility.SetDirty(source);
                    serialized.Update();
                }
            }
            else
            {
                DrawSide("ally", "我方卡面", true);
                EditorGUILayout.Space();
                DrawSide("enemy", "敌方卡面", true);
                serialized.ApplyModifiedProperties();
            }
            EditorGUILayout.EndScrollView();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("恢复默认")) ResetValues();
                if (GUILayout.Button("保存参数")) SaveValues();
            }
            EditorGUILayout.LabelField("支持 Ctrl+Z 撤销；调整只影响上方卡面。", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("请为每张卡保留可见区域，并将卡面留在上方战场。", EditorStyles.miniLabel);
            if (GUI.changed) SceneView.RepaintAll();
        }

        private void DrawSide(string prefix, string title, bool position)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (position) Property(prefix + ".position", "队列位置");
            Property(prefix + ".cardSize.x", "卡面基础宽度");
            EditorGUILayout.LabelField("基础高度固定 274；用内外侧缩放调整整体大小。", EditorStyles.miniLabel);
            Property(prefix + ".overlap", "重叠比例");
            Property(prefix + ".innerScale", "内侧缩放（靠中央）");
            Property(prefix + ".outerScale", "外侧缩放（靠屏幕边）");
            Property(prefix + ".scaleExponent", "大小渐变指数");
            Property(prefix + ".yaw", "镜像透视倾角");
            EditorGUILayout.Space();
            Property(prefix + ".floatEnabled", "开启待机浮动");
            Property(prefix + ".floatAmplitude", "浮动幅度");
            Property(prefix + ".floatPeriod", "浮动周期（秒）");
            Property(prefix + ".floatPhase", "相邻卡节奏差（弧度）");
            Property(prefix + ".transitionDuration", "过渡时长（秒）");
            Property(prefix + ".hoverLift", "悬停抬升");
            Property(prefix + ".hoverScale", "悬停缩放");
        }

        private void Property(string path, string label)
        {
            SerializedProperty property = serialized.FindProperty(path);
            if (property != null) EditorGUILayout.PropertyField(property, new GUIContent(label));
        }

        private void ResetValues()
        {
            Undo.RecordObject(source, "恢复默认卡面布局");
            var defaults = CreateInstance<CardFormationSettings>();
            string originalName = source.name;
            EditorUtility.CopySerialized(defaults, source);
            source.name = originalName;
            DestroyImmediate(defaults);
            EditorUtility.SetDirty(source);
            serialized.Update();
        }

        private void SaveValues()
        {
            if (asset == null) asset = GetOrCreateAsset();
            SavePreview(source, asset);
            ShowNotification(new GUIContent("卡面参数已保存"));
        }

        /// <summary>Persists the same preview used by the window; the destination must already be an asset.</summary>
        public static void SavePreview(CardFormationSettings preview, CardFormationSettings target)
        {
            if (preview == null) throw new System.ArgumentNullException(nameof(preview));
            if (target == null) throw new System.ArgumentNullException(nameof(target));
            if (!EditorUtility.IsPersistent(target))
                throw new System.ArgumentException("保存目标必须是已创建的卡面配置资源。", nameof(target));
            Undo.RecordObject(target, "保存卡面布局参数");
            if (preview != target)
            {
                string originalName = target.name;
                EditorUtility.CopySerialized(preview, target);
                target.name = originalName;
            }
            target.ValidateValues();
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();
        }

        private static CardFormationSettings GetOrCreateAsset()
        {
            CardFormationSettings settings = AssetDatabase.LoadAssetAtPath<CardFormationSettings>(CardFormationSettings.DefaultAssetPath);
            if (settings != null) return settings;
            if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
            if (!AssetDatabase.IsValidFolder("Assets/Data/Battle")) AssetDatabase.CreateFolder("Assets/Data", "Battle");
            settings = CreateInstance<CardFormationSettings>();
            AssetDatabase.CreateAsset(settings, CardFormationSettings.DefaultAssetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        private void DrawSceneDebug(SceneView scene)
        {
            CardFormationSettings current = FindSource();
            if (current == null || !current.showDebug) return;
            foreach (CardFormationLayout layout in FindObjectsOfType<CardFormationLayout>())
            {
                Handles.color = layout.IsEnemy ? new Color(1f, .55f, .45f) : new Color(.35f, .85f, 1f);
                for (int i = 0; i < layout.Cards.Count; i++)
                {
                    CharacterCardView card = layout.Cards[i];
                    if (card == null) continue;
                    CardPresentation presentation = card.GetComponent<CardPresentation>();
                    if (presentation == null) continue;
                    Vector3[] corners = presentation.GetWorldCorners();
                    for (int edge = 0; edge < 4; edge++) Handles.DrawLine(corners[edge], corners[(edge + 1) % 4]);
                    string role = card == layout.HoveredCard ? " · 悬停" : card == layout.PinnedCard ? " · 固定" : "";
                    Handles.Label((corners[1] + corners[2]) * .5f, (layout.IsEnemy ? "敌方 " : "我方 ") + (i + 1) + role);
                }
            }
        }
    }
}
