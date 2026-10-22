namespace OrderPaymentReconciliation.Domain;

/// <summary>
/// 사용자가 고칠 수 있는 실패를 예외 대신 값으로 전달하는 불변 오류 모델입니다.
/// Code는 프로그램이 비교할 안정적인 식별자이고, Message는 사람이 읽는 설명이며, Field는 잘못된 입력 위치입니다.
/// </summary>
/// <param name="Code">분기와 테스트에 사용하는 안정적인 오류 코드입니다.</param>
/// <param name="Message">초보자와 API 사용자가 이해할 수 있는 오류 설명입니다.</param>
/// <param name="Field">특정 입력 필드의 오류라면 그 이름이고, 전체 오류라면 null입니다.</param>
// record는 값이 같으면 같은 것으로 비교되는 불변 데이터 모델을 간결하게 선언하는 C# 문법입니다.
// string?의 ?는 Field에 문자열뿐 아니라 "값 없음(null)"도 올 수 있음을 compiler에 알립니다.
public sealed record Error(string Code, string Message, string? Field = null);
