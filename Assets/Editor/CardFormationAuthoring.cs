using System;
using System.IO;
using CardCards.Battle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CardCards.EditorTools
{
    /// <summary>Targeted card-only wiring. Never rebuilds the Canvas or the book.</summary>
    public static class CardFormationAuthoring
    {
        [MenuItem("Card Cards/Verification/Verify Card Tuning Persistence")]
        public static void VerifyTuningPersistence()
        {
            const string path = "Assets/Data/Battle/CardFormationVerification.asset";
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new InvalidOperationException("Temporary verification path is already occupied; nothing was overwritten.");
            CardFormationSettings preview = ScriptableObject.CreateInstance<CardFormationSettings>();
            try
            {
                CardFormationSettings target = ScriptableObject.CreateInstance<CardFormationSettings>();
                AssetDatabase.CreateAsset(target, path);
                preview.ally.overlap = .41f;
                preview.enemy.overlap = .33f;
                preview.perspectiveDistance = 720f;
                preview.ally.floatAmplitude = 4.5f;
                preview.pauseFloat = true;
                CardFormationWindow.SavePreview(preview, target);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                CardFormationSettings loaded = AssetDatabase.LoadAssetAtPath<CardFormationSettings>(path);
                if (loaded == null || !Mathf.Approximately(loaded.ally.overlap, .41f)
                    || !Mathf.Approximately(loaded.enemy.overlap, .33f)
                    || !Mathf.Approximately(loaded.perspectiveDistance, 720f)
                    || !Mathf.Approximately(loaded.ally.floatAmplitude, 4.5f) || !loaded.pauseFloat)
                    throw new InvalidOperationException("Card tuning save/reimport lost preview values.");
                string yaml = File.ReadAllText(path);
                if (!yaml.Contains("perspectiveDistance: 720") || !yaml.Contains("floatAmplitude: 4.5"))
                    throw new InvalidOperationException("Saved tuning values were not written to disk.");
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/CardFormation-TuningVerification.json", "{\"success\":true,\"source\":\"Editor save path and forced asset reimport\",\"completedUtc\":\""
                    + DateTime.UtcNow.ToString("O") + "\"}");
                Debug.Log("Card tuning preview save and asset reimport passed; authored settings retained.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(preview);
                // This verification owns this disposable asset; it never removes existing project resources.
                AssetDatabase.DeleteAsset(path);
            }
        }

        [MenuItem("Card Cards/Authoring/Apply Card Formation")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Apply card wiring from Edit Mode.");
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
            BattleView view = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.GetComponent<BattleView>() != null) view = root.GetComponent<BattleView>();
            if (view == null) throw new InvalidOperationException("Existing Canvas/BattleView is required.");
            CardFormationSettings settings = AssetDatabase.LoadAssetAtPath<CardFormationSettings>(CardFormationSettings.DefaultAssetPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<CardFormationSettings>();
                AssetDatabase.CreateAsset(settings, CardFormationSettings.DefaultAssetPath);
            }
            var serialized = new SerializedObject(view);
            serialized.FindProperty("cardFormationSettings").objectReferenceValue = settings;
            ConfigureRow(serialized.FindProperty("allyContainer").objectReferenceValue as RectTransform, settings, false);
            ConfigureRow(serialized.FindProperty("enemyContainer").objectReferenceValue as RectTransform, settings, true);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            const string prefabPath = "Assets/Prefabs/Battle/CharacterCard.prefab";
            GameObject prefab = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                if (prefab.GetComponent<CardPresentation>() == null) prefab.AddComponent<CardPresentation>();
                PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/CardFormation-Authoring.json", "{\"success\":true,\"scene\":\"Assets/Scenes/SampleScene.unity\",\"settings\":\""
                + CardFormationSettings.DefaultAssetPath + "\",\"completedUtc\":\"" + DateTime.UtcNow.ToString("O") + "\"}");
            Debug.Log("Card formation wiring saved. Existing book, combat data and portraits retained.");
        }

        private static void ConfigureRow(RectTransform row, CardFormationSettings settings, bool enemy)
        {
            if (row == null) throw new InvalidOperationException("Existing character row binding is missing.");
            HorizontalLayoutGroup oldLayout = row.GetComponent<HorizontalLayoutGroup>();
            if (oldLayout != null) oldLayout.enabled = false;
            CardFormationLayout formation = row.GetComponent<CardFormationLayout>();
            if (formation == null) formation = row.gameObject.AddComponent<CardFormationLayout>();
            formation.Configure(settings, enemy);
            EditorUtility.SetDirty(formation);
        }
    }
}
