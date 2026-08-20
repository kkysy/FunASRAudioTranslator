#!/usr/bin/env python3
"""Small local Fun-ASR HTTP server used by FunASR System Audio Translator."""

from __future__ import annotations

import argparse
import json
import os
import re
import sys
import tempfile
import wave
import warnings
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Any

with warnings.catch_warnings():
    warnings.simplefilter("ignore", DeprecationWarning)
    import cgi


LANGUAGE_MAP = {
    "zh": "中文", "zh-cn": "中文", "cn": "中文", "chinese": "中文", "中文": "中文",
    "en": "英文", "english": "英文", "英文": "英文",
    "ja": "日文", "jp": "日文", "japanese": "日文", "日文": "日文", "日本語": "日文",
}
SPECIAL_TOKEN_PATTERN = re.compile(r"<\|[^|<>]+\|>")


def parse_bool(value: Any, default: bool = False) -> bool:
    if value is None:
        return default
    if isinstance(value, bool):
        return value
    return str(value).strip().lower() in {"1", "true", "yes", "y", "on"}


def first_field(form: cgi.FieldStorage, name: str, default: str | None = None) -> str | None:
    if name not in form:
        return default
    field = form[name]
    if isinstance(field, list):
        field = field[0]
    value = field.value
    return value.decode("utf-8", errors="replace") if isinstance(value, bytes) else str(value)


def normalize_language(value: str | None) -> str | None:
    return LANGUAGE_MAP.get(value.strip().lower(), value.strip()) if value else None


def get_wav_duration(path: str) -> float | None:
    try:
        with wave.open(path, "rb") as wav_file:
            return wav_file.getnframes() / wav_file.getframerate()
    except (wave.Error, ZeroDivisionError):
        return None


def install_tiktoken_special_token_patch() -> None:
    """Allow Fun-ASR timestamp code to process generated control tokens."""
    try:
        import tiktoken.core
    except ImportError:
        return
    encoding_cls = tiktoken.core.Encoding
    original_encode = encoding_cls.encode
    if getattr(original_encode, "_fun_asr_special_token_patch", False):
        return

    def encode_with_fun_asr_specials(self, text, *args, **kwargs):
        if not args and "allowed_special" not in kwargs and "disallowed_special" not in kwargs:
            kwargs["disallowed_special"] = ()
        return original_encode(self, text, *args, **kwargs)

    encode_with_fun_asr_specials._fun_asr_special_token_patch = True
    encoding_cls.encode = encode_with_fun_asr_specials


def strip_asr_control_tokens(text: Any) -> str:
    return SPECIAL_TOKEN_PATTERN.sub("", str(text or "")).strip()


class FunAsrState:
    def __init__(self, model_path: str, device: str, hub: str, vad_model: str | None,
                 vad_max_single_segment_time: int, remote_code: str | None,
                 disable_update: bool) -> None:
        self.model_path = model_path
        self.device = device
        self.hub = hub
        self.vad_model = vad_model
        self.vad_max_single_segment_time = vad_max_single_segment_time
        self.remote_code = remote_code
        self.disable_update = disable_update
        self.model = None

    def load(self) -> None:
        install_tiktoken_special_token_patch()
        try:
            from funasr import AutoModel
        except ImportError as error:
            raise RuntimeError("FunASR is not installed. Run: pip install funasr==1.3.9") from error
        kwargs: dict[str, Any] = {
            "model": self.model_path, "trust_remote_code": True, "device": self.device,
            "hub": self.hub, "disable_update": self.disable_update,
        }
        if self.remote_code:
            kwargs["remote_code"] = self.remote_code
        if self.vad_model:
            kwargs["vad_model"] = self.vad_model
            kwargs["vad_kwargs"] = {"max_single_segment_time": self.vad_max_single_segment_time}
        self.model = AutoModel(**kwargs)


class Handler(BaseHTTPRequestHandler):
    server_version = "FunAsrHTTP/0.1"

    def do_GET(self) -> None:
        if self.path in {"/", "/health"}:
            self.send_json(200, {"status": "ok", "engine": "fun-asr-nano"})
        else:
            self.send_json(404, {"error": "Not found"})

    def do_POST(self) -> None:
        if self.path.split("?", 1)[0] != "/inference":
            self.send_json(404, {"error": "Not found"})
            return
        content_type = self.headers.get("content-type", "")
        if "multipart/form-data" not in content_type:
            self.send_json(400, {"error": "Expected multipart/form-data"})
            return
        try:
            form = cgi.FieldStorage(fp=self.rfile, headers=self.headers, environ={
                "REQUEST_METHOD": "POST", "CONTENT_TYPE": content_type,
                "CONTENT_LENGTH": self.headers.get("content-length", "0"),
            })
            if "file" not in form:
                self.send_json(400, {"error": "Missing file field"})
                return
            file_field = form["file"]
            if isinstance(file_field, list):
                file_field = file_field[0]
            audio_bytes = file_field.file.read()
            if not audio_bytes:
                self.send_json(400, {"error": "Empty audio file"})
                return
            with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as audio_file:
                audio_file.write(audio_bytes)
                audio_path = audio_file.name
            try:
                state: FunAsrState = self.server.state  # type: ignore[attr-defined]
                result = state.model.generate(
                    input=[audio_path], cache={}, batch_size=1,
                    itn=parse_bool(first_field(form, "itn"), default=True),
                    language=normalize_language(first_field(form, "language")),
                )
                item = normalize_result_item(result)
                text = strip_asr_control_tokens(item.get("text"))
                segments = build_segments(item, text, audio_path)
            finally:
                Path(audio_path).unlink(missing_ok=True)
            self.send_json(200, {"text": text, "segments": segments, "engine": "fun-asr-nano"})
        except Exception as error:  # noqa: BLE001
            print(f"[fun-asr] inference error: {error}", file=sys.stderr, flush=True)
            self.send_json(500, {"error": str(error)})

    def log_message(self, format: str, *args: Any) -> None:
        print(f"[fun-asr] {self.address_string()} - {format % args}", file=sys.stderr, flush=True)

    def send_json(self, status_code: int, payload: dict[str, Any]) -> None:
        body = json.dumps(payload, ensure_ascii=False).encode("utf-8")
        self.send_response(status_code)
        self.send_header("content-type", "application/json; charset=utf-8")
        self.send_header("content-length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)


def normalize_result_item(result: Any) -> dict[str, Any]:
    if isinstance(result, list) and result:
        first = result[0]
        if isinstance(first, dict):
            return first
        if isinstance(first, list) and first and isinstance(first[0], dict):
            return first[0]
    return result if isinstance(result, dict) else {"text": str(result or "")}


def build_segments(item: dict[str, Any], text: str, audio_path: str) -> list[dict[str, Any]]:
    sentence_info = item.get("sentence_info")
    if isinstance(sentence_info, list) and sentence_info:
        segments = []
        for sentence in sentence_info:
            if isinstance(sentence, dict):
                segments.append({"start": millis_to_seconds(sentence.get("start")),
                                 "end": millis_to_seconds(sentence.get("end")),
                                 "text": strip_asr_control_tokens(sentence.get("text") or sentence.get("sentence")),
                                 "speaker": sentence.get("spk")})
        if segments:
            return segments
    return [{"start": 0, "end": get_wav_duration(audio_path), "text": text}] if text else []


def millis_to_seconds(value: Any) -> float | None:
    try:
        return float(value) / 1000 if value is not None else None
    except (TypeError, ValueError):
        return None


def main() -> int:
    parser = argparse.ArgumentParser(description="Fun-ASR-Nano /inference server")
    parser.add_argument("--host", default=os.environ.get("FUN_ASR_HOST", "127.0.0.1"))
    parser.add_argument("--port", type=int, default=int(os.environ.get("FUN_ASR_PORT", "8177")))
    parser.add_argument("--model", default=os.environ.get("FUN_ASR_MODEL", "FunAudioLLM/Fun-ASR-Nano-2512"))
    parser.add_argument("--device", default=os.environ.get("FUN_ASR_DEVICE", "cpu"))
    parser.add_argument("--hub", default=os.environ.get("FUN_ASR_HUB", "hf"))
    parser.add_argument("--vad-model", default=os.environ.get("FUN_ASR_VAD_MODEL", "funasr/fsmn-vad"))
    parser.add_argument("--vad-max-single-segment-time", type=int,
                        default=int(os.environ.get("FUN_ASR_VAD_MAX_SINGLE_SEGMENT_TIME", "30000")))
    parser.add_argument("--remote-code", default=os.environ.get("FUN_ASR_REMOTE_CODE"))
    parser.add_argument("--disable-update", action="store_true",
                        default=parse_bool(os.environ.get("FUN_ASR_DISABLE_UPDATE")))
    args = parser.parse_args()
    state = FunAsrState(args.model, args.device, args.hub, args.vad_model or None,
                        args.vad_max_single_segment_time, args.remote_code, args.disable_update)
    print(f"[fun-asr] loading model={state.model_path} device={state.device} hub={state.hub}", file=sys.stderr, flush=True)
    state.load()
    server = ThreadingHTTPServer((args.host, args.port), Handler)
    server.state = state  # type: ignore[attr-defined]
    print(f"[fun-asr] listening on http://{args.host}:{args.port}", file=sys.stderr, flush=True)
    server.serve_forever()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
