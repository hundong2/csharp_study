# 초보자 검증 단계

코드를 보지 않고 답한 뒤 실행 결과로 확인하세요.

1. `record`와 `class`, `string`과 `string?`는 각각 언제 쓰나요?
2. `bool`과 enum이 임의 문자열보다 안전한 이유는 무엇인가요?
3. `async`/`await`가 코드를 자동으로 병렬 실행한다는 뜻이 아닌 이유는 무엇인가요?
4. 예상 가능한 중복 계획은 Result, DB 연결 장애는 예외로 두는 이유는 무엇인가요?
5. Application Service, Repository, Strategy, Composition Root가 각각 무엇을 책임지나요?
6. DI/DIP, SRP, OCP가 테스트 가능성과 정책 확장에 어떤 도움을 주나요?
7. 개인정보 파일에 암호화, 만료, 삭제 작업, 감사 로그가 필요한 이유는 무엇인가요?

완료 기준: `dotnet build`가 경고·오류 없이 성공하고, 기본 실행에서 생성 1건·본인 확인 1건·거절 2건, self-test에서 `4/4 통과`가 표시됩니다.
