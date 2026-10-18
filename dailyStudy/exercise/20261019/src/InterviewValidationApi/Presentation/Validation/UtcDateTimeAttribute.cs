using System.ComponentModel.DataAnnotations;

namespace InterviewValidationApi.Presentation.Validation;

/// <summary>
/// JSON binding이 만든 DateTimeOffset 값의 오프셋이 0인지 확인하는 전송 계층 검증 Attribute입니다.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class UtcDateTimeAttribute : ValidationAttribute
{
    /// <summary>
    /// null은 RequiredAttribute에 맡기고, 값이 있으면 DateTimeOffset이며 오프셋이 0인지 확인합니다.
    /// </summary>
    /// <param name="value">JSON binding이 만든 검사 대상 값입니다.</param>
    /// <param name="validationContext">검사 대상 멤버 이름 등 DataAnnotations 문맥입니다.</param>
    /// <returns>UTC이거나 null이면 Success, 다른 오프셋이나 형식이면 ValidationResult를 반환합니다.</returns>
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
        {
            return ValidationResult.Success;
        }

        // JSON binder는 Z, +00:00, -00:00 같은 원문 표기를 모두 Offset=0인 값으로 바꿉니다.
        // 따라서 이 Attribute는 원문 철자가 아니라 binding 뒤의 의미만 검사합니다.
        // is not은 값의 runtime 형식을 확인하면서 잘못된 형식 분기를 읽기 쉽게 표현하는 패턴 매칭입니다.
        if (value is not DateTimeOffset timestamp || timestamp.Offset != TimeSpan.Zero)
        {
            return new ValidationResult(
                ErrorMessage ?? "시각은 binding 뒤 UTC의 0 오프셋이어야 합니다.",
                [validationContext.MemberName ?? "timestamp"]);
        }

        return ValidationResult.Success;
    }
}
