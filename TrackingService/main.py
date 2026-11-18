
import time
import os
import sys
from collections import deque
from pathlib import Path

import cv2
import mediapipe as mp

from mediapipe.tasks import python
from mediapipe.tasks.python import vision

from websocket_server import PoseWebSocketServer


# ==========================================
# 1. 경로 및 모델 설정
# ==========================================

# Unity에서 실행할 때는 OpenCV 미리보기 창을 띄우지 않습니다.
# 개발자가 직접 실행할 때는 이전과 동일하게 미리보기가 표시됩니다.
VCS_HEADLESS = os.environ.get("VCS_HEADLESS", "0") == "1"

# source: TrackingService/models/*.task
# frozen (PyInstaller --onedir): VCS_Tracker/_internal/models/*.task
# PyInstaller --add-data 옵션은 모델을 bundle root/models에 복사합니다.
BUNDLE_ROOT = Path(getattr(sys, "_MEIPASS", Path(__file__).resolve().parent))
MODEL_DIR = BUNDLE_ROOT / "models"

POSE_MODEL_PATH = MODEL_DIR / "pose_landmarker_full.task"
HAND_MODEL_PATH = MODEL_DIR / "hand_landmarker.task"
FACE_MODEL_PATH = MODEL_DIR / "face_landmarker.task"

for model_path in (
    POSE_MODEL_PATH,
    HAND_MODEL_PATH,
    FACE_MODEL_PATH
):
    print("모델 경로:", model_path)
    print("모델 파일 존재:", model_path.is_file())

    if not model_path.is_file():
        raise FileNotFoundError(
            f"모델 파일을 찾을 수 없습니다: {model_path}"
        )


# ==========================================
# 2. MediaPipe 옵션
# ==========================================

pose_options = vision.PoseLandmarkerOptions(
    base_options=python.BaseOptions(
        model_asset_path=str(POSE_MODEL_PATH)
    ),
    running_mode=vision.RunningMode.VIDEO,
    num_poses=1,
    min_pose_detection_confidence=0.5,
    min_pose_presence_confidence=0.5,
    min_tracking_confidence=0.5,
)

hand_options = vision.HandLandmarkerOptions(
    base_options=python.BaseOptions(
        model_asset_path=str(HAND_MODEL_PATH)
    ),
    running_mode=vision.RunningMode.VIDEO,
    num_hands=2,
    min_hand_detection_confidence=0.5,
    min_hand_presence_confidence=0.5,
    min_tracking_confidence=0.5,
)

face_options = vision.FaceLandmarkerOptions(
    base_options=python.BaseOptions(
        model_asset_path=str(FACE_MODEL_PATH)
    ),
    running_mode=vision.RunningMode.VIDEO,
    num_faces=1,
    min_face_detection_confidence=0.5,
    min_face_presence_confidence=0.5,
    min_tracking_confidence=0.5,
    output_face_blendshapes=True,
    output_facial_transformation_matrixes=False,
)


# ==========================================
# 3. 성능 측정 변수
# ==========================================

pose_inference_times = deque(maxlen=100)
hand_inference_times = deque(maxlen=100)
face_inference_times = deque(maxlen=100)
total_inference_times = deque(maxlen=100)

frame_index = 0
start_time = time.perf_counter()
last_timestamp_ms = -1

fps = 0.0
fps_frame_count = 0
fps_start_time = start_time


# ==========================================
# 4. 웹캠 및 WebSocket 초기화
# ==========================================

cap = cv2.VideoCapture(0, cv2.CAP_DSHOW)

if not cap.isOpened():
    raise RuntimeError("웹캠을 열 수 없습니다.")

print("웹캠 연결 성공")
print("Width:", cap.get(cv2.CAP_PROP_FRAME_WIDTH))
print("Height:", cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
print("Camera FPS:", cap.get(cv2.CAP_PROP_FPS))

pose_server = PoseWebSocketServer()
pose_server.start()


# ==========================================
# 5. 통합 추론 루프
# ==========================================

try:
    with (
        vision.PoseLandmarker.create_from_options(
            pose_options
        ) as pose_landmarker,
        vision.HandLandmarker.create_from_options(
            hand_options
        ) as hand_landmarker,
        vision.FaceLandmarker.create_from_options(
            face_options
        ) as face_landmarker
    ):

        while True:
            success, frame = cap.read()

            if not success or frame is None:
                print("웹캠 프레임을 읽을 수 없습니다.")
                break

            # 거울 모드
            frame = cv2.flip(frame, 1)

            rgb_frame = cv2.cvtColor(
                frame,
                cv2.COLOR_BGR2RGB
            )

            mp_image = mp.Image(
                image_format=mp.ImageFormat.SRGB,
                data=rgb_frame
            )

            # VIDEO 모드 타임스탬프
            timestamp_ms = int(
                (time.perf_counter() - start_time) * 1000
            )

            timestamp_ms = max(
                timestamp_ms,
                last_timestamp_ms + 1
            )

            last_timestamp_ms = timestamp_ms

            # ------------------------------
            # BlazePose 추론
            # ------------------------------

            pose_start = time.perf_counter()

            pose_result = pose_landmarker.detect_for_video(
                mp_image,
                timestamp_ms
            )

            pose_ms = (
                time.perf_counter() - pose_start
            ) * 1000

            pose_inference_times.append(pose_ms)

            # ------------------------------
            # Hand Landmarker 추론
            # ------------------------------

            hand_start = time.perf_counter()

            hand_result = hand_landmarker.detect_for_video(
                mp_image,
                timestamp_ms
            )

            hand_ms = (
                time.perf_counter() - hand_start
            ) * 1000

            hand_inference_times.append(hand_ms)

            # ------------------------------
            # Face Landmarker 추론
            # ------------------------------

            face_start = time.perf_counter()

            face_result = face_landmarker.detect_for_video(
                mp_image,
                timestamp_ms
            )

            face_ms = (
                time.perf_counter() - face_start
            ) * 1000

            face_inference_times.append(face_ms)

            total_ms = pose_ms + hand_ms + face_ms
            total_inference_times.append(total_ms)

            # ------------------------------
            # WebSocket 데이터 전송
            # ------------------------------

            world_landmarks = (
                pose_result.pose_world_landmarks[0]
                if pose_result.pose_world_landmarks
                else []
            )

            pose_server.publish(
                world_landmarks,
                hand_result,
                face_result
            )

            frame_index += 1

            if not VCS_HEADLESS:
                # ------------------------------
                # Pose 랜드마크 시각화
                # ------------------------------

                if pose_result.pose_landmarks:
                    landmarks = pose_result.pose_landmarks[0]

                    for landmark in landmarks:
                        x = int(
                            landmark.x * frame.shape[1]
                        )
                        y = int(
                            landmark.y * frame.shape[0]
                        )

                        if (
                            0 <= x < frame.shape[1]
                            and 0 <= y < frame.shape[0]
                        ):
                            cv2.circle(
                                frame,
                                (x, y),
                                4,
                                (0, 255, 0),
                                -1
                            )

                # ------------------------------
                # Hand 랜드마크 시각화
                # ------------------------------

                for hand_landmarks in hand_result.hand_landmarks:
                    for landmark in hand_landmarks:
                        x = int(
                            landmark.x * frame.shape[1]
                        )
                        y = int(
                            landmark.y * frame.shape[0]
                        )

                        if (
                            0 <= x < frame.shape[1]
                            and 0 <= y < frame.shape[0]
                        ):
                            cv2.circle(
                                frame,
                                (x, y),
                                3,
                                (255, 0, 255),
                                -1
                            )

                # ------------------------------
                # Face 랜드마크 시각화
                # ------------------------------

                if face_result.face_landmarks:
                    face_landmarks = (
                        face_result.face_landmarks[0]
                    )

                    for landmark in face_landmarks:
                        x = int(
                            landmark.x * frame.shape[1]
                        )
                        y = int(
                            landmark.y * frame.shape[0]
                        )

                        if (
                            0 <= x < frame.shape[1]
                            and 0 <= y < frame.shape[0]
                        ):
                            cv2.circle(
                                frame,
                                (x, y),
                                1,
                                (0, 165, 255),
                                -1
                            )

            # ------------------------------
            # FPS 측정
            # ------------------------------

            now = time.perf_counter()
            fps_frame_count += 1

            elapsed = now - fps_start_time

            if elapsed >= 1.0:
                fps = fps_frame_count / elapsed
                fps_frame_count = 0
                fps_start_time = now

            # ------------------------------
            # 평균 추론 시간 출력
            # ------------------------------

            if (
                len(total_inference_times) == 100
                and frame_index % 100 == 0
            ):
                avg_pose = (
                    sum(pose_inference_times)
                    / len(pose_inference_times)
                )

                avg_hand = (
                    sum(hand_inference_times)
                    / len(hand_inference_times)
                )

                avg_face = (
                    sum(face_inference_times)
                    / len(face_inference_times)
                )

                avg_total = (
                    sum(total_inference_times)
                    / len(total_inference_times)
                )

                print(
                    f"Pose: {avg_pose:.2f} ms | "
                    f"Hand: {avg_hand:.2f} ms | "
                    f"Face: {avg_face:.2f} ms | "
                    f"Total: {avg_total:.2f} ms | "
                    f"Inference throughput: "
                    f"{1000 / avg_total:.1f} FPS"
                )

            if not VCS_HEADLESS:
                # ------------------------------
                # 화면 정보
                # ------------------------------

                face_count = len(
                    face_result.face_landmarks
                )

                cv2.putText(
                    frame,
                    f"FPS: {fps:.1f}",
                    (20, 35),
                    cv2.FONT_HERSHEY_SIMPLEX,
                    0.7,
                    (255, 255, 0),
                    2
                )

                cv2.putText(
                    frame,
                    f"Hands: {len(hand_result.hand_landmarks)} "
                    f"Face: {face_count}",
                    (20, 65),
                    cv2.FONT_HERSHEY_SIMPLEX,
                    0.7,
                    (255, 0, 255),
                    2
                )

                cv2.putText(
                    frame,
                    f"Pose: {pose_ms:.1f}ms "
                    f"Hand: {hand_ms:.1f}ms "
                    f"Face: {face_ms:.1f}ms",
                    (20, 95),
                    cv2.FONT_HERSHEY_SIMPLEX,
                    0.55,
                    (255, 255, 255),
                    2
                )

                cv2.imshow(
                    "Virtual Character Studio - Pose + Hand + Face",
                    frame
                )

                if cv2.waitKey(1) & 0xFF == 27:
                    break

finally:
    cap.release()
    cv2.destroyAllWindows()
