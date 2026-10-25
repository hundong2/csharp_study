namespace TelemetryStreamExercise.SelfTesting;

/// <summary>
/// 외부 test package 없이 핵심 계약을 자동 검증하는 작은 학습용 assertion 도구입니다.
/// 실제 제품에서는 MSTest, xUnit, NUnit 같은 test framework와 CI를 사용하는 것이 좋습니다.
/// </summary>
internal sealed class Checkbook
{
    // []는 목표 형식 List<string>에 맞춰 빈 collection을 만드는 C# collection expression입니다.
    private readonly List<string> _failures = [];
    private int _passed;
    private int _total;

    /// <summary>
    /// 조건이 참인지 검사하고 성공 또는 실패를 누적합니다.
    /// </summary>
    /// <param name="condition">참이어야 하는 검증 조건입니다.</param>
    /// <param name="name">실패했을 때 깨진 계약을 알려 줄 이름입니다.</param>
    /// <returns>누적 상태만 바꾸므로 반환값은 없습니다.</returns>
    public void True(bool condition, string name)
    {
        Record(condition, name, condition ? null : "조건이 false였습니다.");
    }

    /// <summary>
    /// 기대값과 실제값을 기본 equality 규칙으로 비교합니다.
    /// </summary>
    /// <typeparam name="T">비교할 두 값의 공통 형식입니다.</typeparam>
    /// <param name="expected">계약이 요구하는 기대값입니다.</param>
    /// <param name="actual">실제 코드가 만든 값입니다.</param>
    /// <param name="name">검증 목적을 설명하는 이름입니다.</param>
    /// <returns>누적 상태만 바꾸므로 반환값은 없습니다.</returns>
    public void Equal<T>(T expected, T actual, string name)
    {
        var isEqual = EqualityComparer<T>.Default.Equals(expected, actual);
        Record(isEqual, name, isEqual ? null : $"expected={expected}, actual={actual}");
    }

    /// <summary>
    /// 비동기 동작이 지정한 예외 형식으로 중단되는지 검사합니다.
    /// </summary>
    /// <typeparam name="TException">반드시 발생해야 하는 예외 형식입니다.</typeparam>
    /// <param name="action">실행할 비동기 동작입니다.</param>
    /// <param name="name">검증 목적을 설명하는 이름입니다.</param>
    /// <returns>예외 검사가 끝날 때 완료되는 Task를 반환합니다.</returns>
    public async Task ThrowsAsync<TException>(Func<Task> action, string name)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            await action();
            Record(false, name, $"{typeof(TException).Name} 예외가 발생하지 않았습니다.");
        }
        catch (TException)
        {
            Record(true, name, null);
        }
        catch (Exception exception)
        {
            Record(
                false,
                name,
                $"기대한 {typeof(TException).Name} 대신 {exception.GetType().Name}이 발생했습니다.");
        }
    }

    /// <summary>
    /// 누적된 검사 결과를 출력하고 process exit code를 결정합니다.
    /// </summary>
    /// <returns>모두 통과하면 0, 하나라도 실패하면 1을 반환합니다.</returns>
    public int Finish()
    {
        if (_failures.Count == 0)
        {
            Console.WriteLine($"[SELF-TEST] {_passed}/{_total} checks passed");
            return 0;
        }

        Console.Error.WriteLine($"[SELF-TEST] {_passed}/{_total} checks passed");
        foreach (var failure in _failures)
        {
            Console.Error.WriteLine($"  - {failure}");
        }

        return 1;
    }

    /// <summary>
    /// 한 검사 결과를 전체·성공 수와 실패 설명에 일관되게 반영합니다.
    /// </summary>
    /// <param name="passed">검사가 통과했는지 나타냅니다.</param>
    /// <param name="name">검증 목적을 설명하는 이름입니다.</param>
    /// <param name="detail">실패했을 때 추가할 실제 결과 설명입니다.</param>
    /// <returns>내부 누적 상태만 바꾸므로 반환값은 없습니다.</returns>
    private void Record(bool passed, string name, string? detail)
    {
        _total++;
        if (passed)
        {
            _passed++;
            return;
        }

        _failures.Add(detail is null ? name : $"{name}: {detail}");
    }
}
