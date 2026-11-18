# MotionCaptureSW (MCSW) — 개발 문서

> **프로젝트명:** VirtualCharacterStudio / MotionCaptureSW (MCSW)  
> **문서 유형:** 소프트웨어 설계 및 구현 기술 문서  
> **기준 소스:** [Junho-Seox/MCSW](https://github.com/Junho-Seox/MCSW), `main` 브랜치의 주요 소스 파일 (2026-11-18 확인)  
> **배포 링크:** [MCSW Release](https://github.com/Junho-Seox/MCSW/releases/tag/MCSW)  
> **문서 상태:** 졸업작품 제출용으로 구현한 상태이다.

---

## 1. 프로젝트 개요

### 1.1 개발 배경과 목적

버추얼 캐릭터를 이용한 방송·콘텐츠 제작에서는 사용자의 움직임을 3D 아바타에 반영하기 위해 모션 캡처 장비 또는 별도의 추적 소프트웨어를 사용한다. MCSW는 일반 RGB 웹캠에서 얻은 영상을 바탕으로 **상반신 자세, 손 동작, 얼굴 표정**을 추정하고, 이를 Unity 기반 VRM 아바타에 실시간 반영하는 데 목적이 있다. 아바타의 움직임을 표현하는 데 그치지 않고, 사전에 정의한 손동작으로 스튜디오 환경과 시각적 이벤트를 조작할 수 있도록 구현하였다.

이 프로젝트의 핵심 개발 목표는 다음과 같다.

1. **단일 웹캠 활용:** Pose·Hand·Face 추정을 별도의 센서 없이 단일 영상 입력에서 수행한다.
2. **실시간 아바타 구동:** Python에서 추정한 위치·표정 정보를 Unity Humanoid/VRM 시스템에 연결한다.
3. **추적 안정성 개선:** 좌표 필터링, 관절 가림 후 복귀 처리, 팔 IK 및 손목·손가락 보정을 조합한다.
4. **제스처 기반 상호작용:** 스와이프, 방패, 헤드셋 제스처를 스튜디오 배경·조명·이펙트와 연결한다.
5. **사용자 중심 실행 절차:** 메인 메뉴에서 아바타를 선택하면 Python 추적 프로세스를 자동 실행하고, 추적 준비가 확인된 후 스튜디오에 진입하도록 한다.
6. **배포 가능 구조:** Unity 애플리케이션과 Python 백엔드를 Windows 실행 패키지로 함께 배포한다.

### 1.2 구현 범위와 비범위

현재 실시간 추적의 주된 범위는 **상체·머리·팔·손·얼굴**이다. Unity 소스에는 `SeatedPoseController`가 있어 하체를 앉은 자세로 유지할 수 있으나, 이를 전신 3D 모션 캡처 또는 하체 동작의 실시간 리타게팅이 아닌 것을 알린다.

또한 현재 구조는 로컬 PC에서 웹캠·Unity·Python을 실행하는 데 집중한다. 모바일 장치의 깊이 센서, 전용 전신 트래커, 다중 카메라 융합, 원격 서버 기반 추론 및 상용 스트리밍 플랫폼 연동을 **현재 구현 기능으로 발표하지 않는다**.

### 1.3 주요 기능 요약

| 구분         | 구현 내용                   | 주요 코드        |
|---|---|---|
| 실시간 영상 입력 | Windows DirectShow 웹캠 입력, 좌우 반전, RGB 변환 | `TrackingService/main.py` |
| 자세 추정 | Pose Landmarker의 3D World Landmarks | `TrackingService/main.py` |
| 손 추정 | 최대 양손 Hand World Landmarks | `TrackingService/main.py` |
| 얼굴 추정 | Face Landmarker 및 Blendshape 점수 | `TrackingService/main.py` |
| 데이터 통신 | 로컬 WebSocket 및 JSON 메시지 | `websocket_server.py`, `PoseWebSocketClient.cs` |
| 리타게팅 | 몸통, 머리, 팔, 손목, 손가락, 얼굴 표정 | `Retargeting/` |
| 제스처 | 양손 스와이프, 방패 자세, 헤드셋 자세 | `StudioGestureRecognizer.cs` |
| 스튜디오 제어 | 배경·조명 프리셋, 방패·헤드셋 시각 효과 | `StudioGestureActions.cs`, `StudioEnvironmentController.cs` |
| 실행 UI | 아바타 선택, 미리보기, 추적 준비, 안내, 스튜디오 진입 | `Runtime/` |
| 배포 | Python 실행 파일 제작 및 Unity 빌드 통합 | `Scripts/*.ps1` |

---

## 2. 개발 환경 및 기술 스택

### 2.1 개발 환경

| 항목 | 채택 기술 및 역할 |
|---|---|
| 운영체제 | Windows 중심 개발 및 배포 |
| 게임 엔진 | **Unity 2022.3.21f1** (`VCStudio/ProjectSettings/ProjectVersion.txt` 기준) |
| 프로그래밍 언어 | C#(Unity), Python(추적 서비스), PowerShell(배포 자동화) |
| 영상 획득·처리 | OpenCV, `cv2.CAP_DSHOW` |
| 추론 | MediaPipe Tasks Vision: Pose, Hand, Face Landmarker |
| 실시간 전송 | Python `websockets` 서버, Unity `NativeWebSocket` 클라이언트 |
| 아바타 | VRM/UniVRM, Unity Humanoid Animator |
| UI 및 렌더링 | Unity UI, Camera/RenderTexture, Unity 씬 |
| 패키징 | PyInstaller `--onedir`, Unity Windows x64 Build |
| 형상 관리·배포 | Git, GitHub, GitHub Releases |

### 2.2 기술 선정 이유

**Unity/C#:** VRM 아바타를 렌더링하고 Humanoid 뼈 구조를 제어하기 위해 사용한다. 씬과 UI를 통합하고, 추적 데이터를 시각적 이벤트로 연결하는 응용 프로그램 계층을 맡는다.

**Python/MediaPipe:** 웹캠 영상으로부터 신체 랜드마크, 손 관절 및 얼굴 표정 계수를 추정하기 위한 계층이다. 모델 추론과 Unity 렌더링을 분리하여 각 계층의 기능을 독립적으로 개발·점검할 수 있도록 했다.

**WebSocket/JSON:** 추적 서비스와 Unity 프로그램을 로컬에서 지속 연결한다. 메시지에 `landmarks`, `hands`, `faceBlendshapes` 필드를 두어 신체·손·얼굴 정보를 하나의 논리적 스트림으로 전달한다.

**UniVRM:** VRM 아바타의 Humanoid 뼈와 얼굴 Expression을 Unity에서 활용하기 위한 기반이다.

**PyInstaller:** 사용자의 컴퓨터에 개발용 Python 가상환경을 별도로 설치하지 않고 실행할 수 있는 Windows 백엔드를 만드는 데 사용한다. 단, 다른 PC에서의 독립 실행 성공 여부는 별도 테스트로 입증해야 한다.

---

## 3. 시스템 아키텍처

### 3.1 모듈별 책임

**추적 서비스(`TrackingService`)**는 카메라 열기, MediaPipe 추론, 직렬화, WebSocket 송신을 담당한다. VRM 모델을 직접 수정하지 않는다.

**Unity 추적 계층(`Tracking/`)**은 패킷 수신, JSON 역직렬화, 좌표 정규화, 신뢰도 및 추적 시간 관리, 필터링을 담당한다.

**리타게팅 계층(`Retargeting/`)**은 신체 관절을 VRM Humanoid Bone 및 표정 Expression에 연결한다. 자세의 기하학적 제약 및 시각적 안정화도 처리한다.

**제스처·스튜디오 계층(`Gestures/`, `Studio/`)**은 추적된 동작을 의미 있는 이벤트로 해석하고, 배경·조명·이펙트를 변경한다.

**응용 프로그램 계층(`Runtime/`)**은 메인 메뉴, 아바타 선택, 카메라 준비, 안내 화면, 씬 전환, Python 프로세스 수명 관리를 담당한다.

### 3.2 소스 폴더 구조

```text
MCSW/
├── VCStudio/
│   ├── Assets/
│   │   └── Scripts/
│   │       ├── Tracking/
│   │       ├── Retargeting/
│   │       ├── Gestures/
│   │       ├── Studio/
│   │       └── Runtime/
│   ├── Packages/
│   └── ProjectSettings/
├── TrackingService/
│   ├── main.py
│   ├── websocket_server.py
│   ├── models/
│   └── requirements.txt
├── Scripts/
│   ├── BuildBackend.ps1
│   └── PrepareRelease.ps1
└── README.md
```

### 3.3 통신 메시지의 형식

`PoseWebSocketServer.publish()`는 추정 결과를 다음 형태의 JSON으로 구성한다. 아래 수치는 **스키마 설명용 예시**이다.

```json
{
  "type": "pose",
  "timestamp": 1795000000.0,
  "landmarks": [
    {"id": 0, "x": 0.01, "y": 0.12, "z": -0.05, "visibility": 0.99}
  ],
  "hands": [
    {
      "label": "Left",
      "score": 0.95,
      "landmarks": [
        {"id": 0, "x": 0.01, "y": 0.02, "z": 0.03}
      ]
    }
  ],
  "faceBlendshapes": [
    {"name": "jawOpen", "score": 0.25}
  ]
}
```

실제 정상 Pose 검출 시 `landmarks`는 **33개 관절**을 가진다. 손은 검출된 손마다 **21개 관절**, 얼굴은 각 Blendshape의 이름과 점수를 담는다. 유효한 사람이 검출되지 않으면 Pose 배열이 비어 있을 수 있으므로 Unity는 **33개라는 길이 조건**으로 정상 Pose 여부를 판정한다.

`timestamp`는 Python 측의 `time.time()` 값이다. Unity는 패킷의 수신 시각과 별도의 최근 수신 상태 변수를 사용해 데이터의 신선도를 판단한다. 해당 메시지는 영상 프레임 자체를 전송하는 것이 아니라 추론 결과를 전달한다.

---

## 4. Python 기반 추적 서비스

### 4.1 입력 전처리

`main.py`는 `cv2.VideoCapture(0, cv2.CAP_DSHOW)`를 사용해 기본 웹캠을 연다. `cap.read()`로 얻은 프레임을 좌우 반전(`cv2.flip(frame, 1)`)하고, BGR을 RGB로 변환한 뒤 `mp.Image(SRGB)`를 구성한다. 이 처리는 하나의 프레임을 세 Landmarker에 공통으로 전달하기 위한 전처리다.

### 4.2 신체 자세 추정

`PoseLandmarker`는 단일 사용자(`num_poses=1`)를 대상으로 한다. 신체 검출·존재·추적 신뢰도의 코드 초기값은 각각 `0.5`이다. 추론 결과 중 `pose_world_landmarks[0]`을 통신에 사용하며, 검출 결과가 없을 경우 빈 배열을 전송한다.

World Landmark 좌표는 3차원 점으로 제공되지만, 이것만으로 다중 시점의 정밀 깊이를 직접 측정하는 것은 아니다. 따라서 팔을 정면으로 뻗거나 특정 관절이 가려졌을 때에는 방향·깊이·관절 굽힘 추정이 불안정해질 수 있다. 이 한계는 Unity의 후처리 및 IK 보정 설계로 이어졌다.

### 4.3 손 관절 및 얼굴 표정 추정

`HandLandmarker`는 `num_hands=2`, 검출·존재·추적 신뢰도 초기값 `0.5`로 설정되어 있다. 결과의 `hand_world_landmarks`와 `handedness`를 조합해 손별 좌표와 좌우 분류 점수를 직렬화한다.

`FaceLandmarker`는 `num_faces=1`이며 `output_face_blendshapes=True`를 설정한다. 얼굴의 전체 메시 좌표를 Unity로 보내는 대신 **Blendshape 이름과 점수**를 JSON에 담아 VRM 표정 제어에 활용한다.

### 4.4 추론 주기와 성능 출력

한 프레임에 대해 Pose → Hand → Face 추론을 순차적으로 수행한다. 각 추론에 걸린 시간을 밀리초 단위로 측정하고, 길이 100인 `deque`로 최근 측정값을 관리한다. 100프레임마다 Pose/Hand/Face별 평균 추론 시간, 세 추론 시간의 합과 그 역수 기반의 추론 처리량 추정치를 출력한다.

이때 코드가 출력하는 **Inference throughput**은 세 모델의 평균 추론 시간 합으로 계산한 이론적 처리량 추정치이다. 실제 FPS와 지연시간에 관한 성능 결론을 내려면 다른 방법의 별도 계측이 필요하지만 현재 작품 내에서의 구현 우선도는 낮기에 무시하였다.

### 4.5 WebSocket 송신 전략

`websocket_server.py`의 `PoseWebSocketServer`는 기본적으로 `127.0.0.1:8765`에 서버를 연다. `publish()`는 직렬화한 최신 메시지 하나를 `latest_message`에 저장하고, 접근 시 `threading.Lock`을 이용한다. 각 클라이언트 처리 루프는 약 `1/60`초 간격으로 확인하면서 **직전에 보낸 메시지와 다른 최신 메시지가 있을 때만 송신**한다.

이 방식은 모든 과거 프레임을 쌓아 전송하는 큐 방식과 달리, 최신 추적 상태를 우선시한다. 다만 느린 소비자가 모든 추론 프레임을 보존한다는 보장은 없으며, 현재 서버에는 인증·암호화·원격 접근 제어 기능이 별도로 구현되어 있지 않다. 기본 바인딩이 루프백 주소라는 점에서 로컬 PC 내부 통신을 전제로 한다.

### 4.6 개발·배포 실행 모드

환경변수 `VCS_HEADLESS=1`인 경우 OpenCV 시각화 창을 띄우지 않는다. 개발자가 `main.py`를 직접 실행하면 시각화와 일부 디버그 정보를 볼 수 있으며, Unity에서 실행하면 추적 기능은 유지하면서 별도 OpenCV 창이 나타나지 않도록 한다.

---

## 5. Unity 추적 데이터 처리와 리타게팅

### 5.1 WebSocket 수신 및 상태 관리

`PoseWebSocketClient.cs`는 `NativeWebSocket`으로 Python 서버에 연결하고 JSON을 `PoseMessage` 구조로 파싱한다. 유효한 `type="pose"` 및 **33개 랜드마크**를 수신한 경우 `HasPoseTracking`, `LastPoseTime` 등을 업데이트한다. 양손에 대해서는 **21개 관절** 조건을 확인하고, Face는 Blendshape 배열의 존재를 확인한다.

패킷을 수신할 때 `trackingProcessor.Process()`로 Pose를 필터링한 후 `CoordinateNormalizer.NormalizePose()`를 적용해 `UpperBodyRetargeter`에 전달한다. 제스처 입력은 별도로 정규화한 최신 Pose를 사용하도록 구성되어 있어, 리타게팅용 평활화와 제스처 판단의 역할을 구분한다.

연결이 끊기거나 `trackingTimeout`을 초과하면 `ClearTracking()`으로 유효 추적 상태를 해제한다. 이는 과거 좌표를 새로운 실시간 추적 결과인 것처럼 계속 취급하지 않기 위한 처리이다.

### 5.2 좌표계와 좌우 매핑

MediaPipe와 Unity의 좌표 축 및 웹캠 거울 모드를 일치시키기 위해 `CoordinateNormalizer.cs`를 사용한다. 코드 기본 설정은 Pose와 Hand World 좌표에 대해 `invertX=false`, `invertY=true`, `invertZ=true`이며, **Pose의 좌우 관절 인덱스 교환과 Hand의 좌우 레이블 교환은 별개의 설정**으로 관리한다. 기본 `swapPoseLeftRight=false`, `swapHandLabels=true`이다.

손 데이터는 손 중심 좌표계로 표현될 수 있으므로, Pose World 좌표와 절대 위치가 동일하다고 가정하여 손과 머리의 거리를 직접 비교하지 않는다. 실제 제스처 코드도 헤드셋 접촉 판단에서 Pose의 손목·손가락 점을 사용하도록 설계되어 있다.

좌표계 변환의 부호 및 좌우 교환 값은 씬에 저장된 Inspector 값이 실제 동작을 결정한다. 따라서 기본 코드값만 보고 최종 캐릭터의 방향이 언제나 동일하다고 단정하지 않고, 실제 데모에서 좌우 팔 올리기·고개 회전·손바닥 방향을 확인해야 한다.

### 5.3 One Euro 필터 및 가림 복구

`PoseTrackingProcessor.cs`는 33개 관절에 대해 각각 `OneEuroFilter3D`를 유지한다. 필터 설정은 머리·몸통·팔·다리·발 그룹별로 분리할 수 있으며, 기본 `PoseFilterSettings`는 `minCutoff=1.0`, `beta=0.007`, `dCutoff=1.0`이다.

One Euro 필터는 속도 변화에 따라 위치 신호의 차단 주파수를 조절한다.

필터의 파라미터에 대한 설명은 다음과 같다. 
`minCutoff`는 정적인 자세에서의 기본 평활화 강도, 
`beta`는 빠른 동작에서 반응성을 늘리는 정도, 
`dCutoff`는 속도 추정의 평활화 파라미터이다. 

관절 `visibility`가 낮거나 좌표가 유효하지 않을 때는 마지막 출력 위치를 일시적으로 유지하되, **출력 `visibility`는 0으로 지정**한다. 다시 관절이 검출되면 이전 값과 새 측정값 사이를 `recoveryDuration`(코드 기본 `0.25`초) 동안 보간하고 필터 상태를 초기화한다. 따라서 단순한 화면 흔들림 완화와 **추적 신뢰도 관리**를 함께 수행한다.

### 5.4 상체 리타게팅 구조

`UpperBodyRetargeter.cs`는 몸통(`TorsoRetargeter`), 양팔(`BothArmsRetargeter`), 머리·목(`HeadNeckRetargeter`)에 Pose를 분배한다. `LateUpdate()`에서 새 Pose 입력을 적용하고, 각 모듈이 아바타 본 회전을 계산하도록 한다. `onlyDispatchNewPose` 옵션을 통해 이미 수신한 동일 Pose 프레임을 새 입력으로 반복 취급하지 않도록 할 수 있다.

`AvatarRigMapper.cs`는 Humanoid Animator에서 본의 초기 자세와 기하학적 길이 등을 읽어 리타게팅 기준을 제공한다. `TorsoRetargeter`와 `HeadNeckRetargeter`에는 기준 방향의 자동 정렬 및 회전 안정화 옵션이 있고, 필요할 때 `X`(몸통), `Z`(머리) 키를 사용한 수동 캘리브레이션 경로도 존재한다.

### 5.5 팔·팔꿈치 안정화와 2-Bone IK

단일 웹캠에서는 팔을 정면으로 뻗었을 때 카메라 방향의 상대 깊이와 팔꿈치 굽힘을 추정하기 어렵다. 이에 `BothArmsRetargeter.cs`는 단순 방향 기반 팔 회전과 선택형 **손목 위치 중심의 2-Bone IK**를 함께 제공한다. `enablePositionIK` 옵션에 따라 동작 모드가 달라진다.

IK 경로에는 다음 보정 장치가 있다.

- 어깨–손목 사이의 상대 도달 거리와 팔 길이의 관계를 고려하는 목표점 처리
- 손목 목표점의 이동 속도 제한 및 평활화
- 팔꿈치 굽힘 평면(**elbow pole**)의 안정화와 의심스러운 갑작스러운 반전 억제
- 손실 뒤 팔이 다시 나타날 때 여러 새 패킷을 확인하는 재검출 안정화
- 팔 본 회전의 프레임별 각속도 제한
- 정면 방패 자세에서 단안 깊이 축소를 보완하기 위한 제한적인 **front-reach assist** 및 캘리브레이션 경로

해당 부분을 기술적으로 구현함에 있어 꽤나 어려움을 겪었으며 지금도 추정 및 보정이 의도하고 싶은 만큼 완벽하지 않다는 것을 본 문서에 기술한다.

### 5.6 손목·손가락 리타게팅

`WristRetargeter.cs`는 추정된 손바닥의 기하학적 방향과 아바타 손목의 방향을 비교하고, 회전 평활화·급격한 방향 변화 거부·swing/twist 회전 제한 등의 선택 기능을 제공한다. 손목은 Pose 기반 팔 방향과 Hand 기반 손바닥 방향을 조합해야 하므로, 좌표계 및 손 레이블 변환의 일관성이 중요하다.

`FingerRetargeter.cs`는 Hand Landmarks의 손가락 관절 굽힘을 각 손가락의 VRM 본 회전에 매핑한다. MCP·PIP·DIP 구간의 굽힘과 엄지 opposition을 다루며, 데드존, 관절별 가중치, 최댓각, 평활화, 추적 손실 시 복귀 기능을 제공한다. `useFreshHandPackets` 옵션은 Unity 렌더링 프레임마다 같은 WebSocket 손 패킷을 새 측정값으로 처리하지 않도록 하는 목적을 가진다.

### 5.7 얼굴 표정 리타게팅

`FaceRetargeter.cs`는 `PoseWebSocketClient`가 전달한 MediaPipe Blendshape 점수를 읽어 `Vrm10Instance.Runtime.Expression`의 표정 가중치에 반영한다. 대표적 매핑은 다음과 같다.

| MediaPipe 입력          | VRM Expression 출력 |
|---|---|
| `eyeBlinkLeft`, `eyeBlinkRight` | `BlinkLeft`, `BlinkRight` 또는 통합 `Blink` |
| `jawOpen` | `Aa` |
| `mouthSmileLeft`, `mouthSmileRight` | `Happy` |
| 눈썹 및 눈 주변 관련 점수 | 선택적 `Angry`, `Sad`, `Surprised` |

입력값은 임계값과 민감도로 정규화한 뒤 평활화한다. 왼쪽·오른쪽 눈의 반응을 개별 조정하거나, 눈 감기·뜨기의 평활화 속도를 달리할 수 있다. 웃음과 입 벌림이 충돌하는 경우에는 가중치를 줄이는 처리도 제공한다. 얼굴 패킷이 오래되면 무한히 동일 표정을 유지하기보다 일정 지연 이후 중립 표정으로 회복하도록 한다.

### 5.8 앉은 자세의 하체 처리

`SeatedPoseController.cs`는 Humanoid 하체 본에 앉은 자세를 적용하는 기능이다. 프로젝트의 주목적은 상체 추적이므로, 이 기능이 하체 모션을 처리했다는 것과 동일하지 않다는 것을 본 문서에 기술한다.

---

## 6. 제스처 기반 스튜디오 상호작용

### 6.1 인식 구조

`StudioGestureRecognizer.cs`는 추적된 최신 Pose/Hand 상태를 이용해 네 가지 이벤트를 계산한다.

| 제스처       | 주요 판단 정보                                            | 연결 동작 |
| `HorizontalSwipe` | 양손 손목의 같은 방향 수평 이동, 이동 거리·속도·방향 우세도 | 다음 배경으로 전환 |
| `DownSwipe` | 양손 손목의 같은 방향 하향 이동 및 속도 | 다음 조명 프리셋으로 전환 |
| `Shield` | 양손의 전방 도달·팔꿈치·손바닥 방향·정지 유지 | 방패 오브젝트 및 선택적 효과음 |
| `Headset` | 손이 귀 주변에 접근한 자세·정지 유지 | 헤드셋 및 음표 시각 효과 |

스와이프 입력은 어깨너비를 기준으로 정규화해 카메라와 사용자 사이의 절대 크기 차이에 대한 의존성을 줄인다. 방패·헤드셋과 같은 정적 동작은 단일 프레임의 포즈만으로 즉시 발생시키지 않고, 일정 시간 동안 자세와 손의 이동 속도가 조건을 만족하는지를 검사한다.

### 6.2 중복·오인식 방지

제스처 간 충돌을 줄이기 위해 **동적 제스처와 정적 제스처의 우선순위, 정지 조건, 발동 잠금, 쿨다운, 재무장**을 적용한다. 예를 들어 방패·헤드셋은 코드 기본값에서 각각 `1.1`초 유지 후 발동하도록 설계되어 있고, 발동 중 다른 제스처가 중복 실행되지 않도록 잠금 상태를 둔다. 손 움직임이 큰 하향 스와이프가 정적 헤드셋 자세로 잘못 판정되는 것도 제한하려는 목적이다.

### 6.3 이벤트 처리와 환경 제어

`StudioGestureActions.cs`는 제스처 인식 결과를 이벤트로 받아 `StudioEnvironmentController`를 호출하거나 Prefab을 생성한다. 이는 **제스처 판정과 시각 효과 실행을 분리**하여, 입력 인식 로직을 바꾸더라도 스튜디오 자산·연출의 수정 범위를 줄이는 구조이다.

`StudioEnvironmentController.cs`는 여러 `BackgroundSlot`과 `LightingPreset`을 관리한다. 배경 슬롯은 대상 루트 GameObject의 활성 상태를 전환하고, 조명 프리셋은 방향광 색·강도·회전 및 환경광을 변경한다. 스튜디오 조작 테스트를 위한 키보드 경로도 존재한다.

방패·헤드셋은 지정된 Prefab이 있으면 이를 생성하고, Prefab이 없는 경우 설정에 따라 절차적 대체 시각 효과를 사용할 수 있다. 헤드셋은 아바타 Head Bone을 기준으로 부착되며, 방패는 팔의 움직임을 따라 배치를 갱신하도록 설계되어 있다. 효과 종료 후 생성 오브젝트를 제거한다.

---

## 7. 사용자 흐름, 자동 실행 및 배포

### 7.1 프로그램 사용 흐름

메인화면과 Scene을 구분하고, 그 사이에 `Avatar Prefab` 을 선택하게 하여 이후의 프로그램 확장성을 고려하였다.

### 7.2 Python 자동 실행

`VCSStudioAppFlow.EnsureBackendStarted()`는 `managePythonProcess`가 활성화된 경우 Python 프로세스를 관리한다. Unity Editor에서는 다음 상대 경로로 개발용 Python을 찾는다.

Windows 배포본에서는 Unity 실행 파일 옆 `Backend/VCS_Tracker/VCS_Tracker.exe`를 찾는다. `ProcessStartInfo`로 작업 디렉터리와 실행 인자를 지정하며, `VCS_HEADLESS=1`, `PYTHONUNBUFFERED=1`을 환경변수로 설정한다. 표준 출력·오류를 수집해 일부 상태를 Unity Console에 보고한다. 메인 메뉴 복귀 또는 애플리케이션 종료 시 자신이 시작한 백엔드 프로세스를 종료하도록 구성하였다.

### 7.3 준비 상태 검증

`VCSStudioPreflight.cs`는 설정된 WebSocket 서버에 접속해 `type="pose"`이면서 `landmarks.Length == 33`인 패킷을 수신했는지 검사한다. `HasFreshPose(maxAge)`는 연결 여부뿐 아니라 **가장 최근 유효 Pose가 일정 시간 이내에 도착했는지** 확인한다.

따라서 단순히 서버 포트가 열렸다는 이유만으로 준비 완료를 선언하지 않는다. 기본 구성에서 실제 사람의 자세 데이터가 들어와야 스튜디오 입장 버튼을 활성화한다. 이는 처음 사용하는 사람이 Python을 수동 실행할 필요를 줄이고, 백엔드 미실행 상태에서 빈 스튜디오로 입장하는 문제를 방지하려는 설계다.

### 7.4 Windows 실행 파일 제작

`Scripts/BuildBackend.ps1`은 검증된 Python `.venv`에 설치된 PyInstaller를 사용하며, MediaPipe의 관련 파일과 세 `.task` 모델을 함께 묶어 `TrackingService/dist/VCS_Tracker/` 아래에 실행 파일을 생성한다.

```powershell
powershell -ExecutionPolicy Bypass -File .\Scripts\BuildBackend.ps1
```

Unity는 Windows x64 대상으로 별도 빌드한다. 이후 `Scripts/PrepareRelease.ps1`이 Unity 빌드 디렉터리와 Python 백엔드 결과물을 다음 구조로 결합하고 ZIP으로 압축한다.

```text
VCS-Windows/
├── VirtualCharacterStudio.exe       # Unity 빌드 파일명은 설정에 따라 달라질 수 있음
├── VirtualCharacterStudio_Data/
├── UnityPlayer.dll
└── Backend/
    └── VCS_Tracker/
        ├── VCS_Tracker.exe
        └── _internal/
```

```powershell
powershell -ExecutionPolicy Bypass -File .\Scripts\PrepareRelease.ps1 -UnityBuildDir '.\Builds\Windows'
```

최종 ZIP은 GitHub Releases에 게시한다. **GitHub의 소스코드 ZIP과 Windows 배포 ZIP은 서로 다른 산출물**이다. 
실제 배포 링크: [MCSW Release](https://github.com/Junho-Seox/MCSW/releases/tag/MCSW).

---

## 8. 개발 과정, 문제 해결 및 검증

### 8.1 개발 단계

1. **단독 추적 서비스 구축:** OpenCV 입력 및 Pose Landmarker로 3D 신체 좌표 추출을 확인했다.
2. **Unity 수신·아바타 연결:** WebSocket 메시지를 Unity C# 구조체/클래스로 파싱하고 Humanoid 본에 연결했다.
3. **상체 범위 확장:** 몸통·머리·양팔을 처리하고 캘리브레이션 및 좌표계 조정을 적용했다.
4. **손·얼굴 기능 확장:** Hand Landmarks를 이용한 손목·손가락, Face Blendshapes를 이용한 VRM 표정을 추가했다.
5. **안정성 개선:** One Euro 필터, 신뢰도 기반 추적 손실 처리, 팔꿈치 폴 안정화 및 가림 후 복귀 보정을 적용했다.
6. **제스처·스튜디오 구현:** 스와이프 환경 제어, 방패·헤드셋 시각 효과를 추가했다.
7. **실행 흐름 통합:** 아바타 선택, 카메라 준비, 자동 Python 실행 및 스튜디오 입장 단계를 구현했다.
8. **소스 통합·배포:** Unity/Python을 단일 저장소에서 관리하고 Windows용 백엔드·실행 ZIP 생성 경로를 마련했다.

위 순서는 구현 항목을 이해하기 위한 **기능 단위 개발 흐름**이다. 정확한 개발 날짜와 단계별 완료 시점은 Git 커밋 또는 별도 개발 일지를 근거로 작성해야 한다.

### 8.2 주요 기술적 문제와 해결 방식

| 개발 중 문제    | 원인·해결 방향                           | 관련 항목 |
|---|---|---|
| Windows 웹캠 입력 실패 | Media Foundation 경로 대신 DirectShow 기반 캡처 사용 | `main.py`의 `CAP_DSHOW` |
| Pose/Hand/Face의 좌표 부호 및 좌우 혼동 | 공통 좌표 정규화와 Hand 레이블 교환 옵션을 분리 | `CoordinateNormalizer` |
| 정적인 자세에서 관절 흔들림 | 속도 적응형 One Euro 필터 적용 | `OneEuroFilter3D` |
| 가림 뒤 팔이 재등장할 때 팔꿈치 방향 급변 | 추적 신뢰도 관리 및 elbow pole 보존 | `PoseTrackingProcessor`, `BothArmsRetargeter` |
| 정면으로 팔을 뻗으면 팔이 충분히 펴지지 않음 | 단안 깊이 추정의 한계를 고려한 선택형 IK 도달 거리 보조 | `BothArmsRetargeter` |
| 제스처 간 오발동 | 유지 시간·정지 조건·우선순위·쿨다운 적용 | `StudioGestureRecognizer` |
| 새 폴더에서 VRM 임포트 실패 | `Shader` 참조 null 문제를 확인하고 해당 `.vrm`을 재임포트하여 복구 | Unity/UniVRM 에셋 임포트 |
| 프로젝트 이동 후 Python 자동 실행 불가 | 고정 절대경로 대신 상대 경로와 새 `.venv` 사용 | `VCSStudioAppFlow` |

문제 해결의 일반화된 효과를 과장하지 않기 위해, 위 사례는 **구현한 처리와 실제 확인한 복구 경험**으로 설명한다. 모든 카메라·아바타·조명 환경에서 동일한 결과가 보장되는 것은 아니다.

### 8.3 기능 검증 현황

현재까지 확인된 사용자 실행 과정과 코드 구조를 구분하여 다음과 같이 기록한다.

| 검증 항목 | 확인 기준 | 상태 |
|---|---|---|
| Python 단독 추적 | `.venv`에서 실행하여 웹캠 Pose/Hand/Face 추적 | **수동 실행 정상 동작 확인** |
| Unity 메인 씬 아바타 동작 | 웹캠 움직임이 아바타에 반영 | **개발 환경에서 동작 확인** |
| 아바타 선택 및 Python 자동 실행 | 메뉴에서 다음 클릭 후 Python 프로세스 시작 | **개발 환경에서 동작 확인** |
| 실시간 추적 준비 확인 | 최근 정상 Pose 데이터를 받은 뒤 입장 | **개발 환경에서 동작 확인** |
| Github Release 등록 | 배포 ZIP 게시 여부 | **`VCS-Windows.zip` 등록 확인** |
| Python 미설치 PC에서 독립 실행 | 깨끗한 Windows 환경에서 모든 흐름 실행 | **검증 필요** |

**성능 수치 작성 원칙:** 측정하지 않은 FPS, 지연시간, 정확도 향상률 또는 오인식률은 개발 문서에 임의로 기재하지 않는다. Python의 추론 시간 통계가 Unity의 화면 프레임률을 직접 입증하지도 않는다.

### 8.4 한계 및 향후 개선 방향

본 시스템의 깊이 추정은 단일 RGB 카메라에 의존하므로 빠른 움직임, 심한 가림, 낮은 조도, 손이 얼굴이나 몸통과 겹치는 자세에 취약할 수 있다. 또한 개인별 체형 차이와 아바타 리그 길이 차이 때문에 동일한 리타게팅 설정을 모든 VRM 모델에 적용하는 것은 어렵다.

미래에 추가적으로 고려할 사항은 다음과 같다.

- 장면 및 사용자별 캘리브레이션 절차 표준화
- 반복 시험을 통한 IK·필터 파라미터의 정량적 비교
- 네트워크 패킷 신선도 및 추적 복구 상태의 시각화 개선
- 모델/Unity 설정/런타임 의존성의 버전 고정과 재현 가능한 빌드 절차
- 오디오·방송 프로그램 등 외부 시스템 연동 가능성 검토

---

## 9. 설치 및 실행 재현 방법

### 9.1 소스 기반 개발 실행

프로젝트 루트에서 `VCStudio/`와 `TrackingService/`가 같은 상위 폴더에 놓이도록 한다. Unity Hub에는 `VCStudio/`를 프로젝트로 등록한다.

```powershell
cd .\TrackingService
py -3.11 -m venv .venv
.\.venv\Scripts\python.exe -m pip install -r requirements.txt
.\.venv\Scripts\python.exe main.py
```

위 Python 3.11 명령은 **환경 구성 예시**이다. 실제로 검증한 Python 버전을 확인해 교체한다. 모델 파일은 `TrackingService/models/` 아래에 `pose_landmarker_full.task`, `hand_landmarker.task`, `face_landmarker.task`라는 이름으로 존재해야 한다.

Python 단독 실행이 정상이라면 종료한 뒤 Unity에서 메인 메뉴를 실행한다. `VCSStudioAppConfig`의 `Manage Python Process`를 활성화하고 WebSocket URL을 `ws://127.0.0.1:8765`로 설정한다. 해당 설정은 프로젝트에 직렬화된 에셋 값이 실제로 올바른지 확인해야 한다.

### 9.2 문제 해결

| 증상 | 확인할 사항 |
|---|---|
| Python을 찾을 수 없음 | `TrackingService/.venv/Scripts/python.exe` 존재, 폴더의 상대 위치 |
| 모델 파일 로딩 실패 | `.task` 모델의 이름·위치·패키징 경로 |
| WebSocket 연결 실패 | 8765 포트 점유, Python 프로세스 실행 여부, 방화벽 및 예외 로그 |
| 33개 Pose 수신 대기 지속 | 카메라 권한, 인물의 화면 내 위치, 실제 Pose 추론 여부 |
| VRM Prefab이 비어 보임 | UniVRM 임포트 오류, Shader 오류, `.vrm` Reimport 및 `.meta` 참조 |
| 버튼이 보이지 않음 | 메뉴 Canvas/Panel/RenderTexture 카메라, 버튼 RectTransform 및 이벤트 바인딩 |
| 배경 또는 시각 효과 미작동 | `StudioGestureActions.executeActions`, 슬롯/Prefab/Animator 참조, 제스처 로그 |
| 배포 EXE에서 Python 미실행 | `Backend/VCS_Tracker/VCS_Tracker.exe` 포함 여부, `_internal` 파일 및 모델 경로 |

---
