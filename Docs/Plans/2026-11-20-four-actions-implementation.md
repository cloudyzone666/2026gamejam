# 四操作、4v4、信息与背包：获批实施记录

日期：2026-11-20（北京时间）。用户先审查聊天中的分阶段方案，再明确要求“开始实施”。本轮按该方案实现单场整备→4v4→胜负重开，不扩展30战、奖励、存档或随机成长。

## 规则与验收基线

- 三形态数值使用策划案9.1：剑士8/1/1/1，静置-2、旋转0、翻面-3；守卫4/0.9/1.2/1.5，静置1、旋转3、翻面0；镜影6/1.1/0.9/1，翻面1。字段顺序为初始攻击/攻击倍率/生命倍率/护甲倍率。
- 攻击距离按角色固定配置，真实限制伤害目标。四人验证阵容复用现有职业，射程2/3/4/5；敌人射程4，保持可攻击存活前排。
- 静置0AP，每回合一次，使用后本回合不能静置/旋转/翻面；旋转1AP，翻面2AP；每回合共享4AP。
- 背面不能静置/旋转；翻回恢复原朝向。旋转先触发下端，再改变朝向。结算先职业后装备；装备不触发翻面词条。
- 词条数值在行动提交时取快照：旋转职业使用下端形态攻击，但装备属性使用切换前生效端，装备旋转词条取下端。翻回正面两职业分别使用各自形态攻击、装备属性仍使用保留朝向。
- 当前生命/护甲在形态或换装降低上限时夹取；提高上限不补满。出生装备装好后才初始化满值。
- 单体伤害不溢出转移；目标死亡后跳过对其后续单体伤害。取消所有目标选择不改状态或扣点；每次行动只提交一次。
- 整顿为全队状态，进入扣1AP，退出免费，最后一人仍可换装；静置角色可被换位/换装。最后1AP进入整顿时等主动退出才交棒。
- 三个装备区分别2×1、2×1、2×2，共8格；共享背包8×3。90°网格转向只改占地，180°卡面旋转改变生效端。
- 六件装备未定义部分为本轮初始实现值，须在数据说明中明确标注，不能归为策划原文。

## 并行实现接口（本轮内部协作契约）

命名空间沿用 `CardCards.Battle`。核心负责 BattleDefinition/BattleUnitState/BattleController/BattleActionResolver；装备模型负责 EquipmentDefinition/InventoryGridState；UI负责视图；主代理负责Editor场景资源、验证和交付文档。

### 数据与装备

- `BattleActionKind { Standby, Rotate, Flip, Reorganize }`。
- `EffectKind { Damage, Heal, GainArmor }`；`EffectTarget { Self, AllySingle, AllyAll, EnemySingle, EnemyAll }`。
- `[Serializable] EffectDefinition`：`kind`, `target`, `amount`, `scaling`（职业默认1、装备默认0.5；伤害为floor(形态攻击*scaling)+amount，最低0）。
- `[Serializable] FormDefinition`：`name`, `initialAttack`, `attackMultiplier`, `healthMultiplier`, `armorMultiplier`, `standby`, `rotation`, `flip`（后三项为EffectDefinition数组）。
- `AllyDefinition` 保留现有基础字段与旧字段供现有代码过渡；新增 `frontTop`, `frontBottom`, `back` 三个FormDefinition，`startingEquipment` 为EquipmentDefinition数组。
- `BattleDefinition` 新增 `backpackEquipment`（EquipmentDefinition数组）；敌人新增 `attackRange`。
- `EquipmentDefinition : ScriptableObject`：`displayName`, `kind`（已有EquipmentSlotKind）, `width`, `height`, `canRotate`, `description`, `top`, `bottom`。
- `[Serializable] EquipmentEndDefinition`：`attack`, `health`, `armor`, `standby`, `rotation`（EffectDefinition数组）。
- `EquipmentInstance`：构造EquipmentDefinition；只读 `Id`, `Definition`, `Rotated`, `Width`, `Height`；Rotated仅由成功网格提交修改。
- `InventoryPlacement`：只读 `Item`, `X`, `Y`。
- `InventoryGridState`：构造 `(int width, int height, string name, EquipmentSlotKind? allowedKind = null, BattleUnitState owner = null)`；只读 `Width`, `Height`, `Name`, `AllowedKind`, `Owner`, `Placements`。
- 网格方法：`CanPlace(item,x,y,rotated,out string error)`（同网格移动忽略该item现有占格）；`TryAdd(item,x,y,bool rotated,out error)`（初始化）；`TryFindSpace(item,bool rotated,out int x,out int y)`；`Find(item)`返回placement或null；静态 `TryTransfer(source,destination,item,x,y,bool rotated,out error)` 原子移动/转向，来源物品必须存在，失败不改变任何状态。

### 运行状态与控制器

- `BattleUnitState`：保留Name/IsEnemy/CurrentHealth/MaxHealth/CurrentArmor/MaxArmor/AttackRange/StandbyDamage/HasUsedStandby/IsAlive/NextIntentDamage；新增 `IsBack`, `IsRotated`, `HasChangedFormThisTurn`, `CurrentForm`, `Attack`, `EquipmentGrids`（IReadOnlyList<InventoryGridState>）, `FrontTop`, `FrontBottom`, `BackForm`。公开 `GetEquipmentEnd(item)` 和 `RecalculateStats()`；新回合清全部动作标记。
- `BattlePhase` 保留旧枚举名称，并新增 `Preparation`, `Reorganizing`。
- `BattleController` 保留原序列化字段 `definition`, `view`, `enemyActionDelay`。公开 `Phase`, `TurnNumber`, `ActionPoints`, `SelectedAlly`, `ActiveEnemy`, `LastMessage`, `Allies`, `Enemies`, `Backpack`, `CanModifyEquipment`, `RecentLogs`（IReadOnlyList<string>，最近6条）。
- 行动方法：`SelectAlly(unit)`, `BeginStandby()`, `bool BeginAction(kind)`, `bool CanBeginAction(kind,out string reason)`, `bool CanTarget(unit)`, `SelectTarget(unit)`, `CancelSelection()`, `StartBattle()`, `EndPlayerTurn()`, `ExitReorganization()`, `RestartBattle()`。
- 查询：`GetLivingOrder(unit)`从1开始，死亡/不存在0；`GetDistance(a,b)`；`CanReach(a,b)`；`PendingTargetRule`。
- 站位：`bool TryMoveAlly(unit,int livingIndex,out string error)`，livingIndex从0开始；仅整备或整顿允许，移除后插入，其他相对顺序不变。
- 装备：`bool TryMoveEquipment(item,source,destination,x,y,rotated,out error)`；`bool TryRotateEquipment(item,grid,out error)`；`bool TryAutoMoveEquipment(item,source,BattleUnitState recipient,out error)`，背包→recipient对应装备区，装备→背包，first-fit。所有入口验证phase/所属当前局/owner存活，成功重算影响角色并刷新UI。
- 结算后调用 `view.Refresh()`；UI绑定不在规则层拼接额外副作用。保留Start正常初始化为Preparation。

### UI序列化接线

- `BattleView`沿用旧字段，新增 `rotateButton`, `flipButton`, `adjustButton`, `actionPointText`, `logText`, `infoPanel`, `backpackGrid`, `dragLayer`。`Initialize(controller)`, `Refresh()`, `GetInterfaceFont()`保持公开静态字体入口。提供 `Controller`, `DragLayer`, `InfoPanel`只读属性供子视图。
- `BattleInfoPanelView`字段：`characterTabButton`, `itemTabButton`, `enemyTabButton`, `characterPage`, `itemPage`, `enemyPage`, `characterDetailsText`, `itemDetailsText`, `enemyDetailsText`, `handheldGrid`, `wornGrid`, `accessoryGrid`。`Initialize(BattleView)`, `Refresh()`, `InspectUnit(unit)`, `InspectItem(item)`, `ShowCharacterPage()`；保留被查看角色，不受行动选择覆盖。
- `InventoryGridView`：`Initialize(BattleView)`, `Bind(InventoryGridState)`, `Refresh()`，只读 `Grid`, `View`；可运行时生成格子与物品，root只author各网格根RectTransform。网格40px、gap5px。
- `EquipmentItemView`由网格运行时创建；不得每帧重建拖动中物品。统一使用Canvas拖拽层；CanModifyEquipment=false时只可预览。
- `CharacterCardView`沿用原字段，新增 `visualRoot`, `topProfessionText`, `bottomProfessionText`。可新增控制器/拖动初始化接口但需告知root。完整卡面视觉旋转，位次/距离/目标提示保持外侧可读。

## 计划验证

静态/编译；Play Mode实际点击与拖拽；AP/形态/多段/距离/死亡/整顿与装备事务检查；Windows构建与Player截图。结果在执行后填写ENV_CHECK，不能提前宣称通过。
