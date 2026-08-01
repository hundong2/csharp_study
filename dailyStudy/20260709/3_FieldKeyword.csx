/*
문제: 목적지 주소를 저장할 때 공백을 제거하고 빈 값은 거부하는 속성을 구현하세요.

답안 포인트:
- C# 14 field 키워드 대신 명시적 백킹 필드 _destinationAddress를 사용합니다.
- set 접근자에서 유효성 검사를 수행합니다.
*/

using System;

public sealed class HighSpeedNetworkGateway
{
    private string _destinationAddress = "";

    public string DestinationAddress
    {
        get => _destinationAddress;
        set => _destinationAddress = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentNullException(nameof(value))
            : value.Trim();
    }
}

var gateway = new HighSpeedNetworkGateway();
gateway.DestinationAddress = "   10.0.0.5  ";
Console.WriteLine($"[Gateway Safe Mode] Pinned Target: '{gateway.DestinationAddress}'");

/*
실행 결과:
[Gateway Safe Mode] Pinned Target: '10.0.0.5'
*/

