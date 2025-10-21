Blazor의 상태 관리(State Management) 기술과 관련하여 아키텍처 수립 및 개발에 참고하실 만한 핵심 자료 및 오픈소스 프로젝트 10가지를 선정하여 설명해 드립니다.

DI를 활용한 상태 컨테이너 패턴부터 Flux/Redux 패턴 라이브러리, 그리고 최신 .NET 8의 SSR/하이브리드 환경까지 고려하여 구성했습니다.

-----

## 1\. ASP.NET Core Blazor 상태 관리 (공식 문서)

  * **링크:** [Microsoft Docs - Blazor State Management](https://www.google.com/search?q=https://learn.microsoft.com/aspnet/core/blazor/state-management)
  * **설명:**
    가장 먼저 참고해야 할 공식 문서입니다. DI 서비스를 이용한 **상태 컨테이너(State Container)** 패턴, URL의 쿼리 매개변수를 이용한 상태 전달, 그리고 `PersistentComponentState`를 사용한 Prerendering(사전 렌더링) 상태 유지 등 Blazor가 공식적으로 제안하는 모든 핵심 전략을 설명합니다. 아키텍처의 기본기를 다지는 데 필수적입니다.

## 2\. eShop (Microsoft 공식 샘플)

  * **링크:** [GitHub - dotnet-architecture/eShopOnWeb](https://github.com/dotnet-architecture/eShopOnWeb) (혹은 `eShopOnBlazor`)
  * **설명:**
    Microsoft의 대표적인 e-커머스 참조 아키텍처입니다. Blazor UI 프로젝트에서 **장바구니(Basket) 상태**를 어떻게 관리하는지 보여주는 훌륭한 예시입니다. `BasketViewModelService`와 같은 DI 서비스를 통해 사용자의 장바구니 상태를 격리하고 여러 컴포넌트가 공유하는 **상태 컨테이너 패턴의 교과서적인 구현**을 보여줍니다.

## 3\. Fluxor (라이브러리)

  * **링크:** [GitHub - mrpmorris/Fluxor](https://github.com/mrpmorris/Fluxor)
  * **설명:**
    Blazor를 위한 대표적인 **Flux/Redux 패턴** 구현 라이브러리입니다. 상태를 중앙 `Store`에 저장하고, `Action`을 통해 상태 변경을 요청하며, `Reducer`가 실제 상태를 변경합니다. 상태가 분산되어 복잡해지는 대규모 애플리케이션에서 상태를 예측 가능하고 일관되게 관리할 수 있도록 도와줍니다.

## 4\. Blazor-State (라이브러리)

  * **링크:** [GitHub - TimeWarpEngineering/blazor-state](https://www.google.com/search?q=https://github.com/TimeWarpEngineering/blazor-state)
  * **설명:**
    Fluxor와 마찬가지로 Redux 패턴을 기반으로 하지만, **MediatR 라이브러리**를 적극적으로 활용하는 것이 특징입니다. `Action`을 MediatR의 `Request`로 처리하여 상태 로직을 캡슐화합니다. 강력한 형식 지원과 CQRS 패턴을 선호하는 개발자에게 적합합니다.

## 5\. Blazored.LocalStorage (라이브러리)

  * **링크:** [GitHub - Blazored/LocalStorage](https://github.com/Blazored/LocalStorage)
  * **설명:**
    어제 설명해 드린 브라우저 저장소(LocalStorage/SessionStorage)를 C\#으로 쉽게 사용할 수 있게 해주는 사실상의 **표준 라이브러리**입니다. 사용자의 테마 설정, 로그인 토큰 등 **새로고침(F5)이나 브라우저 재시작 후에도 유지되어야 하는 상태**를 저장할 때 필수적으로 사용됩니다.

## 6\. Blazor Hero (클린 아키텍처 템플릿)

  * **링크:** [GitHub - blazorhero/CleanArchitecture](https://github.com/blazorhero/CleanArchitecture)
  * **설명:**
    Blazor 기반의 엔터프라이즈급 애플리케이션을 위한 클린 아키텍처 보일러플레이트입니다. 이 프로젝트의 `Client.Infrastructure` 등을 살펴보면, **사용자 인증 상태(AuthenticationState)나 다크 모드 같은 UI 상태**를 DI 서비스와 `Blazored.LocalStorage`를 조합하여 어떻게 관리하는지 실제 사례를 배울 수 있습니다.

## 7\. .NET Podcast App (하이브리드 샘플)

  * **링크:** [GitHub - microsoft/dotnet-podcasts](https://github.com/microsoft/dotnet-podcasts)
  * **설명:**
    Microsoft의 공식 .NET MAUI 및 Blazor Hybrid 샘플입니다. 사용자님의 관심사인 Blazor MAUI Hybrid 환경에서 **네이티브 UI(MAUI)와 웹 UI(Blazor)가 어떻게 동일한 상태(예: 현재 재생 중인 팟캐스트)를 공유**하는지 보여줍니다. `AddSingleton`으로 등록된 상태 서비스를 양쪽에서 주입받아 사용하는 패턴을 참고할 수 있습니다.

## 8\. .NET 8 Blazor 샘플 (최신 패턴)

  * **링크:** [GitHub - dotnet/blazor-samples/tree/main/8.0](https://github.com/dotnet/blazor-samples/tree/main/8.0)
  * **설명:**
    .NET 8의 정적 서버 렌더링(SSR)과 인터랙티브 렌더링이 혼합된 환경에서의 상태 관리를 보여줍니다. 특히 `PersistentComponentState`를 사용하여 **서버에서 렌더링된 상태를 클라이언트(Wasm/Server)로 전달**하는 예제(`Component-prerendering` 폴더)나 `[SupplyParameterFromQuery]`를 사용해 URL로 상태를 관리하는 예제를 눈여겨보셔야 합니다.

## 9\. Blazor WebAssembly HistoryEntryState (.NET 7+ 기능)

  * **링크:** [Microsoft Docs - Handle navigation history](https://www.google.com/search?q=https://learn.microsoft.com/aspnet/core/blazor/fundamentals/routing%3Fview%3Daspnetcore-8.0%23handle-navigation-history-state)
  * **설명:**
    어제 설명해 드린 `HistoryEntryState` 기능에 대한 공식 문서 및 예제입니다. `NavigationManager`를 사용하여 페이지 이동 시 JSON으로 직렬화 가능한 데이터를 \*\*브라우저 탐색 기록(History)\*\*에 저장합니다. '뒤로 가기' 버튼을 눌렀을 때 이전 페이지의 스크롤 위치나 폼 데이터를 복원하는 시나리오에 특화되어 있습니다.

## 10\. Awesome Blazor (리소스 모음)

  * **링크:** [GitHub - AdrienTorris/awesome-blazor](https://github.com/AdrienTorris/awesome-blazor)
  * **설명:**
    Blazor와 관련된 거의 모든 라이브러리, 샘플, 기사가 정리된 "Awesome List"입니다. 이 리스트의 **"State Management" 섹션**에 가면 Fluxor, Blazor-State 외에도 `Blazor.Flux`, `ReduxSimple`, `Blazor.Storage` 등 다양한 상태 관리 라이브러리들을 한눈에 비교하고 탐색할 수 있습니다.