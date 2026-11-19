# 首版战斗：技术学习报告

日期：2026-11-20。本文以本次五个战斗脚本为依据，解释实现方式；不记录或替代 Unity 编译、Play Mode、人工试玩及 Windows 构建结果，实际验证请查看交付记录。

## 先理解数据流

`BattleDefinition` 保存 Inspector 可编辑的开局配置；`BattleController.InitializeBattle()` 每次开局创建新的 `BattleUnitState`，战斗扣血只修改这些运行状态。敌人意图数组、装备格与 buff 占位也被复制，避免一场战斗改变配置或污染下一次重开。

`BattleView` 和 `CharacterCardView` 读取运行状态，显示色块、血条、护甲、意图与按钮。点击通过回调交给控制器，伤害、行动资格与胜负统一由控制器决定。卡面只是角色显示方式。

## 本次实际使用的技术

| 技术与层次 | 在项目中的职责 | 为什么采用；学习深度 |
| --- | --- | --- |
| **基础：MonoBehaviour、SerializeField、Inspector 引用** | `BattleController` 挂在战斗根对象，`BattleView` 接入 Canvas，`CharacterCardView` 接入角色 Prefab；字段连接配置、文本和按钮 | Unity 常规组件足以完成本场战斗；应独立掌握组件挂载、引用绑定及 `Start` 初始化顺序 |
| **核心：ScriptableObject、Serializable 类、构造函数** | `BattleDefinition` 保存配置，普通 C# 类 `BattleUnitState` 保存每局状态；构造函数计算初始血甲与静置伤害 | 数值可以在 Inspector 调整，状态无需挂到 GameObject；值得深入理解引用类型与深拷贝，避免把配置当当前生命扣除 |
| **核心：enum、属性、List、IReadOnlyList、条件判断** | `BattlePhase` 限定当前操作；状态属性限制外部写入；列表保留初始顺序，界面按 `IsAlive` 过滤并重新编号 | 1–4 人编成直接遍历即可；先掌握每次行动的条件与状态变化，暂不需要事件总线或通用状态机框架 |
| **基础且核心：Mathf.FloorToInt、Min、Max** | 静置伤害为 `floor(10 × 1) + 6 = 16`；伤害先扣护甲，剩余扣生命且不低于 0 | 明确表达现有规则；应能手算“30 血/2 甲受 16 伤害后为 16 血/0 甲” |
| **基础：UGUI Image/Text/Button、Prefab、RectTransform、CanvasScaler** | 两排色块角色、数值、生命条和操作界面；`Instantiate` 创建角色卡，`fillAmount` 显示血量比例 | 复用项目已有 UGUI，之后可替换美术；重点掌握 Prefab 引用、布局及 `Bind` 先解绑再注册点击，重开不会重复监听 |
| **值得深入：Coroutine、IEnumerator、yield、WaitForSecondsRealtime** | `RunEnemyTurn()` 分步展示敌方出手；每击重新找存活前排，并立即检查胜负 | 单个协程满足短顺序流程；`yield` 让出执行并在后续帧恢复，不是创建线程；应理解阶段锁、协程句柄与禁用/启用恢复 |
| **暂时了解：New Input System UI 模块、动态字体、URP 与平台构建** | EventSystem 的 UI 模块传递鼠标点击；`Font.CreateDynamicFontFromOSFont` 提供系统中文字体；场景沿用已有渲染管线 | 首版不需要自定义 InputAction、Shader 或字体系统；了解点击传递路径与目标机器字体依赖，运行效果以实际验证为准 |

## 最值得独立掌握的方法

源码位于 `Assets/Scripts/Battle/`，建议依次阅读：

1. `BattleDefinition.TryValidate()`：在开局前拒绝人数、生命或意图等无效配置。
2. `BattleUnitState` 两个构造函数与 `ApplyDamage()`：看清初始值如何复制、护甲如何吸收伤害；装备与 buff 当前只留结构，不参与计算。
3. `BattleController.InitializeBattle()` → `SelectTarget()`：合法提交才标记静置已用；先进入 `Resolving`，再修改数值，防止连续点击重复结算。界面置灰之外，控制器仍校验阶段和目标。
4. `RunEnemyTurn()`、`FirstAlive()`、`TryFinishBattle()`：死敌跳过，每击重新获取前排；胜负立即中止后续行动。`OnDisable()` 停止协程，保留已提交位置，`OnEnable()` 继续剩余行动。
5. `RestartBattle()`、`BattleView.RefreshCards()` 与 `CharacterCardView.Bind()`：终局才可重开；重建状态、复用界面实例、重新绑定点击是三个不同职责。

本版最核心的是**配置与状态分离、行动只提交一次、每击后处理死亡、重开恢复全部状态**。协程原理可随后深入；装备/buff 占位、系统字体和渲染管线目前理解用途即可。旋转、翻面、调整和 AP 消耗尚未实现，不需要为本版预学这些系统。

## 可选独立练习

在单独练习工程中，用普通 C# 类建立“30 生命、2 护甲”的单位，写一个受伤方法，再提供“伤害 16”和“重新开始”两个按钮。连续受伤两次应死亡，生命不为负；重开恢复 30/2，初始配置始终不变。先独立完成数值与重开，再尝试添加一个枚举，禁止死亡后继续受伤。无需复制当前项目架构或安装额外包。
