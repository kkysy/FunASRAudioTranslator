<div align="center">

<img src="src/FunASRAudioTranslator.ico" width="128" height="128" alt="FunASR System Audio Translator Icon"/>

# FunASR System Audio Translator

> This fork replaces Windows LiveCaptions recognition with local Japanese or English FunASR recognition. Translation, context handling, overlay, and history continue to use the upstream pipeline.
>
> The application captures only the Windows default system output through WASAPI loopback. It does not capture a microphone or select an individual application. Either connect to an existing FunASR HTTP service exposing `/inference`, or configure the optional local auto-start path below. The default address is `http://127.0.0.1:8177` and can be changed on the settings page.

> The application can start a local FunASR service when its Python environment, server script, and model paths are configured through `FUN_ASR_PYTHON`, `FUN_ASR_SERVER`, `FUN_ASR_MODEL`, and `FUN_ASR_VAD_MODEL`. A runtime started by the application is terminated on normal application exit. Set `FUN_ASR_DEVICE=cpu` when CUDA is unavailable; the default is `cuda:0`.
>
> Recognition language can be switched between Japanese and English on the settings page without changing the target language or Ollama translation configuration.

### *Real-time system-audio caption and translation tool*

[![Build](../../actions/workflows/dotnet-build.yml/badge.svg)](../../actions/workflows/dotnet-build.yml)
[![Windows 11](https://img.shields.io/badge/platform-Windows11-blue?logo=windows11&style=&color=1E9BFA)](https://www.microsoft.com/en-us/software-download/windows11)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue)](LICENSE)

**English** | [中文](README_zh-CN.md)

</div>

## Overview

**✨ FunASR System Audio Translator = local FunASR speech recognition + translation API ✨**

This is a lightweight Windows tool that captures the default system output through WASAPI loopback, sends short audio windows to a local FunASR service, and translates the resulting captions.

The project keeps the upstream translation, overlay, and history workflow while replacing Windows LiveCaptions recognition with a locally hosted FunASR service. Recognition and translation therefore remain on the local machine when both services are configured locally.

**🚀 Quick Start:** Download a package from this repository's [Releases](../../releases), then follow the FunASR and Ollama setup instructions below. Model files and the Python runtime are intentionally not included in this repository.

For release builds, set `FUN_ASR_REPOSITORY_URL` to the repository URL to enable update checks. The legacy `LIVE_CAPTIONS_REPOSITORY_URL` variable is still accepted for compatibility. When neither variable is set, update checks remain disabled.

<div align="center">
  <img src="images/main-caption.png" alt="FunASR caption and translation window" width="90%" />
  <br>
  <em style="font-size:80%">FunASR caption and translation window</em>
  <br>
</div>

## Features

- **🔄 Local speech recognition**

  Captures the Windows default output through WASAPI loopback and sends short audio windows to a local FunASR service. No microphone is required.

  The source language can be selected in the application settings. The local service exposes `/health` and `/inference` endpoints.

- **🎨 Modern Interface**

  Easy-to-use and clean Fluent UI aligned with modern Windows aesthetics.

  It can automatically switches between light and dark themes 🌓 based on the system setting.

- **🌐 Local translation with Ollama**

  Uses only a local Ollama service. No caption text is sent to third-party translation providers by this application.

  <div align="center">

  | API                                  | Type      | Hosting     |
  |--------------------------------------|-----------|-------------|
  | [Ollama](https://ollama.com)         | LLM-based | Self-hosted |

  </div>

  Ollama runs locally and supports incomplete captions and context-aware translation.

- **🪟 Overlay Window**

  Open a borderless, transparent overlay window to display subtitles, providing the most immersive experience. This is very useful for scenarios like gaming, videos, and live streams!

  You can even make it completely embedded into the screen, becoming part of it. This means it won't affect any of your operations at all! This is perfect for gamers.

  You can open the Overlay Window on the taskbar and adjust its parameters such as the window background and subtitle color, font size, and transparency. Extremely high configurability allows it to completely match your preferences!

  You can adjust the number of sentences displayed simultaneously in the *Overlay Sentences* section of the setting page.

  <div align="center">
    <img src="images/overlay-window.png" alt="FunASR overlay window" width="90%" />
    <br>
    <em style="font-size:80%">FunASR overlay window</em>
    <br>
  </div>

- **⚙️ Flexible Controls**

  Supports Always-on-top window and convenient translation pause/resume, and you can copy text with a single click for quick share or saving.

- **📒 History Management**

  Records original and translated text, perfect for meetings, lectures, and important discussions.

  You can export all records as a CSV file.

  <div align="center">
    <img src="images/history.png" alt="FunASR translation history" width="90%" />
    <br>
    <em style="font-size:80%">FunASR translation history</em>
    <br>
  </div>

- **🎞️ Log Cards**

  Recent transcription records can be displayed as Log Cards, which helps you better grasp the context.

  You can enable it on the taskbar of the main page and change the number of cards in the *Log Cards* section of the setting page.

  <div align="center">
    <img src="images/log-cards.png" alt="FunASR log cards" width="90%" />
    <br>
    <em style="font-size:80%">FunASR log cards</em>
    <br>
  </div>

## Before you download

### Choose the correct release file

| Your PC | Recommended asset | What it contains |
|---|---|---|
| Most Intel/AMD Windows PCs | `FunASRAudioTranslator-win-x64-withruntime.exe` | Application and .NET runtime |
| Windows on ARM | `FunASRAudioTranslator-win-arm64-withruntime.exe` | Application and .NET runtime |
| Either architecture, with .NET Desktop Runtime 8 already installed | The matching file **without** `-withruntime` | Smaller application-only build |

`-withruntime` only removes the .NET prerequisite. It does **not** include Python, FunASR, the ASR/VAD models, or an Ollama model.

## Prerequisites

<div align="center">

| Requirement                                                                                                           | Details                                     |
|-----------------------------------------------------------------------------------------------------------------------|---------------------------------------------|
| <img src="https://img.shields.io/badge/Windows-11%20(22H2+)-0078D6?style=for-the-badge&logo=windows&logoColor=white"> | Required for system-audio loopback capture. |
| <img src="https://img.shields.io/badge/.NET-8.0+-512BD4?style=for-the-badge&logo=dotnet&logoColor=white">             | Required only for the release files without `-withruntime`. Install the **.NET Desktop Runtime**, not the SDK. |

</div>

This tool requires Windows 11 22H2 or later for system-audio loopback support. It does not use Windows LiveCaptions for speech recognition.

You also need a local FunASR service and a local Ollama model. They are separate, substantial downloads; the application will not download or configure either one for you. The commands below use **Python 3.10–3.12**. For CUDA, install the PyTorch build matching your GPU and driver from the [official PyTorch selector](https://pytorch.org/get-started/locally/); CPU works with `FUN_ASR_DEVICE=cpu`, but recognition will be slower.

<div align="center">
  <p align="center">
    <a href="../../blob/main/README.md">
      <img src="https://img.shields.io/badge/📚_Read_the_project_documentation-2ea44f?style=for-the-badge" alt="Read the project documentation">
    </a>
  </p>
</div>

## First-time setup

> ⚠️ **IMPORTANT:** You must complete the following steps before running FunASR System Audio Translator for the first time.
>
> The application captures the Windows default output device directly; no microphone or per-application audio selection is required.

### 1. Install the FunASR runtime and models

The repository provides the compatible [`fun_asr_server.py`](fun_asr_server.py). Put it and the models in a stable directory outside the application folder; the following PowerShell example uses `C:\FunASR`.

```powershell
$root = 'C:\FunASR'
$python = "$root\.venv\Scripts\python.exe"
New-Item -ItemType Directory -Force -Path "$root\models" | Out-Null
py -3.12 -m venv "$root\.venv"
& $python -m pip install --upgrade pip

# CPU-only example. For CUDA, use the command from pytorch.org instead.
& $python -m pip install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cpu
& $python -m pip install funasr==1.3.9 huggingface_hub
Invoke-WebRequest https://raw.githubusercontent.com/kkysy/FunASRAudioTranslator/main/fun_asr_server.py -OutFile "$root\fun_asr_server.py"

& $python -c "from huggingface_hub import snapshot_download; snapshot_download(repo_id='FunAudioLLM/Fun-ASR-Nano-2512', local_dir=r'C:\FunASR\models\Fun-ASR-Nano-2512')"
& $python -c "from huggingface_hub import snapshot_download; snapshot_download(repo_id='funasr/fsmn-vad', local_dir=r'C:\FunASR\models\fsmn-vad')"
```

The complete ASR directory must contain `model.pt` (it is over 1 GB). The service and models are intentionally separate from the application release. If you already run another service, it must provide `GET /health` and accept the app's `multipart/form-data` `POST /inference`, returning JSON with a `text` field.

### 2. Let the application start FunASR automatically

Set these **user** environment variables once, then close and reopen the application. The default device is `cuda:0`; use `cpu` if you installed CPU PyTorch or do not have a supported CUDA GPU.

```powershell
setx FUN_ASR_PYTHON "C:\FunASR\.venv\Scripts\python.exe"
setx FUN_ASR_SERVER "C:\FunASR\fun_asr_server.py"
setx FUN_ASR_MODEL "C:\FunASR\models\Fun-ASR-Nano-2512"
setx FUN_ASR_VAD_MODEL "C:\FunASR\models\fsmn-vad"
setx FUN_ASR_DEVICE "cpu"
```

At startup the app checks `http://127.0.0.1:8177/health`; if it is not available, it launches the configured script. An app-owned service stops when the app exits normally. To diagnose the service independently, run this command in a new PowerShell window and wait for `listening on`:

```powershell
& 'C:\FunASR\.venv\Scripts\python.exe' 'C:\FunASR\fun_asr_server.py' --host 127.0.0.1 --port 8177 --model 'C:\FunASR\models\Fun-ASR-Nano-2512' --vad-model 'C:\FunASR\models\fsmn-vad' --device cpu --hub hf --disable-update
```

Then verify it from another window:

```powershell
Invoke-RestMethod http://127.0.0.1:8177/health
```

### 3. Install and configure Ollama

Install Ollama using its [official Windows installer](https://ollama.com/download/windows). It normally runs in the background and exposes `http://localhost:11434`. Download a local instruction model that fits your hardware, then note its exact name:

```powershell
ollama pull <model-name>
ollama ls
```

In the application **Settings**, set the Ollama URL (normally `http://localhost:11434`) and enter that exact installed model name. A model name is deliberately not prefilled, so translation will fail until this is done.

### 4. Configure and run the application

Start the downloaded EXE, open **Settings**, and:

1. Confirm the FunASR address is `http://127.0.0.1:8177`, or enter your existing compatible server address and click **Reconnect FunASR**.
2. Select Japanese or English as the recognition language.
3. Select the target language and complete the Ollama settings above.
4. Play audio through the current Windows **default output** device. There is no microphone or per-application capture setting.

`FUN_ASR_REPOSITORY_URL` is optional and only enables in-app update checks; it is not required to caption or translate.

<div align="center">
  <img src="images/settings.png" alt="FunASR and Ollama settings" width="90%" />
  <br>
  <em style="font-size:80%">FunASR and Ollama settings</em>
  <br>
</div>

After configuration, play audio through the Windows default output device to start captioning.

## Screenshots

The screenshots above were captured from the current local FunASR build after testing.

## Project Stats

### Activity

<div align="center">
  <img src="https://img.shields.io/github/issues?style=for-the-badge&label=Issues&color=yellow" alt="GitHub Issues">
  <img src="https://img.shields.io/github/issues-pr?style=for-the-badge&label=Pull%20Requests&color=blue" alt="GitHub Pull Requests">
  <img src="https://img.shields.io/badge/Discussions-maintainer-configured-orange" alt="Discussions">
  <img src="https://img.shields.io/badge/Last%20commit-maintainer-configured-purple" alt="Last commit">
</div>

### Contributors

<div align="center">
  <img src="https://img.shields.io/badge/Contributors-maintainer-configured-success" alt="Contributors">
  <br>
  <a href="../../graphs/contributors">
    <span>Contributor statistics will be available after the repository is published.</span>
  </a>
</div>

### Star History

Star history will be available after the repository is published.
