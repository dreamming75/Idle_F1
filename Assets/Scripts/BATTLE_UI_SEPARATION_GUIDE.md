# 전투 영역과 UI 영역 분리 가이드

## 개요
Player와 Enemy가 전투하는 영역과 UI 영역을 명확하게 분리하여 관리하는 방법입니다.

## 구성 방법

### 방법 1: Canvas 분리 (권장)

#### 1. 전투 영역 (Battle Area)
- **Canvas 설정**: `Screen Space - Camera` 모드
- **카메라**: 전투 전용 카메라에 연결
- **레이어**: 기본 레이어 (0) 또는 전투 전용 레이어
- **Sorting Order**: 낮은 값 (예: 0)
- **포함 요소**:
  - Player GameObject
  - Enemy (Monster) GameObject들
  - 전투 관련 이펙트
  - 배경 이미지

#### 2. UI 영역 (UI Area)
- **Canvas 설정**: `Screen Space - Overlay` 모드
- **레이어**: UI 레이어 (5)
- **Sorting Order**: 높은 값 (예: 100)
- **포함 요소**:
  - VirtualJoystick
  - HP 바
  - 스킬 버튼
  - 인벤토리
  - 메뉴 버튼
  - 기타 UI 요소

### 방법 2: Viewport 분할

화면을 수평 또는 수직으로 나누어 전투 영역과 UI 영역을 분리합니다.

#### 설정 방법:
1. `BattleAreaManager` 컴포넌트를 씬에 추가
2. `Use Viewport Split` 옵션 활성화
3. `Battle Area Ratio`로 전투 영역 비율 조정 (예: 0.7 = 70%)

### 방법 3: 레이어 기반 분리

#### 레이어 구성:
- **Battle Layer (0)**: 전투 오브젝트들
- **UI Layer (5)**: UI 요소들

#### 카메라 Culling Mask 설정:
- 전투 카메라: Battle Layer만 렌더링
- UI는 Overlay Canvas로 별도 렌더링

## Unity 에디터에서 설정하기

### 1. 전투 영역 Canvas 생성
```
1. Hierarchy에서 우클릭 > UI > Canvas
2. 이름을 "BattleCanvas"로 변경
3. Canvas 컴포넌트 설정:
   - Render Mode: Screen Space - Camera
   - Render Camera: BattleCamera (전투용 카메라)
   - Sorting Order: 0
4. 이 Canvas 아래에 Player, Enemy 등을 배치
```

### 2. UI 영역 Canvas 생성
```
1. Hierarchy에서 우클릭 > UI > Canvas
2. 이름을 "UICanvas"로 변경
3. Canvas 컴포넌트 설정:
   - Render Mode: Screen Space - Overlay
   - Sorting Order: 100
4. 이 Canvas 아래에 모든 UI 요소를 배치
```

### 3. BattleAreaManager 설정
```
1. 빈 GameObject 생성 (이름: "BattleAreaManager")
2. BattleAreaManager 스크립트 추가
3. Inspector에서 설정:
   - Battle Area Rect: BattleCanvas의 RectTransform
   - Battle Camera: 전투용 카메라
   - Battle Canvas: BattleCanvas 참조
   - UI Canvas: UICanvas 참조
   - Battle Layer: 0
   - UI Layer: 5
```

## 코드에서 사용하기

### 전투 영역에 오브젝트 추가
```csharp
BattleAreaManager manager = FindObjectOfType<BattleAreaManager>();
manager.AddToBattleArea(playerTransform);
manager.AddToBattleArea(enemyTransform);
```

### UI 영역에 오브젝트 추가
```csharp
BattleAreaManager manager = FindObjectOfType<BattleAreaManager>();
manager.AddToUIArea(joystickTransform);
manager.AddToUIArea(hpBarTransform);
```

## 장점

1. **명확한 분리**: 전투 로직과 UI 로직이 독립적으로 관리됨
2. **성능 최적화**: 각 영역을 독립적으로 최적화 가능
3. **유지보수 용이**: UI 변경이 전투 로직에 영향 없음
4. **확장성**: 새로운 UI 추가가 쉬움
5. **테스트 용이**: 각 영역을 독립적으로 테스트 가능

## 주의사항

1. **좌표계 차이**: 전투 영역은 World Space, UI는 Screen Space이므로 좌표 변환 필요
2. **이벤트 처리**: UI 이벤트가 전투 영역에 영향을 주지 않도록 주의
3. **카메라 설정**: 전투 카메라와 UI 카메라가 충돌하지 않도록 설정

## 예시 구조

```
Scene
├── BattleAreaManager
├── BattleCamera
├── BattleCanvas (Screen Space - Camera)
│   ├── BattleArea (RectTransform)
│   │   ├── Player
│   │   ├── Mob (Enemy Spawner)
│   │   └── Background
│   └── BattleEffects
└── UICanvas (Screen Space - Overlay)
    ├── SafeArea
    │   ├── VirtualJoystick
    │   ├── HPBar
    │   └── SkillButtons
    └── MenuPanel
```

