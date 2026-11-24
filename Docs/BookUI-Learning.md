# 书本界面：技术学习报告

日期：2026-11-24。本次恢复中断的书本界面改造，复用已有战斗、立绘与拖拽系统。本文说明代码职责；实际编译、运行和截图结果以 `ENV_CHECK.md` 本轮记录为准。

## 使用的技术、职责与取舍

| 技术与层次 | 在本项目中的职责 | 为什么这样实现、学习到什么程度 |
| --- | --- | --- |
| uGUI `Image`、Sprite、`Image.Type.Filled`、`fillAmount`：基础，本次核心 | `BattleInfoPanelView.Refresh` 用生命/上限填生命条；`CharacterCardView.UpdateShieldBar` 用护盾/上限填蓝条，`Horizontal` 与 `OriginHorizontal.Left` 决定从左向右显示。攻击没有上限，因此攻击条是固定底纹，攻击数字单独更新 | 复用现有 Canvas 和 Image，资源只负责纹理，代码只更新颜色与比例，不需要动画控制器或绘图插件。需独立掌握分母为 0 时填 0、`Clamp01` 限制到 0–1；不要为攻击条杜撰最大值 |
| `RectTransform` 父子关系、锚点与缩放：基础，本次核心 | 卡面 `CardVisual` 下的立绘和上下职业继承 180° 旋转；`ShieldBar` 与它同级，条和数字保持正向。战场行缩放适配书本上方空间，条宽随卡宽变化 | 由 Transform 层级表达“谁跟着谁转”，比每帧反向旋转文字更直接。需独立掌握父物体旋转/缩放会传给子物体，锚点决定容器变宽时怎样排布 |
| `EventSystem` 指针接口、`PointerEventData`、`CanvasGroup`：基础到进阶，本次核心 | `BookTooltipTarget.OnPointerEnter/Exit` 与 `EquipmentItemView` 触发临时详情；单击固定。`BookTooltipView` 用 `raycastTarget=false` 和 `blocksRaycasts=false` 让浮窗不抢点击，使用 Input System 的鼠标/键盘查询关闭 | 继续使用既有 uGUI 输入链路，避免为详情再增加模态页面。指针进入/离开由 EventSystem 派发；浮窗记录来源与固定状态，离开来源只关闭未固定浮窗。关闭键、外部点击与打开帧保护值得进一步阅读 |
| `RectTransformUtility`、`Text.preferredHeight`、`Mathf.Clamp`：值得深入，先会读和调整 | `BookTooltipView.RefreshContent/Position` 按文字高度调整浮窗，将屏幕坐标换成父 Canvas 的局部坐标，再夹取到可见区域 | 指针是屏幕像素，UI 在父坐标系里布局，必须先换算。当前使用 Screen Space Overlay，所以相机参数为 `null`；先掌握当前坐标关系，暂不扩展其他 Canvas 渲染模式 |
| `ScriptableObject`、普通 C# 实例、只读属性与可空枚举：基础，本次核心 | `EquipmentDefinition` 保存共享原始宽高、部位和效果；`EquipmentInstance` 保存这一件物品的 `Rotated/Grid/X/Y`。`InventoryGridState.AllowedKind` 为 `EquipmentSlotKind?`，空值表示不限制部位的背包；有值表示对应部位槽 | 装备进入单槽时只改摆放规则，不把共享资源改成 1×1，也不丢掉转向。多个实例可以复用同一资源；需独立理解“资源定义”和“本局实例”的区别 |
| 事务式修改、引用身份、`out` 返回信息：值得深入，本次核心 | `InventoryGridState.CanPlace/TryTransfer` 先检查类型、边界与占用，成功后才修改来源、目标及实例位置；控制器验证战斗阶段并在成功后重算角色。`ReferenceEquals` 保证操作的是同一件物品 | 无效落点不先拿走物品，因此不用靠 UI 猜原位置补救。单装备槽只接受一件，背包仍按原尺寸占格。需能独立解释失败时哪些状态必须不变；`out string error` 同时给调用者失败原因 |
| `[SerializeField]`、`SerializedObject` 与显式 Editor 工具：基础接线，工具细节暂时了解 | 序列化字段将书页文字、条、网格与浮窗绑定到脚本；`BookUiAuthoring.Apply` 更新界面和 Prefab 并保留立绘/战斗数据 | 将接线保存为资源，正常游戏读取现成场景。字段改名或删除后旧场景不会自动得到新引用，这是本次中断的关键断点；需会检查 Inspector，暂不必深究 Unity YAML 与序列化内部实现 |

## 按实际代码阅读

1. 先读 `Assets/Scripts/Battle/BattleInfoPanelView.cs` 的 `Refresh`、`InspectUnit`、`ShowItem/ShowEnemy`：选中我方决定左页内容，查看敌人/物品仅影响浮窗。
2. 读 `BookTooltipTarget.OnPointerEnter/OnPointerExit` → `BookTooltipView.Show/HidePreview/Update/Position`：追踪一次悬停、单击固定、外部点击关闭。注意 `openedFrame` 防止打开浮窗的同一次点击马上关闭它。
3. 读 `CharacterCardView.UpdateShieldBar/UpdateVisualWidth/UpdatePose`，对照 Prefab 层级：只有 `visualRoot` 旋转，`ShieldBar` 留在根层。比例来自已有 `BattleUnitState`，显示代码不扣血、不扣护盾。
4. 读 `EquipmentDefinition` → `EquipmentInstance.Width/Height` → `InventoryGridState.CanPlace/TryTransfer` → `BattleController.TryMoveEquipment/TryAutoMoveEquipment`：装备在槽中只显示一个位置，但回到背包仍是原占地；自动放置先尝试当前朝向。
5. 最后看 `Assets/Editor/BookUiAuthoring.cs` 的 `Apply`。菜单为 `Card Cards/Authoring/Apply Book UI`，它是保存接线的辅助入口，正常玩游戏不需要重复执行。

## 接线与独立练习

书本场景中，`Canvas/BookInfo` 的 `BattleInfoPanelView` 绑定姓名、生命/攻击数字与填充图、三类装备槽、浮窗；`Canvas/BackpackGrid` 显示共享背包。`Canvas/BookTooltip` 挂 `BookTooltipView`，绑定 `TooltipPanel`、`Title`、`Body`。`CharacterCard/ShieldBar` 下的 `Frame`、`Fill` 与 `Armor` 绑定到 `armorTrack`、`armorFill`、`armorText`，与 `CardVisual` 同级。接线与验证进度见本轮环境记录。

做一个独立的空场景练习，不复制战斗系统：

1. 在 Canvas 中做可旋转的矩形头像，旁边做生命条，下方做与头像同级的护盾条；把头像转 180°，确认护盾仍正向，设置 `0/0` 不报错。
2. 给按钮添加临时悬停浮窗；单击固定、离开不关，Esc 或外部点击关闭。将浮窗放到按钮上方，确认按钮仍收到点击。
3. 用一个数据资源定义 2×1 物品，创建两个实例；把其中一件转成 1×2，再放进一个单装备槽。确认另一实例及资源宽高没变。
4. 将该物品移回 3×2 背包，保留转向；再尝试放进已占满槽或背包外。确认失败时物品引用、来源、坐标、朝向全部不变。

本次最值得独立掌握的是：UI 父子变换、输入与显示分离、共享定义与实例状态分离，以及先检查再提交的装备移动。暂时只需了解 Editor 批处理、Sprite 导入元数据、序列化文件格式及迭代器内部机制；不用为这次界面加入新架构或插件。
