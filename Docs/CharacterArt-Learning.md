# 人物立绘导入与学习记录

日期：2026-11-23。来源为用户提供的两张图片；图片内的设定文字仅作为素材内容，不是本轮要实现的玩法需求。

## 素材与接线

- `Assets/Art/Characters/SkullSpider.png`：从怪物图裁出左上蜘蛛，裁框为原图像素 `(80,94,550,356)`，未包含文字和下方巫师；清除与外边缘相连的近白背景，保留骨骼内部浅色。
- `Assets/Art/Characters/NamelessSwordsman.png`：保留角色双向全幅与彩色构图，清除外侧近白底，等比缩至604×1024。没有新增独立背面图，镜影沿用角色图并加紫色。
- `FourVsFour.asset` 的我方四人引用剑客Sprite、敌方四人引用蜘蛛Sprite；各自Inspector的Portrait字段可替换。出生数值、意图和战斗规则沿用原配置。
- `CharacterCard.prefab/CardVisual/Portrait` 的Image已接到CharacterCardView.portraitImage；上下端文字有深色衬底，敌人姓名、生命、护甲和血条分开布置。场景沿用原Prefab引用，无需重新接线。

## 本次实际用到的技术

| 技术 | 层次与职责 | 为什么选它 |
| --- | --- | --- |
| TextureImporter、Sprite Single、透明通道 | 基础，需掌握。把PNG导为单张Sprite，关闭MipMap、保持原始尺寸比例，使用双线性过滤和Clamp | 立绘用于固定尺寸UGUI，不需要图集切片、SpriteRenderer或新插件 |
| `Image.sprite`、`preserveAspect`、`raycastTarget` | 本次核心，需独立掌握。视图读取角色的Sprite，等比显示，关闭立绘的射线拦截 | 复用现有卡面按钮和拖动，图片不会抢走输入；不使用SetNativeSize把大图撑出卡面 |
| `[SerializeField]`、Serializable配置、只读属性 | 基础。配置保存资源引用，BattleUnitState.Portrait把引用交给视图 | 不按姓名判断图片，不在运行时从桌面读取文件；后续新角色可在Inspector配置 |
| RectTransform父子关系 | 本次核心。Portrait放在CardVisual下，继承原180°旋转；外侧位次和距离保持原方向 | 延用已有旋转协程，避免再写一套图片旋转逻辑 |
| PrefabUtility、SerializedObject | 暂时了解即可。显式Editor菜单导入并增量绑定Prefab与数据 | 接线保存为资源，正常Play不会生成或重写场景；不调用整场景重建来导入两张图 |

重要区别：Sprite是对纹理的资源引用；Image是Canvas里负责显示它的组件。配置决定“用哪张”，视图决定“放在哪、怎么显示”，两者都不参与伤害计算。

阅读顺序：`BattleDefinition.cs` 的portrait → `BattleUnitState` 两个构造函数 → `CharacterCardView.UpdatePortrait`。Editor辅助入口为菜单 `Card Cards/Authoring/Apply Character Art`；正常运行无需再点。`Tools/prepare_character_art.ps1` 记录裁图过程，默认输入为原桌面文件，原文件不改动。

小练习：在空Canvas里建立一个120×200的Image与Button，换两张宽高比不同的Sprite；打开preserveAspect，再把Image放到父RectTransform下旋转180°。确认图不变形、按钮仍可点击。这对应本轮立绘显示的核心。

当前不必深入纹理压缩算法、自动分割模型和SpriteAtlas打包。本次使用确定的裁框与边缘连通去白，不依赖AI生成或重画素材。实际编译与画面验证见ENV_CHECK.md最新记录。
