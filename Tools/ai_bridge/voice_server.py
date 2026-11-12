"""CyberDefender: Kernel Clash — голосовые команды.

Микрофон -> Vosk (офлайн, ограниченная грамматика) -> UDP в Unity.
Распознаёт речь только пока Unity прислала "LISTEN 1" (шкала Kernel Energy заполнена).
Протокол: "V <VoiceCommand> <confidence>", раз в секунду "HB".
"""
import argparse
import json
import os
import queue
import socket
import threading
import time

import sounddevice as sd
from vosk import KaldiRecognizer, Model, SetLogLevel

PHRASES = {
    "purge system": "PurgeSystem",
    "purge": "PurgeSystem",
    "firewall activate": "FirewallActivate",
    "firewall": "FirewallActivate",
    "isolate": "Isolate",
}
SAMPLE_RATE = 16000
COMMAND_COOLDOWN = 1.0


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--model", default=os.path.join(here, "model"), help="папка модели Vosk")
    ap.add_argument("--host", default="127.0.0.1")
    ap.add_argument("--port", type=int, default=5066)
    ap.add_argument("--control-port", type=int, default=5067)
    ap.add_argument("--device", default=None, help="индекс или имя микрофона (python -m sounddevice)")
    ap.add_argument("--always-listen", action="store_true", help="слушать всегда (для отладки)")
    args = ap.parse_args()

    if not os.path.isdir(args.model):
        raise SystemExit(f"Модель Vosk не найдена: {args.model}\n"
                         "Запустите start_ai_bridge.bat — он скачает vosk-model-small-en-us-0.15.")

    device = int(args.device) if args.device is not None and args.device.isdigit() else args.device

    SetLogLevel(-1)
    model = Model(args.model)
    # Грамматика из нескольких фраз: распознавание быстрее и почти без ложных срабатываний
    recognizer = KaldiRecognizer(model, SAMPLE_RATE, json.dumps(list(PHRASES) + ["[unk]"]))

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    target = (args.host, args.port)

    listening = threading.Event()
    reset_requested = threading.Event()
    if args.always_listen:
        listening.set()

    def control_loop():
        ctl = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        ctl.bind(("127.0.0.1", args.control_port))
        while True:
            data, _ = ctl.recvfrom(64)
            msg = data.decode("ascii", "ignore").strip()
            if msg == "LISTEN 1" and not listening.is_set():
                listening.set()
                reset_requested.set()
                print("[voice] энергия 100% — слушаю")
            elif msg == "LISTEN 0" and listening.is_set() and not args.always_listen:
                listening.clear()
                print("[voice] микрофон на паузе")

    threading.Thread(target=control_loop, daemon=True).start()

    audio = queue.Queue()

    def on_audio(indata, frames, time_info, status):
        audio.put(bytes(indata))

    last_fire = 0.0
    last_heartbeat = 0.0

    def fire(text, conf):
        command = PHRASES.get(text.strip())
        if command is None:
            return False
        sock.sendto(f"V {command} {conf:.2f}".encode("ascii"), target)
        print(f"[voice] {text!r} -> {command}")
        return True

    # 50 мс на блок — компромисс между задержкой и нагрузкой
    with sd.RawInputStream(samplerate=SAMPLE_RATE, blocksize=800, dtype="int16",
                           channels=1, device=device, callback=on_audio):
        print(f"[voice] микрофон -> udp://{args.host}:{args.port}, управление на :{args.control_port}. Ctrl+C — выход.")
        try:
            while True:
                try:
                    chunk = audio.get(timeout=0.5)
                except queue.Empty:
                    chunk = None

                now = time.monotonic()
                if now - last_heartbeat >= 1.0:
                    sock.sendto(b"HB", target)
                    last_heartbeat = now

                if chunk is None or not listening.is_set():
                    continue

                if reset_requested.is_set():
                    recognizer.Reset()
                    reset_requested.clear()

                if recognizer.AcceptWaveform(chunk):
                    text = json.loads(recognizer.Result()).get("text", "")
                    if now - last_fire > COMMAND_COOLDOWN and fire(text, 0.95):
                        last_fire = now
                else:
                    # Частичный результат — срабатываем, не дожидаясь паузы после фразы
                    partial = json.loads(recognizer.PartialResult()).get("partial", "")
                    if partial and now - last_fire > COMMAND_COOLDOWN and fire(partial, 0.85):
                        last_fire = now
                        recognizer.Reset()
        except KeyboardInterrupt:
            pass


if __name__ == "__main__":
    main()
