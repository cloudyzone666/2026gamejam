using System;
using UnityEngine;

namespace CardCards.Battle
{
    /// <summary>Presentation-only values. Positions use the reference Canvas's top-left coordinates.</summary>
    [CreateAssetMenu(fileName = "CardFormation", menuName = "Card Cards/卡面布局参数")]
    public sealed class CardFormationSettings : ScriptableObject
    {
        public const string DefaultAssetPath = "Assets/Data/Battle/CardFormation.asset";

        [Tooltip("以参考 Canvas 左上角为原点的透视中心。1280×720 界面中央为 x=640。")]
        public Vector2 perspectiveCenter = new Vector2(640f, 150f);
        [Min(180f), Tooltip("等效观察距离；越小，近大远小越明显。")]
        public float perspectiveDistance = 850f;
        public SideSettings ally = new SideSettings { position = new Vector2(300f, 150f) };
        public SideSettings enemy = new SideSettings { position = new Vector2(980f, 150f) };
        public bool pauseFloat;
        public bool showDebug;

        [Serializable]
        public sealed class SideSettings
        {
            public Vector2 position;
            // The authored card uses a 274-unit root, including the labels outside its 214-unit face.
            public Vector2 cardSize = new Vector2(190f, 274f);
            [Range(0f, .8f)] public float overlap = .5f;
            [Range(.3f, 2f)] public float innerScale = .68f;
            [Range(.3f, 2f)] public float outerScale = .84f;
            [Range(.2f, 4f)] public float scaleExponent = 1f;
            [Range(0f, 40f)] public float yaw = 16f;
            public bool floatEnabled = true;
            [Range(0f, 12f)] public float floatAmplitude = 2.5f;
            [Min(.2f)] public float floatPeriod = 3f;
            [Range(0f, 6.283185f)] public float floatPhase = .9f;
            [Range(0f, 1f)] public float transitionDuration = .16f;
            [Range(0f, 32f)] public float hoverLift;
            [Range(1f, 1.25f)] public float hoverScale = 1f;

            public void ValidateValues()
            {
                cardSize.x = Mathf.Clamp(cardSize.x, 80f, 260f);
                cardSize.y = 274f;
                overlap = Mathf.Clamp(overlap, 0f, .8f);
                innerScale = Mathf.Clamp(innerScale, .3f, 2f);
                outerScale = Mathf.Clamp(outerScale, .3f, 2f);
                scaleExponent = Mathf.Clamp(scaleExponent, .2f, 4f);
                yaw = Mathf.Clamp(yaw, 0f, 40f);
                floatAmplitude = Mathf.Clamp(floatAmplitude, 0f, 12f);
                floatPeriod = Mathf.Max(.2f, floatPeriod);
                floatPhase = Mathf.Clamp(floatPhase, 0f, Mathf.PI * 2f);
                transitionDuration = Mathf.Clamp01(transitionDuration);
                hoverLift = Mathf.Clamp(hoverLift, 0f, 32f);
                hoverScale = Mathf.Clamp(hoverScale, 1f, 1.25f);
            }
        }

        public void ValidateValues()
        {
            perspectiveDistance = Mathf.Max(180f, perspectiveDistance);
            if (ally == null) ally = new SideSettings { position = new Vector2(300f, 150f) };
            if (enemy == null) enemy = new SideSettings { position = new Vector2(980f, 150f) };
            ally.ValidateValues();
            enemy.ValidateValues();
        }

        private void OnValidate() { ValidateValues(); }
    }
}
