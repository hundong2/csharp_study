# 실습 1: Process 출력 동시 수집

## 목표

자식 프로세스가 stdout과 stderr를 모두 많이 쓰는 상황에서 두 스트림을 동시에 읽어야 하는 이유를 확인합니다. 한쪽 스트림만 먼저 끝까지 읽는 코드는 다른 쪽 pipe 버퍼가 꽉 차면 자식 프로세스를 멈추게 만들 수 있습니다.

## 실행

```powershell
cd D:\workspace\csharp_study\weekly-dotnetdev\20260718\weekly
.\run.ps1 process
```

## 관찰 포인트

- 출력 줄 수가 stdout과 stderr 모두 정상적으로 수집되는지 확인합니다.
- `ReadToEndAsync()`를 두 스트림에 먼저 걸고 `WaitForExitAsync()`와 함께 기다리는 순서를 확인합니다.
- 실제 서비스 코드에서는 timeout, cancel token, 최대 출력 크기 제한을 같이 둬야 합니다.

## 확장 과제

- `ProcessStartInfo`에 `WorkingDirectory`를 지정해 상대 경로 실행 문제를 줄입니다.
- 자식 프로세스가 끝나지 않는 경우를 가정해 `CancellationTokenSource.CancelAfter()`를 추가합니다.
- stderr가 한 줄이라도 있으면 실패로 볼지, 종료 코드만 볼지 정책을 분리합니다.
