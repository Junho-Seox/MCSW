
import json
import threading
import time

from websockets.sync.server import serve


class PoseWebSocketServer:
    def __init__(self, host="127.0.0.1", port=8765):
        self.host = host
        self.port = port

        self.latest_message = None
        self.lock = threading.Lock()

    def publish(
        self,
        landmarks,
        hand_result=None,
        face_result=None
    ):

        hands = []

        if hand_result is not None:
            for hand_landmarks, handedness in zip(
                hand_result.hand_world_landmarks,
                hand_result.handedness
            ):
                hand_label = handedness[0].category_name
                hand_score = handedness[0].score

                hands.append({
                    "label": hand_label,
                    "score": float(hand_score),
                    "landmarks": [
                        {
                            "id": i,
                            "x": float(h.x),
                            "y": float(h.y),
                            "z": float(h.z)
                        }
                        for i, h in enumerate(hand_landmarks)
                    ]
                })

        face_blendshapes = []

        if (
            face_result is not None
            and face_result.face_blendshapes
        ):
            blendshapes = (
                face_result.face_blendshapes[0]
            )

            face_blendshapes = [
                {
                    "name": category.category_name,
                    "score": float(category.score)
                }
                for category in blendshapes
            ]

        message = {
            "type": "pose",
            "timestamp": time.time(),
            "landmarks": [
                {
                    "id": i,
                    "x": float(p.x),
                    "y": float(p.y),
                    "z": float(p.z),
                    "visibility": float(p.visibility)
                }
                for i, p in enumerate(landmarks)
            ],
            "hands": hands,
            "faceBlendshapes": face_blendshapes
        }

        with self.lock:
            self.latest_message = json.dumps(message)

    def handle_client(self, websocket):
        print("Unity 클라이언트 연결됨")

        try:
            last_message = None

            while True:
                with self.lock:
                    message = self.latest_message

                if (
                    message is not None
                    and message != last_message
                ):
                    websocket.send(message)
                    last_message = message

                time.sleep(1 / 60)

        except Exception as e:
            print("클라이언트 연결 종료:", e)

    def start(self):
        def run():
            with serve(
                self.handle_client,
                self.host,
                self.port
            ) as server:
                print(
                    f"WebSocket 서버 시작: "
                    f"ws://{self.host}:{self.port}"
                )

                server.serve_forever()

        thread = threading.Thread(
            target=run,
            daemon=True
        )

        thread.start()
