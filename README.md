# BoundaryTick

BoundaryTick은 윈도우에서 여러 모니터를 쓸 때, 마우스가 모니터 경계를 넘는 순간 살짝 걸렸다가 넘어가게 해주는 작은 트레이 프로그램입니다.

## 다운로드

[BoundaryTick.exe 다운로드](https://github.com/dccormierdc-lang/BoundaryTick/raw/refs/heads/main/BoundaryTick.exe)

## 사용 방법

1. 위 링크에서 `BoundaryTick.exe`를 다운로드합니다.
2. `BoundaryTick.exe`를 더블클릭해서 실행합니다.
3. 작업 표시줄 알림 영역의 `BoundaryTick` 아이콘을 우클릭해서 켜기/끄기와 걸림 강도를 바꿉니다.

처음 실행할 때 Windows SmartScreen 경고가 뜰 수 있습니다. 개인 개발자가 만든 미서명 실행 파일이라서 생기는 경고입니다.

## 종료

알림 영역 아이콘을 우클릭한 뒤 `종료`를 누르세요.
아이콘을 찾기 어려우면 `stop.cmd`를 실행해도 됩니다.

## 동작 방식

- 모니터끼리 맞닿은 내부 경계만 감지합니다.
- 커서가 경계를 넘어가려는 순간 기본값 기준 약 `140ms` 동안 현재 모니터의 마지막 픽셀에 붙잡습니다.
- 계속 밀면 잠깐 뒤 다음 모니터로 넘어갑니다.
- 설정은 `BoundaryTick.ini`에 저장됩니다.

## Build From Source

직접 빌드하고 싶다면 `build.cmd`를 실행하세요. `BoundaryTick.exe`가 생성됩니다.
`run.cmd`는 필요할 때 빌드한 뒤 프로그램을 실행합니다.

## Notes

This uses a global low-level mouse hook on Windows. Unsigned builds may trigger SmartScreen or antivirus warnings.
