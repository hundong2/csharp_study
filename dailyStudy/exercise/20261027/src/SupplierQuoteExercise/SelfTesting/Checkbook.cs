namespace SupplierQuoteExercise.SelfTesting;

/// <summary>
/// 외부 테스트 package 없이 pass/fail을 누적해 예제를 바로 실행 가능하게 하는 작은 assertion 도구입니다.
/// 운영 프로젝트에서는 MSTest, xUnit, NUnit처럼 test runner가 제공되는 도구를 사용하세요.
/// </summary>
public sealed class Checkbook
{
    private int _passed;
    // []는 목표 List<string> 형식에 맞춰 빈 collection을 만드는 collection expression 문법입니다.
    private readonly List<string> _failures = [];

    /// <summary>
    /// 조건이 true인지 검사하고 성공 또는 실패를 기록합니다.
    /// </summary>
    /// <param name="condition">통과해야 하는 boolean 조건입니다.</param>
    /// <param name="name">실패했을 때 무엇이 깨졌는지 알려 줄 검사 이름입니다.</param>
    /// <returns>기록만 수행하므로 반환값은 없습니다.</returns>
    public void True(bool condition, string name)
    {
        if (condition)
        {
            _passed++;
            return;
        }

        _failures.Add($"{name}: 조건이 false입니다.");
    }

    /// <summary>
    /// 예상 값과 실제 값이 같은지 기본 equality로 검사합니다.
    /// </summary>
    /// <typeparam name="T">비교할 두 값의 공통 형식입니다.</typeparam>
    /// <param name="expected">정답으로 기대한 값입니다.</param>
    /// <param name="actual">실행 결과로 얻은 값입니다.</param>
    /// <param name="name">실패 원인을 찾기 위한 검사 이름입니다.</param>
    /// <returns>기록만 수행하므로 반환값은 없습니다.</returns>
    public void Equal<T>(T expected, T actual, string name)
    {
        if (EqualityComparer<T>.Default.Equals(expected, actual))
        {
            _passed++;
            return;
        }

        _failures.Add($"{name}: expected={expected}, actual={actual}");
    }

    /// <summary>
    /// 두 문자열 sequence가 같은 순서와 같은 값을 갖는지 검사합니다.
    /// </summary>
    /// <param name="expected">기대한 문자열 순서입니다.</param>
    /// <param name="actual">실제 실행에서 기록한 문자열 순서입니다.</param>
    /// <param name="name">실패 원인을 찾기 위한 검사 이름입니다.</param>
    /// <returns>기록만 수행하므로 반환값은 없습니다.</returns>
    public void SequenceEqual(
        IEnumerable<string> expected,
        IEnumerable<string> actual,
        string name)
    {
        var expectedArray = expected.ToArray();
        var actualArray = actual.ToArray();
        if (expectedArray.SequenceEqual(actualArray, StringComparer.Ordinal))
        {
            _passed++;
            return;
        }

        _failures.Add(
            $"{name}: expected=[{string.Join(", ", expectedArray)}], "
            + $"actual=[{string.Join(", ", actualArray)}]");
    }

    /// <summary>
    /// 예상한 예외 형식이 동기 동작에서 발생하는지 검사합니다.
    /// </summary>
    /// <typeparam name="TException">발생해야 하는 예외 형식입니다.</typeparam>
    /// <param name="action">예외를 발생시킬 것으로 기대하는 동기 동작입니다.</param>
    /// <param name="name">실패 원인을 찾기 위한 검사 이름입니다.</param>
    /// <returns>검사 결과를 누적하므로 반환값은 없습니다.</returns>
    public void Throws<TException>(Action action, string name)
        where TException : Exception
    {
        try
        {
            action();
            _failures.Add($"{name}: {typeof(TException).Name} 예외가 발생하지 않았습니다.");
        }
        catch (TException)
        {
            _passed++;
        }
        catch (Exception exception)
        {
            _failures.Add(
                $"{name}: {typeof(TException).Name} 대신 {exception.GetType().Name}이 발생했습니다.");
        }
    }

    /// <summary>
    /// 예상한 예외 형식이 비동기 동작에서 발생하는지 검사합니다.
    /// </summary>
    /// <typeparam name="TException">발생해야 하는 예외 형식입니다.</typeparam>
    /// <param name="action">예외를 발생시킬 것으로 기대하는 비동기 동작입니다.</param>
    /// <param name="name">실패 원인을 찾기 위한 검사 이름입니다.</param>
    /// <returns>예외 검사가 끝날 때 완료되는 Task를 반환합니다.</returns>
    public async Task ThrowsAsync<TException>(Func<Task> action, string name)
        where TException : Exception
    {
        try
        {
            await action();
            _failures.Add($"{name}: {typeof(TException).Name} 예외가 발생하지 않았습니다.");
        }
        catch (TException)
        {
            _passed++;
        }
        catch (Exception exception)
        {
            _failures.Add(
                $"{name}: {typeof(TException).Name} 대신 {exception.GetType().Name}이 발생했습니다.");
        }
    }

    /// <summary>
    /// 누적한 검사 결과를 console에 출력하고 process exit code를 결정합니다.
    /// </summary>
    /// <returns>실패가 없으면 0, 하나라도 있으면 1을 반환합니다.</returns>
    public int Finish()
    {
        foreach (var failure in _failures)
        {
            Console.Error.WriteLine($"[FAIL] {failure}");
        }

        Console.WriteLine($"[SELF-TEST] passed={_passed}, failed={_failures.Count}");
        return _failures.Count == 0 ? 0 : 1;
    }
}
