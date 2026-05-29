# 파이널 택틱스 실행 보정 런처

Windows 95/98 시절 게임인 한국어 Windows판 **파이널 택틱스**를 최신 Windows에서 조금 덜 삐걱거리게 실행하기 위한 보정 런처입니다.

이 저장소와 배포 파일은 게임 본편을 포함하지 않습니다. 사용자가 직접 보유한 원본 매체 또는 설치된 게임 파일이 필요합니다.

## 무엇을 해 주나요?

런처는 선택한 게임 폴더에 필요한 설정과 패치를 한 번에 적용합니다.

- 초반 이벤트 중 진행이 멈추는 문제 보정
- 이벤트 대사 표시 직후 클릭 선입력이 들어가는 문제 완화
- Indeo/AVI 초기화 때문에 시작이 꼬이거나 프로세스가 남는 문제 우회
- 최신 Windows에서 필요한 `ddraw.dll`, `DSETUP.DLL` 호환성 파일 생성
- 해상도, 창 모드, 고해상도 전체화면, FPS 제한, DPI 처리 설정
- 적용한 런처 패치를 되돌리는 `원본 복구` 기능

이미 호환성 래퍼 파일이 들어 있는 폴더에서는 기존 `ddraw.dll`, `DSETUP.DLL`을 덮어쓰지 않습니다.

## 다운로드와 실행

GitHub Releases에서 `finaltactics-startup-patch-*.zip`을 내려받아 원하는 폴더에 압축을 풉니다.

압축 파일 안에는 다음 파일이 들어 있습니다.

- `FinalTacticsLauncher.exe`
- `README.txt`
- `THIRD_PARTY_NOTICES.txt`

`FinalTacticsLauncher.exe`를 실행한 뒤 `게임 폴더`에 파이널 택틱스가 설치된 폴더를 지정하고 `적용` 또는 `실행`을 누르면 됩니다.

처음 실행하면 런처와 같은 폴더에 `FinalTacticsLauncher.ini`가 생길 수 있습니다. 마지막으로 선택한 게임 폴더와 화면 설정을 저장하는 파일입니다.

## 원본 복구

`원본 복구`는 런처가 건드린 것으로 확인되는 부분만 되돌립니다.

- `FT.exe`의 알려진 패치 바이트를 원본 바이트로 복구
- 런처가 생성한 `DDrawCompat-FT.ini` 삭제
- 런처가 생성한 `ddraw.dll`, `DSETUP.DLL`만 제거 또는 백업 복구
- 기존 호환성 래퍼 파일, 음악 폴더, 게임 설정 레지스트리는 보존

패치 적용 전에는 게임 폴더의 `analysis_out` 폴더에 백업이 생성됩니다.

## 지원하는 실행 파일

현재 런처는 다음 실행 파일을 기준으로 검증했습니다.

- 순정 설치본 `FT.exe`
- 바이트 레이아웃이 호환되는 일부 `FT.exe`

패치는 파일 해시 하나에만 의존하지 않고, 적용 가능한 실행 파일인지 먼저 확인한 뒤 진행합니다. 지원하지 않는 실행 파일이나 임의 수정본에서는 적용을 중단할 수 있습니다.

## 권리와 라이선스

- Final Tactics, Elemental Arts, InterSoft 및 관련 이름과 게임 데이터의 권리는 각 권리자에게 있습니다.
- 이 저장소와 배포 파일은 게임 본편, 원본 `FT.exe`, 음악, 영상, 모델, 데이터 파일, 세이브 파일, 원작 아이콘을 포함하지 않습니다.
- `DDrawCompat v0.7.1`의 `ddraw.dll`은 런처 exe 안에 내장되어 있으며 0BSD 라이선스입니다.
- `DSETUP.DLL`은 Microsoft 원본 DirectX DLL이 아니라 이 런처용 최소 호환성 stub입니다.
- 자세한 제3자 고지는 `THIRD_PARTY_NOTICES.md`를 확인하세요.

## 빌드

Windows에서 .NET Framework 4.x의 C# 컴파일러가 있으면 직접 빌드할 수 있습니다.

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

릴리스 zip까지 만들려면:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package_release.ps1
```

결과물은 `dist\finaltactics-startup-patch-v0.1.0.zip`에 생성됩니다.
