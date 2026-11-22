# Gravity Shift

중력을 뒤집어 적을 처치하고 탈출하는 2D 액션 로그라이크 프로토타입입니다. [기획서](docs/기획서.md)에 구역과 전투 규칙을 정리했습니다.

## 실행

- **Unity 에디터**에서는 Unity Hub로 `GravityShift` 폴더를 엽니다. 에디터 버전은 **6000.6.10f1**입니다. `Assets/Scenes/GravityShift.unity`를 연 뒤 Play를 누릅니다.
- **Windows 빌드**는 에디터에서 **File → Build Profiles**를 열고 Windows 대상으로 빌드합니다. 출력 폴더를 `GravityShift/Build/Windows`로 지정한 뒤 `Gravity Shift.exe`를 실행합니다.
- **특정 시드**를 재현하려면 Main Camera의 Gravity Shift Game 컴포넌트에 있는 **Initial Seed**를 설정하거나, Windows 실행 파일에 `-seed 42`를 전달합니다. 기본값은 새 시드입니다.

## 조작

| 공통 | 키 |
|---|---|
| 이동 | A/D 또는 ←/→. 중력이 좌우로 작용하면 W/S 또는 ↑/↓ |
| 점프 | Space. 길게 누르면 더 높이 점프 |
| 대시 | Shift. 8방향이며 짧은 무적 |
| 근접 공격 | J |
| 스타일 전환 | Tab |
| 방 재시작 | R |
| 새 런 | N |

| 사이킥 | 엔지니어 |
|---|---|
| 좌클릭으로 대상 중력을 5초간 반전 | 좌클릭으로 중력 발판 설치 |
| 우클릭으로 잡고 버튼을 놓으면 던지기 | 마우스 휠 또는 Q로 설치 방향 회전 |
| F로 자신의 중력 반전 전환 | 우클릭으로 발판 제거, F로 설치된 발판 모두 반전 |

연구동, 마법탑, 궤도 정거장에는 각각 소개 방 하나와 시드로 생성하는 방 두 개가 있습니다. 모든 적을 처치하면 출구가 열립니다. 다음 방에 들어갈 때 체력을 1 회복하고, 사망하거나 마지막 방을 클리어하면 새 시드로 시작합니다.

## Unity 코드

| 파일 | 역할 |
|---|---|
| `GravityShiftGame.cs`, `Controls.cs` | 60Hz 게임 루프, Unity Input System, 카메라 |
| `GameState.cs`, `Body.cs`, `Abilities.cs` | 방 진행, 중력·타일 충돌, 전투와 능력 |
| `RoomMap.cs`, `ZoneDefinition.cs`, `RoomGenerator.cs` | 타일 좌표, 소개 방, 시드 기반 방 생성 |
| `RoomView.cs`, `HudView.cs` | URP 2D 스프라이트와 uGUI HUD |

소스는 `GravityShift/Assets/GravityShift/Runtime`에 있습니다. 월드 단위는 타일 한 칸이며, 중력과 접촉 판정은 엔티티별로 적용합니다. 웹과 같은 Mulberry32 알고리즘으로 시드별 방 배치를 재현합니다.

