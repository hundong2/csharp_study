# 실습 2: false sharing 재현

## 목표

스레드마다 다른 카운터를 증가시키더라도 카운터가 같은 CPU 캐시 라인에 있으면 서로의 성능을 떨어뜨릴 수 있습니다. 인접한 배열 배치와 128바이트 패딩 배치를 비교해 false sharing의 형태를 확인합니다.

## 실행

```powershell
cd D:\workspace\csharp_study\weekly-dotnetdev\20260718\weekly
.\run.ps1 false-sharing
```

## 관찰 포인트

- `long[]` 인접 배치와 `PaddedCounter[]` 배치의 elapsed 값을 비교합니다.
- 노트북 전원 모드, 코어 수, 백그라운드 작업에 따라 결과 차이가 달라질 수 있습니다.
- 성능 최적화는 핫 패스에서만 적용하고, 패딩으로 늘어나는 메모리 비용을 함께 봐야 합니다.

## 확장 과제

- 반복 횟수를 늘려 결과가 안정되는지 확인합니다.
- worker 수를 `Environment.ProcessorCount`보다 작게 고정해 차이를 비교합니다.
- 구조체 크기를 64바이트와 128바이트로 바꿔 실행 환경별 차이를 봅니다.
