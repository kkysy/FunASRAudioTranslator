<div align="center">

<img src="src/FunASRAudioTranslator.ico" width="128" height="128" alt="FunASR System Audio Translator图标"/>

# FunASR System Audio Translator

> 本分支版本已将 Windows 实时字幕识别替换为本地 FunASR 日语或英语识别。翻译、上下文、Overlay 与历史记录仍沿用上游实现。
>
> 应用只采集 Windows 默认输出设备的系统音频（WASAPI Loopback），不采集麦克风，也不选择特定应用。既可以连接已有的 FunASR HTTP 服务，也可以按下面的说明配置本地自动启动；服务需提供 `/inference` 接口。默认地址为 `http://127.0.0.1:8177`，可在设置页修改并重连。

> 当前版本可以自动启动本地 FunASR 服务，但需要通过 `FUN_ASR_PYTHON`、`FUN_ASR_SERVER`、`FUN_ASR_MODEL` 和 `FUN_ASR_VAD_MODEL` 配置 Python、服务脚本和模型路径。由本软件启动的服务会在正常退出时终止。没有 CUDA 时可设置 `FUN_ASR_DEVICE=cpu`，默认值为 `cuda:0`。
>
> 设置页可在日语和英语之间切换识别语言。切换后会清空当前未完成的识别片段，但不会改变目标语言或 Ollama 翻译配置。

### *实时系统音频字幕与翻译工具*

[![Build](../../actions/workflows/dotnet-build.yml/badge.svg)](../../actions/workflows/dotnet-build.yml)
[![Windows 11](https://img.shields.io/badge/platform-Windows11-blue?logo=windows11&style=&color=1E9BFA)](https://www.microsoft.com/en-us/software-download/windows11)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue)](LICENSE)

[English](README.md) | **中文**

</div>

## 概述

**✨ FunASR System Audio Translator = 本地 FunASR 语音识别 + 翻译 API ✨**

这是一个 Windows 轻量级工具：通过 WASAPI Loopback 获取默认系统输出，将短音频片段发送给本地 FunASR 服务，再对识别出的字幕进行翻译。

本项目保留上游的翻译、悬浮窗口和历史记录流程，同时将 Windows 实时字幕识别替换为本地 FunASR 服务。正确配置本地识别和翻译服务后，识别与翻译均可在本机完成。

**🚀 快速开始:** 从本仓库的[发布页面](../../releases)下载程序，并按照下面的 FunASR 和 Ollama 配置说明操作。模型文件和 Python 运行环境不会包含在本仓库中。

发布版本如需启用更新检查，请设置 `FUN_ASR_REPOSITORY_URL` 为你的仓库地址；为兼容旧配置，也继续接受 `LIVE_CAPTIONS_REPOSITORY_URL`。两个变量都未设置时会自动关闭更新检查。

<div align="center">
  <img src="images/main-caption.png" alt="FunASR 字幕与翻译主界面" width="90%" />
  <br>
  <em style="font-size:80%">FunASR 字幕与翻译主界面</em>
  <br>
</div>

## 功能特性

- **🔄 本地语音识别**

  通过 WASAPI Loopback 获取 Windows 默认输出，并将短音频片段发送给本地 FunASR 服务，不需要麦克风。

  可在程序设置中选择源语言。本地服务提供 `/health` 和 `/inference` 接口。

- **🎨 现代化界面**

  易于使用且简洁的Fluent UI与现代Windows美学保持一致。

  它可以根据系统设置自动在浅色和深色主题🌓之间切换。

- **🌐 使用 Ollama 本地翻译**

  本版本只使用本地 Ollama 服务，字幕文本不会发送给第三方在线翻译服务。

  <div align="center">

  | API                          | 类型     | 托管方式 |
  |------------------------------|----------|----------|
  | [Ollama](https://ollama.com) | 基于LLM  | 自托管   |

  </div>

  Ollama 在本地运行，并支持不完整字幕和上下文翻译。

- **🪟 悬浮窗口**

  打开无边框、透明的悬浮窗口显示字幕，提供最沉浸式的体验。这对游戏、视频和直播等场景非常有用！

  您甚至可以使其完全嵌入到屏幕中，成为屏幕的一部分。这意味着它不会影响您的任何操作！这对游戏玩家来说再合适不过了。

  您可以在任务栏上打开悬浮窗口，以及调整诸如窗口背景和字幕颜色、字体大小和透明度等参数。极高的可配置性使其能够完全符合您的偏好！

  您可以在设置页的 *Overlay Sentences* 选项调整同时显示的句子数量。

  <div align="center">
    <img src="images/overlay-window.png" alt="FunASR 悬浮字幕窗口" width="90%" />
    <br>
    <em style="font-size:80%">FunASR 悬浮字幕窗口</em>
    <br>
  </div>

- **⚙️ 灵活控制**

  支持窗口置顶和便利的翻译暂停/恢复，并且您可以一键复制文本以便快速分享或保存。

- **📒 历史记录管理**

  记录原文和翻译文本，非常适合会议、讲座和重要讨论。

  您可以将所有记录导出为CSV文件。

  <div align="center">
    <img src="images/history.png" alt="FunASR 翻译历史" width="90%" />
    <br>
    <em style="font-size:80%">FunASR 翻译历史</em>
    <br>
  </div>

- **🎞️ 日志卡片**

  最近的转录记录可以显示为日志卡片，这有助于您更好地把握上下文。

  您可以在主页任务栏上启用它，并在设置页的 *Log Cards* 选项调整卡片数量。

  <div align="center">
    <img src="images/log-cards.png" alt="FunASR 日志卡片" width="90%" />
    <br>
    <em style="font-size:80%">FunASR 日志卡片</em>
    <br>
  </div>

## 系统要求

<div align="center">

| 要求                                                                                                                    | 详情          |
|-----------------------------------------------------------------------------------------------------------------------|-------------|
| <img src="https://img.shields.io/badge/Windows-11%20(22H2+)-0078D6?style=for-the-badge&logo=windows&logoColor=white"> | 系统音频回环采集所需版本 |
| <img src="https://img.shields.io/badge/.NET-8.0+-512BD4?style=for-the-badge&logo=dotnet&logoColor=white">             | 推荐。未在之前版本测试 |

</div>

本工具需要 Windows 11 22H2 或更高版本以支持系统音频回环采集，但不再使用 Windows 实时字幕进行语音识别。

我们建议您安装 **.NET运行时8.0** 或更高版本。如果您无法安装，可以下载 ***with runtime*** 版本，但其文件较大。

<div align="center">
  <p align="center">
    <a href="../../blob/main/README_zh-CN.md">
      <img src="https://img.shields.io/badge/📚_查看我们的Wiki获取详细信息-2ea44f?style=for-the-badge" alt="查看我们的Wiki">
    </a>
  </p>
</div>

## 入门指南

> ⚠️ **重要:** 首次运行 FunASR System Audio Translator 前，您必须完成以下步骤。
>
> 程序直接采集 Windows 默认输出设备的系统音频，不需要麦克风，也不能选择单独的应用程序音频。

### 步骤1: 准备 FunASR

单独安装 FunASR 服务，并从官方仓库下载所需模型：

- ASR 模型：[FunAudioLLM/Fun-ASR-Nano-2512](https://huggingface.co/FunAudioLLM/Fun-ASR-Nano-2512)
- VAD 模型：[funasr/fsmn-vad](https://huggingface.co/funasr/fsmn-vad)
- FunASR 项目与服务端说明：[modelscope/FunASR](https://github.com/modelscope/FunASR)

模型文件会被明确排除在本仓库之外。请将模型放在 Git 工作区外，或将本地模型目录加入 `.gitignore`。

如果希望程序自动启动本地 FunASR 服务，请准备好服务端脚本和模型，并设置以下环境变量：

- `FUN_ASR_PYTHON`: Python 可执行文件
- `FUN_ASR_SERVER`: `fun_asr_server.py`
- `FUN_ASR_MODEL`: 完整 `Fun-ASR-Nano-2512` 模型目录
- `FUN_ASR_VAD_MODEL`: FSMN-VAD 模型目录
- `FUN_ASR_DEVICE`: `cuda:0` 或 `cpu`

### 步骤2: 配置程序

启动程序，打开设置页，确认 FunASR 服务地址，选择日语或英语作为源语言，选择目标语言，并配置本地 Ollama 服务。默认 FunASR 服务地址为 `http://127.0.0.1:8177`。

<div align="center">
  <img src="images/settings.png" alt="FunASR 与 Ollama 设置" width="90%" />
  <br>
  <em style="font-size:80%">FunASR 与 Ollama 设置</em>
  <br>
</div>

配置完成后，通过 Windows 默认输出设备播放音频即可开始识别字幕。

## 截图

以上截图均来自当前本地 FunASR 版本的实际测试。

## 项目统计

### 活动

<div align="center">
  <img src="https://img.shields.io/github/issues?style=for-the-badge&label=Issues&color=yellow" alt="GitHub Issues">
  <img src="https://img.shields.io/github/issues-pr?style=for-the-badge&label=Pull%20Requests&color=blue" alt="GitHub Pull Requests">
  <img src="https://img.shields.io/badge/Discussions-maintainer-configured-orange" alt="Discussions">
  <img src="https://img.shields.io/badge/Last%20commit-maintainer-configured-purple" alt="Last commit">
</div>

### 贡献者

<div align="center">
  <img src="https://img.shields.io/badge/Contributors-maintainer-configured-success" alt="Contributors">
  <br>
  <a href="../../graphs/contributors">
    <span>仓库发布后将显示贡献者统计。</span>
  </a>
</div>

### Star历史

仓库发布后将显示 Star 历史。
