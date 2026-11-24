# Card_Cards：四操作与 4v4 战斗

Unity 工程目录为本目录，版本 **2022.3.62f3c1**，使用 URP 14.0.12 与 New Input System 1.14.2，目标为 Windows。角色以矩形卡面展示；这是回合制 RPG，不含抽牌、出牌或牌堆玩法。

当前范围为单场 **战前整备 → 4 对 4 战斗 → 胜负 → 重开**。已接入三形态、四操作、共享行动点、真实射程、位次、信息栏与共享背包。2026-11-24 已完成中断的书本界面集成：下方使用用户提供的书本布局，上方保留战场；双向剑客与骷髅蜘蛛立绘沿用 2026-11-23 导入结果。书本界面的本轮编译、运行及截图证据以 `ENV_CHECK.md` 顶部记录为准。30 战推进、奖励、存档与状态持续效果尚未实现。

## 从 Unity 运行

1. 用指定版本打开当前目录，等待包解析和脚本编译。
2. 打开 `Assets/Scenes/SampleScene.unity`，进入 Play Mode。
3. 开局处于“战前整备”：选中我方角色，左页显示姓名、生命与攻击；可整理三个装备槽和右页背包，或拖动角色调整位次。
4. 点击“开始战斗”。选我方角色 → 点行动 → 选择高亮的合法目标；需要多段目标时，全部选齐后才结算。可点击“取消”，不消耗 AP、次数或改变姿态。
5. 每个我方回合全队共享 4 AP。AP 用尽或点击“结束回合”后，存活敌人依次攻击我方当前前排；随后 AP 回满并清空本回合行动标记。
6. 敌方全灭胜利，我方全灭失败；点击“重新开始”返回战前整备。

| 操作 | 消耗 | 当前规则 |
| --- | --- | --- |
| 静置 | 0 AP | 正面、本回合未旋转或翻面时可用；每回合一次，之后该角色本回合收手。 |
| 旋转 | 1 AP | 先触发下端职业与装备词条，再将整张卡面旋转 180°；正面可连续使用。 |
| 翻面 | 2 AP | 正面转背面触发镜影，背面转正面触发正面两个职业；背面不能静置或旋转，翻回保留原朝向。 |
| 调整 | 1 AP | 进入全队整顿，可拖动站位与换装；必须主动退出，退出免费。用最后 1 AP 进入时，退出后才交给敌方。 |

双方位次由前到后为 1–4，死亡后紧凑排列。距离为“我方位次 + 敌方位次 − 1”；伤害只能命中角色射程内的敌人，超距或没有合法目标时显示原因且不扣点。当前验证阵容我方射程为 2/3/4/5，敌人为 4；距离按角色配置，不随形态改变。

护甲先吸收伤害，不会每回合自动恢复。形态和装备改变上限时，当前生命/护甲夹到新上限；上限提高不补满。词条支持伤害、治疗和加甲，职业先于装备结算，单体伤害不溢出转移。

## 书本界面与背包

- 左页保留当前查看的我方角色，显示姓名、生命与攻击，以及 2×2 排列的静置、旋转、翻面、调整按钮；左下为手持、身着、佩戴三个单装备槽。静置后的角色仍可查看和在整顿时换装。
- 右页共享背包为连续 **5×3** 格。每个角色的每个部位只容纳一件对应类型装备，穿戴时用一个槽显示；背包中的占地仍按装备原始尺寸与 90° 转向计算。占满的槽会拒绝新物品，需先卸下当前装备。
- 悬停物品或敌人显示临时详情浮窗，离开后关闭；单击固定浮窗，点击浮窗外或按 Esc 关闭。敌人详情不会替换左页我方角色。浮窗不拦截点击和拖动。
- 拖动超过 6 px 后产生虚影，绿框可放、红框不可放，松手提交。非法落点保留原位；双击背包物品穿戴到当前查看角色对应的空槽，双击已装备物品卸到背包。
- 右键或选中物品后按 R 进行 90°“转向”，拖动中也可转向；它只改变背包占地，不改变装备生效端。穿戴不会改写装备的原始尺寸或转向，卸回背包时优先保留朝向；只有当前朝向放不下时，自动放置才尝试另一方向。角色卡面的 180°“旋转”才改变装备生效端。
- 仅战前整备或战斗整顿时允许移动装备；普通行动、敌方回合和胜负结算时可查看。整顿期间可以换位、换装已静置角色。
- 生命条按当前生命/上限填充。攻击没有“最大攻击”属性，绿色攻击条只是视觉底纹，以数字表示实际攻击。双方卡面下方显示蓝色护盾条与当前值/上限，卡面旋转时仍保持正向；战斗规则中的护甲在界面上称为护盾。
- 回合、AP、开始/结束回合、取消及终局重开保留为精简控件。旧日志区、角色/物品/敌人页签和面板已由书页与浮窗取代；参考图上方四块区域暂不制作。

## 卡面布局与调参

2026-11-24 新增上方卡面重叠与透视：双方各自按屏幕左到右计算 1–4 显示编号，1/3 覆盖 2/4；悬停临时置顶，每侧点击可固定一张，战场空白或 Esc 解除。编号不改变战斗前排与距离。卡面待机浮动、悬停暂停；下方书本沿用既有布局。

菜单 `Card Cards/UI/卡面布局调节` 提供透视中心、倾角、内外缩放、重叠、浮动与悬停参数。Play Mode 调整运行时副本，点击“保存参数”才写入 `Assets/Data/Battle/CardFormation.asset`，退出后保留。场景与 Prefab 已接线，正常使用无需 authoring 菜单。详细说明见 `Docs/CardFormation-Usage.md`，技术学习报告见 `Docs/CardFormation-Learning.md`；当前验证以 `ENV_CHECK.md` 最新记录为准。

## 文件阅读顺序

1. `Assets/Data/Battle/FourVsFour.asset`、`Assets/Data/Equipment/`：当前场景使用的 4v4 编成、形态与装备资源；`FirstBattle.asset` 保留为旧首战配置。
2. `Assets/Scripts/Battle/BattleDefinition.cs`、`BattleUnitState.cs`：配置与每局当前状态。
3. `Assets/Scripts/Battle/BattleController.cs`、`BattleActionResolver.cs`：阶段、AP、目标、效果结算、敌方回合、胜负与重开。
4. `Assets/Scripts/Battle/EquipmentDefinition.cs`、`InventoryGridState.cs`：装备数据、占格校验与原子移动。
5. `Assets/Scripts/Battle/BattleView.cs`、`CharacterCardView.cs`、`BattleInfoPanelView.cs`、`BookTooltipView.cs`、`BookTooltipTarget.cs`、`InventoryGridView.cs`、`EquipmentItemView.cs`：战场、书页、浮窗与拖拽交互。

`SampleScene` 的 `BattleController.definition` 使用 `FourVsFour.asset`。角色和物品视图在运行时根据配置创建。书本接线入口是 `Assets/Editor/BookUiAuthoring.cs` 的 `BookUiAuthoring.Apply`（菜单 `Card Cards/Authoring/Apply Book UI`），更新现有界面并保留战斗配置与角色立绘；正常游戏无需执行此菜单。当前接线和运行验收状态见 `ENV_CHECK.md` 的 2026-11-24 记录。

## 交付文档

- `Docs/Plans/2026-11-20-four-actions-implementation.md`：本轮获批规则与实施契约。
- `Docs/FourActions-Learning.md`：本轮技术学习报告与代码阅读顺序。
- `Docs/CharacterArt-Learning.md`：人物素材来源、裁图范围、Inspector 接线与立绘显示学习记录。
- `Docs/BookUI-Learning.md`：书本界面、填充条、正向护盾、浮窗及单装备槽的学习报告与练习。
- `Docs/CardFormation-Usage.md`、`Docs/CardFormation-Learning.md`：卡面重叠、透视、浮动、调参工具及学习报告。
- `Docs/Plans/2026-11-24-book-ui-recovery.md`：本次中断恢复时的只读检查、已完成部分和续做范围。
- `ENV_CHECK.md`：实际编译、Play Mode、Windows 构建、Player 及人工检查结果；以此文件记录的日期和证据为准。

本次卡面优化的 Windows Development 包入口为 `Builds/CardFormation/Windows/CardCards.exe`，中文界面使用 Windows 系统微软雅黑。运行或分发时必须保留整个 `Builds/CardFormation/Windows/` 目录，包括数据目录和依赖文件，不能只复制 exe。当前验证状态及证据见 `ENV_CHECK.md`。
