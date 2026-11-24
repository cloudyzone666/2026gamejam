# 卡面排列与透视：技术学习报告

2026-11-24。本报告对应本次卡面表现实现；实际验证结果见 `ENV_CHECK.md`。

本次最值得掌握的是：战斗位次决定规则，布局槽位决定位置，兄弟节点顺序决定遮挡。三者应当独立，悬停置顶才不会改变前排、射程或整顿换位。

| 技术与知识层次 | 当前职责 | 选择理由与学习重点 |
| --- | --- | --- |
| `RectTransform`、锚点、坐标转换，基础、核心 | `CardFormationLayout` 按固定槽位布置双方，`GetCardScreenPosition` 返回与动画无关的位置 | 现有 `HorizontalLayoutGroup` 会让兄弟顺序影响排列；专用布局允许位置不动而单独置顶。独立掌握锚点、局部坐标及 `TransformPoint`。 |
| `SetSiblingIndex`、`IPointerEnterHandler` / `IPointerExitHandler`，基础、核心 | 默认 2/4/1/3 层级；当前悬停覆盖固定卡，固定卡覆盖基础层 | 复用 uGUI 事件与原 Button，保持行动选择、目标选择和整顿拖拽。固定状态保存角色视图身份，不保存会变化的显示编号。 |
| `Time.unscaledTime`、`Mathf.Sin`、插值，基础 | 待机上下浮动、换位和悬停过渡 | 无需动画资源或额外 tween 插件。每帧从基础位置计算偏移，避免不断累加产生漂移；悬停冻结当前高度，拖拽回归基线。 |
| `ScriptableObject`、`[Serializable]`、`[SerializeField]`，基础、核心 | `CardFormationSettings` 保存参数；`BattleView` 在运行时 `Instantiate` 一份共享副本 | 调参资源与每局表现状态分开。Play Mode 的尝试不会自动写回资源，点击保存才持久化。 |
| `EditorWindow`、`SerializedObject`、`Undo`、`AssetDatabase`，值得深入 | 中文参数窗口、联动编辑、运行时副本保存与配置资源持久化 | 复用 Unity 编辑器工作流，游戏界面无需增加调试控件。重点阅读窗口的 `OnGUI` 和 `SavePreview`。 |
| `BaseMeshEffect`、`VertexHelper`、矩阵与平面透视，值得深入、表现核心 | `CardPerspectiveEffect.ModifyMesh` 把每个图形的顶点转换到卡根坐标后统一投影 | Overlay Canvas 直接绕 Y 轴旋转不能形成梯形透视。图像适度细分可减轻纹理折线；内部 `CardVisual` 仍独立负责原有 180°旋转。 |
| `ICanvasRaycastFilter`、`GraphicRaycaster`，值得深入、交互核心 | `CardHitArea` 先用包围框筛选，再按实际投影四边形命中 | 单纯变形外观会留下原透明矩形抢邻卡点击的问题。悬停保持区与实际点击区分开，支持抬升后仍稳定悬停，点击仍对应可见卡面。 |
| C# 类、属性、只读集合和 `Dictionary`，基础 | 保存每个角色的运动状态并向其他脚本提供只读列表 | 复用稳定视图身份；每帧布局复用列表与缓存，避免为每帧创建新的状态对象。 |

## 建议阅读顺序

1. `Assets/Scripts/Battle/CardFormationLayout.cs`：`SetCards`、`ApplyLayout`、`ApplyStacking`，理解位置和层级为何分开。
2. `Assets/Scripts/Battle/CharacterCardView.cs`：鼠标进入/离开、点击及原有拖拽，理解新表现如何接入现有操作。
3. `Assets/Scripts/Battle/CardFormationSettings.cs` 与 `Assets/Editor/CardFormationWindow.cs`：理解资源、运行时副本和保存的关系。
4. `Assets/Scripts/Battle/CardPresentation.cs`：`ProjectRootPoint`、`ContainsScreenPoint`、`RetainsHover`，再读 `CardPerspectiveEffect.ModifyMesh`。

先独立掌握布局、层级、指针事件及配置副本。矩阵投影、UI 网格细分、编辑器序列化可以逐步深入；暂时只需理解其输入输出和职责，无需重写渲染框架。

## 独立练习

新建一个练习场景，放四个互相重叠的 UI 矩形。先实现默认 1/3 覆盖 2/4，再加入悬停临时置顶和点击固定，要求矩形的 X 坐标始终不变。最后给每个矩形增加相位不同的上下浮动，鼠标进入时停在当前高度。这个练习无需战斗数据、立绘、透视或书本，就能检验本次最核心的知识。
