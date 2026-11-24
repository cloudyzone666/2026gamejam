using System;
using CardCards.Battle;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CardCards.EditorTools
{
    /// <summary>Explicit, incremental art import. Leaves scene wiring and battle rules intact.</summary>
    public static class CharacterArtAuthoring
    {
        private const string ArtRoot = "Assets/Art/Characters/";

        [MenuItem("Card Cards/Authoring/Apply Character Art")]
        public static void Apply()
        {
            AssetDatabase.Refresh();
            Sprite hero = ImportPortrait("NamelessSwordsman.png");
            Sprite monster = ImportPortrait("SkullSpider.png");
            var definition = AssetDatabase.LoadAssetAtPath<BattleDefinition>("Assets/Data/Battle/FourVsFour.asset");
            if (definition == null) throw new InvalidOperationException("Current 4v4 battle definition is missing.");
            foreach (AllyDefinition ally in definition.allies) ally.portrait = hero;
            foreach (EnemyDefinition enemy in definition.enemies) enemy.portrait = monster;
            EditorUtility.SetDirty(definition);

            const string prefabPath = "Assets/Prefabs/Battle/CharacterCard.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Transform visual = root.transform.Find("CardVisual");
                if (visual == null) throw new InvalidOperationException("CardVisual is missing from the card prefab.");
                Image portrait = EnsureImage(visual, "Portrait", Vector2.zero, new Vector2(124f, 204f), Color.white);
                portrait.sprite = hero;
                portrait.preserveAspect = true;
                portrait.transform.SetSiblingIndex(visual.Find("CardBackground").GetSiblingIndex() + 1);
                Image upper = EnsureImage(visual, "UpperTextBacking", new Vector2(0f, 85f), new Vector2(130f, 42f), new Color(.035f, .046f, .067f, .82f));
                Image lower = EnsureImage(visual, "LowerTextBacking", new Vector2(0f, -85f), new Vector2(130f, 42f), new Color(.035f, .046f, .067f, .82f));
                upper.transform.SetSiblingIndex(portrait.transform.GetSiblingIndex() + 1);
                lower.transform.SetSiblingIndex(upper.transform.GetSiblingIndex() + 1);
                ConfigureLabel(visual, "TopProfession", 95f, 15, 20f);
                ConfigureLabel(visual, "BottomProfession", -95f, 15, 20f);
                ConfigureLabel(visual, "Name", 76f, 12, 18f);
                ConfigureLabel(visual, "MirroredName", -76f, 12, 18f);
                var serialized = new SerializedObject(root.GetComponent<CharacterCardView>());
                serialized.FindProperty("portraitImage").objectReferenceValue = portrait;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("Character art imported and bound: full bidirectional swordsman, cropped skull spider.");
        }

        private static Sprite ImportPortrait(string fileName)
        {
            string path = ArtRoot + fileName;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Portrait texture missing: " + path);
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
            importer.spritePixelsPerUnit = 100f;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException("Portrait did not import as a single Sprite: " + path);
            return sprite;
        }

        private static Image EnsureImage(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            Transform existing = parent.Find(name);
            Image image = existing == null
                ? new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>()
                : existing.GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.gameObject.layer = parent.gameObject.layer;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void ConfigureLabel(Transform visual, string name, float y, int size, float height)
        {
            Text label = visual.Find(name).GetComponent<Text>();
            label.rectTransform.anchoredPosition = new Vector2(0f, y);
            label.rectTransform.sizeDelta = new Vector2(126f, height);
            label.fontSize = size;
        }
    }
}
