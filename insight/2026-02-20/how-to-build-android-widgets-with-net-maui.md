> 🔗 **참고 자료**: https://devblogs.microsoft.com/dotnet/how-to-build-android-widgets-with-dotnet-maui/
> **출처**: Microsoft .NET Blog

---

다음은 .NET 개발자를 위한 기술 뉴스레터 콘텐츠입니다.

# .NET MAUI로 안드로이드 위젯 구축하기: 홈 화면에 앱 기능 추가하기

## 📌 개요
안드로이드 위젯은 사용자가 앱을 실행하지 않고도 핵심 정보와 기능을 홈 화면에서 직접 활용할 수 있게 해주는 강력한 도구입니다. 이 뉴스레터에서는 .NET MAUI를 사용하여 인터랙티브한 안드로이드 위젯을 구축하는 방법을 설명합니다. .NET 개발자들은 C# 코드 베이스를 재사용하여 모바일 앱과 위젯을 동시에 개발할 수 있어, 개발 효율성을 높이고 사용자 경험을 향상시킬 수 있습니다. `RemoteViews`, `Intent` 및 공유 데이터를 활용하여 홈 화면에서 직접 데이터를 표시하고 상호 작용하는 위젯을 만들어 보세요.

## 🔍 핵심 내용
1.  **`AppWidgetProvider` 상속:** 모든 안드로이드 위젯은 `Android.Appwidget.AppWidgetProvider` 클래스를 상속받는 C# 클래스를 필요로 합니다. 이 클래스는 위젯의 라이프사이클 이벤트(생성, 업데이트, 삭제 등)를 처리하는 `OnUpdate`, `OnEnabled`, `OnDisabled`, `OnDeleted`와 같은 콜백 메서드를 제공합니다.
2.  **`RemoteViews`를 사용한 UI 정의:** 안드로이드 위젯의 UI는 일반적인 안드로이드 레이아웃 XML 파일(`.axml`)이 아닌 `Android.Widget.RemoteViews` 객체를 통해 정의됩니다. `RemoteViews`는 제한된 수의 표준 Android 뷰(TextView, Button, ImageView 등)와 레이아웃(LinearLayout, RelativeLayout, FrameLayout 등)만을 지원하며, 이를 통해 위젯의 레이아웃과 콘텐츠를 원격 프로세스에서 업데이트할 수 있습니다.
3.  **`Intent`와 `PendingIntent`를 통한 상호작용:** 위젯의 버튼 클릭 등 사용자 상호작용은 `Android.Content.Intent`를 통해 처리됩니다. `RemoteViews` 내의 특정 뷰에 `Android.App.PendingIntent`를 연결하여, 사용자가 위젯과 상호작용할 때 앱의 특정 액티비티를 실행하거나 브로드캐스트 리시버로 메시지를 보낼 수 있습니다.
4.  **`AppWidgetManager`를 통한 위젯 업데이트:** 위젯의 데이터나 UI를 프로그램적으로 업데이트하려면 `Android.Appwidget.AppWidgetManager` 인스턴스를 사용해야 합니다. `UpdateAppWidget` 메서드를 호출하여 새로운 `RemoteViews` 객체를 전달하면, 홈 화면에 있는 위젯이 새로운 내용으로 갱신됩니다.
5.  **공유 데이터를 이용한 앱-위젯 간 통신:** 앱과 위젯 간에 데이터를 효율적으로 공유하려면 Android의 "App Group" 기능을 활용할 수 있습니다. `context.GetSharedPreferences` 메서드 호출 시 앱 그룹 ID를 지정하여, 앱과 위젯 모두에서 동일한 저장소에 접근하여 데이터를 읽고 쓸 수 있습니다. 이를 통해 위젯이 앱의 최신 데이터를 반영하거나 위젯에서 발생한 액션을 앱에 전달할 수 있습니다.
6.  **위젯 메타데이터 정의:** 위젯의 초기 레이아웃, 최소/최대 크기, 크기 조정 가능 여부, 업데이트 주기 등은 `appwidget_info.xml` 파일을 통해 정의됩니다. 이 XML 파일은 `Resources/xml` 폴더에 위치해야 하며, `AndroidManifest.xml`에 위젯 리시버와 함께 `<meta-data>` 태그로 등록되어야 합니다.

## 💻 코드 예시

아래 C# 코드는 .NET MAUI 앱을 위한 간단한 카운터 위젯을 구현합니다. 위젯은 숫자를 표시하고, 버튼을 누르면 숫자가 증가하며, 이 값은 앱 그룹을 통해 저장됩니다.

```csharp
// MauiAppWidget.cs 파일 (YourAppNamespace.Widgets 폴더 등)

using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.OS;
using Android.Widget;
using System.Diagnostics; // Debug.WriteLine을 위해 추가

namespace MyMauiApp.Widgets
{
    // C# 특성을 사용하여 AndroidManifest.xml에 BroadcastReceiver를 자동으로 등록합니다.
    // Label은 위젯 선택 시 표시되는 이름입니다.
    [BroadcastReceiver(Enabled = true, Exported = true, Label = "My MAUI 카운터 위젯")]
    // 이 리시버가 처리할 Intent 필터를 정의합니다.
    // APPWIDGET_UPDATE 액션은 위젯 업데이트 시 호출됩니다.
    [IntentFilter(new string[] { AppWidgetManager.ActionAppwidgetUpdate })]
    // 위젯의 메타데이터를 정의하는 XML 파일의 경로를 지정합니다.
    [MetaData("android.appwidget.provider", Resource = "@xml/appwidget_info")]
    public class MauiAppWidget : AppWidgetProvider
    {
        // 위젯의 카운터 상태를 저장할 SharedPreferences 키
        private const string COUNT_KEY = "my_maui_widget_counter";
        // 버튼 클릭 시 사용될 사용자 정의 액션 문자열
        private const string ACTION_INCREMENT_COUNTER = "com.mymauiapp.INCREMENT_COUNTER";
        // 앱 그룹 ID (SharedPreferences를 공유하기 위해 필요, 실제 앱 그룹 ID로 변경하세요)
        // .csproj 파일의 PropertyGroup에 <MauiAppWidgetAppGroupId>group.com.mymauiapp</MauiAppWidgetAppGroupId> 설정 필요
        // AndroidManifest.xml의 <application> 태그에 android:appComponentFactory="androidx.core.app.CoreComponentFactory" 설정이 필요할 수 있습니다.
        private const string APP_GROUP_ID = "group.com.mymauiapp"; 

        /// <summary>
        /// 위젯이 업데이트될 때마다 호출되는 메서드입니다. (또는 위젯이 처음 추가될 때)
        /// </summary>
        public override void OnUpdate(Context context, AppWidgetManager appWidgetManager, int[] appWidgetIds)
        {
            // 모든 위젯 인스턴스를 순회하며 업데이트 로직을 적용합니다.
            foreach (int appWidgetId in appWidgetIds)
            {
                UpdateAppWidget(context, appWidgetManager, appWidgetId);
            }
        }

        /// <summary>
        /// 위젯의 첫 번째 인스턴스가 활성화될 때 호출됩니다.
        /// </summary>
        public override void OnEnabled(Context context)
        {
            base.OnEnabled(context);
            Debug.WriteLine("MAUI Widget Enabled!");
        }

        /// <summary>
        /// 위젯의 마지막 인스턴스가 비활성화될 때 호출됩니다. (모든 위젯이 제거될 때)
        /// </summary>
        public override void OnDisabled(Context context)
        {
            base.OnDisabled(context);
            Debug.WriteLine("MAUI Widget Disabled!");
        }

        /// <summary>
        /// 위젯이 호스트에서 삭제될 때 호출됩니다.
        /// </summary>
        public override void OnDeleted(Context context, int[] appWidgetIds)
        {
            base.OnDeleted(context, appWidgetIds);
            Debug.WriteLine($"MAUI Widget Deleted: {string.Join(", ", appWidgetIds)}");
        }

        /// <summary>
        /// 위젯이 특정 액션(예: 버튼 클릭)을 받을 때 호출됩니다.
        /// OnUpdate 외의 사용자 정의 Intent를 처리하는 데 사용됩니다.
        /// </summary>
        public override void OnReceive(Context context, Intent intent)
        {
            base.OnReceive(context, intent);

            // 버튼 클릭 액션인 경우 카운터를 증가시키고 위젯을 업데이트합니다.
            if (intent.Action == ACTION_INCREMENT_COUNTER)
            {
                // 공유 저장소에서 현재 카운터 값을 읽습니다.
                var sharedPrefs = context.GetSharedPreferences(APP_GROUP_ID, FileCreationMode.Private);
                int currentCount = sharedPrefs.GetInt(COUNT_KEY, 0);
                currentCount++; // 카운터 증가

                // 증가된 카운터 값을 저장합니다.
                sharedPrefs.Edit().PutInt(COUNT_KEY, currentCount).Apply();

                // 위젯을 강제로 업데이트하여 변경된 값을 표시합니다.
                var appWidgetManager = AppWidgetManager.GetInstance(context);
                // 현재 위젯 인스턴스 ID를 가져와 OnUpdate를 호출합니다.
                int[] appWidgetIds = appWidgetManager.GetAppWidgetIds(new ComponentName(context, Java.Lang.Class.FromType(typeof(MauiAppWidget))));
                if (appWidgetIds != null && appWidgetIds.Length > 0)
                {
                    OnUpdate(context, appWidgetManager, appWidgetIds);
                }
            }
        }

        /// <summary>
        /// 개별 위젯을 업데이트하는 헬퍼 메서드입니다.
        /// </summary>
        private static void UpdateAppWidget(Context context, AppWidgetManager appWidgetManager, int appWidgetId)
        {
            // RemoteViews 객체 생성 (Resources/layout/widget_layout.xml에 정의된 레이아웃 사용)
            RemoteViews views = new RemoteViews(context.PackageName, Resource.Layout.widget_layout);

            // 공유 저장소에서 현재 카운터 값을 읽습니다.
            var sharedPrefs = context.GetSharedPreferences(APP_GROUP_ID, FileCreationMode.Private);
            int currentCount = sharedPrefs.GetInt(COUNT_KEY, 0);

            // 텍스트뷰(ID: widget_text)에 카운터 값을 설정합니다.
            views.SetTextViewText(Resource.Id.widget_text, currentCount.ToString());

            // 버튼 클릭 시 수행할 Intent를 정의합니다.
            // ACTION_INCREMENT_COUNTER 액션을 가진 Intent를 이 리시버(MauiAppWidget)로 다시 보냅니다.
            Intent intent = new Intent(context, typeof(MauiAppWidget));
            intent.SetAction(ACTION_INCREMENT_COUNTER);
            
            // PendingIntent 생성 (버튼 클릭 시 BroadcastReceiver가 활성화되도록 함)
            // PendingIntentFlags.UpdateCurrent: 기존 PendingIntent가 있으면 새 데이터로 업데이트합니다.
            // PendingIntentFlags.Immutable: Android 6.0 (API 23) 이상에서 PendingIntent는 변경 불가능해야 합니다.
            PendingIntent pendingIntent = PendingIntent.GetBroadcast(context, 0, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            // 버튼(ID: widget_button)에 PendingIntent를 연결하여 클릭 이벤트를 처리합니다.
            views.SetOnClickPendingIntent(Resource.Id.widget_button, pendingIntent);

            // 위젯 업데이트를 AppWidgetManager에 요청합니다.
            appWidgetManager.UpdateAppWidget(appWidgetId, views);
        }
    }
}
```

**[필요한 XML 파일 (MAUI 프로젝트의 `Resources` 폴더 아래에 생성)]**

**1. `Resources/xml/appwidget_info.xml`**
```xml
<?xml version="1.0" encoding="utf-8"?>
<appwidget-provider xmlns:android="http://schemas.android.com/apk/res/android"
    android:minWidth="110dp"             <!-- 위젯의 최소 너비 -->
    android:minHeight="110dp"            <!-- 위젯의 최소 높이 -->
    android:updatePeriodMillis="86400000"<!-- 위젯 업데이트 주기 (24시간 = 86400000ms). 자주 업데이트하지 마세요. -->
    android:initialLayout="@layout/widget_layout" <!-- 위젯의 초기 레이아웃 -->
    android:resizeMode="horizontal|vertical" <!-- 위젯 크기 조절 가능 여부 -->
    android:widgetCategory="home_screen"   <!-- 위젯이 표시될 카테고리 (홈 화면) -->
    android:previewImage="@drawable/widget_preview" /> <!-- 위젯 미리보기 이미지 (선택 사항) -->
```

**2. `Resources/layout/widget_layout.xml`**
```xml
<?xml version="1.0" encoding="utf-8"?>
<LinearLayout xmlns:android="http://schemas.android.com/apk/res/android"
    android:layout_width="match_parent"
    android:layout_height="match_parent"
    android:background="#80000000" <!-- 반투명 검정 배경 -->
    android:orientation="vertical"
    android:gravity="center"
    android:padding="8dp">

    <TextView
        android:id="@+id/widget_text"
        android:layout_width="wrap_content"
        android:layout_height="wrap_content"
        android:textColor="#FFFFFF"
        android:textSize="24sp"
        android:textStyle="bold"
        android:text="0" /> <!-- 초기 텍스트 -->

    <Button
        android:id="@+id/widget_button"
        android:layout_width="wrap_content"
        android:layout_height="wrap_content"
        android:text="카운터 증가"
        android:layout_marginTop="8dp"/>
</LinearLayout>
```

## 📊 실행 결과
위 코드를 사용하여 .NET MAUI 앱을 빌드하고 Android 기기에 배포한 후, 안드로이드 홈 화면에서 위젯 추가 메뉴를 통해 `My MAUI 카운터 위젯`을 선택하여 추가할 수 있습니다. 위젯은 중앙에 "0"이라는 큰 글자와 "카운터 증가" 버튼을 표시합니다. "카운터 증가" 버튼을 누를 때마다 위젯의 숫자가 1씩 증가하여 "1", "2", "3" 등으로 변경되는 것을 확인할 수 있습니다. 이 카운터 값은 앱 그룹을 통해 영구적으로 저장되므로, 앱을 닫거나 기기를 재부팅해도 위젯의 카운터 값은 유지됩니다.

```
(안드로이드 홈 화면에 위젯이 추가된 모습)
+-------------------+
| My MAUI 카운터 위젯 |
|                   |
|        0          |
|                   |
|   [카운터 증가]   |
+-------------------+

(버튼 클릭 후)
+-------------------+
| My MAUI 카운터 위젯 |
|                   |
|        1          |
|                   |
|   [카운터 증가]   |
+-------------------+

(또 다른 버튼 클릭 후)
+-------------------+
| My MAUI 카운터 위젯 |
|                   |
|        2          |
|                   |
|   [카운터 증가]   |
+-------------------+
```

## 🚀 실무 활용 방법
1.  **실시간 정보 대시보드:** 주식 시세, 날씨 정보, 뉴스 헤드라인, 스포츠 경기 스코어 등 자주 확인해야 하는 정보를 앱을 실행하지 않고 위젯을 통해 실시간으로 업데이트하여 사용자에게 빠르게 제공할 수 있습니다. 예를 들어, 사용자가 설정한 주식 종목의 현재가를 위젯에 표시하고 탭하면 상세 차트 페이지로 이동하게 할 수 있습니다.
2.  **빠른 액션 및 바로가기:** 특정 앱 기능(예: Wi-Fi/Bluetooth 토글, 새 이메일 작성, 할 일 추가, 음악 재생 제어)을 위젯에 버튼으로 배치하여 사용자가 한 번의 탭으로 해당 기능을 실행하도록 할 수 있습니다. 예를 들어, '할 일 추가' 버튼을 눌러 앱의 새 할 일 작성 화면으로 바로 이동시킵니다.
3.  **데이터 동기화 및 알림:** 앱 내에서 중요한 데이터가 변경되거나 새로운 알림이 발생했을 때, 위젯을 통해 사용자에게 즉시 알리고 관련 정보를 표시할 수 있습니다. 예를 들어, 메일 앱에서 새로운 메시지 개수를 위젯에 표시하고, 탭하면 앱의 메시지 함으로 이동하는 기능을 구현할 수 있습니다.

## ⚠️ 주의사항 및 팁
*   **`RemoteViews`의 제한:** `RemoteViews`는 모든 Android 뷰를 지원하지 않으며, 복잡하거나 커스텀된 UI를 구현하기 어렵습니다. 애니메이션 같은 동적인 요소도 제한적이므로, 단순하고 효율적인 디자인을 지향해야 합니다. 지원되는 뷰 목록을 Android 공식 문서를 통해 미리 확인하는 것이 좋습니다.
*   **성능 최적화:** 위젯은 주기적으로 업데이트될 수 있으므로, `OnUpdate` 메서드 내에서 네트워크 요청이나 복잡한 데이터 처리와 같은 무거운 작업을 직접 수행하지 않도록 주의해야 합니다. 이러한 작업은 백그라운드 서비스에서 비동기적으로 수행하고, 결과를 위젯에 전달하여 업데이트하는 방식을 고려해야 합니다.
*   **데이터 공유 보안:** 앱 그룹을 통한 `SharedPreferences`는 앱과 위젯 간의 데이터 공유에 편리하지만, 비밀번호나 개인 정보와 같은 민감한 정보를 저장할 때는 암호화 등 추가적인 보안 조치를 고려해야 합니다.
*   **플랫폼 특이성:** 위젯은 안드로이드 플랫폼에 특화된 기능입니다. .NET MAUI는 크로스 플랫폼 개발을 지향하지만, 안드로이드 위젯 관련 코드는 Android 프로젝트 내에서 플랫폼별 코드(`Platforms/Android` 폴더)로 관리해야 합니다. iOS에는 '홈 화면 위젯'이 있지만, 구현 방식이 완전히 다르므로 별도로 개발해야 합니다.

## 📚 더 알아보기
*   **원본 블로그 포스트:** [How to Build Android Widgets with .NET MAUI](https://devblogs.microsoft.com/dotnet/how-to-build-android-widgets-with-dotnet-maui/)
*   **Android Developers - App Widgets:** [https://developer.android.com/develop/ui/views/appwidgets](https://developer.android.com/develop/ui/views/appwidgets)
*   **.NET MAUI 공식 문서:** [https://docs.microsoft.com/dotnet/maui](https://docs.microsoft.com/dotnet/maui)

---
*출처: Microsoft .NET Blog | 생성일: 2026년 02월 20일*