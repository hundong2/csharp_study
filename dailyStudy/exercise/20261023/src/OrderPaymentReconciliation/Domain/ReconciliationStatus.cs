namespace OrderPaymentReconciliation.Domain;

/// <summary>
/// 한 주문과 결제의 대사 결과를 제한된 다섯 상태 중 하나로 표현합니다.
/// enum은 임의 문자열 대신 compiler가 검사할 수 있는 이름 집합을 만드는 C# 문법입니다.
/// </summary>
public enum ReconciliationStatus
{
    Matched,
    MissingPayment,
    AmountMismatch,
    CurrencyMismatch,
    OrphanPayment
}
