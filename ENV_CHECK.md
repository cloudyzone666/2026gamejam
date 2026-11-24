# 四操作、4v4 与装备 UI：交付及验证记录

## 最新恢复记录：2026-11-24 书本界面

依据：恢复检查找回原对话中用户对书本界面的要求，以及 2026-11-23 的三项确认；本次用户要求先只读审计、保留正确实现，再从未完成处续做。下方 2026-11-23 立绘和 2026-11-20 四操作记录保持原样，它们的通过结果不能替代书本界面的验证。

本轮原始范围：

- 以用户的 `UI.png` 书本作为下方界面主体，保留既有角色立绘与卡面样式；上方战场适配空间，参考图上方四块暂不实现。
- 左页显示我方姓名、生命条及攻击条，四行动为 2×2；左下手持、身着、佩戴各一个装备槽，右页背包改为 5×3。
- 装备原始占地及 90° 转向在穿戴时保留；装备槽只容纳一件对应类型物品，不把其背包尺寸改为 1×1。
- 物品和敌人详情使用悬停/单击固定浮窗；离开来源对象后关闭临时浮窗，点击外部或 Esc 关闭固定浮窗，不切换左页我方角色，也不拦截正常操作。
- 移除旧日志、三页签及旧信息面板，保留回合、AP、取消、开始/结束回合与重开；双方卡下新增正向蓝色护盾条，卡面旋转不带动护盾显示。
- 复用现有战斗规则、装备移动与立绘；攻击没有上限，攻击条只作视觉底纹，实际值以数字表示。

中断时的检查结果（2026-11-24 本次只读审计）：

| 项目 | 已确认状态 | 来源与边界 |
| --- | --- | --- |
| 事前快照 | 相对快照有四个既有脚本改变，无快照文件缺失 | `Logs/BookUI-baseline.json` 与当前文件比对；检查阶段未覆盖或回滚文件 |
| 已写代码 | 新书页视图字段、物品/敌人悬停与浮窗脚本已写；单装备槽、5×3 背包已有实现 | `BattleInfoPanelView`、`BattleView`、`EquipmentItemView`、`InventoryGridView`、`BattleUnitState`、`BattleController` 及两个新 Tooltip 脚本的源码检查；不代表已运行 |
| 已有图片 | BookSurface、StatBarFill、StatBarFrame 已生成，属于恢复前已完成资源 | `Assets/Art/UI/`；不重复生成 |
| 接线断点 | 场景仍保存旧页签字段，新生命/攻击/浮窗字段未绑定，会造成空引用；旧 authoring 与验证器仍使用已删除字段 | `SampleScene.unity`、`FourActionsAuthoring.cs`、`BattleInfoPanelView.cs` 与验证源码 |
| 未完成资源 | UI 目录、三张 PNG、两个 Tooltip 脚本共六项缺 `.meta`；正向护盾条尚未接入 | 资源/GUID 与 Prefab 检查；未发现零字节文件、冲突标记或现有引用丢失 |
| 历史验证 | 最后成功结果属于 2026-11-23 立绘集成，早于书本代码修改 | `Logs/CharacterArt-Import.log`、`Logs/CharacterArt-Build.log`、`Logs/CharacterArt-PlayerVerification.json`；不能用于宣称本轮通过 |

恢复续做针对这些断点补齐书本场景与 Prefab 接线、缺失 `.meta`、正向盾条及验证器；保留已完成图片、现有立绘和正确代码。学习报告见 `Docs/BookUI-Learning.md`。没有以重跑旧整场景生成命令来恢复任务。

**恢复续做已完成，场景接线已保存，无需手工挂脚本或拖字段。** 本轮使用 Unity 2022.3.62f3c1，实际核对 URP 14.0.12、Input System 1.14.2、`activeInputHandler=1`；未安装依赖或更改 Project Settings。恢复检查详见 `Docs/Plans/2026-11-24-book-ui-recovery.md`。

| 本次实际检查 | 结果及边界 | 新证据 |
| --- | --- | --- |
| Unity 导入与编译 | 书本三张图片导入为 Sprite Single，缺失 meta 已补齐，场景与 Prefab 保存；后续最终 Windows 脚本编译通过 | `Logs/BookUI-Import.log`、`Logs/BookUI-Build.log` |
| Editor Play Mode | 33 项通过，捕获运行错误 0；完成 UTC 04:43:55。Null 图形设备、640×480，不证明视觉效果。此结果早于最终边线和验证器输入隔离调整，最终版本由下方 Player 验证 | `Logs/BookUI-PlayModeVerification.json`、`Logs/BookUI-PlayMode.log` |
| 最终 Windows 构建 | Succeeded，0 errors / 0 warnings；完成 UTC 04:58:58。独立输出至 `Builds/BookUI/Windows/CardCards.exe` | `Logs/BookUI-WindowsBuild.json`、`Logs/BookUI-Build.log` |
| 最终 Windows 实际运行 | 34 项通过，捕获运行错误 0；Direct3D11、1280×720、Microsoft YaHei；完成 UTC 04:59:41 | `Logs/BookUI-PlayerVerification.json`、`Logs/BookUI-Player.log` |
| 最终独立静态审查 | 58 项 meta、325 个序列化对象的引用与必需字段绑定通过；两个可选空字段已明确列出；67 个受保护配置、数据和既有美术文件与本次恢复快照逐字节一致，问题 0 项 | `Logs/BookUI-static-review.json`、`Logs/BookUI-Recovery-20261124/baseline.json` |
| 画面复核 | 实际查看最终 7 张截图：整备、物品浮窗、穿戴、选目标、整顿、胜利、失败。5×3 细边线可见且保留纸纹，左页生命/攻击及三槽可读，浮窗文字未裁切，旋转卡下护盾保持正向 | 上述 Player 报告 `screenshots` 内文件，均位于 `Logs/BookUI-Screenshots/`，本轮时间后缀为 `045933` 至 `045941` |
| 人工试玩 | 尚待用户操作与评价手感、美术；自动输入和截图不能代替人工试玩 | 下方旧人工验收步骤可作为战斗回归参考，书本操作见 README |

本次覆盖：5×3 与三单件槽；满槽、错部位、非法落点、满背包拒绝移动时状态不变；原 2×1 装备穿脱保持实例、尺寸和转向；敌人/物品真实悬停、离开、单击固定、Esc/外部点击关闭与拖动关闭；禁用行动悬停显示原因；四个书页按钮、取消、选目标与整顿退出；护盾比例及正向显示；完整胜负与两条重开路径。

验证中修正了两类问题：半透明格子使用 Outline 会产生整块深色叠影，现改为四条不拦截输入的细 Image；自动输入检查结束后恢复实体鼠标，会干扰随后浮窗检查，现将整套显式验证包在输入隔离的 `try/finally` 中，结束或失败时恢复设备。正常游戏启动不运行验证，也不隔离设备。失败的初次编译、Play Mode 和两次 Player 报告已保留为 `Logs/BookUI-Import-compilefailure.log`、`Logs/BookUI-PlayMode-firstfailure.*`、`Logs/BookUI-Player-firstfailure.*`、`Logs/BookUI-Player-secondfailure.*`，不作为通过证据。

较早两次构建各有一条 `-nographics` 无法更新 Ambient/Reflection Probes 的警告；最终增量构建报告为 0 warnings，实际 D3D11 画面也已检查。构建自动补写的 URP 资源已留存于 `Logs/BookUI-UniversalRP-final-build-generated.asset`；项目中的原资源按本次恢复快照逐字节保留，没有以 Git 回滚其他工作。旧 `Builds/Windows/` 包保留，本次运行或分发必须保留整个 `Builds/BookUI/Windows/` 目录。

本轮可观察验收：1280×720 下书本与双方队列互不遮挡；左页姓名、生命、攻击和三个单装备槽可读；右页恰为 5×3；四行动、取消及回合切换可用；物品/敌人悬停及固定浮窗可关闭且不影响拖拽；满槽、非法落点与背包放不下时物品不丢失；2×1 装备进槽再卸下保留实际尺寸和合法朝向；卡面旋转后护盾条与数字保持正向；完整胜负与重开维持原行为。

## 最新更新：2026-11-23 人物素材导入

依据：用户提供怪物设定PNG与双向剑客JPG，要求裁出骷髅蜘蛛并导入角色素材。蜘蛛裁框为原图像素 `(80,94,550,356)`，去除外侧连通白底；剑客保留双向全幅，等比缩至604×1024并去外侧白底。素材位于 `Assets/Art/Characters/`。

四名我方均绑定剑客Sprite，四名敌人均绑定蜘蛛Sprite；`CharacterCard.prefab/CardVisual/Portrait` 已绑定到视图，随原CardVisual旋转。上/下端文字有深色衬底，敌人图像与生命/护甲/血条分开。镜影没有独立素材，暂共用剑客图并加紫色。当前场景无需手工接线；角色数值、意图与规则未改。

| 本次实际检查 | 结果 | 来源 |
| --- | --- | --- |
| Unity导入与编译 | 通过，Unity2022.3.62f3c1实际导入为Sprite Single并保存Prefab/数据引用 | Logs/CharacterArt-Import.log |
| Windows构建 | Succeeded，0 errors / 1 warning | Logs/CharacterArt-Build.log、Logs/BattleWindowsBuild.json；本次完成UTC04:58:23 |
| Windows实际运行 | 30项现有检查通过，捕获运行错误0，Direct3D11、1280×720 | Logs/CharacterArt-PlayerVerification.json、Logs/CharacterArt-Player.log；本次完成UTC04:59:41 |
| 视觉复核 | 实际查看整备和整顿截图：蜘蛛完整可见，双向角色可见，180°旋转及镜影紫色表现正常，文字/生命条可读 | Player报告内01-preparation和05-reorganization截图 |
| 静态引用与保护范围 | meta、GUID、Inspector及资源引用审查通过；本轮事前快照中的场景、FirstBattle、Packages、ProjectSettings、AGENTS与恢复后的URP逐字节一致 | Logs/CharacterArt-static-review.json、Logs/CharacterArt-baseline.json |

唯一构建警告：`-nographics`批处理无法更新Ambient/Reflection Probes。当前卡面是Screen Space Overlay UI，实际Windows画面已检查；没有因此修改光照或Project Settings。未另跑本轮Editor Play Mode；上表Windows Player是本次运行证据，旧Play Mode结果属于下方2026-11-20记录。人工鼠标试玩与美术效果评价仍待用户。

构建对URP的补写版本保留为 `Logs/CharacterArt-UniversalRP-build-generated.asset`，原资源已按本轮快照恢复。上轮构建/Play Mode/静态报告备份在 `Logs/CharacterArt-PreviousResults/`，下方历史表的构建报告应读该备份；当前 `Logs/BattleWindowsBuild.json` 是本次立绘包报告。

学习与换图说明见 `Docs/CharacterArt-Learning.md`。原桌面素材未改；本轮未安装依赖、提交或推送。

日期：2026-11-20（北京时间）。本轮依据用户审查的方案与“开始实施”授权。旧首版2v2的结果是历史记录，不能替代下列本轮验证。

## 交付与入口

已完成单场“战前整备 → 4v4 → 胜负 → 重开”。四种操作与职业/装备词条、共享AP、三形态、真实射程、紧凑位次、角色/物品/敌人信息页、装备三区和共享背包已集成。

使用 Unity **2022.3.62f3c1** 运行 `Assets/Scenes/SampleScene.unity`，或启动 `Builds/Windows/CardCards.exe`。场景和Prefab引用已保存，无需手工挂脚本或拖字段。Windows包必须保留整个 `Builds/Windows/` 目录。

详细操作见 `README.md`，本轮数值和规则取舍见 `Docs/FourActions-Data.md`，技术学习报告见 `Docs/FourActions-Learning.md`。四人复用剑客职业作为验证阵容，射程为2/3/4/5；当前美术为占位视觉，尚需用户评价平衡与手感。

## 实际环境

| 项目 | 本次核对结果 | 依据 |
| --- | --- | --- |
| 工程根 | D:/cby/project/2026gamejam本身，也是Git工作区 | 文件结构、Git状态 |
| Unity | 2022.3.62f3c1 | ProjectVersion.txt、Editor/Player日志 |
| URP | 14.0.12 | manifest.json、packages-lock.json |
| 输入 | New Input System 1.14.2，activeInputHandler=1 | 包配置、ProjectSettings.asset、实际UI输入验证 |
| 窗口 | 1280×720，Windowed | 实际Windows Player断言及截图 |
| 字体 | Microsoft YaHei | Player报告和截图；使用本机系统字体 |
| 构建 | StandaloneWindows64 Development | 构建报告；未执行Release发布 |

本轮未安装Package、切换版本或主动修改Project Settings。Unity构建会补写UniversalRP.asset的预过滤/序列化字段；构建生成版本保留在 `Logs/FourActions-UniversalRP-build-generated.asset`，最终项目资源已逐字节恢复原快照。静态报告核对受保护配置；SceneTemplateSettings.json缺少事前快照，只记录当前SHA，不声称它未变化。

## 本次实际验证

| 检查 | 结果及边界 | 证据 |
| --- | --- | --- |
| 静态资源审查 | 46个资源/目录的meta齐全，GUID唯一；342个序列化对象的引用及Inspector字段检查通过；原FirstBattle未改 | Logs/FourActions-static-review.json |
| Unity编译 | 实际Editor和Windows脚本编译通过 | Logs/FourActions-PlayMode.log、Logs/FourActions-Build.log |
| 真实Play Mode | 23项检查通过，捕获运行错误0；批处理为Null图形设备、640×480，不能证明渲染效果 | Logs/BattleVerification.json |
| Windows构建 | Succeeded，0 errors / 0 warnings | Logs/BattleWindowsBuild.json |
| Windows程序运行 | 24项检查通过，捕获运行错误0；Direct3D11、1280×720、字体与输入链路通过 | Logs/FourActions-PlayerVerification.json、Logs/FourActions-Player.log |
| 可见画面复核 | 实际查看Player的整备、物品详情、装备、射程、整顿、胜利、失败七张截图；最终文字修正再次截图检查 | Player报告内screenshots的绝对路径 |
| 人工试玩 | 待用户操作；自动输入与截图不代表人工玩过 | 下方验收步骤 |

最终报告的completedUtc为准，转换北京时间需加8小时。Play Mode与Windows Player都通过同一套核心规则和输入检查，Player额外验证字形与截图。最后一次状态文字布局修正后重新完成Windows编译、构建和Player全套验证；Editor Play Mode的23项结果在该文字修正前取得，核心规则未再改变。

覆盖内容：

- AP4、动作费用、重复旋转、背面限制、恢复朝向、静置收手、取消不变更、重复提交保护。
- 职业/装备取值快照，静置16、旋转12/9，形态及装备上限下降夹取、上升不补满。
- 多段敌我目标选齐才提交；伤害、治疗、加甲；死亡跳过后段且无溢伤转移。
- 距离1–7、按角色限制射程、死亡紧凑、敌方射程及逐击重新选前排。
- 全队整顿、最后1AP等待主动退出、单人生存换装、静置后换位换装、阶段与死者权限。
- 网格类型、边界、碰撞、转向、失败回退、实例唯一性与外来网格拒绝。
- 虚拟鼠标经Input System → EventSystem实际选人、按钮、拖装备、角色插入换位；双击穿脱、右键/R转向、非法拖放回原位、单击只预览。
- 完整4v4胜利、只结束回合走到失败、两种终局重开、整备和背包重置。

验证输入来自虚拟鼠标/键盘。检查期间克隆InputSettings、隔离UI Actions设备并允许无焦点输入，结束后恢复；不会写入输入项目配置。截图证据来自可见Windows窗口；早期隐藏窗口的黑图和失败运行不作为通过依据。

已修正初轮验证发现的队列布局/缩放、装备双击和R转向、信息行裁切、敌人装饰叠数值、降上限误报受击、状态文字叠阵营标题等问题。最终结果以最新成功报告为依据。

## 场景及Inspector接线

- SampleScene/BattleRoot：BattleController引用FourVsFour.asset、场景BattleView；正常敌方间隔0.55秒，测试缩短值仅在内存中。
- Canvas：BattleView引用双方容器、CharacterCard Prefab、四操作/取消/结束/重开按钮、AP与提示、信息面板、背包网格、拖拽层和终局面板。CanvasScaler为1280×720、Expand。
- 信息面板：BattleInfoPanelView绑定三个书签、对应页面与文本，以及手持2×1、身着2×1、佩戴2×2网格；共享背包8×3。
- CharacterCard Prefab：按钮、卡面视觉根、中心对称姓名/职业、敌人血条与意图、外侧位次/距离引用齐全。卡面180°旋转，外侧文字保持可读。
- EventSystem：InputSystemUIInputModule，实际输入链路已验证。角色和装备视图按配置及稳定实例身份在运行时创建。

未新增Layer、碰撞或动画事件。`Assets/Editor/FourActionsAuthoring.cs` 只在显式菜单/批处理调用时写场景与数据，导入或正常运行不会执行；无需重复生成场景，避免覆盖后续手工调整。

## 范围与复跑

本轮复用原战斗脚本；新增效果解析器、装备/网格数据、信息与装备视图、六件装备资源和FourVsFour配置及配套meta。旧FirstBattle保留。用户已有AGENTS.md修改和策划案文件保留，未执行提交或推送。

C#与Markdown的差异空白检查通过；Unity自身生成的scene/prefab含空字段行尾空格，保留序列化格式，完整git diff --check仍会提示这些行，未把它报告为全量通过。

复跑：Unity菜单 `Card Cards/Verification/Run Play Mode Verification`；构建菜单 `Card Cards/Verification/Build Windows Development Player`。开发包参数 `--battle-smoke --battle-smoke-report <报告绝对路径>` 会运行检查并自动退出。普通启动不运行验证器。

尚未实现：30战推进、奖励、存档、随机词缀及状态/Buff持续/叠加玩法。伤害/治疗/加甲支持五种目标规则，但当前内容只投放敌方单体伤害和自身恢复。系统字体跨机器分发、Release构建与最终美术尚未验收。

## 用户可观察验收

1. 打开场景或程序：四名我方、四只敌人，前排靠中央，整备状态显示完整信息栏与8×3背包。
2. 选角色，拖动或双击装备穿脱；右键/R转向，非法位置回原位。拖角色只移动该角色，其他人相对顺序不变。
3. 开始战斗，选择前排剑客→旋转：只能选择射程内目标；取消不扣点，提交扣1点并卡面旋转。翻面扣2点，背面只能翻回。
4. 静置后该角色本回合收手；进入整顿扣1点，仍可换位换装。最后1点进入整顿时应等待主动退出才由敌方行动。
5. 检查护甲吸伤、死亡压缩、敌人意图与攻击；战斗胜负后重开应回到初始整备、装备和背包。
6. 评价中文可读性、选择提示、拖拽手感、行动节奏与数值，记录下一轮需要调整的内容。
