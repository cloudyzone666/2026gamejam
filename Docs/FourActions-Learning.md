# 四操作与 4v4：核心技术学习

日期：2026-11-20。本文说明本轮实际实现使用的技术；编译、Play Mode、Player 与构建证据以 `ENV_CHECK.md` 的实际记录为准。

## 先读这条执行链

`BattleView` 接收按钮输入 → `BattleController.CanBeginAction` 检查阶段与规则 → `BattleActionResolver.BuildPlan` 收集职业、装备的有序效果 → 玩家选齐所有单体目标 → `BattleController.CommitAction` 一次提交 → `BattleUnitState` 修改数值与形态 → 刷新视图。

选择目标只是准备过程。取消前还没有扣 AP、伤害或姿态变化；提交后立即进入 `Resolving`，同一个点击不能再次提交。这是本轮最值得独立掌握的部分。

| 技术 | 学习层次 | 在项目中的职责与选择原因 |
| --- | --- | --- |
| `ScriptableObject`、`[Serializable]`、数组 | 基础，需掌握 | `BattleDefinition` 和装备资源存策划数据，形态、词条、效果可在 Inspector 配置。避免把每种职业伤害写成按钮分支。 |
| 普通 C# 类、构造函数、只读属性 | 基础，需掌握 | `BattleUnitState` 保存本场生命、护甲、朝向与回合标记，复制形态数据而不改原始资源。重开重新构造状态，避免上一场伤害被写进资产。 |
| `enum`、`switch` 与状态机 | 核心，需独立掌握 | `BattlePhase` 限制整备、选目标、结算、敌方回合等阶段允许的操作；`BattleActionKind`、`EffectKind` 和 `EffectTarget` 让行动、效果、目标规则各有明确含义。 |
| 列表、引用身份与查询方法 | 核心，需独立掌握 | 队伍列表顺序表示队形，角色引用表示同一个人。`GetLivingOrder` 跳过死者得到紧凑位次，`GetDistance` 统一计算距离；UI 和伤害规则不再各算一套。 |
| 快照、分段目标与一次提交 | 值得深入学习 | `BuildPlan` 先把职业和装备效果排好序，`CommitAction` 在提交时计算数值。连续同类单体效果共享目标；跨规则重新选。后段对死亡目标跳过，不自动把溢伤转给别人。 |
| 事务式修改 | 值得深入学习 | 控制器先检查换装阶段、网格归属和存活状态，调用网格原子移动；失败保留原位置，成功才重算相关角色。这样不会出现物品离开来源后因放不下而丢失。 |
| `Mathf.FloorToInt`、`Min`、`Max` | 基础，需掌握 | 攻击向下取整，生命与护甲夹在合法范围。`RecalculateStats` 重算上限；上限下降会夹取当前值，提高上限不会免费治疗。 |
| `IEnumerator`、协程、`StartCoroutine`、`WaitForSecondsRealtime` | 先会读，再深入 | 敌人逐个行动并留间隔。控制器禁用时停协程，重新启用按已提交游标恢复；游标在伤害提交时推进，避免同一敌人恢复后重复攻击。 |
| `EventSystem` 拖拽接口、`PointerEventData` | 基础，需掌握 | `EquipmentItemView` 与 `CharacterCardView` 通过 `IBeginDragHandler`、`IDragHandler`、`IEndDragHandler` 接收拖动，点击和双击另由指针点击接口处理。统一 6 px 阈值避免点击变成拖动。 |
| `RectTransformUtility`、`CanvasGroup` 与拖动虚影 | 基础到进阶 | 将屏幕指针换算成 Canvas/网格坐标，虚影在独立拖拽层跟随；`blocksRaycasts=false` 避免虚影挡住目标，原物品仅降低透明度，松手才提交真实位置。 |
| `Dictionary` 与稳定身份映射 | 值得深入学习 | `BattleView` 按角色对象、`InventoryGridView` 按装备实例映射视图。刷新更新同一个视图，而不是按列表下标冒充另一个角色，也不会在拖动中反复重建物品。 |

## 为什么不再建一套战斗系统

原有配置已支持双方 1–4 人，敌人也按列表轮流行动，所以本轮复用 `BattleController` 和 `BattleUnitState`，只抽出有明确职责的 `BattleActionResolver`。数据驱动用于重复出现的词条与效果，没有引入事件总线、依赖注入或通用技能框架。

90°装备网格转向与180°角色旋转是两条独立规则：前者只影响占格，`BattleUnitState.GetEquipmentEnd` 根据角色朝向决定装备生效端。翻到背面仍保留该朝向，翻回后恢复原来的正面方向。

## 建议阅读顺序与练习

先读 `CanBeginAction`、`BeginAction`、`SelectTarget`、`CommitAction`，再读 `BuildPlan`，最后读 `BattleUnitState.RecalculateStats` 与敌方协程。先理解“何时允许改状态”，再看具体伤害数字。

可独立做一个不依赖 Unity 的小练习：用 A/B/C/D 四个对象表示队伍，实现存活位次查询与“把 D 插到第 2 位”。要求得到 A/D/B/C，B 死亡后编号变为 A1/D2/C3，同时对象引用保持不变。它直接对应本轮队形与距离的核心思路。

UI 阅读顺序：`EquipmentItemView.OnBeginDrag/OnDrag/OnEndDrag` → `InventoryGridView.TryGetCell` → `BattleController.TryMoveEquipment` → `InventoryGridState.TryTransfer`。预览只问“能否放”，原子提交同时更新来源、目标与实例位置；失败时三者都不变。

UI 小练习：在空场景做 3×2 网格和一件 2×1 物品，拖动时显示不挡射线的虚影，合法位置绿框、非法位置红框，松到网格外回到原位。先只写占格判断，再接拖拽接口；不用复制本项目的完整战斗流程。

暂时只需了解 Unity 序列化的底层格式、协程编译成迭代器的细节、完整状态效果系统；本轮没有实现增益/减益持续时间和叠加规则，`BuffDefinition` 仍是预留数据。
