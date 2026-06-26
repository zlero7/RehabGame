# 별자리 잇기 (Constellation Trace) — Unity 2D 구현 기술 기획서 (v3)

> 원본: `30906_김탁건_재활게임_기획서.pdf` (스마트 보드(2D) 전용 AROM 미니게임)
> 본 문서는 원본 게임 디자인 문서를 **Unity 2022 LTS + Input System(신규)** 기반으로 실제 구현하기 위한 기술 기획서이다.
>
> **v3 변경사항**: 평가 피드백(위험 분석·권한관리/감사로그·회차별 추세 분석)과 의료 데이터 보안 관점을 반영하여 보안 설계·권한/감사·추세 분석·위험 분석 섹션을 신규 추가하고 저장 계층을 암호화 기반으로 강화함. 상세 이력은 15절 참고.

---

## 0. 개발 환경 사양

| 항목 | 내용 |
|---|---|
| 엔진 | Unity 2022 LTS (2022.3.x) |
| 렌더 파이프라인 | URP 2D (또는 Built-in 2D, 프로젝트 상황에 맞게 선택) |
| 입력 시스템 | Input System 패키지(신규), `com.unity.inputsystem` |
| 입력 디바이스 | 마우스(보조 기구 부착) — **하드웨어 커서는 숨기고, 델타값을 누적하는 가상 커서로 동작** (이유는 0.1절) |
| 플랫폼 | PC (Windows 단일 빌드 기준) |
| 해상도 기준 | 1920x1080 (16:9), Canvas Scaler `Scale With Screen Size` |
| 언어 | C# |
| JSON 직렬화 | `Newtonsoft.Json` (`com.unity.nuget.newtonsoft-json` 패키지) — 이유는 5.1절 |
| 저장 방식 | 로컬 JSON 파일(1차) → DB/클라우드 연동(확장) |

### 0.1 입력 아키텍처 — 가상 커서 방식 (v1 대비 핵심 변경)

**v1 문제점**: v1은 OS 하드웨어 마우스 좌표를 그대로 `ScreenToWorldPoint`로 변환해 사용했다. 이 경우 "가동범위가 좁은 환자는 마우스 감도를 높여 화면 전체에 도달하게 한다"(원본 8p)는 요구사항을 **소프트웨어에서 구현할 방법이 없다** — 하드웨어 커서는 OS가 이동시키므로 게임 코드가 배율을 곱해도 실제 커서 위치엔 반영되지 않는다.

**v2 해결**: 하드웨어 커서를 숨기고, 직접 그린 커서 스프라이트를 게임 내 가상 좌표로 구동한다.

```
[OS 마우스 delta] → [감도 배율 곱연산] → [가상 커서 위치 누적] → [화면 밖으로 안 나가게 Clamp] → [가상 커서 스프라이트 이동]
```

```csharp
public class VirtualCursor : MonoBehaviour
{
    public static VirtualCursor Instance { get; private set; }

    [SerializeField] private InputActionReference mouseDeltaAction;
    [SerializeField] private RectTransform cursorVisual;
    [SerializeField] private RectTransform playAreaBounds; // 가상 커서가 움직일 수 있는 영역

    public Vector2 ScreenPosition { get; private set; } // 가상 커서의 화면(UI) 좌표
    public Vector2 WorldPosition { get; private set; }  // 가상 커서의 월드 좌표
    public Vector2 RawDelta { get; private set; }        // 감도 적용 전 raw delta (거리 측정용)
    public Vector2 ScaledDelta { get; private set; }     // 감도 적용 후 delta (판정/이동용)

    private float sensitivityMultiplier = 1f;

    void Awake()
    {
        Instance = this;
        ScreenPosition = new Vector2(Screen.width / 2f, Screen.height / 2f); // 시작은 화면 중앙
    }

    void OnEnable() => mouseDeltaAction.action.Enable();
    void OnDisable() => mouseDeltaAction.action.Disable();

    public void SetSensitivityMultiplier(float multiplier) => sensitivityMultiplier = multiplier;

    void Update()
    {
        Vector2 rawDelta = mouseDeltaAction.action.ReadValue<Vector2>();
        RawDelta = rawDelta;

        Vector2 scaledDelta = rawDelta * sensitivityMultiplier;
        ScaledDelta = scaledDelta;

        ScreenPosition += scaledDelta;
        ScreenPosition = ClampToPlayArea(ScreenPosition);

        cursorVisual.position = ScreenPosition;
        WorldPosition = Camera.main.ScreenToWorldPoint(new Vector3(ScreenPosition.x, ScreenPosition.y, 0));

        DirectionAnalyzer.Instance?.RecordMovement(rawDelta); // 분석은 항상 raw 기준 (5.2절 참고)
    }

    private Vector2 ClampToPlayArea(Vector2 pos)
    {
        Rect bounds = playAreaBounds.rect;
        return new Vector2(
            Mathf.Clamp(pos.x, bounds.xMin, bounds.xMax),
            Mathf.Clamp(pos.y, bounds.yMin, bounds.yMax)
        );
    }
}
```

**시작 시 처리**:
```csharp
void Start()
{
    Cursor.visible = false;
    Cursor.lockState = CursorLockMode.Confined; // OS 커서가 화면 밖으로 나가 멈추는 것을 방지
}
```

**Input Action 구성**: `Mouse Position`이 아니라 **`Mouse Delta`**(`<Mouse>/delta`)를 바인딩해야 한다. 절대 위치가 아니라 프레임 간 이동량이 필요하기 때문이다.

> **클릭 입력 미사용 원칙(유지)**: Input Action 자산에 클릭 관련 액션은 일체 만들지 않는다. `Mouse Delta` 1개 액션만 존재 — 이 구조적 제약이 "순수 이동 기반 판정"을 보장한다.

### 0.2 거울 모드와의 연결

거울 모드는 `RawDelta.x`를 반전시켜 `VirtualCursor`에 주입하는 방식으로 구현한다 (3절에서 상세). v1처럼 씬을 반전하거나 좌표를 사후 변환하지 않고, **델타 단계에서 반전**하므로 가상 커서·판정·UI 전체가 일관되게 동작한다.

---

## 1. 프로젝트 구조

```
Assets/
 ├─ Scenes/
 │   ├─ Title.unity
 │   ├─ Calibration.unity             # ROM 캘리브레이션 (신규: 별도 씬으로 분리)
 │   ├─ DifficultySelect.unity
 │   ├─ Gameplay.unity
 │   └─ TherapistMonitor.unity
 ├─ Scripts/
 │   ├─ Core/
 │   │   ├─ GameManager.cs
 │   │   ├─ GameState.cs              # enum: Ready, Playing, Completed, Result
 │   │   └─ SessionResult.cs
 │   ├─ Input/
 │   │   └─ VirtualCursor.cs          # 가상 커서 (0.1절)
 │   ├─ Gameplay/
 │   │   ├─ StarNode.cs
 │   │   ├─ ConstellationController.cs
 │   │   └─ ConnectionLineRenderer.cs
 │   ├─ Modes/
 │   │   ├─ IGameMode.cs
 │   │   ├─ BasicMode.cs
 │   │   ├─ MirrorMode.cs
 │   │   └─ TimerMode.cs
 │   ├─ LevelDesign/
 │   │   ├─ ConstellationPattern.cs
 │   │   ├─ DifficultyConfig.cs
 │   │   └─ PatternValidator.cs
 │   ├─ UI/
 │   │   ├─ HUDController.cs
 │   │   ├─ ResultScreen.cs
 │   │   ├─ RangeWarningArrow.cs
 │   │   └─ TherapistDashboard.cs
 │   ├─ Data/
 │   │   ├─ DataLogger.cs
 │   │   ├─ DataExporter.cs
 │   │   ├─ DirectionAnalyzer.cs
 │   │   └─ ROMUsageTracker.cs        # 신규: 가동 영역 사용 비율 계산 (4.5절)
 │   └─ Calibration/
 │       └─ ROMCalibrator.cs
 ├─ ScriptableObjects/
 │   ├─ Patterns/
 │   └─ DifficultyConfigs/
 ├─ Prefabs/
 │   ├─ StarNode.prefab
 │   ├─ ConnectionLine.prefab
 │   ├─ VirtualCursorVisual.prefab     # 신규
 │   └─ UI/
 ├─ InputActions/
 │   └─ GameplayInputActions.inputactions   # Mouse Delta 액션 1개
 └─ Art/
     ├─ Sprites/Stars/
     └─ Backgrounds/
```

---

## 2. 핵심 시스템 설계

### 2.1 핵심 루프 (Core Loop) 구현

```
[Spawn] → [Highlight Next Star] → [Wait For Cursor Arrival] → [Connect] → (반복) → [Complete]
```

**`ConstellationController.cs`** — v1 대비 (a) 상태 가드 추가로 완료 후 인덱스 초과 접근 방지, (b) 선분 기반 판정으로 빠른 이동 시 터널링 방지, (c) 모드 변환을 실제로 거치도록 배선.

```csharp
public enum NodeState { Idle, Active, Connected }
public enum GameState { Ready, Playing, Completed, Result }

public class ConstellationController : MonoBehaviour
{
    public static ConstellationController Instance { get; private set; }

    [SerializeField] private List<StarNode> nodes;
    [SerializeField] private float judgeRadius = 0.6f; // 난이도 설정값으로 오버라이드

    private int currentIndex = 0;
    private GameState state = GameState.Ready;
    private IGameMode currentMode;
    private Vector2 lastCursorWorldPos;

    public Vector2 CurrentTargetPosition => nodes[currentIndex].WorldPosition;

    void Awake() => Instance = this;

    public void StartConstellation(IGameMode mode)
    {
        currentMode = mode;
        currentIndex = 0;
        state = GameState.Playing;
        nodes[0].SetState(NodeState.Active);
        lastCursorWorldPos = GetTransformedCursorPos();
    }

    void Update()
    {
        if (state != GameState.Playing) return; // v1 버그 수정: 완료 후 인덱스 초과 접근 방지

        Vector2 cursorWorldPos = GetTransformedCursorPos();
        StarNode target = nodes[currentIndex];

        // v1 버그 수정: 점-점 거리 대신, 직전 프레임~현재 프레임 선분과 별 사이 최단거리로 판정
        // (보조 기구 사용 시 프레임 드롭/저주사율로 인한 터널링 방지)
        float dist = DistancePointToSegment(target.WorldPosition, lastCursorWorldPos, cursorWorldPos);

        if (dist <= judgeRadius)
            ConnectNode(target);

        lastCursorWorldPos = cursorWorldPos;
    }

    private Vector2 GetTransformedCursorPos()
    {
        // v1 버그 수정: 모드 변환을 실제로 거침 (거울 모드 등)
        return currentMode != null
            ? currentMode.TransformCursorPosition(VirtualCursor.Instance.WorldPosition)
            : VirtualCursor.Instance.WorldPosition;
    }

    private float DistancePointToSegment(Vector2 point, Vector2 segA, Vector2 segB)
    {
        Vector2 ab = segB - segA;
        float lenSq = ab.sqrMagnitude;
        if (lenSq < 0.0001f) return Vector2.Distance(point, segA);
        float t = Mathf.Clamp01(Vector2.Dot(point - segA, ab) / lenSq);
        Vector2 projection = segA + t * ab;
        return Vector2.Distance(point, projection);
    }

    void ConnectNode(StarNode node)
    {
        node.SetState(NodeState.Connected);
        if (currentIndex > 0)
            ConnectionLineRenderer.Instance.Draw(nodes[currentIndex - 1], node);

        DataLogger.Instance.LogStarConnected(currentIndex, Time.time);

        currentIndex++;
        if (currentIndex >= nodes.Count)
            OnConstellationComplete();
        else
            nodes[currentIndex].SetState(NodeState.Active);
    }

    void OnConstellationComplete()
    {
        state = GameState.Completed;
        DataLogger.Instance.FinalizeSession();
    }
}
```

> **설계 포인트(유지)**: `currentIndex`로 지정된 활성 별 1개에만 판정을 수행하므로 "오작동 허용 / 순서 이탈 없음" 규칙이 구조적으로 보장된다.

### 2.2 판정 규칙 구현 상세

| 규칙 | 구현 방식 |
|---|---|
| 연결 판정 | 직전~현재 프레임 사이 **선분**과 별 사이 최단거리로 체크 (점 거리 대신 — 터널링 방지) |
| 오작동 허용 | 비활성(`Idle`) 별에는 판정 로직을 적용하지 않음. 시각적으로만 존재 |
| 순서 이탈 없음 | 활성 목표 1개 외 판정 자체가 존재하지 않으므로 자동 충족 |
| 완료 조건 | `currentIndex == nodes.Count` 도달 시 `OnConstellationComplete()` 호출, 이후 `Update()` 즉시 종료(상태 가드) |

### 2.3 게임 모드 구현 (전략 패턴)

거울 모드는 v2에서 **델타 반전 방식**으로 변경 — `VirtualCursor`가 매 프레임 읽는 raw delta의 x값을 모드가 미리 반전시킨다.

```csharp
public interface IGameMode
{
    void OnModeStart();
    Vector2 TransformCursorPosition(Vector2 worldPos); // 추가 변환이 필요한 모드용 (현재는 항등 함수가 대부분)
    bool IsTimeLimited { get; }
}

public class BasicMode : IGameMode
{
    public bool IsTimeLimited => false;
    public void OnModeStart() => VirtualCursor.Instance.SetHorizontalInvert(false);
    public Vector2 TransformCursorPosition(Vector2 worldPos) => worldPos;
}

public class MirrorMode : IGameMode
{
    public bool IsTimeLimited => false;
    public void OnModeStart() => VirtualCursor.Instance.SetHorizontalInvert(true); // delta.x 반전을 VirtualCursor에 위임
    public Vector2 TransformCursorPosition(Vector2 worldPos) => worldPos; // 반전은 이미 VirtualCursor 단계에서 처리됨
}

public class TimerMode : IGameMode
{
    public float TimeLimitSeconds = 60f; // 치료사 설정값 주입
    public bool IsTimeLimited => true;
    public void OnModeStart() => VirtualCursor.Instance.SetHorizontalInvert(false);
    public Vector2 TransformCursorPosition(Vector2 worldPos) => worldPos;
}
```

`VirtualCursor`에 반전 플래그 추가:
```csharp
private bool horizontalInvert = false;
public void SetHorizontalInvert(bool invert) => horizontalInvert = invert;

// Update() 내 ReadValue 직후
if (horizontalInvert) rawDelta.x = -rawDelta.x;
```

> v1에서는 `TransformCursorPosition`을 정의해놓고 `ConstellationController`가 이를 호출하지 않아 거울 모드가 판정에 반영되지 않는 결함이 있었다. v2는 (a) 반전을 델타 단계로 옮기고, (b) `GetTransformedCursorPos()`가 항상 `currentMode.TransformCursorPosition()`을 거치도록 강제하여 이중으로 안전장치를 둔다.

---

## 3. 레벨 디자인 데이터 구조

### 3.1 ScriptableObject 설계

```csharp
[CreateAssetMenu(fileName = "Pattern_", menuName = "ConstellationTrace/Pattern")]
public class ConstellationPattern : ScriptableObject
{
    public string patternName;
    public DifficultyTier tier;
    public ThemeType theme;
    [Tooltip("정규화 좌표 (-1~1), 가동범위 스케일링 시 곱연산")]
    public List<Vector2> starPositions;
}

public enum DifficultyTier { Beginner, Basic, Advanced }
public enum ThemeType { Shape, Object, RealConstellation }
```

### 3.2 난이도 파라미터 테이블

| 단계 | starCount | ROM 커버리지 | 비고 |
|---|---|---|---|
| 1단계(입문) | 4 | 30~40% | 좁은 범위, 기본 동작 습득 |
| 2단계(기본) | 6 | 50~70% | 표준 훈련 범위 |
| 3단계(심화) | 8~10 | 90%↑ | 대각선 비중 확대 |

```csharp
[CreateAssetMenu(fileName = "Difficulty_", menuName = "ConstellationTrace/DifficultyConfig")]
public class DifficultyConfig : ScriptableObject
{
    public DifficultyTier tier;
    public int starCount;
    [Range(0f,1f)] public float minRomCoverage;
    [Range(0f,1f)] public float maxRomCoverage;
    public float diagonalWeight;
    public float judgeRadius; // v2 추가: 난이도별로 분리하여 좌절감/훈련효과 균형 튜닝 (8절 참고)
}
```

### 3.3 패턴 배치 원칙 — 자동 검증 툴 (에디터 확장)

v2에서 **두 별의 좌표가 같을 때 발생하는 NaN 예외**를 방지하는 가드를 추가했다.

```csharp
public static class PatternValidator
{
    public static List<string> Validate(ConstellationPattern pattern)
    {
        var errors = new List<string>();

        var coveredZones = new HashSet<Vector2Int>();
        foreach (var pos in pattern.starPositions)
            coveredZones.Add(GetZone(pos));
        if (coveredZones.Count < 9)
            errors.Add($"3x3 구역 중 {9 - coveredZones.Count}개 구역에 별이 없음");

        int sameDirCount = 1;
        Vector2 prevDir = Vector2.zero;
        for (int i = 1; i < pattern.starPositions.Count; i++)
        {
            Vector2 diff = pattern.starPositions[i] - pattern.starPositions[i - 1];

            // v1 버그 수정: 좌표가 동일하면 normalized가 NaN이 되어 이후 모든 비교가 깨짐
            if (diff.sqrMagnitude < 0.0001f)
            {
                errors.Add($"인덱스 {i - 1}, {i}: 별 좌표가 동일함 (이동 불가능한 패턴)");
                continue;
            }

            Vector2 dir = diff.normalized;
            if (Vector2.Dot(dir, prevDir) > 0.9f) sameDirCount++;
            else sameDirCount = 1;
            if (sameDirCount >= 3)
                errors.Add($"인덱스 {i - 2}~{i}: 같은 방향 3회 이상 연속 이동");
            prevDir = dir;
        }
        return errors;
    }

    private static Vector2Int GetZone(Vector2 normalizedPos)
    {
        int x = Mathf.Clamp(Mathf.FloorToInt((normalizedPos.x + 1f) / 2f * 3f), 0, 2);
        int y = Mathf.Clamp(Mathf.FloorToInt((normalizedPos.y + 1f) / 2f * 3f), 0, 2);
        return new Vector2Int(x, y);
    }
}
```

### 3.4 ROM 자동 스케일링 (개인화) — 가상 커서 구조와 통합

v1에서는 `CalculateSensitivityMultiplier()`가 어디에도 연결되지 않아 실제로 동작할 수 없었다. v2는 이 값을 `VirtualCursor.SetSensitivityMultiplier()`에 직접 주입한다.

```csharp
public class ROMCalibrator : MonoBehaviour
{
    public float patientMaxReachX; // Calibration 씬에서 측정
    public float patientMaxReachY;

    public Vector2 ScaleToPatientRange(Vector2 normalizedStarPos, float coverageRatio)
    {
        return new Vector2(
            normalizedStarPos.x * patientMaxReachX * coverageRatio,
            normalizedStarPos.y * patientMaxReachY * coverageRatio
        );
    }

    public float CalculateSensitivityMultiplier(float coverageRatio)
    {
        return Mathf.Lerp(2.0f, 1.0f, coverageRatio);
    }

    // v2 추가: 난이도 진입 시 호출하여 VirtualCursor에 실제로 배율을 적용
    public void ApplyToVirtualCursor(float coverageRatio)
    {
        float multiplier = CalculateSensitivityMultiplier(coverageRatio);
        VirtualCursor.Instance.SetSensitivityMultiplier(multiplier);
    }
}
```

> **캘리브레이션 절차**: `Calibration.unity` 씬에서 환자가 상/하/좌/우 최대로 팔을 뻗는 동작을 1회씩 수행 → `patientMaxReachX/Y` 측정 → 이후 모든 난이도의 좌표 변환과 감도 배율의 기준이 됨.
>
> **주의(v2 신규 검토 항목)**: 감도 배율을 적용하면 "총 이동 거리"가 회차마다 다른 의미를 갖게 된다(4.5절 ROM 사용비율, 5.1절 측정 항목과 함께 참고). 물리적 팔 동작량을 추적하려면 `RawDelta`(배율 적용 전) 기준으로 거리를 누적해야 한다.

---

## 4. UI/UX 구현

### 4.1 화면 구성 (Canvas 레이아웃)

```
Canvas (Screen Space - Camera)
 ├─ TopBar
 │   ├─ ProgressText      "진행도 4/8"
 │   └─ ElapsedTimeText   "경과 00:42"
 ├─ PlayArea (별자리 플레이 영역, 어두운 배경)
 │   ├─ (StarNode 인스턴스들, 런타임 생성)
 │   └─ VirtualCursorVisual (가상 커서 스프라이트)
 ├─ BottomBar
 │   └─ ROMUsageText      "가동 영역 사용 비율: 62%"
 └─ RangeWarningArrow (조건부 활성화)
```

### 4.2 HUD 컨트롤러

```csharp
public class HUDController : MonoBehaviour
{
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private TMP_Text elapsedTimeText;
    [SerializeField] private TMP_Text romUsageText;

    public void UpdateProgress(int current, int total) =>
        progressText.text = $"진행도 {current}/{total}";

    // v2 수정: Time.time 대신 unscaledTime 사용 권장 (일시정지 시에도 표시값이 어긋나지 않게)
    public void UpdateElapsedTime(float seconds) =>
        elapsedTimeText.text = $"경과 {TimeSpan.FromSeconds(seconds):mm\\:ss}";

    public void UpdateROMUsage(float ratio) =>
        romUsageText.text = $"가동 영역 사용 비율: {ratio * 100f:F0}%";
}
```

### 4.3 피드백 요소 구현

| 이벤트 | 구현 |
|---|---|
| 연결 성공 시 | `StarNode`에 `Animator` 트리거(점등) + `AudioSource.PlayOneShot()` + `LineRenderer`로 선 애니메이션 |
| 별자리 완성 시 | 모든 `StarNode` 동시 점등 애니메이션 → `ResultScreen` 패널로 페이드 전환 |
| 범위 경고 | 최근 N초간 `RawDelta` 누적 이동량이 임계값 미만이면 활성화, 목표 별 방향으로 화살표 회전 |

```csharp
public class RangeWarningArrow : MonoBehaviour
{
    [SerializeField] private float lowMovementThreshold = 0.05f;
    [SerializeField] private float warningTriggerSeconds = 3f;
    private float lowMovementTimer = 0f;

    void Update()
    {
        // v1 버그 수정: 존재하지 않던 VirtualCursor.RawDelta 참조로 교체
        if (VirtualCursor.Instance.RawDelta.magnitude < lowMovementThreshold)
            lowMovementTimer += Time.deltaTime;
        else
            lowMovementTimer = 0f;

        bool shouldWarn = lowMovementTimer >= warningTriggerSeconds
            && ConstellationController.Instance.IsPlaying; // 완료/대기 상태에서는 표시 안 함
        gameObject.SetActive(shouldWarn);

        if (shouldWarn)
            RotateTowardTarget();
    }

    void RotateTowardTarget()
    {
        Vector2 dir = ConstellationController.Instance.CurrentTargetPosition
            - (Vector2)VirtualCursor.Instance.WorldPosition;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }
}
```

> `ConstellationController`에 `IsPlaying` 프로퍼티(`state == GameState.Playing`)를 노출해야 한다 (2.1절 클래스에 추가 필요).

### 4.4 결과 화면 구성

```csharp
public class ResultScreen : MonoBehaviour
{
    [SerializeField] private TMP_Text totalDistanceText;
    [SerializeField] private TMP_Text romUsageText;
    [SerializeField] private TMP_Text avgTimeBetweenStarsText;
    [SerializeField] private TMP_Text deltaFromLastSessionText;

    public void Display(SessionResult result, SessionResult previousResult)
    {
        // v2 수정: 단위를 "m"으로 표기하지 않음 (가상 커서 픽셀/유닛 거리이지 실측 미터가 아님)
        totalDistanceText.text = $"총 이동량: {result.totalRawDistance:F0} units";
        romUsageText.text = $"가동 영역 사용 비율: {result.romUsageRatio * 100f:F0}%";
        avgTimeBetweenStarsText.text = $"별 사이 평균 이동 시간: {result.avgTimePerStar:F1}s";

        if (previousResult != null)
        {
            float delta = result.romUsageRatio - previousResult.romUsageRatio;
            deltaFromLastSessionText.text = $"직전 회차 대비: {(delta >= 0 ? "+" : "")}{delta * 100f:F1}%p";
        }
        else
        {
            deltaFromLastSessionText.text = "직전 회차 데이터 없음";
        }
    }
}
```

### 4.5 가동 영역 사용 비율(ROM Usage Ratio) 계산 — v2 신규

**v1 결함**: HUD, 결과화면, 치료사 대시보드가 모두 참조하는 핵심 지표 `romUsageRatio`를 계산하는 코드가 v1에 존재하지 않았다. v2는 화면을 격자(grid)로 나누고 가상 커서가 방문한 셀의 비율로 계산하는 방식을 도입한다.

```csharp
public class ROMUsageTracker : MonoBehaviour
{
    [SerializeField] private int gridCols = 12;
    [SerializeField] private int gridRows = 8;
    [SerializeField] private RectTransform playAreaBounds;

    private bool[,] visitedCells;

    public void ResetTracking()
    {
        visitedCells = new bool[gridCols, gridRows];
    }

    void Update()
    {
        Vector2 normalizedPos = GetNormalizedPositionInPlayArea(VirtualCursor.Instance.ScreenPosition);
        int col = Mathf.Clamp((int)(normalizedPos.x * gridCols), 0, gridCols - 1);
        int row = Mathf.Clamp((int)(normalizedPos.y * gridRows), 0, gridRows - 1);
        visitedCells[col, row] = true;
    }

    public float GetUsageRatio()
    {
        int visited = 0;
        foreach (bool cell in visitedCells)
            if (cell) visited++;
        return (float)visited / (gridCols * gridRows);
    }

    private Vector2 GetNormalizedPositionInPlayArea(Vector2 screenPos)
    {
        Rect bounds = playAreaBounds.rect;
        return new Vector2(
            Mathf.InverseLerp(bounds.xMin, bounds.xMax, screenPos.x),
            Mathf.InverseLerp(bounds.yMin, bounds.yMax, screenPos.y)
        );
    }
}
```

> 격자 해상도(`gridCols x gridRows`)는 판정 반경 및 화면 크기에 맞춰 튜닝이 필요한 값이며, 너무 세밀하면 비율이 항상 낮게 나오고 너무 거칠면 변별력이 떨어진다. 1단계 플레이테스트에서 조정 대상으로 별도 표시(12절 검토 항목).

---

## 5. 데이터 시스템

### 5.1 측정 항목 데이터 구조 — JsonUtility 한계 반영

**v1 결함**: `JsonUtility`는 `Dictionary<TKey,TValue>`와 `DateTime`을 직렬화하지 못한다(예외 없이 조용히 빈 값으로 저장됨). v2는 둘 다 직렬화 가능한 형태로 교체하고, 프로젝트에 `Newtonsoft.Json` 패키지를 추가해 사용한다.

```csharp
[Serializable]
public class SessionResult
{
    public string sessionId;          // GUID
    public string patientId;          // v2 추가: 환자 식별자 (sessionId와 분리, 6절 참고)
    public string timestampIso;       // v2 수정: DateTime → ISO8601 string
    public DifficultyTier tier;
    public GameModeType mode;
    public float totalRawDistance;    // v2 수정: 감도 적용 전 raw delta 누적 (회차 간 비교 가능하도록)
    public float romUsageRatio;
    public float avgTimePerStar;
    public float[] directionFrequency; // v2 수정: Dictionary → 8방향 고정 배열 (인덱스 = Direction enum 값)
    public bool completed;
    public float elapsedSeconds;
}

public enum Direction { Up, UpRight, Right, DownRight, Down, DownLeft, Left, UpLeft }
public enum GameModeType { Basic, Mirror, Timer }
```

### 5.2 방향별 사용 빈도 분석 (DirectionAnalyzer)

`RecordMovement`는 항상 **raw delta**를 받는다 (`VirtualCursor.Update()`에서 호출 시점 확인, 0.1절). 감도 배율이나 거울 반전이 적용된 값을 넘기면 "보상 동작(편향) 탐지"라는 측정 목적 자체가 왜곡되므로, 물리적 팔 움직임 원본을 기준으로 분석해야 한다.

```csharp
public class DirectionAnalyzer : MonoBehaviour
{
    public static DirectionAnalyzer Instance { get; private set; }
    private float[] accumulated = new float[8];

    void Awake() => Instance = this;

    public void ResetTracking() => accumulated = new float[8];

    public void RecordMovement(Vector2 rawDelta)
    {
        if (rawDelta.sqrMagnitude < 0.0001f) return; // 사실상 정지 상태는 제외
        int sector = ClassifyDirection(rawDelta);
        accumulated[sector] += rawDelta.magnitude;
    }

    private int ClassifyDirection(Vector2 delta)
    {
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360f;
        return Mathf.RoundToInt(angle / 45f) % 8;
    }

    public float[] GetNormalizedFrequency()
    {
        float total = 0f;
        foreach (float v in accumulated) total += v;

        float[] result = new float[8];
        for (int i = 0; i < 8; i++)
            result[i] = total > 0f ? accumulated[i] / total : 0f;
        return result;
    }
}
```

> **v1 버그 수정**: 기존 `CursorTracker`는 `lastPosition = WorldPosition;`을 먼저 대입한 뒤 `WorldPosition - lastPosition`을 계산해 델타가 항상 `(0,0)`이 되는 결함이 있었다. v2는 입력 단계(`VirtualCursor`)에서 이미 프레임 delta를 직접 읽으므로(절대 위치 차분이 아니라 `Mouse Delta` 액션 자체가 델타값) 이 버그 자체가 구조적으로 사라진다.

### 5.3 저장 및 내보내기 — 암호화·무결성·장애 대응

> **v3 핵심 변경**: v2의 평문 JSON 저장은 환자 의료 데이터를 다루기에 부적합했다. v3는 (a) 저장 시 암호화, (b) 저장 실패/파일 손상 시 데이터 유실 방지(원자적 쓰기 + 백업), (c) 개인정보 물리 분리를 적용한다. 보안 설계 전반은 6절(보안)에서 상세히 다루며, 여기서는 저장 계층 구현만 정리한다.

```csharp
public class DataExporter : MonoBehaviour
{
    public static DataExporter Instance { get; private set; }
    void Awake() => Instance = this;

    private string SavePath => Path.Combine(Application.persistentDataPath, "sessions.enc");
    private string BackupPath => Path.Combine(Application.persistentDataPath, "sessions.enc.bak");

    public bool SaveSession(SessionResult result)
    {
        try
        {
            List<SessionResult> all = LoadAll();
            all.Add(result);
            string json = JsonConvert.SerializeObject(all, Formatting.None);
            byte[] encrypted = SecureStorage.Encrypt(json); // 6.2절: DPAPI 또는 AES

            // 원자적 쓰기: 임시 파일에 먼저 쓰고, 성공하면 교체 (쓰기 도중 크래시로 인한 손상 방지)
            string tempPath = SavePath + ".tmp";
            File.WriteAllBytes(tempPath, encrypted);

            if (File.Exists(SavePath)) File.Copy(SavePath, BackupPath, true); // 직전 버전 백업
            File.Delete(SavePath);
            File.Move(tempPath, SavePath);

            AuditLogger.Instance.Log(AuditAction.SessionSaved, result.patientId, result.sessionId); // 7절
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"세션 저장 실패: {e.Message}");
            AuditLogger.Instance.Log(AuditAction.SaveFailed, result.patientId, result.sessionId, e.Message);
            return false; // 호출 측에서 재시도/사용자 알림 처리
        }
    }

    public List<SessionResult> LoadAll()
    {
        if (!File.Exists(SavePath)) return new List<SessionResult>();
        try
        {
            byte[] encrypted = File.ReadAllBytes(SavePath);
            string json = SecureStorage.Decrypt(encrypted);
            return JsonConvert.DeserializeObject<List<SessionResult>>(json) ?? new List<SessionResult>();
        }
        catch (Exception e)
        {
            Debug.LogError($"주 파일 손상, 백업 복구 시도: {e.Message}");
            return LoadFromBackup(); // 파일 손상 시 백업본에서 복구
        }
    }

    private List<SessionResult> LoadFromBackup()
    {
        if (!File.Exists(BackupPath)) return new List<SessionResult>();
        try
        {
            string json = SecureStorage.Decrypt(File.ReadAllBytes(BackupPath));
            AuditLogger.Instance.Log(AuditAction.BackupRestored, null, null);
            return JsonConvert.DeserializeObject<List<SessionResult>>(json) ?? new List<SessionResult>();
        }
        catch { return new List<SessionResult>(); } // 백업도 손상 시 빈 리스트 (데이터 유실 로깅됨)
    }
}
```

**핵심 설계 포인트**:
- `SaveSession`이 `bool`을 반환하여 저장 실패를 호출 측이 인지하고 재시도/알림 가능 (v2는 실패 시 조용히 데이터 유실).
- 원자적 쓰기(`.tmp` → `Move`)로 쓰기 도중 전원 차단/크래시에도 기존 파일이 보존됨.
- 직전 저장본을 `.bak`으로 유지하여 주 파일 손상 시 1세대 복구 가능.
- 디스크 공간 부족(`IOException`)도 `catch`로 잡혀 사용자에게 알림 가능.

> **보존 정책**: 의료 데이터 보존 기간(의료법상 진료기록 관련 규정 검토 필요)과 파기 절차는 운영 정책으로 별도 수립해야 하며, 본 문서는 기술 구조만 제공한다. 자동 파기 기능은 오히려 위험할 수 있어 수동 승인 기반으로 설계할 것을 권장한다.

---

## 6. 보안 설계 (Security)

> 본 게임은 환자의 의료·재활 데이터를 다루므로, 일반 게임과 달리 보안이 필수 요구사항이다. v2까지의 "AES 권장" 한 줄을 실제 위협 모델 기반 설계로 확장한다.

### 6.1 위협 모델 (무엇으로부터 보호하는가)

| 위협 | 시나리오 | 대응 |
|---|---|---|
| 로컬 파일 직접 열람 | 공용/도난 PC에서 `sessions.enc`를 텍스트 에디터로 열어봄 | 저장 시 암호화 (6.2절) |
| 빌드 디컴파일 | exe를 디컴파일하여 하드코딩된 키 추출 | 키를 빌드에 넣지 않음 — DPAPI 사용 (6.2절) |
| 권한 없는 데이터 접근 | 환자가 다른 환자의 데이터를 봄 / 환자가 치료사 화면 진입 | 역할 기반 접근 제어 (7절) |
| 데이터 위·변조 | 기록이 몰래 수정·삭제됨 | 감사 로그 + 무결성 검증 (7절) |
| 개인정보 노출 | 훈련 데이터 파일에 실명이 함께 저장되어 유출 | 식별정보 물리 분리 (6.3절) |

> **범위 명시**: 1차 구현은 **단독 PC 로컬 환경**을 전제한다. 네트워크 전송(DB/클라우드 연동)은 확장 단계이며, 그 시점에는 전송 구간 TLS, 서버 측 접근 제어, 감사 로그 중앙화가 추가로 필요하다(별도 보안 검토).

### 6.2 저장 데이터 암호화 — 키 관리가 핵심

**가장 흔한 실수는 AES 키를 코드에 하드코딩하는 것이다.** 빌드된 exe는 디컴파일이 가능하므로 하드코딩된 키는 사실상 평문과 같다.

**권장: Windows DPAPI** — 키를 직접 관리하지 않고 OS(현재 사용자 계정)에 위임한다. 같은 PC의 같은 사용자만 복호화할 수 있어, 파일을 다른 PC로 복사해도 열리지 않는다.

```csharp
using System.Security.Cryptography;
using System.Text;

public static class SecureStorage
{
    // DPAPI: 키를 앱이 보관하지 않음. OS가 사용자 계정 기반으로 보호.
    private static readonly byte[] entropy = Encoding.UTF8.GetBytes("ConstellationTrace.v1"); // 추가 엔트로피(키 아님)

    public static byte[] Encrypt(string plainText)
    {
        byte[] data = Encoding.UTF8.GetBytes(plainText);
        return ProtectedData.Protect(data, entropy, DataProtectionScope.CurrentUser);
    }

    public static string Decrypt(byte[] encrypted)
    {
        byte[] data = ProtectedData.Unprotect(encrypted, entropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(data);
    }
}
```

> **DPAPI 한계와 대안**: DPAPI는 Windows 전용이며 "같은 사용자 계정" 범위로 묶인다. 여러 치료사가 한 PC를 공유(각자 다른 환자 담당)하거나, 데이터를 다른 기기로 이관해야 하는 운영 시나리오에서는 부적합하다. 그 경우 (a) 별도 마스터 키를 안전한 키 저장소(예: Windows Credential Manager)에서 로드하는 AES 방식, 또는 (b) 서버 기반 인증으로 전환해야 한다. 운영 환경이 확정되면 재검토가 필요한 항목이다(11절 검토 항목에 등재).

### 6.3 개인정보 물리 분리

훈련 데이터(`sessions.enc`)에는 실명·생년월일 등 식별정보를 절대 함께 저장하지 않는다. 식별정보는 별도 암호화 파일(`patients.enc`)에 저장하고, 두 저장소는 `patientId`(무의미한 발급 코드)로만 연결한다.

```
patients.enc   : { patientId → {실명, 생년월일, 진단명...} }   ← 접근 권한 최상위
sessions.enc   : { sessionId, patientId, 훈련/측정 데이터... }  ← patientId만 존재, 실명 없음
```

이렇게 하면 `sessions.enc`만 유출되어도 누구의 데이터인지 특정할 수 없다(가명처리). 두 파일을 모두 확보해야만 연결이 가능하며, `patients.enc`는 관리자 권한으로만 접근하도록 제한한다(7절).

---

## 7. 권한 관리 및 감사 로그 (Access Control & Audit)

> 평가 피드백 반영. 원본 기획서 8p "역할별 접근 권한 분리", "변경 이력 추적"의 실제 구현 설계.

### 7.1 역할 기반 접근 제어 (RBAC)

세 가지 역할을 정의하고, 각 역할이 접근 가능한 기능을 분리한다.

| 역할 | 접근 가능 범위 |
|---|---|
| 환자(Patient) | 자신의 게임 플레이, 자신의 결과화면만 |
| 치료사(Therapist) | 담당 환자들의 누적 데이터·대시보드, 난이도/모드 처방 |
| 관리자(Admin) | 전체 환자, 식별정보(`patients.enc`), 계정 관리, 감사 로그 열람 |

```csharp
public enum UserRole { Patient, Therapist, Admin }

public class SessionContext
{
    public static SessionContext Current { get; private set; }

    public string userId { get; private set; }
    public UserRole role { get; private set; }
    public List<string> assignedPatientIds { get; private set; } // 치료사가 담당하는 환자 목록

    public static void SignIn(string userId, UserRole role, List<string> assigned)
    {
        Current = new SessionContext { userId = userId, role = role, assignedPatientIds = assigned };
        AuditLogger.Instance.Log(AuditAction.SignIn, null, null, $"role={role}");
    }

    public bool CanAccessPatient(string patientId)
    {
        return role switch
        {
            UserRole.Admin => true,
            UserRole.Therapist => assignedPatientIds.Contains(patientId),
            UserRole.Patient => userId == patientId,
            _ => false
        };
    }
}
```

치료사 대시보드(10절)는 데이터 로드 전 권한을 검증하도록 수정한다.

```csharp
public void LoadPatientData(string patientId)
{
    // v3 추가: 권한 검증 게이트
    if (!SessionContext.Current.CanAccessPatient(patientId))
    {
        AuditLogger.Instance.Log(AuditAction.AccessDenied, patientId, null);
        ShowAccessDeniedMessage();
        return;
    }
    AuditLogger.Instance.Log(AuditAction.PatientDataViewed, patientId, null); // 열람 기록

    var sessions = DataExporter.Instance.LoadAll()
        .Where(s => s.patientId == patientId)
        .OrderBy(s => s.timestampIso)
        .ToList();
    // ... (이하 차트 표시 동일)
}
```

> **인증 수준 주의**: 로컬 단독 환경에서 "로그인"은 강력한 보안 경계가 아니다(코드 우회 가능). 1차 구현의 RBAC는 **실수·우발적 접근 방지** 수준이며, 강력한 인증이 필요하면 서버 기반 인증(확장 단계)으로 가야 한다. 이 한계를 명확히 인지하고 운영 정책으로 보완할 것.

### 7.2 감사 로그 (Audit Log)

"누가·언제·무엇을 했는가"를 기록한다. 의료 데이터의 열람/수정/삭제는 추적 가능해야 한다.

```csharp
public enum AuditAction
{
    SignIn, SignOut, AccessDenied,
    SessionSaved, SaveFailed, BackupRestored,
    PatientDataViewed, DataExported, DataDeleted,
    PrescriptionChanged // 치료사가 난이도/모드 변경
}

[Serializable]
public class AuditEntry
{
    public string timestampIso;
    public string actorUserId;   // 행위자
    public string action;        // AuditAction
    public string targetPatientId;
    public string targetSessionId;
    public string detail;
    public string prevHash;      // 직전 항목의 해시 (체인) — 위변조 탐지용
    public string entryHash;     // 본 항목의 해시
}

public class AuditLogger : MonoBehaviour
{
    public static AuditLogger Instance { get; private set; }
    void Awake() => Instance = this;

    private string LogPath => Path.Combine(Application.persistentDataPath, "audit.log.enc");

    public void Log(AuditAction action, string patientId, string sessionId, string detail = "")
    {
        var entry = new AuditEntry
        {
            timestampIso = DateTime.UtcNow.ToString("o"),
            actorUserId = SessionContext.Current?.userId ?? "system",
            action = action.ToString(),
            targetPatientId = patientId,
            targetSessionId = sessionId,
            detail = detail
        };

        var log = LoadLog();
        entry.prevHash = log.Count > 0 ? log[^1].entryHash : "GENESIS";
        entry.entryHash = ComputeHash(entry); // prevHash 포함하여 계산 → 해시 체인
        log.Add(entry);

        SaveLog(log); // append-only 의미로 운영 (수정 금지)
    }

    // 해시 체인 검증: 중간 항목이 변조되면 이후 모든 해시가 어긋나 탐지됨
    public bool VerifyIntegrity()
    {
        var log = LoadLog();
        string prev = "GENESIS";
        foreach (var e in log)
        {
            if (e.prevHash != prev) return false;
            string recomputed = ComputeHash(e);
            if (e.entryHash != recomputed) return false;
            prev = e.entryHash;
        }
        return true;
    }

    private string ComputeHash(AuditEntry e)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        string payload = $"{e.timestampIso}|{e.actorUserId}|{e.action}|{e.targetPatientId}|{e.targetSessionId}|{e.detail}|{e.prevHash}";
        byte[] hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
        return Convert.ToBase64String(hash);
    }

    // LoadLog / SaveLog: SecureStorage로 암호화하여 저장 (구현 생략, DataExporter와 동일 패턴)
}
```

**설계 포인트**:
- **해시 체인**: 각 항목이 직전 항목의 해시를 포함하므로, 중간 기록을 몰래 수정·삭제하면 이후 해시가 전부 어긋나 `VerifyIntegrity()`로 탐지된다(완전한 위변조 방지는 아니지만, 무단 수정의 흔적을 남긴다).
- **Append-only 운영**: 감사 로그는 추가만 하고 수정·삭제하지 않는다.
- **무엇을 남기는가**: 로그인, 접근 거부, 데이터 열람/저장/삭제/내보내기, 처방 변경. **단, 감사 로그 자체에 민감정보(실명, 측정값 원본)를 넣지 않는다** — `patientId`만 기록.

---

## 8. 회차별 추세 분석 (Progress Trend Analysis)

> 평가 피드백 반영. 치료사 대시보드(10절)가 단순히 회차별 값을 점으로 찍던 것을, "개선되고 있는가"를 판단하는 분석 계층으로 확장한다.

### 8.1 분석 항목

원본 11p의 "환자 개인의 회차별 변화 추이를 기준으로 평가한다"를 실제 지표로 구현한다.

| 분석 | 방법 | 목적 |
|---|---|---|
| 추세 방향 | 최근 N회차 선형 회귀 기울기 | 개선 / 정체 / 악화 판정 |
| 이동평균 | 단순 이동평균(SMA) | 회차별 변동성 제거, 큰 흐름 파악 |
| 편향 개선도 | 방향별 빈도의 표준편차 변화 | 보상 동작(특정 방향 편향) 감소 여부 |
| 개인 기준선 대비 | 첫 회차(또는 초기 평균) 대비 변화율 | 일반 평균이 아닌 개인 기준 평가 |

### 8.2 구현

```csharp
public enum TrendDirection { Improving, Stable, Declining, InsufficientData }

public class TrendAnalyzer
{
    // ROM 사용비율 추세 분석
    public TrendDirection AnalyzeRomTrend(List<SessionResult> ordered, int window = 5)
    {
        if (ordered.Count < 3) return TrendDirection.InsufficientData;

        var recent = ordered.Skip(Mathf.Max(0, ordered.Count - window)).ToList();
        float slope = LinearRegressionSlope(recent.Select(s => s.romUsageRatio).ToList());

        if (slope > 0.01f) return TrendDirection.Improving;
        if (slope < -0.01f) return TrendDirection.Declining;
        return TrendDirection.Stable;
    }

    // 단순 이동평균
    public List<float> MovingAverage(List<float> values, int window = 3)
    {
        var result = new List<float>();
        for (int i = 0; i < values.Count; i++)
        {
            int start = Mathf.Max(0, i - window + 1);
            float avg = values.Skip(start).Take(i - start + 1).Average();
            result.Add(avg);
        }
        return result;
    }

    // 방향 편향 개선도: 표준편차가 작아질수록 균형 잡힌 동작(편향 감소)
    public float DirectionBalanceScore(float[] directionFrequency)
    {
        float mean = directionFrequency.Average();
        float variance = directionFrequency.Select(f => (f - mean) * (f - mean)).Average();
        return Mathf.Sqrt(variance); // 낮을수록 균형적 (편향 적음)
    }

    // 개인 기준선 대비 변화율
    public float ImprovementVsBaseline(List<SessionResult> ordered)
    {
        if (ordered.Count < 2) return 0f;
        float baseline = ordered.Take(Mathf.Min(3, ordered.Count)).Average(s => s.romUsageRatio); // 초기 최대 3회 평균
        float recent = ordered.Last().romUsageRatio;
        return baseline > 0f ? (recent - baseline) / baseline : 0f;
    }

    private float LinearRegressionSlope(List<float> y)
    {
        int n = y.Count;
        float sumX = 0, sumY = 0, sumXY = 0, sumXX = 0;
        for (int i = 0; i < n; i++)
        {
            sumX += i; sumY += y[i];
            sumXY += i * y[i]; sumXX += i * i;
        }
        float denom = n * sumXX - sumX * sumX;
        return Mathf.Abs(denom) < 0.0001f ? 0f : (n * sumXY - sumX * sumY) / denom;
    }
}
```

### 8.3 대시보드 표시

치료사 대시보드는 단순 그래프에 더해 추세 판정 결과를 텍스트로 함께 제공한다.

```csharp
public void DisplayTrend(List<SessionResult> sessions)
{
    var analyzer = new TrendAnalyzer();
    TrendDirection trend = analyzer.AnalyzeRomTrend(sessions);
    float improvement = analyzer.ImprovementVsBaseline(sessions);
    float balanceNow = analyzer.DirectionBalanceScore(sessions.Last().directionFrequency);

    string trendText = trend switch
    {
        TrendDirection.Improving => "개선 추세",
        TrendDirection.Stable => "정체 (변화 적음)",
        TrendDirection.Declining => "주의: 하락 추세 — 난이도/피로도 점검 권장",
        _ => "데이터 부족 (3회 이상 필요)"
    };
    // trendText, improvement(%), 이동평균 곡선을 차트와 함께 표시
}
```

> **해석 주의**: 추세 분석은 **의사결정 보조**일 뿐 진단이 아니다. "하락 추세"가 곧 악화를 의미하지 않으며(피로·컨디션·난이도 변경 등 교란요인 존재), 최종 판단은 치료사 몫이다. 대시보드 문구도 단정적 표현을 피하고 "점검 권장" 수준으로 제시한다.

---

## 9. 위험 분석 (Risk Analysis)

> 평가 피드백 반영. 재활 의료 소프트웨어로서 환자 안전·데이터·기술 위험을 식별하고 완화책을 정의한다.

### 9.1 환자 안전 위험 (가장 중요)

| 위험 | 영향 | 발생 가능성 | 완화책 |
|---|---|---|---|
| 캘리브레이션 오류로 과도한 가동범위 요구 | 환자가 무리한 동작 → 부상·통증 | 중 | 캘리브레이션 값에 상한 클램프, 측정값 이상치 검증, 치료사 확인 후 적용 |
| 잘못된 난이도 적용 | 환자 수준에 안 맞는 훈련 | 중 | 난이도 변경은 치료사 권한으로만, 변경 시 감사 로그 기록 |
| 거울/타이머 모드의 부적절한 사용 | 보상 동작 유발 또는 과도한 속도 압박 | 저 | 원본 설계대로 치료사 설정 시에만 활성화 |
| 장시간 연속 플레이로 인한 피로 | 누적 피로·부상 | 중 | 권장 플레이 시간(원본 2~5분) 초과 시 휴식 안내 알림 |
| 화면 응시 피로·어지럼 | 어두운 배경 + 밝은 별의 장시간 응시 | 저 | 세션 간 휴식 권장, 명도 대비 조정 옵션 |

> **면책 범위 명시**: 본 소프트웨어는 의료기기가 아니라 **재활 보조 콘텐츠**다. 실제 처방·운동 강도 결정은 치료사·의료진의 판단에 따르며, 소프트웨어는 보조 도구로서의 한계를 UI에 명시하고 의료진 감독 하 사용을 전제한다. (의료기기 인증 대상 여부는 별도 법적 검토 필요.)

### 9.2 데이터 위험

| 위험 | 영향 | 완화책 |
|---|---|---|
| 로컬 파일 손상 | 훈련 기록 유실 | 원자적 쓰기 + 1세대 백업 + 복구 로직 (5.3절) |
| 저장 실패(디스크 부족 등) 미인지 | 조용한 데이터 유실 | `SaveSession` bool 반환 + 실패 시 알림·재시도 (5.3절) |
| 개인정보 유출 | 법적·윤리적 문제 | 식별정보 물리 분리 + 암호화 (6.3절) |
| 데이터 위·변조 | 평가 신뢰성 훼손 | 감사 로그 해시 체인 (7.2절) |
| PC 분실·도난 | 전체 데이터 노출 | DPAPI 암호화로 타 기기에서 복호화 불가 (6.2절) |

### 9.3 기술 위험

| 위험 | 영향 | 완화책 |
|---|---|---|
| 가상 커서 입력 구조 미검증 | 입력 파이프라인 전면 재작업 | M0 단계에서 우선 검증 (11절 마일스톤) |
| 프레임 드롭 시 판정 누락 | 별 연결 실패로 좌절감 | 선분 기반 판정 적용 (2.1절) |
| `CursorLockMode.Confined` 멀티모니터 이슈 | 커서 동작 불안정 | 실제 현장 PC 환경에서 검증 (12절 검토 항목) |
| DPAPI 사용자 종속성 | 계정/기기 변경 시 복호화 불가 | 운영 시나리오 확정 후 키 관리 방식 재검토 (6.2절) |
| 외부 패키지 의존(Newtonsoft.Json, 차트 에셋) | 버전 충돌·유지보수 | 버전 고정(package manifest), 라이선스 확인 |

### 9.4 위험 우선순위

가장 먼저 다뤄야 할 순서: **(1) 환자 안전(9.1) → (2) 가상 커서 검증(M0) → (3) 데이터 암호화·분리 → (4) 권한·감사 로그**. 안전 위험은 다른 모든 기능보다 우선하며, 캘리브레이션 상한 클램프는 M6 이전이라도 안전장치로 먼저 넣는다.

---

## 10. 치료사 모니터링 화면 (확장)

**v1 결함**: `sessionId.StartsWith(patientId)`로 환자를 필터링했는데, `sessionId`는 GUID라 `patientId`로 시작할 수 없어 필터링이 항상 빈 결과를 반환했다. v2에서 `patientId` 필드로 직접 필터링하도록 수정했고, v3에서 권한 검증 게이트(7.1절)와 추세 분석(8절)을 통합한다.

```csharp
public class TherapistDashboard : MonoBehaviour
{
    [SerializeField] private LineChartUI romTrendChart;
    [SerializeField] private BarChartUI directionBiasChart;
    [SerializeField] private TMP_Text trendSummaryText;

    public void LoadPatientData(string patientId)
    {
        // v3: 권한 검증 (7.1절) — 통과하지 못하면 데이터 접근 자체를 차단
        if (!SessionContext.Current.CanAccessPatient(patientId))
        {
            AuditLogger.Instance.Log(AuditAction.AccessDenied, patientId, null);
            ShowAccessDeniedMessage();
            return;
        }
        AuditLogger.Instance.Log(AuditAction.PatientDataViewed, patientId, null); // 열람 감사 기록

        var sessions = DataExporter.Instance.LoadAll()
            .Where(s => s.patientId == patientId) // v2 수정: 전용 필드로 정확히 매칭
            .OrderBy(s => s.timestampIso)
            .ToList();

        if (sessions.Count == 0) return; // 빈 리스트 가드 (First()/Last() 예외 방지)

        // 기본 차트
        var movingAvg = new TrendAnalyzer().MovingAverage(sessions.Select(s => s.romUsageRatio).ToList());
        romTrendChart.SetData(sessions.Select(s => s.romUsageRatio).ToList(), movingAvg); // 원본값 + 이동평균 함께
        directionBiasChart.SetComparison(sessions.First().directionFrequency, sessions.Last().directionFrequency);

        // v3: 추세 분석 결과 텍스트 (8.3절)
        DisplayTrend(sessions);
    }
}
```

> 차트 UI(`LineChartUI`/`BarChartUI`)는 Unity 기본 UI 또는 외부 차트 에셋(XCharts 등) 도입을 권장하며, 본 문서는 인터페이스 수준만 정의한다.

---

## 11. 개발 우선순위 / 마일스톤

| 단계 | 내용 | 핵심 산출물 |
|---|---|---|
| M0 | **입력 아키텍처 확정** | 가상 커서(`VirtualCursor`) 단독 동작 검증 — 감도 배율 적용 시 실제로 화면 도달 범위가 변하는지 확인 (다른 모든 단계의 전제조건) |
| M1 | 핵심 루프 + 판정 규칙 | 별 1~N개 순서대로 연결되는 프로토타입 (1개 패턴, Basic Mode만, 선분 판정 적용) |
| M2 | 난이도 시스템 + ScriptableObject 패턴 데이터화 | 1~3단계 전환 가능, PatternValidator 동작(NaN 가드 포함) |
| M3 | UI/UX + ROM 사용비율 계산 | HUD, 피드백, 결과화면, `ROMUsageTracker` 동작 확인 |
| M4 | 게임 모드 확장 | 거울 모드(델타 반전 검증), 타이머 모드 |
| M5 | 데이터 시스템 | Newtonsoft.Json 기반 저장/로드, 방향별 빈도 분석(raw delta 기준) |
| M6 | ROM 캘리브레이션 + **안전 클램프** | 환자 개인화 스케일링 + `VirtualCursor` 감도 배율 실제 연동 + 캘리브레이션 상한 클램프(9.1절 안전장치) |
| M7 | **데이터 보안** | 암호화 저장(DPAPI), 개인정보 물리 분리, 원자적 쓰기/백업/복구 |
| M8 | **권한·감사 로그** | RBAC, 감사 로그 해시 체인, 권한 검증 게이트 |
| M9 | 치료사 모니터링 + 추세 분석 | `patientId` 기반 대시보드 + `TrendAnalyzer`(추세 판정·이동평균·편향 개선도) |
| M10(확장) | COORDINATION 모드, 보조 로봇 연동 | 정지 판정 옵션, Send Force 데이터 기록 연동 |

> M0을 별도로 분리한 이유: 가상 커서 구조는 입력 파이프라인 전체에 영향을 미치므로, 프로토타입(M1) 단계에서 검증 없이 진행하면 이후 전부 재작업해야 할 위험이 가장 크다.
>
> **안전장치 선제 적용**: M6의 캘리브레이션 상한 클램프(9.1절)는 환자 안전 직결 항목이므로, 일정상 M6 이전에 캘리브레이션 관련 작업을 만지게 되면 그 시점에 먼저 넣는다. 보안(M7~M8)은 실제 환자 데이터를 저장하기 시작하는 시점 이전에 반드시 완료되어야 한다 — 평문으로 먼저 운영하다 나중에 암호화하면 기존 평문 데이터가 그대로 남는 문제가 생긴다.

---

## 12. 원본 기획서 대비 구현 시 검토 필요 항목 (지속 검토 대상)

1. **판정 반경(`judgeRadius`)** — 난이도별 `DifficultyConfig`에 분리되어 있으나 초기값은 추정치이며, 플레이테스트로 좌절감 vs 훈련 효과 균형을 맞춰야 함.
2. **거울/타이머 모드 활성화 조건의 치료사 설정 UI** — 구조는 정의했으나 실제 입력 UI(토글, 시간 입력 필드)는 M7에서 구체화.
3. **별자리 패턴의 임상적 타당성** — `PatternValidator`는 기하학적 규칙만 검증하며, 임상적 타당성은 작업치료사 자문이 필요한 영역(코드로 대체 불가).
4. **ROM 사용비율 격자 해상도(`gridCols x gridRows`)** — v2에서 신규 도입된 값으로, 너무 세밀하면 비율이 낮게 나오고 거칠면 변별력이 떨어짐. 플레이테스트 튜닝 필요.
5. **감도 배율 적용 시 "총 이동 거리"의 의미 변화** — raw delta 기준으로 통일했으나(5.1절), 환자가 "실제로 얼마나 팔을 움직였는가"를 물리량으로 환산하려면 캘리브레이션 단계의 화면-물리 비율 정보가 추가로 필요할 수 있음.
6. **Confined 커서 락 모드의 다중 모니터 환경 동작** — `CursorLockMode.Confined`가 일부 멀티 모니터 구성에서 예기치 않게 동작할 수 있어, 실제 재활 현장 PC 환경에서 검증 필요.
7. **암호화 키 관리 방식 확정** — 1차는 DPAPI(사용자 계정 종속)를 쓰지만, PC/계정 공유·데이터 이관 등 실제 운영 시나리오가 확정되면 AES+키 저장소 또는 서버 인증으로 재검토 필요(6.2절).
8. **의료기기 인증 대상 여부** — 본 소프트웨어가 의료기기에 해당하는지, 개인정보보호법·의료법상 어떤 의무가 적용되는지 법적 검토 필요. 본 문서는 기술 구조만 제공하며 규제 판단은 범위 밖.
9. **데이터 보존·파기 정책** — 보존 기간과 파기 절차는 운영 정책으로 수립해야 하며, 자동 파기는 위험하므로 수동 승인 기반 권장(5.3절).
10. **로그인/인증 강도** — 로컬 RBAC는 우발적 접근 방지 수준이며 강력한 보안 경계가 아님(7.1절). 강한 인증이 필요하면 서버 기반으로 전환.

---

## 13. 참고: 씬 흐름도

```
Title
  ↓
Login (역할 선택/인증 — 7.1절 RBAC, SessionContext 설정)
  ↓
 ├─[환자] → Calibration (최초 1회 또는 재측정 시)
 │            ↓
 │          DifficultySelect (치료사가 처방한 난이도/모드 범위 내에서)
 │            ↓
 │          Gameplay (핵심 루프, VirtualCursor + ROMUsageTracker)
 │            ↓
 │          ResultScreen (오버레이 패널, 씬 전환 없이 처리 권장)
 │            ↓
 │          DifficultySelect로 복귀 (반복 플레이) 또는 종료
 │
 └─[치료사/관리자] → TherapistMonitor (담당 환자 대시보드 + 추세 분석 + 처방)
                       ↑ 권한 검증(CanAccessPatient) 통과한 환자만 열람

* 모든 데이터 열람·저장·처방 변경은 AuditLogger에 기록됨 (7.2절)
```

---

## 14. 구현 시 추가로 챙겨야 할 세부사항 (사소하지만 누락 시 버그가 됨)

- `Camera.main`은 내부적으로 태그 검색이 발생하므로 매 프레임 호출하지 말고 `Awake()`에서 캐싱.
- 모든 싱글톤(`Instance`)은 `Awake()`에서 명시적으로 할당해야 하며, 씬에 중복 배치되지 않도록 가드 또는 `DontDestroyOnLoad` 처리 필요.
- 타이머 등 시간 관련 로직은 `Time.time` 대신 `Time.unscaledTime` 사용을 검토 (일시정지 기능 추가 시 `timeScale = 0`이어도 정상 동작).
- `VirtualCursor.ScreenPosition` 초기값은 화면 중앙으로 설정하되, 멀티 해상도 대응 시 `Screen.width/height` 변경 이벤트에 대한 재배치 로직 필요.
- 씬 전환 시 `DataLogger`가 진행 중인 세션 데이터를 잃지 않도록, 씬 전환 직전에 `FinalizeSession()` 또는 임시 저장 호출 위치를 명확히 정의해야 함.

---

## 15. 변경 이력

| 버전 | 주요 변경 |
|---|---|
| v1 | 최초 기술 기획서 작성 (원본 PDF 기반) |
| v2 | 코드 리뷰 결과 반영: 가상 커서 입력 구조 도입(ROM 감도 변환 실제 동작 가능하도록), 방향 분석 델타 계산 버그 수정, JsonUtility → Newtonsoft.Json 전환(Dictionary/DateTime 직렬화 문제 해결), 점-점 판정 → 선분 판정(터널링 방지), 상태 가드 추가(완료 후 인덱스 초과 접근 방지), ROM 사용비율 계산 모듈(`ROMUsageTracker`) 신규 추가, 거울 모드를 델타 반전 방식으로 재설계, 치료사 대시보드 환자 매칭 버그 수정(`patientId` 필드 분리), 미정의 참조(`Instance`, `FrameDelta` 등) 정리 |
| v3 | 평가 피드백 + 의료 데이터 보안 관점 반영: **보안 설계(6절)** 신규 — 위협 모델, DPAPI 기반 암호화(키 하드코딩 회피), 개인정보 물리 분리. **권한 관리·감사 로그(7절)** 신규 — RBAC, 권한 검증 게이트, 해시 체인 기반 감사 로그(위변조 탐지). **회차별 추세 분석(8절)** 신규 — 선형회귀 추세 판정, 이동평균, 방향 편향 개선도, 개인 기준선 대비 변화율. **위험 분석(9절)** 신규 — 환자 안전/데이터/기술 위험 식별 및 완화책, 안전 우선순위. 저장 계층(5.3절) 강화 — 암호화·원자적 쓰기·백업/복구·저장 실패 인지. 마일스톤에 보안(M7)·권한감사(M8)·안전 클램프 단계 추가. 씬 흐름도에 로그인/권한 분기 반영. 검토 항목에 키 관리·의료기기 인증·보존정책·인증강도 등재 |
