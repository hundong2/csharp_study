# 📰 .NET Daily Insight

> 매일 아침 6시(KST), AI가 선별한 .NET 최신 소식과 학습 자료를 뉴스레터 형식으로 제공합니다.

---

## 📁 폴더 구조

```
insight/
├── README.md              ← 이 파일 (전체 안내)
├── used_urls.json         ← 이미 참고된 URL 추적 파일 (자동 관리)
├── 2025-01-15/
│   ├── README.md          ← 해당 날짜의 주제 인덱스
│   ├── whats-new-in-csharp-13.md
│   ├── aspnet-core-minimal-apis.md
│   └── dotnet-9-performance-improvements.md
├── 2025-01-16/
│   └── ...
└── ...
```

---

## 🗞️ 콘텐츠 구성

각 `.md` 파일은 다음 구조로 작성됩니다.

| 섹션 | 내용 |
|------|------|
| 🔗 참고 자료 | 파일 최상단에 원본 URL 및 출처 명시 |
| 📌 개요 | 해당 기능/주제 소개 |
| 🔍 핵심 내용 | 주요 특징 4~6개 항목 |
| 💻 코드 예시 | 실제 동작하는 C# 코드 (주석 포함) |
| 📊 실행 결과 | 코드 실행 시 예상 출력 |
| 🚀 실무 활용 방법 | 실제 프로젝트 적용 시나리오 |
| ⚠️ 주의사항 및 팁 | 버전·성능·호환성 관련 팁 |
| 📚 더 알아보기 | 관련 공식 문서 링크 |

---

## 📡 참고 소스

### Microsoft 공식
| 사이트 | 설명 |
|--------|------|
| [Microsoft .NET Blog](https://devblogs.microsoft.com/dotnet/) | .NET 공식 블로그 |
| [ASP.NET Blog](https://devblogs.microsoft.com/aspnet/) | ASP.NET 공식 블로그 |
| [Visual Studio Blog](https://devblogs.microsoft.com/visualstudio/) | Visual Studio 공식 블로그 |
| [Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/) | .NET 공식 문서 |

### 커뮤니티 & 인기 블로그
| 사이트 | 설명 |
|--------|------|
| [Andrew Lock Blog](https://andrewlock.net/) | .NET 심화 기술 블로그 |
| [Scott Hanselman Blog](https://www.hanselman.com/blog/) | .NET 생태계 인사이트 |
| [JetBrains .NET Blog](https://blog.jetbrains.com/dotnet/) | Rider/ReSharper 관련 블로그 |
| [Khalid Abuhakmeh Blog](https://khalidabuhakmeh.com/) | .NET 실무 팁 블로그 |
| [Reddit r/dotnet](https://www.reddit.com/r/dotnet/) | .NET 커뮤니티 |
| [Reddit r/csharp](https://www.reddit.com/r/csharp/) | C# 커뮤니티 |

---

## ⚙️ 동작 방식

```
[GitHub Actions - 매일 06:00 KST]
        │
        ▼
[RSS 피드 수집] ← devblogs, reddit, andrewlock, hanselman 등
        │
        ▼
[used_urls.json 대조] → 이미 사용된 URL 제외
        │
        ▼
[3개 주제 선택] ← 부족 시 Microsoft Learn 폴백 목록 사용
        │
        ▼
[Gemini / OpenAI API] → 한국어 교육용 콘텐츠 생성
        │
        ▼
[insight/{날짜}/{주제}.md 저장]
        │
        ▼
[used_urls.json 업데이트] → 사용된 URL 기록
        │
        ▼
[git commit & push]
```

---

## 🔑 GitHub Secrets 설정

레포지토리 → **Settings → Secrets and variables → Actions** 에서 아래 중 하나 이상 등록:

| Secret 이름 | 설명 | 우선순위 |
|-------------|------|----------|
| `GEMINI_API_KEY` | Google Gemini API 키 | 1순위 (설정 시 우선 사용) |
| `OPENAI_API_KEY` | OpenAI API 키 | 2순위 (Gemini 없을 때 사용) |

> 두 키가 모두 등록된 경우 Gemini를 우선 사용하며, Gemini 호출 실패 시 OpenAI로 자동 폴백합니다.

### API 키 발급 링크
- **Gemini**: [Google AI Studio](https://aistudio.google.com/app/apikey)
- **OpenAI**: [OpenAI Platform](https://platform.openai.com/api-keys)

---

## 🔄 수동 실행

GitHub Actions 탭 → **Daily .NET Insight Newsletter** → **Run workflow** 버튼으로 즉시 실행 가능합니다.

---

## 📅 최근 인사이트

<!-- 아래 목록은 자동으로 생성됩니다 -->

> 아직 생성된 인사이트가 없습니다. GitHub Actions가 실행되면 여기에 날짜별 폴더가 생성됩니다.
