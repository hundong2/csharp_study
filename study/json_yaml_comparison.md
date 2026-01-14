# JSON vs YAML Comparison

JSON과 YAML의 공통점과 차이점을 상세히 설명드리겠습니다.

## 공통점

둘 다 **계층적 데이터 구조**를 표현하는 직렬화 형식입니다:

- 객체/딕셔너리 (key-value 쌍)
- 배열/리스트
- 문자열, 숫자, 불리언, null

```csharp
public class Person
{
    public string Name { get; set; }
    public int Age { get; set; }
    public List<string> Hobbies { get; set; }
    public Address Address { get; set; }
}

public class Address
{
    public string City { get; set; }
    public string Country { get; set; }
}
```

## 차이점 비교

### 1. 구문 스타일

**JSON - 엄격하고 명시적**
```json
{
  "name": "홍길동",
  "age": 30,
  "hobbies": ["독서", "운동", "음악"],
  "address": {
    "city": "서울",
    "country": "한국"
  }
}
```

**YAML - 간결하고 가독성 중심**
```yaml
name: 홍길동
age: 30
hobbies:
  - 독서
  - 운동
  - 음악
address:
  city: 서울
  country: 한국
```

### 2. 문자열 처리

**JSON - 항상 따옴표 필요**
```json
{
  "message": "Hello, World!",
  "multiline": "첫 줄\n두 번째 줄\n세 번째 줄"
}
```

**YAML - 따옴표 선택적, 여러 줄 표현 방식**
```yaml
# 따옴표 없이
message: Hello, World!

# 리터럴 블록 (줄바꿈 유지)
multiline: |
  첫 줄
  두 번째 줄
  세 번째 줄

# 폴드 블록 (한 줄로 합침)
description: >
  이것은 긴 텍스트입니다.
  여러 줄로 작성했지만
  실제로는 한 줄로 처리됩니다.

# 따옴표 필요한 경우 (특수문자)
special: "값: 콜론이 있음"
```

### 3. 주석 지원

**JSON - 주석 없음 (표준)**
```json
{
  "setting": "value"
}
```

**YAML - 주석 지원**
```yaml
# 이것은 주석입니다
setting: value  # 인라인 주석도 가능
```

### 4. 데이터 타입

**JSON - 제한적**
```json
{
  "string": "text",
  "number": 42,
  "float": 3.14,
  "boolean": true,
  "null": null,
  "array": [1, 2, 3],
  "object": {}
}
```

**YAML - 더 풍부한 타입**
```yaml
string: text
number: 42
float: 3.14
boolean: true
null: null
null_alt: ~  # null의 다른 표현

# 날짜/시간 (자동 인식)
date: 2024-01-15
datetime: 2024-01-15T10:30:00Z

# 명시적 타입 지정
hex_number: !!int 0x1A
binary: !!binary R0lGODlhAQABAAAAACw=

# 집합
unique_items: !!set
  ? item1
  ? item2
  ? item3
```

### 5. 참조와 앵커 (YAML만 가능)

**JSON - 중복 작성 필요**
```json
{
  "default_config": {
    "timeout": 30,
    "retry": 3
  },
  "service1": {
    "timeout": 30,
    "retry": 3,
    "endpoint": "api1"
  },
  "service2": {
    "timeout": 30,
    "retry": 3,
    "endpoint": "api2"
  }
}
```

**YAML - 앵커와 별칭으로 재사용**
```yaml
default_config: &defaults
  timeout: 30
  retry: 3

service1:
  <<: *defaults  # 앵커 참조
  endpoint: api1

service2:
  <<: *defaults
  endpoint: api2
  timeout: 60  # 오버라이드 가능
```

### 6. 리스트 표현

**JSON - 한 가지 방식**
```json
{
  "items": ["item1", "item2", "item3"]
}
```

**YAML - 두 가지 방식**
```yaml
# 블록 스타일 (선호)
items:
  - item1
  - item2
  - item3

# 플로우 스타일 (JSON과 유사)
items: [item1, item2, item3]

# 복잡한 객체 리스트
users:
  - name: 홍길동
    age: 30
    hobbies:
      - 독서
      - 운동
  - name: 김철수
    age: 25
    hobbies:
      - 게임
      - 영화
```

## C# 코드 비교

### JSON 직렬화
```csharp
using System.Text.Json;

var person = new Person
{
    Name = "홍길동",
    Age = 30,
    Hobbies = new List<string> { "독서", "운동", "음악" },
    Address = new Address { City = "서울", Country = "한국" }
};

var options = new JsonSerializerOptions 
{ 
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};

string json = JsonSerializer.Serialize(person, options);
var restored = JsonSerializer.Deserialize<Person>(json, options);
```

### YAML 직렬화
```csharp
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

var person = new Person
{
    Name = "홍길동",
    Age = 30,
    Hobbies = new List<string> { "독서", "운동", "음악" },
    Address = new Address { City = "서울", Country = "한국" }
};

var serializer = new SerializerBuilder()
    .WithNamingConvention(CamelCaseNamingConvention.Instance)
    .Build();

string yaml = serializer.Serialize(person);

var deserializer = new DeserializerBuilder()
    .WithNamingConvention(CamelCaseNamingConvention.Instance)
    .Build();

var restored = deserializer.Deserialize<Person>(yaml);
```

## 실전 사용 케이스

### JSON이 더 적합한 경우
```csharp
// API 응답
public class ApiResponse
{
    public int StatusCode { get; set; }
    public string Message { get; set; }
    public object Data { get; set; }
}

// 웹 브라우저와 통신
// JavaScript에서 직접 파싱 가능
```

### YAML이 더 적합한 경우
```yaml
# 설정 파일 (appsettings.yaml)
database:
  connection_string: Server=localhost;Database=mydb
  pool_size: 10
  timeout: 30

logging:
  level: Information
  providers:
    - Console
    - File
    
# Docker Compose
version: '3.8'
services:
  web:
    image: nginx:latest
    ports:
      - "80:80"
  db:
    image: postgres:13
    environment:
      POSTGRES_PASSWORD: secret
```

### 복잡한 설정 예시

**JSON - 중복과 장황함**
```json
{
  "environments": {
    "development": {
      "database": {
        "host": "localhost",
        "port": 5432,
        "timeout": 30
      },
      "cache": {
        "host": "localhost",
        "port": 6379,
        "timeout": 30
      }
    },
    "production": {
      "database": {
        "host": "prod-db.example.com",
        "port": 5432,
        "timeout": 30
      },
      "cache": {
        "host": "prod-cache.example.com",
        "port": 6379,
        "timeout": 30
      }
    }
  }
}
```

**YAML - 간결하고 재사용**
```yaml
_defaults: &defaults
  port: 5432
  timeout: 30

_cache_defaults: &cache_defaults
  port: 6379
  timeout: 30

environments:
  development:
    database:
      <<: *defaults
      host: localhost
    cache:
      <<: *cache_defaults
      host: localhost
      
  production:
    database:
      <<: *defaults
      host: prod-db.example.com
    cache:
      <<: *cache_defaults
      host: prod-cache.example.com
```

## 성능과 파일 크기

```csharp
// 동일한 데이터의 크기 비교
var data = new List<Person>();
for (int i = 0; i < 1000; i++)
{
    data.Add(new Person { Name = $"Person{i}", Age = 20 + i });
}

// JSON: 약간 더 작은 파일 크기 (압축된 형식)
var json = JsonSerializer.Serialize(data);
// 파싱 속도: 매우 빠름

// YAML: 더 읽기 쉽지만 파일이 큼 (들여쓰기)
var yaml = new SerializerBuilder().Build().Serialize(data);
// 파싱 속도: JSON보다 느림
```

## 실용적 조언

**JSON을 선택하세요:**
- API 통신
- 웹 애플리케이션 (브라우저 호환)
- 성능이 중요한 경우
- 작은 데이터 전송

**YAML을 선택하세요:**
- 설정 파일 (appsettings, docker-compose)
- CI/CD 파이프라인 (GitHub Actions, GitLab CI)
- 인프라 코드 (Kubernetes, Ansible)
- 사람이 직접 편집하는 파일

리스트 형태의 데이터를 많이 다루신다고 하셨는데, API 데이터 전송인가요 아니면 설정 파일 관리인가요? 용도에 따라 더 구체적인 예시를 드릴 수 있습니다.
