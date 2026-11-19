# Card_Cards：首场战斗

Unity 工程目录为本目录，版本 **2022.3.62f3c1**。角色使用矩形色块表示；当前实现范围是 2 对 2 回合制 RPG 战斗。

## 从 Unity 运行

1. 用指定版本打开当前目录，等待包解析和脚本编译。
2. 打开 `Assets/Scenes/SampleScene.unity`，进入 Play Mode。
3. 点击蓝色我方角色 → 点击“静置” → 点击红色敌人。每名角色每个我方回合可静置一次，每次伤害 16。
4. 点击“结束回合”。敌人依次攻击我方存活前排，行动后进入下一我方回合。
5. 胜利或失败后点击“重新开始”。

选目标时可以取消，不消耗静置次数。护甲先于生命吸收伤害，不会每回合恢复。旋转、翻面、调整和行动点消耗留到后续。

## 文件阅读顺序

1. `Assets/Data/Battle/FirstBattle.asset`：Inspector 调整开局编成和数值。
2. `Assets/Scripts/Battle/BattleDefinition.cs`、`BattleUnitState.cs`：配置与每局当前状态。
3. `Assets/Scripts/Battle/BattleController.cs`：静置、敌方回合、胜负与重开。
4. `Assets/Scripts/Battle/BattleView.cs`、`CharacterCardView.cs`：按钮、色块角色、血条与意图。

场景和 Prefab 已保存引用，正常运行无需手工添加组件或拖拽字段。角色实例在运行时根据配置创建。

## 交付文档

- `Docs/Plans/2026-11-20-first-battle-plan.md`：用户已审查的规划与验收路径。
- `Docs/FirstBattle-Learning.md`：本次技术学习报告。
- `ENV_CHECK.md`：本次实际验证结果及剩余事项；以此文件的结果为准。

已生成可审查的 Windows Development 构建：`Builds/Windows/CardCards.exe`。本次 Unity 编译、Play Mode 和 Windows 程序自动验证已通过，实际证据与人工审查步骤见 `ENV_CHECK.md`。中文界面使用 Windows 系统微软雅黑；构建目录需整体保留，不能只复制 exe。
