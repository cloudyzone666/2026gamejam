# 首场战斗交付与验证记录

日期：2026-11-20（北京时间）。本文件记录本次实际执行结果，供用户审查；规划中的待办不等于验证结果。

## 交付状态

已完成一场默认 2 对 2 的色块战斗，场景与 Prefab 接线完成。使用 Unity **2022.3.62f3c1** 打开当前目录，运行 `Assets/Scenes/SampleScene.unity` 即可游玩，无需手工挂脚本或绑定字段。Windows Development 构建位于 `Builds/Windows/CardCards.exe`，复制时需保留整个 `Builds/Windows/` 目录。

玩法：蓝色角色 → 静置 → 红色敌人；每名角色每回合一次、伤害 16。取消不消耗次数，点击“结束回合”才进入敌方行动；护甲先吸收伤害且不自动恢复。敌方按顺序攻击存活前排，意图各自循环 6/6/9；每击处理死亡并重新选前排。胜负后可重开。存活角色紧凑排列，距离仅显示。配置与布局支持双方各 1–4 人。

旋转、翻面、调整、共享行动点玩法、装备与 buff 效果、肉鸽推进留待后续；装备格与 buff 仅保留数据结构。本次没有外部美术依赖。

## 实际环境与授权配置

工程根为 `D:/cby/project/2026gamejam` 本身，`Assets/`、`Packages/`、`ProjectSettings/` 直接位于根目录。当前目录不是 Git 仓库，未执行提交或推送。

| 项目 | 实际状态 | 来源 |
| --- | --- | --- |
| Unity | 2022.3.62f3c1 | ProjectVersion.txt；Editor/Player 日志 |
| URP | 14.0.12，沿用现有管线 | manifest.json、packages-lock.json |
| UGUI / TMP | 1.0.0 / 3.0.7，已有依赖 | 包配置 |
| 输入 | New Input System 1.14.2；activeInputHandler = 1 | 用户单独授权；包解析和 ProjectSettings.asset |
| 窗口 | 1280×720，Windowed | 用户单独授权；设置及实际 Player 断言 |
| 字体 | Microsoft YaHei（当前 Windows 系统字体） | Player 报告及截图 |
| 构建入口 | 已启用的 SampleScene | EditorBuildSettings.asset；构建入口 |

## 本次验证结果

| 检查 | 结果与边界 | 证据 |
| --- | --- | --- |
| 静态检查 | 通过；资源及目录 `.meta` 完备，GUID 合法且唯一，154 个序列化对象的本地引用及项目资源引用有效，Inspector 字段一致 | `Logs/FirstBattle-static-review.json` |
| Unity 编译 | 通过；实际 Editor 编译及后续 Windows 编译完成 | `Logs/FirstBattle-Compile.log`、`Logs/FirstBattle-PlayMode.log`、`Logs/FirstBattle-Build.log` |
| 真实 Play Mode | 10 项检查通过，捕获的运行错误 0；批处理 Editor 的 Game View 为 640×480，不能用于证明目标窗口尺寸 | `Logs/BattleVerification.json`，完成时间 01:32:46 |
| Windows 构建 | Succeeded；0 errors / 0 warnings；StandaloneWindows64 Development | `Logs/BattleWindowsBuild.json`，最终构建完成时间 01:39:18 |
| Windows 构建产物运行 | 10 项检查通过，捕获的运行错误 0；实际 1280×720 窗口、Direct3D 11、中文字体及输入链路通过 | `Logs/BattlePlayerVerification.json`、`Logs/FirstBattle-Player.log`，完成时间 01:39:46 |
| 可见画面检查 | 实际读取下列 5 张 Player 截图；中文、血条、选中/目标边框、终局面板及 4 对 4 排列可见，无明显裁切或重叠 | `Logs/BattleScreenshots/` |
| 普通启动 | 已不带验证参数启动，日志显示正常初始化与退出；没有执行该次启动的人工点击验收 | `Logs/FirstBattle-NormalLaunch.log` |
| 人工体验审查 | 待用户执行；自动验证与截图不替代操作手感、节奏和趣味性判断 | 下方审查步骤 |

Play Mode 与 Player 覆盖：初始数值、虚拟鼠标经 New Input System → EventSystem → 按钮选人与静置、取消、非法输入、重复提交、每人一次、16 点伤害与护甲、距离与死亡压缩、死敌跳过、手动回合推进、两回合最短胜利、终局锁定、胜败重开、敌方 6/6/9 循环、只结束回合的第七回合失败、每击重新选前排，以及内存中的 1 对 1 / 4 对 4 配置完整战斗。配置资产在验证前后保持不变。

截图：

- `01-initial-20261119-173944-534.png`：默认开局。
- `02-target-selection-20261119-173944-809.png`：选目标、角色/敌人边框和距离。
- `03-victory-20261119-173944-925.png`：第二回合胜利与重开按钮。
- `04-defeat-20261119-173945-674.png`：第七回合失败与重开按钮。
- `05-four-vs-four-20261119-173945-897.png`：四人编成布局。

文件名使用 UTC，因此日期部分为前一天；上述报告时间已换算为北京时间。

### 验证限制与已修正的问题

输入检查注入的是虚拟鼠标事件，不是人工物理鼠标试玩。批处理 Editor 无焦点，因此验证临时克隆 InputSettings 并允许输入；Player 检查期间将 UI Actions 限制到验证鼠标，结束后恢复原设置和设备筛选。这些修改只存在于验证运行内存中，没有保存到项目输入配置。

初次 Play Mode 发现血条内置 Sprite 引用错误，已改用项目内 `Assets/UI/White.png`，之后完整重跑无运行错误。初次 Player 验证遭遇真实与虚拟鼠标竞争，且隐藏窗口截图全黑；修复验证设备隔离和截图同步后，已重新构建并在可见窗口中重跑通过。Editor 批处理截图没有生成，最终视觉证据全部来自 Windows Player。早期失败不作为通过依据，以上结果取自最终报告。

Windows 当前依赖系统微软雅黑；字体没有随包分发。最终交付是供本轮审查的 Development 构建，画面有开发标记；未执行面向发布的 Release 构建或其他机器兼容性验收。

## 场景与 Inspector 接线

- `SampleScene/BattleRoot`：`BattleController` 绑定 `FirstBattle.asset` 和 `BattleView`，敌方行动间隔 0.55 秒。
- 场景 Canvas：`BattleView` 绑定角色 Prefab、双方容器、回合/提示文本、静置/取消/结束按钮与结果面板/重开按钮；CanvasScaler 参考分辨率 1280×720。
- `CharacterCard.prefab`：`CharacterCardView` 绑定背景、边框、Button、各文本与生命条；生命条使用本地白色 Sprite 和水平 Filled Image。
- EventSystem：`InputSystemUIInputModule` 在启用时分配包内默认 UI Actions，已由实际输入链路验证。

没有新增 Layer、碰撞或动画事件。运行时仅实例化角色卡视图；场景、Canvas 与按钮均已保存为项目资源。

## 改动范围与复查入口

基线包含 42 个原文件，最终只有以下 5 个原文件变化，无原文件删除：`AGENTS.md`（真实根目录与当前静置范围）、`Assets/Scenes/SampleScene.unity`、`Packages/manifest.json`、`Packages/packages-lock.json`、`ProjectSettings/ProjectSettings.asset`。

新增主要资源：5 个 `Assets/Scripts/Battle/` 脚本、`FirstBattle.asset`、`CharacterCard.prefab`、白色 Sprite，以及各自 `.meta`；另含 Editor/Development 验证入口、README、规划与学习报告。验证器仅在 Editor 菜单或带 `--battle-smoke` 的开发包启动时运行，普通启动不会创建验证对象。资源写入辅助脚本留在 `Logs/`，不参与运行，也无需再次执行。

构建曾使 URP 资源补写 32 行默认/预过滤数据。已将构建生成版本保留在 `Logs/FirstBattle-UniversalRP-build-generated.asset`，并将项目资源逐字节恢复到原基线；SHA256 对照见静态报告，未保留渲染配置改动。

复跑入口：Unity 菜单 `Card Cards/Verification/Run Play Mode Verification`；构建菜单 `Card Cards/Verification/Build Windows Development Player`。Player 自动检查参数为 `--battle-smoke --battle-smoke-report <报告绝对路径>`；会自动结束程序。测试用缩短间隔只修改内存，正常游戏仍用场景间隔。

## 用户审查步骤（未代替用户执行）

1. 打开 `Builds/Windows/CardCards.exe`，或在指定 Unity 版本运行 SampleScene。
2. 两名角色依次静置攻击同一只敌人：第一击后目标应为 16 生命/0 护甲，第二击死亡。
3. 点击结束回合，确认存活敌人攻击前排；下一回合击败另一只，出现胜利并重开。
4. 重开后只点击结束回合，应在第七回合失败；再次重开恢复开局血甲、意图与资格。
5. 检查取消、连续点击、中文可读性、边框提示和敌方出手节奏，记录希望调整的体验。

代码学习顺序与本次实现取舍见 `Docs/FirstBattle-Learning.md`。本轮功能与场景集成已完成，等待用户最终审查。
