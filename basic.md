# .NET 

## C# runtime and C# 간의 관계

1. C# 소스코드 빌드 ( 런타임로더 + 중간 언어 ( IL Code ) = 실행 파일 )
2. IL ( Intermediate Language ) 중간 언어로 실행파일 ( EXE/ DLL) 내부에 생성.
3. 프로그램이 시작하자마자 닷넷 런타임을 로드하는 코드를 자동으로 실행. 

## .NET 호환 언어

- C#, Visual Basic, .NET, F#, C++/CLI
- COBOL, Lisp, Python, PHP, Ruby

## 공통 중간 언어 ( CIL: Common Intermediate Language )

- JAVA VM에서는 바이트코드(bytecode)  
- .NET 런타임에서는 CIL( Common Intermediate Language )
- runtime이 실행 될 때 IL 코드를 CPU의 기계어로 최종 번역. ( ilasm )

## 공용 타입 시스템 ( CTS: Common Type System )

- 닷넷 호환 언어가 지켜야할 타입의 표준 규격을 정의한 것
- 새로운 언어를 만들어 닷넷 런타임에서 실행하고 싶다면 CTS 규약을 만족하는 한도 내에서만 구현할 수 있다. 

## 공용 언어 사양 ( CLS: Common Language Specification )

- 닷넷 호환 언어가 지켜야 할 최소한의 언어 사양을 정의한 것.  
- 닷넷 호환 언어를 만들고 싶다면 CTS 전체를 구현할 필요는 없지만 적어도 CLS에 명시된 사양만큼은 완벽하게 구현해야 한다. 

## 메타데이터 ( metadata )

- 데이터를 위한 데이터
- C# 언어로 컴파일 된 실행 파일에도 메타데이터가 담겨 있다. 실행 파일에서 어떤 클래스와 메서드가 제공되는지를 확인 할 수 있다. 

## 어셈블리, 모듈, 매니페스트 

- `EXE` and `DLL` => `Assembly`  
- `Assembly`는 1개 이상의 모듈(Module)로 구성 된다. 
- Module하나 당 1개의 파일. 
- 

## reference 

[tools for dotnet](https://learn.microsoft.com/ko-kr/dotnet/framework/tools/)  


