# csharp resource.xaml 

## for style setting to xaml 

WPF 리소스(Resource)란?

​

n 리소스(Resource)는 일반적으로 한 번 이상 자주 사용하기를 원하는 자원을 이야기 한다. 컨트롤 또는 현재 창(Window)에 대해 또는 전체 응용 프로그램에 대해 전역으로 어떤 데이터를 저장하는 기능이다.


n 객체를 리소스로 정의하면 다른 윈도우, 컨트롤, 다른 어셈블리에서 객체에 접근할 수 있다. 즉 객체가 재사용 될 수 있다.



n 리소스는 리소스 사전(ResourceDictionary)에 정의되며 모든 객체는 리소스로 정의 될 수 있다. 고유 키는 XAML 리소스에 지정되며 해당 키를 사용하여 StaticResource, DynamicResource 태그 확장을 사용하여 참조 할 수 있다.


n 리소스에는 StaticResource, DynamicResource 두 가지 유형이 있는데 StaticResource는 참조가 처음 한번 이루어지고 변하지 않는 것이지만 DynamicResource는 런타임시에 값을 알수 있는 경우, 사용자 지정 컨트롤에 대한 테마스타일을 만들거나 참조하는 경우, 응용프로그램 라이프 사이클 기간 동안 ResourceDictionary의 내용을 조정, 변경하려는 경우에 이용되며 값이 사용될 때마다 변경여부에 대한 확인이 이루어진다. 즉 실시간 계산이 보류 되었다가 한번에 계산이 되는 형태인데 데이터 바인딩과 같이 변경이 동시에 이루어지는 형태에는 DynamicResource를 사용하지만 그외엔 StaticResource를 사용하면 된다.


n 정적 리소스(Static Resource)는 Window 또는 응용 프로그램이 시작될 때 로드 되므로 응용 프로그램이 무거워 느릴 때는 좋지 못할 수도 있다. 이에 반해 동적 리소스는 최초 사용될 때 로드되기 때문에 이 경우 동적 리소스가 장점이 있다.


n StaticResource와 DynamicResource의 차이는 참조하는 요소가 리소스를 검색하는 방법에 있다. StaticResource는 참조하는 요소에 의해 한 번만 검색되며 리소스의 전체 수명기간 동안 사용되지만 DynamicResource는 참조 된 객체가 사용될 때마다 획득된다.


n 실습 : 프로젝트에서 오른쪽 마우스 클릭 -> 추가 -> 리소스 사전(Resource Dictionary) 선택 후 새창에서 “리소스 사전” 클릭 후 파일명을 “mystyle.xaml”로 설정

## reference 

[ojc.asia - c# resource setting ](http://www.ojc.asia/bbs/board.php?bo_table=WPF&wr_id=123)  

