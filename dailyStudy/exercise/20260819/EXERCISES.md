# 실습 과제

각 단계 뒤에 `dotnet build`와 `dotnet run -- --self-test`를 실행하세요. 한 번에 하나만 바꾸면 오류 원인을 찾기 쉽습니다.

## 1단계: 초보 검증

`LEAVE-100`의 요청 일수를 9로 바꾸고 결과가 거절로 변하는지 확인하세요. 이어서 `BalancedLeaveApprovalPolicy.Review`의 각 `if`를 한국어로 말해 보세요.

## 2단계: 문법과 LINQ

`ReviewSummary`에 `TotalCount` 속성을 추가하고 기본 실행에서 전체 건수도 출력하세요. 그 다음 `Where`를 사용해 관리자 검토 결과의 ID만 출력하세요.

## 3단계: Strategy

모든 병가를 관리자 검토로 보내는 `StrictSickLeavePolicy`를 구현하고 Composition Root에서 정책 한 줄만 교체하세요. 서비스 코드는 수정하지 않는 것이 핵심입니다.

## 4단계: 테스트 가능성과 운영

저장 두 번째 호출에서 실패하는 가짜 Repository를 만들고 서비스가 `InvalidOperationException`을 발생시키는 self-test를 추가하세요. 실제 DB라면 트랜잭션, 동시성 토큰, 멱등성 키 중 무엇이 필요한지도 주석으로 적으세요.
