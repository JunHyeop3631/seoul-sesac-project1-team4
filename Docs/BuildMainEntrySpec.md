# 빌드 메인 진입 — 작업42

기본 활성 씬 순서를 Title→ThrowYut으로 정리해 빌드 최초 화면을 메인으로 만든다. Title의 게임 시작은 게임 씬의1웨이브/하트10/일반120·특별0/배속1/미구매·보유0으로 들어간다. 진행 중 확인 후 메인 복귀와 재시작도 같은 새 판을 보장한다. 별도 활성 빌드 프로필의 씬 덮어쓰기가 있는지 확인한다.

Windows64 개발 검증 빌드를 실제로 생성하고 실행해 최초 씬/순서·메인 시작·게임→메인 확인/취소·재시작·플레이어 Application.Quit 종료를 확인한다. 검증 전용 명령행 프로브는 별도 복사본에만 넣고 명령행 플래그가 있을 때만 동작한다. 원본 런타임에는 검증 컴포넌트를 추가하지 않는다. 실제 빌드에는 원본과 같은 게임 코드/씬을 사용하며 테스트 동작은 Unity Button 콜백으로 실행한다. 배포용 최종 빌드와 한 판 전체 점검은46에서 구분한다.

## 변경과 실행 경로

1. [EditorBuildSettings](../ProjectSettings/EditorBuildSettings.asset)의 기존 ThrowYut→Title을 Title→ThrowYut으로 바꿨다. 활성 상태·씬 GUID·입력 설정 참조는 유지한다. 별도 프로필 자산의 씬 덮어쓰기는 검색에서 발견되지 않았고 실제 빌드 조회도 Global scene list였다.
2. [BuildMainEntryVerification.Build](../Assets/Editor/BuildMainEntryVerification.cs)는 전역 활성 씬 순서를 검사하고 그 목록 그대로 BuildPipeline.BuildPlayer에 전달한다. Windows64/Development·현재 Standalone Mono 백엔드를 사용한다. 빌드 결과·오류/경고·용량·시간·실제 씬 목록을 기록한다.
3. [MainEntryPlayerProbe 템플릿](../Tools/Verification/MainEntryPlayerProbe.cs)은 원본 Assets 밖에 둔다. 검증 복사본의 Assets/__Verification에만 복사하며 -verify-main-entry 인자가 있을 때만 실행한다. 최초 씬을 강제로 로드하지 않고 실제 부팅 씬/인덱스를 먼저 기록한다. 원본 게임 코드·씬·설정74개 파일이 복사본과 SHA-256으로 일치한다.
4. 프로브가 실제 타이틀/나가기 Unity Button 콜백으로 새 게임·취소·메인 이동·재시작을 실행한다. Application.quitting에서 실제 플레이어 종료 확인과 결과를 기록하고 외부 실행기가 프로세스 종료 코드0을 확인한다. 이 계측 객체만 검증 중 씬을 넘어 유지하며 원본 런타임의 진행 상태 저장 구조를 바꾸지 않는다.

## 실제 Windows 빌드 검증

- [빌드 결과](Verification/BuildMainEntryBuild.json): Unity6000.3.28f1·StandaloneWindows64·Mono2x·Development 빌드 성공,오류0/경고0. 약59.2초,보고 용량3,282,766,781바이트. 검증용 개발 빌드이며 배포용 용량 최적화 결과가 아니다.
- [실제 플레이어 결과](Verification/BuildMainEntryPlayer.json):30개 검사 통과·검사 구간 런타임 오류0. 최초 Title/index0,실제 씬 순서 Title→ThrowYut→Title→ThrowYut,명시적 종료 확인 뒤 Application.quitting과 프로세스 종료0을 확인했다.
- 메인 첫 화면/게임시작·중복 시작 거부,새 판1웨이브/하트10/120·0/배속1/미구매·보유0/유닛0·효과 없음,실제 아이템/유닛/배속 진행 후 메인 확인 취소 보존과 확인 이동,재시작 초기화·첫 던지기120→100을 검사했다.
- [종합 근거](Verification/BuildMainEntryVerification.json)에 원본/복사본74개 비교·프로브의 원본 Assets 미포함·실행파일/관리 어셈블리 해시·종료 코드를 기록했다.

실행 파일은 Builds/Verification/Task42/MainEntry.exe이며 같은 폴더의 데이터와 함께 사용한다. 일반 실행에는 자동 검증 인자가 없어 프로브가 동작하지 않는다. 자동 재검증 시에는 별도 복사본에 프로브를 넣고 -executeMethod BuildMainEntryVerification.Build -verification-build-output <절대 exe 경로>로 빌드한 뒤,실행 파일에 -verify-main-entry <절대 결과 디렉터리>를 전달한다. 테스트 재화/유닛/구매/효과는 실행 중에만 만들었다.

1920×1080 실제 플레이어 렌더링: [최초 메인](Previews/BuildMainEntryTitle.png)·[게임 진입](Previews/BuildMainEntryGame.png)·[플레이어 종료 확인](Previews/BuildMainEntryQuit.png).

플레이어 종료 정리 단계에는 ComputeBuffer GC 정리와 D3D12 자원 해제 경고가 남았다. [원문](Verification/BuildMainEntryPlayerShutdown.txt). 검사 구간 스크립트 오류는0이고 종료는0이지만 이 경고의 원인은 확정하지 않았다. 이번 작업에서 렌더 파이프라인을 임의 수정하지 않았다. 전체 한 판 수동 완주·OS 마우스/키보드 입력 자동 조작·배포용 최종 최적화는 수행하지 않았다. 이번 입력은 실제 플레이어 안의 Unity Button 콜백이다.

직접 확인은 실행 파일을 열어 메인에서 화면 클릭/Enter→게임시작으로 진입하고,메인 확인 취소/이동 후 새 게임과 종료 확인을 확인한다. 빌드 기본 첫 씬 변경은 에디터가 현재 열어 둔 씬을 강제로 바꾸지 않는다. 다음43은 일반 완료 보상 두루마리다.
