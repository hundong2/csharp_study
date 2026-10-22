namespace OrderPaymentReconciliation.Domain;

/// <summary>
/// 우리가 소유하지 않아 직접 수정할 수 없는 형식에도 읽기 좋은 도메인 표현을 붙일 수 있음을 보여 줍니다.
/// 이 예제에서는 C# 14의 extension block 문법으로 보고서 행에 계산 속성을 추가합니다.
/// </summary>
public static class ReconciliationLineExtensions
{
    // extension 블록은 C# 14 문법입니다. 원본 record를 상속하거나 수정하지 않고도
    // line.NeedsReview처럼 인스턴스 멤버로 보이는 확장 속성을 선언할 수 있습니다.
    extension(ReconciliationLine line)
    {
        /// <summary>
        /// 현재 행이 자동 일치가 아니어서 사람이 검토해야 하는지 알려 줍니다.
        /// </summary>
        // =>는 식 하나를 속성 값으로 쓰는 식 본문이고, is not은 특정 enum 값이 아님을 검사하는 패턴입니다.
        public bool NeedsReview => line.Status is not ReconciliationStatus.Matched;
    }
}
