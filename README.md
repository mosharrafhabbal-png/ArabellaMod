# 艾拉贝拉 · ArabellaMod

中文 | [English](README.en.md)

> 从卡厄思来到尖塔的神秘而又迷人的女人。

**艾拉贝拉**是《Slay the Spire 2》的自定义角色 Mod，基于 RitsuLib 开发。她是佣兵团「黑角宿一」的团长，以蛇鞭、裂创与不断回流的卡牌展开战斗。

玩法围绕**消耗、回收与连续触发**展开：让卡牌进入消耗牌堆，触发放纵效果；通过掌控将它们收回手中，再触发感应。在这个循环中积累欲火、提升兴奋，为后续连招提供资源。

## 项目内容

- 艾拉贝拉角色、专属卡池、初始遗物及其升级形态。
- 掌控、放纵、感应三类卡牌机制，以及欲火、兴奋两种战斗资源。
- 裂创持续伤害、衍生牌与围绕消耗回收展开的卡牌组合。
- 角色序列帧动画、背身动画、卡牌与战斗特效、人物语音和音效。
- 欲火计数器、兴奋进度显示、掌控进度及卡牌高亮反馈。
- 简体中文和英文本地化，以及主菜单和暂停菜单中的 Mod 设置页。

当前 Mod 版本为 `0.1.0`，仍在开发和调整中。部分辅助文案与资源配置仍保留模板内容，具体效果以当前代码和游戏内表现为准。

## 角色与初始配置

| 项目 | 内容 |
| --- | --- |
| 角色 | 艾拉贝拉 / Arabella |
| 初始生命 | 75 |
| 初始金币 | 99 |
| 初始牌组 | 4 张斜切打击、4 张猩红领域、1 张纵情一瞬、1 张初次驯服、1 张蛇蝎天性 |
| 初始遗物 | 猩红蛇鞭 |
| 作者 | Kalipei |

**猩红蛇鞭**：每当打出并结算一张能力牌后，可以选择至多 1 张其他手牌将其消耗，为放纵与掌控循环创造起点。

## 核心机制

| 机制 | 效果 |
| --- | --- |
| **掌控 X** | 此牌在消耗牌堆中时，独立累计打出其他卡牌实际花费的能量与欲火。达到 X 点后回到手牌，本回合费用降低 1 点，最低为 0。自身打出费用不计入进度，每次重新进入消耗牌堆时进度归零。 |
| **放纵** | 此牌被消耗后，触发牌面注明的额外效果。 |
| **感应** | 此牌通过任意方式进入手牌时触发，包括战斗开始时的初始抽牌。 |
| **裂创** | 敌方回合结束时，造成等同于层数的无视格挡伤害，随后层数减半并向下取整；其他能力可以进一步改变其收益。 |

### 欲火

欲火是艾拉贝拉的额外战斗资源，初始上限为 **9 点**。每当卡牌从消耗牌堆中移出时获得 1 点，也可以通过卡牌和能力效果获得或消耗。

每场战斗一次，拥有欲火时受到致命伤害，会消耗全部欲火并避免死亡，每实际消耗 1 点恢复 3 点生命。每次触发后，本局游戏的欲火上限永久降低 1 点，最低为 0；降低后的上限跨战斗和读档保留，新开一局恢复为 9 点。

### 兴奋

兴奋上限为 **10 点**。每实际消耗 1 点欲火，或每次触发放纵，获得 1 点兴奋；每次受到未被格挡的伤害后失去 2 点。

| 阈值 | 状态 | 收益 |
| --- | --- | --- |
| 3 点 | 躁动 | 每次触发感应，获得 2 点格挡。 |
| 6 点 | 沉迷 | 每次因掌控回手，该牌费用额外降低 1 点，最低为 0；此减费可跨回合叠加。兴奋低于 6 点时清除全部沉迷减费，重新达到阈值后从零累计。 |
| 10 点 | 亢奋 | 进入时抽 2 张牌；打牌时每缺少 1 点能量，可以额外支付 1 点欲火代替。 |

## 安装与运行

运行 Mod 需要游戏、RitsuLib，以及构建后的艾拉贝拉文件。当前仓库中的 [Mod 清单](ArabellaMod.json) 声明最低游戏版本为 `0.110.0`，RitsuLib 依赖版本为 `0.5.20`。这些是本项目的配置值，其他版本的兼容性需要实际验证。

将构建产物放入游戏安装目录，目录结构如下：

```text
Slay the Spire 2/
└── mods/
    ├── STS2-RitsuLib/          # RitsuLib 框架文件
    └── ArabellaMod/
        ├── ArabellaMod.dll
        ├── ArabellaMod.json
        └── ArabellaMod.pck
```

启动游戏并确认框架与 Mod 正常加载后，在角色选择界面选择艾拉贝拉。仓库提供源码和资源，直接下载源码 ZIP 后仍需构建，才能得到上述运行文件。

## 从源码构建

项目使用 **.NET 9、C# 13、Godot.NET.Sdk 4.5.1**。完整资源导出还需要与项目匹配的 MegaDot/Godot .NET 可执行文件及导出环境；本机路径填写方式见 [local.props.template](local.props.template)。

克隆项目并创建本机配置：

```powershell
git clone https://github.com/mosharrafhabbal-png/ArabellaMod.git
cd ArabellaMod
Copy-Item .\local.props.template .\local.props
```

编辑 `local.props`，设置以下路径：

| 字段 | 用途 |
| --- | --- |
| `Sts2Dir` | 游戏安装目录。 |
| `Sts2DataDir` | 游戏程序集目录，默认是游戏目录下的 `data_sts2_windows_x86_64`。 |
| `GodotExe` | 用于导出 `.pck` 的 MegaDot/Godot .NET 可执行文件。 |
| `RitsuLibDeployDir` | 可选，RitsuLib 框架的部署目录。 |

执行完整构建：

```powershell
dotnet build .\ArabellaMod.csproj
```

默认构建会编译 C#，将 DLL 和 Mod 清单复制到 `mods/ArabellaMod/`，并调用 Godot 向同一目录导出 PCK。

只检查 C# 编译、跳过本 Mod 的复制和 PCK 导出时，可使用：

```powershell
dotnet build .\ArabellaMod.csproj /p:RunPckExport=false /p:CopyModOnBuild=false
```

此命令仍需要有效的游戏程序集路径。项目当前以 `Version="*"` 引用 RitsuLib，构建时会将实际解析到的依赖版本同步到 Mod 清单；更新依赖后，请核对游戏版本和运行环境。

## 画面与音效设置

在主菜单或局内暂停菜单的艾拉贝拉设置页，可以调整：

- 背身动画开关与显示大小（80%～150%）。
- 序列帧特效开关。
- 人物语音、特效声音的独立开关与音量（50%～150%）。

设置会保存，并提供恢复默认选项。

## 源码导航

| 路径 | 内容 |
| --- | --- |
| [ArabellaModCode/Cards](ArabellaModCode/Cards) | 卡牌效果与卡图配置。 |
| [ArabellaModCode/Mechanics](ArabellaModCode/Mechanics) | 掌控、放纵、感应、欲火、兴奋及相关 UI。 |
| [ArabellaModCode/Powers](ArabellaModCode/Powers) | 裂创和其他战斗状态。 |
| [ArabellaModCode/Relics](ArabellaModCode/Relics) | 猩红蛇鞭及其升级形态。 |
| [ArabellaModCode/Animation](ArabellaModCode/Animation) | 角色动画与播放控制。 |
| [ArabellaModCode/Audio](ArabellaModCode/Audio)、[Vfx](ArabellaModCode/Vfx) | 语音、音效及战斗特效。 |
| [ArabellaMod](ArabellaMod) | Godot 场景、图像、音频和本地化资源。 |
| [tools](tools) | 部署辅助脚本与机制回归检查。 |

已有的局部回归检查可在项目根目录运行：

```powershell
powershell -File .\tools\card-rewrite-tests\Run.ps1
powershell -File .\tools\indulgent-nature-tests\Run.ps1
powershell -File .\tools\lust-survival-tests\Run.ps1
```

这些检查覆盖部分卡牌结算、纵欲本相和欲火救命逻辑，完整效果仍需进入游戏验证。

## 仓库范围

仓库保存项目源码、美术音频资源、构建配置与开发工具。卡牌设计表格所在的 `design/` 目录仅保留在本地，不上传；`local.props`、构建缓存、`artifacts/` 和 `tmp/` 同样由 `.gitignore` 排除。

项目基于 RitsuLib 及其 Mod 模板开发，感谢框架与工具作者的工作。
