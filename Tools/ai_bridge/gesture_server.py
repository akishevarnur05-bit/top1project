"""CyberDefender: Kernel Clash — распознавание жестов.

Веб-камера -> OpenCV -> MediaPipe Hands (21 landmark) -> классификатор -> UDP в Unity.
Протокол: "G <GestureType> start|end <confidence>", раз в секунду "HB".
Выход: клавиша Q или Esc в окне предпросмотра.
"""
import argparse
import math
import socket
import time
from collections import deque

import cv2
import mediapipe as mp

# (кончик, средний сустав) для указательного, среднего, безымянного, мизинца
FINGERS = ((8, 6), (12, 10), (16, 14), (20, 18))


def _dist(a, b):
    return math.hypot(a.x - b.x, a.y - b.y)


def fingers_extended(lm):
    # Палец разогнут, если кончик дальше от запястья, чем средний сустав — не зависит от поворота кисти
    wrist = lm[0]
    return [_dist(lm[tip], wrist) > _dist(lm[pip], wrist) * 1.15 for tip, pip in FINGERS]


def classify_hand(lm):
    ext = fingers_extended(lm)
    count = sum(ext)
    if count == 0:
        return "Fist"
    if count == 4:
        return "OpenPalm"
    if ext[0] and count == 1 and lm[8].y < lm[5].y - 0.05:
        return "PointUp"
    return None


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--camera", type=int, default=0, help="индекс веб-камеры")
    ap.add_argument("--host", default="127.0.0.1")
    ap.add_argument("--port", type=int, default=5065)
    ap.add_argument("--width", type=int, default=640)
    ap.add_argument("--height", type=int, default=480)
    ap.add_argument("--fps", type=int, default=60)
    ap.add_argument("--stable-frames", type=int, default=2, help="сколько кадров подряд жест должен держаться")
    ap.add_argument("--swipe-distance", type=float, default=0.18, help="смещение запястья (доля ширины кадра)")
    ap.add_argument("--swipe-window", type=float, default=0.25, help="за какое время (с) должно произойти смещение")
    ap.add_argument("--no-preview", action="store_true")
    args = ap.parse_args()

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    target = (args.host, args.port)

    def send(msg):
        sock.sendto(msg.encode("ascii"), target)

    cap = cv2.VideoCapture(args.camera, cv2.CAP_DSHOW)
    cap.set(cv2.CAP_PROP_FRAME_WIDTH, args.width)
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, args.height)
    cap.set(cv2.CAP_PROP_FPS, args.fps)
    cap.set(cv2.CAP_PROP_BUFFERSIZE, 1)  # не копим старые кадры — меньше задержка
    if not cap.isOpened():
        raise SystemExit(f"Камера {args.camera} не открылась. Попробуйте --camera 1")

    mp_hands = mp.solutions.hands
    drawing = mp.solutions.drawing_utils
    hands = mp_hands.Hands(
        max_num_hands=2,
        model_complexity=0,  # лёгкая модель — быстрее
        min_detection_confidence=0.6,
        min_tracking_confidence=0.5,
    )

    held = None
    candidate = None
    candidate_frames = 0
    wrist_track = deque()
    last_swipe = 0.0
    last_heartbeat = 0.0
    fps_timer = time.monotonic()
    fps_frames = 0
    fps = 0.0

    print(f"[gesture] камера {args.camera} -> udp://{args.host}:{args.port}. Q — выход.")

    try:
        while True:
            ok, frame = cap.read()
            if not ok:
                continue

            frame = cv2.flip(frame, 1)  # зеркально: движение вправо = вправо на экране
            rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            rgb.flags.writeable = False
            result = hands.process(rgb)
            now = time.monotonic()

            landmarks = result.multi_hand_landmarks or []
            scores = [h.classification[0].score for h in (result.multi_handedness or [])]

            gesture = None
            conf = 0.0
            if (len(landmarks) >= 2
                    and classify_hand(landmarks[0].landmark) == "OpenPalm"
                    and classify_hand(landmarks[1].landmark) == "OpenPalm"):
                gesture = "DoublePalm"
                conf = min(scores[0], scores[1]) if len(scores) >= 2 else 1.0
                wrist_track.clear()
            elif landmarks:
                lm = landmarks[0].landmark
                gesture = classify_hand(lm)
                conf = scores[0] if scores else 1.0
                wrist_track.append((now, lm[0].x))
            else:
                wrist_track.clear()

            while wrist_track and now - wrist_track[0][0] > args.swipe_window:
                wrist_track.popleft()

            moving = False
            if len(wrist_track) >= 2:
                dt = max(now - wrist_track[0][0], 1e-3)
                dx = wrist_track[-1][1] - wrist_track[0][1]
                moving = abs(dx) / dt > 0.4
                if abs(dx) >= args.swipe_distance and now - last_swipe > 0.3:
                    name = "SwipeRight" if dx > 0 else "SwipeLeft"
                    send(f"G {name} start {conf:.2f}")
                    send(f"G {name} end {conf:.2f}")
                    last_swipe = now
                    wrist_track.clear()

            # Ладонь в движении — это свайп, а не удар
            if moving and gesture == "OpenPalm":
                gesture = None

            if gesture == candidate:
                candidate_frames += 1
            else:
                candidate, candidate_frames = gesture, 1

            if candidate != held and candidate_frames >= args.stable_frames:
                if held:
                    send(f"G {held} end 1.00")
                if candidate:
                    send(f"G {candidate} start {conf:.2f}")
                held = candidate

            if now - last_heartbeat >= 1.0:
                send("HB")
                last_heartbeat = now

            fps_frames += 1
            if now - fps_timer >= 1.0:
                fps = fps_frames / (now - fps_timer)
                fps_frames = 0
                fps_timer = now

            if not args.no_preview:
                for hand in landmarks:
                    drawing.draw_landmarks(frame, hand, mp_hands.HAND_CONNECTIONS)
                cv2.putText(frame, f"{held or '-'}   {fps:.0f} FPS", (10, 30),
                            cv2.FONT_HERSHEY_SIMPLEX, 0.9, (0, 255, 200), 2)
                cv2.imshow("CyberDefender - Gestures", frame)
                key = cv2.waitKey(1) & 0xFF
                if key in (ord("q"), 27):
                    break
    except KeyboardInterrupt:
        pass
    finally:
        if held:
            send(f"G {held} end 1.00")
        cap.release()
        cv2.destroyAllWindows()


if __name__ == "__main__":
    main()
