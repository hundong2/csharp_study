> 🔗 **참고 자료**: https://devblogs.microsoft.com/dotnet/dotnet-framework-3-5-moves-to-standalone-deployment-in-new-versions-of-windows/
> **출처**: Microsoft .NET Blog

---

# .NET Framework 3.5, 새로운 Windows 버전에서 독립형 배포 방식으로 전환

## 📌 개요
마이크로소프트는 최근 .NET 블로그를 통해 .NET Framework 3.5의 서비스 업데이트 방식이 새로운 버전의 Windows 운영체제에서 변경될 것임을 발표했습니다. 기존에는 Windows 기능의 일부로 간주되어 .NET Framework 3.5가 설치되지 않았더라도 관련 업데이트가 모든 시스템에 배포되었으나, 이제는 필요한 시스템에만 독립적으로 업데이트가 제공됩니다. 이는 .NET Framework 3.5에 의존하는 레거시 애플리케이션을 관리하는 개발자들에게 중요한 변화이며, 향후 운영체제 배포 및 업데이트 전략 수립에 영향을 미칠 수 있습니다.

## 🔍 핵심 내용
이번 발표의 주요 특징과 변경사항은 다음과 같습니다.

*   **기존 배포 방식의 비효율성 해소**: 기존에는 .NET Framework 3.5가 Windows의 '구성 요소 기반 서비스(CBS)'의 일부로 간주되어, 해당 프레임워크가 설치되지 않은 시스템에도 관련 보안 및 안정성 업데이트가 Windows Update를 통해 배포되었습니다. 이는 불필요한 네트워크 대역폭과 저장 공간을 낭비하는 원인이었습니다.
*   **새로운 독립형 배포 모델 도입**: Windows 11 22H2 및 Windows Server 2022 이후에 출시될 새로운 버전의 Windows 클라이언트 및 서버 운영체제부터 .NET Framework 3.5는 더 이상 CBS 구성 요소가 아닌 '독립형(Standalone)' 배포 모델로 전환됩니다.
*   **업데이트 방식의 변화**: 이제 .NET Framework 3.5가 설치된 시스템에만 해당 프레임워크의 서비스 업데이트가 Windows Update를 통해 제공됩니다. 설치되지 않은 시스템에는 더 이상 .NET Framework 3.5 관련 업데이트가 배포되지 않습니다.
*   **사용자 설치 경험 유지**: .NET Framework 3.5를 설치하는 사용자 경험 자체는 변하지 않습니다. 여전히 'Windows 기능 켜기/끄기'를 통하거나 DISM 명령어를 사용하여 필요한 경우 활성화하고 설치할 수 있습니다.
*   **효율성 증대**: 이 변화는 Windows Update의 효율성을 크게 향상시킵니다. 불필요한 업데이트 배포를 줄여 Microsoft의 인프라 비용을 절감하고, 사용자 입장에서는 업데이트 다운로드 및 설치에 소요되는 시간과 리소스를 줄일 수 있습니다.
*   **기존 Windows 버전에는 영향 없음**: Windows 8.x, 10, 11(22H2 포함), Windows Server 2012, 2016, 2019, 2022 등 이미 출시된 운영체제에서는 기존의 배포 및 업데이트 방식이 그대로 유지됩니다. 이 변경 사항은 오직 향후 출시될 새로운 버전의 Windows에만 적용됩니다.

## 💻 코드 예시
.NET Framework 3.5에서 도입된 LINQ(Language Integrated Query) 기능을 활용하여 간단한 데이터 처리를 수행하는 콘솔 애플리케이션 예시입니다. 이 코드는 .NET Framework 3.5 환경에서 빌드 및 실행되어야 합니다.

```csharp
using System;
using System.Collections.Generic;
using System.Linq; // LINQ는 .NET Framework 3.5부터 사용 가능합니다.

namespace NetFramework35Example
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine(".NET Framework 3.5 LINQ 예제");
            Console.WriteLine("----------------------------------");

            // 1. 숫자 리스트 정의
            List<int> numbers = new List<int> { 1, 5, 8, 12, 15, 22, 30, 35, 40, 42 };

            Console.WriteLine($"원본 숫자 리스트: {string.Join(", ", numbers)}");

            // 2. LINQ를 사용하여 10보다 큰 짝수만 필터링하고 정렬
            var result = from num in numbers
                         where num > 10 && num % 2 == 0
                         orderby num descending // 내림차순 정렬
                         select num;

            Console.WriteLine("\n10보다 큰 짝수 (내림차순):");
            foreach (var num in result)
            {
                Console.WriteLine($"- {num}");
            }

            // 3. LINQ 메서드 체이닝을 사용하여 홀수의 평균 계산
            //    (이 경우, 명시적으로 .NET Framework 3.5 기능을 사용함을 보여줍니다.)
            double averageOfOdds = numbers.Where(n => n % 2 != 0).Average();
            Console.WriteLine($"\n홀수들의 평균: {averageOfOdds}");
            
            // 4. 특정 조건에 맞는 요소의 개수 세기
            int countGreaterThanTwenty = numbers.Count(n => n > 20);
            Console.WriteLine($"\n20보다 큰 숫자의 개수: {countGreaterThanTwenty}개");

            Console.WriteLine("\n프로그램을 종료하려면 아무 키나 누르세요...");
            Console.ReadKey();
        }
    }
}
```

## 📊 실행 결과
위 C# 코드를 .NET Framework 3.5 환경에서 컴파일하고 실행했을 때의 예상 출력은 다음과 같습니다.

```
.NET Framework 3.5 LINQ 예제
----------------------------------
원본 숫자 리스트: 1, 5, 8, 12, 15, 22, 30, 35, 40, 42

10보다 큰 짝수 (내림차순):
- 42
- 40
- 30
- 22
- 12

홀수들의 평균: 17.0

20보다 큰 숫자의 개수: 5개

프로그램을 종료하려면 아무 키나 누르세요...
```

## 🚀 실무 활용 방법
이러한 .NET Framework 3.5 배포 방식의 변경은 실제 프로젝트에서 다음과 같이 활용될 수 있습니다.

1.  **레거시 애플리케이션 배포 환경 최적화**: 기업 내에 .NET Framework 3.5 기반의 레거시 애플리케이션이 많고, 동시에 새로운 버전의 Windows를 도입하는 경우, 시스템 이미지 생성 시 .NET Framework 3.5의 설치 여부를 명확히 결정하여 불필요한 업데이트 부담을 줄일 수 있습니다. 애플리케이션의 필수 요구사항을 분석하여 필요한 경우에만 .NET Framework 3.5를 포함시키도록 배포 스크립트나 이미지 빌드 프로세스를 조정합니다.
2.  **새로운 Windows OS 도입 전략 수립**: 최신 Windows 버전을 도입할 때, .NET Framework 3.5 의존성이 있는 애플리케이션을 위한 별도의 프로비저닝(provisioning) 단계를 계획해야 합니다. 'Windows 기능 켜기/끄기' 또는 DISM 명령을 자동화하여 .NET Framework 3.5를 사전 설치하거나, 필요시 사용자에게 설치를 안내하는 절차를 문서화하여 배포 혼란을 최소화할 수 있습니다.
3.  **운영체제 업데이트 관리 효율화**: IT 관리 부서에서는 Windows Update를 통해 배포되는 패치의 양을 줄여 전반적인 네트워크 대역폭 사용량을 최적화할 수 있습니다. .NET Framework 3.5가 설치되지 않은 PC에는 해당 업데이트가 전달되지 않으므로, 패치 관리 시스템의 부하를 줄이고 업데이트 배포 프로세스를 간소화하는 데 기여합니다.

## ⚠️ 주의사항 및 팁
.NET Framework 3.5 배포 방식 변경과 관련하여 다음 사항들을 주의하고 활용하세요.

*   **새로운 Windows 버전에만 적용**: 이 변경 사항은 Windows 11 22H2 및 Windows Server 2022 이후에 출시될 미래 버전의 Windows에만 해당합니다. 현재 사용 중인 대부분의 Windows 운영체제에는 영향을 미치지 않으므로, 기존 환경의 업데이트 방식에 혼란을 겪을 필요는 없습니다.
*   **명시적인 설치 필요성 증가**: 만약 미래의 Windows 버전에서 .NET Framework 3.5 기반 애플리케이션을 실행해야 한다면, 운영체제 설치 후 'Windows 기능 켜기/끄기' 또는 DISM 명령어를 통해 .NET Framework 3.5를 명시적으로 설치해야 함을 인지하고 계획에 반영해야 합니다.
*   **레거시 지원 계획 점검**: .NET Framework 3.5에 대한 의존성이 높은 레거시 애플리케이션을 가지고 있다면, 향후 새로운 Windows 버전으로 마이그레이션할 때 .NET Framework 3.5 설치 전략과 해당 애플리케이션의 호환성을 다시 한번 점검하는 것이 좋습니다.
*   **새로운 개발은 .NET (Core) 고려**: .NET Framework 3.5는 레거시 지원을 위한 것이며, 새로운 애플리케이션 개발에는 성능, 크로스 플랫폼 지원, 최신 기능 등을 고려하여 .NET 6, 7, 8 등 최신 .NET 플랫폼(.NET Core의 후속)을 사용하는 것을 강력히 권장합니다.

## 📚 더 알아보기
*   **Microsoft .NET Blog 원문**: [.NET Framework 3.5 Moves to Standalone Deployment in new versions of Windows](https://devblogs.microsoft.com/dotnet/dotnet-framework-3-5-moves-to-standalone-deployment-in-new-versions-of-windows/)
*   **Microsoft Learn - .NET Framework 3.5 설치 방법**: [Windows 10, Windows 8.1 및 Windows 8에서 .NET Framework 3.5 설치](https://learn.microsoft.com/ko-kr/dotnet/framework/install/dotnet-35-windows-10)
*   **Microsoft Learn - .NET Framework 수명 주기**: [.NET Framework 수명 주기 정책](https://learn.microsoft.com/ko-kr/lifecycle/products/microsoft-net-framework)
*   **Microsoft Learn - DISM을 사용한 .NET Framework 3.5 설치**: [DISM을 사용하여 .NET Framework 3.5 사용](https://learn.microsoft.com/ko-kr/windows-hardware/manufacture/desktop/enable-net-framework-35-using-deployment-image-servicing-and-management?view=windows-11)

---
*출처: Microsoft .NET Blog | 생성일: 2026년 02월 19일*