Blazor에서 페이지 이동 시 데이터를 유지하는(State Management) 방법은 Blazor의 호스팅 모델(Server 또는 WebAssembly)과 데이터의 생명주기에 따라 여러 가지가 있습니다.

사용자(아키텍트)님의 전문성을 고려하여, 가장 일반적인 아키텍처 패턴부터 최신 기능까지 정리해 드립니다.

## 1\. DI 서비스를 이용한 State Container 패턴 (가장 권장)

가장 강력하고 표준적인 방법입니다. 상태를 저장할 클래스(State Container)를 만들고 \*\*Dependency Injection(DI)\*\*을 통해 여러 페이지가 공유하는 방식입니다.

  * **Blazor Server:** `AddScoped()`로 등록합니다. 유저의 *서킷(Circuit)* 단위로 서비스가 유지되므로, 페이지를 이동해도 동일한 인스턴스가 유지됩니다. (가장 적합)
  * **Blazor WebAssembly:** `AddSingleton()` 또는 `AddScoped()`로 등록합니다. (Wasm에서 둘은 동일하게 앱이 실행되는 동안 싱글톤으로 동작)

### 단계 1: 상태 컨테이너 서비스 정의

상태가 변경될 때 컴포넌트에게 알릴 수 있도록 `event`를 포함하는 것이 좋습니다.

```csharp
// /Services/MyPageStateService.cs

public class MyPageStateService : IDisposable
{
    // 1. 유지하려는 데이터
    public string? SharedData { get; private set; }
    public int SharedCounter { get; private set; }

    // 2. 상태 변경을 알리기 위한 이벤트
    public event Action? OnChange;

    public void SetData(string data, int counter)
    {
        SharedData = data;
        SharedCounter = counter;
        NotifyStateChanged();
    }

    // 3. UI가 다시 렌더링되도록 알림
    private void NotifyStateChanged() => OnChange?.Invoke();

    public void Dispose()
    {
        // Blazor Server에서 서킷 종료 시 리소스 정리 (필요시)
    }
}
```

### 단계 2: 서비스 등록 (`Program.cs`)

```csharp
// Blazor Server 또는 WebAssembly 모두
// Server에서는 Scoped가 유저별, Wasm에서는 Scoped가 Singleton처럼 동작합니다.
builder.Services.AddScoped<MyPageStateService>();
```

### 단계 3: 페이지 A (데이터 설정)

`@inject`로 서비스를 주입받고, 컴포넌트가 `OnChange` 이벤트를 구독/해제하도록 `IDisposable`을 구현합니다.

```razor
@page "/page-a"
@inject MyPageStateService StateService
@implements IDisposable

<h3>페이지 A</h3>

<input @bind="currentData" />
<input type="number" @bind="currentCounter" />
<button @onclick="SaveState">상태 저장 및 이동</button>

@code {
    private string? currentData;
    private int currentCounter;

    protected override void OnInitialized()
    {
        // 1. 서비스가 변경되면 이 컴포넌트도 다시 렌더링
        StateService.OnChange += StateHasChanged;

        // 2. 현재 상태로 UI 초기화
        currentData = StateService.SharedData;
        currentCounter = StateService.SharedCounter;
    }

    private void SaveState()
    {
        // 3. 서비스에 상태 저장
        StateService.SetData(currentData, currentCounter);
        
        // NavigationManager.NavigateTo("/page-b"); // (옵션) 저장 후 즉시 이동
    }

    // 4. 메모리 누수 방지를 위해 구독 해제
    public void Dispose()
    {
        StateService.OnChange -= StateHasChanged;
    }
}
```

### 단계 4: 페이지 B (데이터 읽기)

페이지 B도 동일하게 서비스를 주입받아 데이터를 사용합니다.

```razor
@page "/page-b"
@inject MyPageStateService StateService
@implements IDisposable

<h3>페이지 B</h3>
<p>페이지 A에서 저장된 데이터:</p>
<strong>@StateService.SharedData</strong>
<strong>@StateService.SharedCounter</strong>

@code {
    protected override void OnInitialized()
    {
        // 페이지 A가 데이터를 변경했을 때 B도 업데이트되도록 구독
        StateService.OnChange += StateHasChanged;
    }

    public void Dispose()
    {
        StateService.OnChange -= StateHasChanged;
    }
}
```

-----

## 2\. `HistoryEntryState` 활용 (Blazor WebAssembly 전용)

.NET 7부터 Blazor WebAssembly(및 MAUI Hybrid)에서는 브라우저의 `history.state` API를 활용하는 기능이 추가되었습니다. 이는 **뒤로 가기/앞으로 가기** 시 상태를 복원하는 데 특화되어 있습니다.

  * DI 서비스(위 1번)가 앱 전역 상태를 관리한다면, 이것은 **페이지 탐색 기록**에 종속된 임시 상태를 관리합니다.
  * **주의:** 데이터는 JSON으로 직렬화되며 약 640k의 문자열 제한이 있습니다. 하드 리프레시(F5) 시 사라집니다.

### 페이지 A (상태와 함께 이동)

```razor
@page "/page-a"
@inject NavigationManager NavigationManager

<input @bind="myFormData" />
<button @onclick="GoToPageB">B로 이동</button>

@code {
    private string myFormData = "유지할 데이터";

    private void GoToPageB()
    {
        NavigationManager.NavigateTo(
            "page-b",
            new NavigationOptions
            {
                // 이 데이터를 브라우저 탐색 기록에 저장
                HistoryEntryState = myFormData 
            });
    }
}
```

### 페이지 B (상태 복원)

페이지 B는 `NavigationManager.HistoryEntryState`에서 값을 읽어올 수 있습니다.

```razor
@page "/page-b"
@inject NavigationManager NavigationManager

<h3>페이지 B</h3>
<p>History State: @restoredData</p>

@code {
    private string? restoredData;

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
        {
            // HistoryEntryState는 OnInitialized가 아닌 OnAfterRender에서
            // 읽는 것이 안정적입니다.
            try
            {
                restoredData = NavigationManager.HistoryEntryState as string;
                StateHasChanged();
            }
            catch (Exception ex)
            {
                // (예외 처리)
            }
        }
    }
}
```

페이지 B에서 다시 A로 \*\*'뒤로 가기'\*\*를 하면, 페이지 A도 `OnAfterRender`에서 `HistoryEntryState`를 확인하여 이전 `myFormData`를 복원할 수 있습니다.

-----

## 3\. 기타 방법 및 주의사항

### Browser Storage (LocalStorage / SessionStorage)

  * `IJSRuntime`을 직접 사용하거나 `Blazored.LocalStorage` 같은 라이브러리를 사용합니다.
  * **장점:** 하드 리프레시(F5)나 브라우저를 재시작해도 데이터가 유지됩니다 (LocalStorage 기준).
  * **단점:** JS Interop 오버헤드가 발생하며, 모든 데이터를 직렬화/역직렬화해야 합니다. Blazor Server에서는 상태가 서버가 아닌 클라이언트에 저장되어 아키텍처가 복잡해질 수 있습니다.

### `PersistentComponentState` (주의\!)

.NET 8에서 SSR(정적 서버 렌더링)과 함께 자주 언급되는 기능입니다.

  * 이것은 페이지 A $\rightarrow$ 페이지 B 이동 시 **사용하는 것이 아닙니다.**
  * 이 기능의 용도는, **최초 페이지 로드 시** (1)서버에서 사전 렌더링(Prerendering)된 상태를 (2)클라이언트(Wasm 또는 Server Interactive)가 이어받을 때 상태를 유지하기 위함입니다. 즉, "Prerender $\rightarrow$ Interactive" 전환용입니다.

### 요약

| 방법 | 추천 대상 | 생명 주기 | 장점 | 단점 |
| :--- | :--- | :--- | :--- | :--- |
| **DI State Container** | **모두 (특히 Blazor Server)** | Scoped(Server) / Singleton(Wasm) | C\# 네이티브, 강력함, 테스트 용이, 아키텍처가 깔끔함 | 초기 설정 코드 필요 |
| **HistoryEntryState** | **Blazor WebAssembly 전용** | 브라우저 탐색 세션 | "뒤로 가기"에 특화됨, DI 불필요 | Wasm 전용, 데이터 제한, 하드 리프레시 시 소멸 |
| **Browser Storage** | 하드 리프레시 시 유지 필요시 | 영구적 (LocalStorage) | 데이터가 영구 보존됨 | JS Interop 필요, 직렬화/역직렬화 오버헤드 |