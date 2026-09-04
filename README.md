# Window Memory

[![Build](https://github.com/Jieyijiang/WindowMemory/actions/workflows/build.yml/badge.svg)](https://github.com/Jieyijiang/WindowMemory/actions/workflows/build.yml)

一个轻量、便携的 Windows 窗口与布局记忆工具。所有配置只保存在本机，不联网、不上传窗口标题或使用数据。

![Window Memory 概览](docs/images/overview.png)

## 下载

前往 [Latest Release](https://github.com/Jieyijiang/WindowMemory/releases/latest) 下载 `WindowMemory-版本号-portable.zip`，解压后运行 `WindowMemory.exe`。如果只是使用软件，不要下载 GitHub 自动提供的 “Source code” 压缩包。

## 核心功能

- 记忆单个窗口的位置、尺寸、显示器和最大化状态。
- 在匹配窗口再次出现时自动恢复。
- 将多个窗口保存为布局存档，并通过按钮或全局快捷键整体还原。
- 快捷键可以修改；默认使用 `Ctrl+Alt+Z` 记忆当前活动窗口。
- 支持按程序路径、进程名、窗口类和标题进行精确、包含、开头或正则匹配。
- 显示器分辨率变化后可以按工作区比例适配。
- 托盘运行、暂停自动恢复、可选开机启动。
- EXE、窗口标题栏、任务栏和系统托盘使用统一应用图标；是否最小化到托盘可以自行设置。
- 绿色数据模式：`portable.flag` 存在时，配置保存在程序旁边的 `Data\settings.json`。

## 使用

1. 解压发布包后运行 `WindowMemory.exe`；或直接运行源码目录内的 `dist\WindowMemory.exe`。
2. 在“窗口规则”中选择一个当前窗口，保存后该窗口再次出现时会自动归位。
3. 在“布局存档”中勾选多个窗口，设置名称和快捷键后保存。
4. 以后按布局快捷键，即可一次恢复这些窗口。

布局快捷键可设置为 `Ctrl+1`；程序默认建议 `Ctrl+Alt+1`，主要是为了降低与其他软件快捷键冲突的概率。布局会优先按程序路径和窗口类型识别，只有同一个程序同时保存了多个窗口时，才额外用窗口标题区分。

## 构建

运行 `build.ps1`。项目使用本机 Visual Studio Build Tools 的 Roslyn 编译器，并以系统自带的 .NET Framework/WPF 程序集构建，不依赖第三方 NuGet 包。

GitHub Actions 会在每次推送和 Pull Request 时自动构建并运行自测；推送形如 `v1.0.1` 的版本标签后，会自动创建 GitHub Release，上传便携版 ZIP 和 SHA-256 校验文件。

## 项目结构

- `src/`：窗口识别、位置恢复、快捷键、配置与界面源码。
- `build.ps1`：本地及 GitHub 自动构建入口。
- `.github/workflows/`：持续集成和版本发布流程。
- `dist/`：本地构建结果，不提交进 Git 仓库。

## 参与贡献

问题和功能建议请通过 GitHub Issues 提交；代码修改建议通过独立分支和 Pull Request 提交。具体约定见 `CONTRIBUTING.md`。

## 说明

这是按功能行为独立实现的新程序，没有复制 WinSize3 的源码。项目以 MIT License 开源。

- 当前版只还原已经打开的窗口，不会自动启动缺失的软件。
- 普通权限程序通常不能调整以管理员权限运行的窗口；需要时请让两边处于相同权限级别。
- 本地生成的 EXE 未做代码签名，Windows 首次运行时可能显示安全提醒。
