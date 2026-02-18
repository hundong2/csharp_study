#!/usr/bin/env python3
"""
Daily .NET Insight Newsletter Generator
- Gemini API 또는 OpenAI API를 사용하여 .NET 관련 교육 콘텐츠 자동 생성
- 매일 3개의 주제를 선택하여 insight/{날짜}/ 폴더에 마크다운 파일로 저장
- 이미 사용된 URL은 used_urls.json으로 추적하여 재사용 방지
"""

import os
import json
import re
import sys
import time
import random
import requests
import feedparser
from datetime import datetime, timezone, timedelta
from pathlib import Path
from typing import Optional

# ─────────────────────────────────────────────────────────────
# 상수 정의
# ─────────────────────────────────────────────────────────────

KST = timezone(timedelta(hours=9))
USED_URLS_FILE = "insight/used_urls.json"
INSIGHTS_DIR = "insight"
TARGET_COUNT = 3

# ─────────────────────────────────────────────────────────────
# RSS 피드 소스 목록 (우선순위 순)
# ─────────────────────────────────────────────────────────────

RSS_FEEDS = [
    {"url": "https://devblogs.microsoft.com/dotnet/feed/",          "name": "Microsoft .NET Blog",        "priority": 1},
    {"url": "https://devblogs.microsoft.com/aspnet/feed/",          "name": "ASP.NET Blog",               "priority": 1},
    {"url": "https://devblogs.microsoft.com/visualstudio/feed/",    "name": "Visual Studio Blog",         "priority": 1},
    {"url": "https://devblogs.microsoft.com/typescript/feed/",      "name": "TypeScript Blog",            "priority": 2},
    {"url": "https://andrewlock.net/rss.xml",                       "name": "Andrew Lock Blog",           "priority": 2},
    {"url": "https://www.hanselman.com/blog/feed",                  "name": "Scott Hanselman Blog",       "priority": 2},
    {"url": "https://blog.jetbrains.com/dotnet/feed/",              "name": "JetBrains .NET Blog",        "priority": 2},
    {"url": "https://khalidabuhakmeh.com/feed.xml",                 "name": "Khalid Abuhakmeh Blog",      "priority": 2},
    {"url": "https://www.reddit.com/r/dotnet/.rss",                 "name": "Reddit r/dotnet",            "priority": 3},
    {"url": "https://www.reddit.com/r/csharp/.rss",                 "name": "Reddit r/csharp",            "priority": 3},
    {"url": "https://www.reddit.com/r/aspnetcore/.rss",             "name": "Reddit r/aspnetcore",        "priority": 3},
]

# ─────────────────────────────────────────────────────────────
# 폴백 주제 목록 (RSS에서 충분한 기사를 못 찾을 경우 사용)
# Microsoft 공식 문서 기반으로 구성
# ─────────────────────────────────────────────────────────────

FALLBACK_TOPICS = [
    # C# 최신 기능
    {"url": "https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-13",                     "title": "What's new in C# 13",                           "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-12",                     "title": "What's new in C# 12",                           "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-11",                     "title": "What's new in C# 11",                           "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-10",                     "title": "What's new in C# 10",                           "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/patterns",   "title": "Pattern matching in C#",                        "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record", "title": "Records in C#",                                 "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/dotnet/csharp/nullable-references",                     "title": "Nullable reference types in C#",                "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/init",        "title": "Init-only properties in C#",                    "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/switch-expression", "title": "Switch expressions in C#",               "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/primary-constructors", "title": "Primary constructors in C#",  "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/lambda-expressions", "title": "Lambda expressions in C#",              "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/using",     "title": "using declarations and statements in C#",       "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/attributes/general",   "title": "Attributes in C#",                              "source": "Microsoft Learn"},

    # .NET 플랫폼
    {"url": "https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-9/overview",               "title": "What's new in .NET 9",                          "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8/overview",               "title": "What's new in .NET 8",                          "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/",                     "title": ".NET Native AOT Compilation",                   "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection",            "title": ".NET Dependency Injection",                     "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration",                  "title": ".NET Configuration system",                    "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/dotnet/core/extensions/logging",                        "title": ".NET Logging",                                  "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/task-parallel-library", "title": "Task Parallel Library (TPL)",             "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/dotnet/standard/async-in-depth",                        "title": "Async programming deep dive in .NET",           "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/dotnet/standard/linq/",                                 "title": "LINQ in .NET",                                  "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/dotnet/standard/collections/",                          "title": ".NET Collections and data structures",          "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/dotnet/standard/memory-and-spans/memory-t-usage-guidelines", "title": "Memory<T> and Span<T> usage guidelines",  "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/dotnet/core/diagnostics/",                              "title": ".NET Diagnostics and observability",            "source": "Microsoft Learn"},

    # ASP.NET Core
    {"url": "https://learn.microsoft.com/en-us/aspnet/core/release-notes/aspnetcore-9.0",              "title": "What's new in ASP.NET Core 9.0",                "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/aspnet/core/release-notes/aspnetcore-8.0",              "title": "What's new in ASP.NET Core 8.0",                "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/overview",        "title": "ASP.NET Core Minimal APIs",                     "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/",                  "title": "ASP.NET Core Middleware",                       "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/aspnet/core/web-api/",                                  "title": "ASP.NET Core Web API",                          "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/aspnet/core/signalr/introduction",                      "title": "ASP.NET Core SignalR",                          "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/aspnet/core/grpc/",                                     "title": "gRPC with ASP.NET Core",                        "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/aspnet/core/security/authentication/",                  "title": "ASP.NET Core Authentication",                  "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/aspnet/core/performance/caching/overview",              "title": "Caching in ASP.NET Core",                       "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/aspnet/core/fundamentals/rate-limiting",                "title": "Rate limiting in ASP.NET Core",                 "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/overview",             "title": "OpenAPI support in ASP.NET Core",               "source": "Microsoft Learn"},

    # Blazor
    {"url": "https://learn.microsoft.com/en-us/aspnet/core/blazor/",                                   "title": "ASP.NET Core Blazor overview",                  "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/aspnet/core/blazor/components/",                        "title": "Blazor Components",                             "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/aspnet/core/blazor/javascript-interoperability/",       "title": "Blazor JavaScript Interop",                     "source": "Microsoft Learn"},

    # Entity Framework Core
    {"url": "https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-9.0/whatsnew",             "title": "What's new in EF Core 9",                       "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-8.0/whatsnew",             "title": "What's new in EF Core 8",                       "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/ef/core/performance/",                                  "title": "EF Core Performance",                           "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/ef/core/querying/",                                     "title": "EF Core Querying Data",                         "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/ef/core/saving/",                                       "title": "EF Core Saving Data",                           "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/ef/core/modeling/",                                     "title": "EF Core Model Configuration",                  "source": "Microsoft Learn"},

    # .NET MAUI
    {"url": "https://learn.microsoft.com/en-us/dotnet/maui/what-is-maui",                              "title": "What is .NET MAUI",                             "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/dotnet/maui/whats-new/dotnet-9",                       "title": "What's new in .NET MAUI (.NET 9)",              "source": "Microsoft Learn"},

    # 성능 & 진단
    {"url": "https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-9/runtime",               "title": ".NET 9 Runtime Performance improvements",       "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-counters",               "title": "dotnet-counters performance monitoring",        "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/aspnet/core/performance/objectpool",                    "title": "Object pooling in ASP.NET Core",                "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/dotnet/standard/io/pipelines",                          "title": "System.IO.Pipelines in .NET",                   "source": "Microsoft Learn"},

    # 테스트
    {"url": "https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices",       "title": "Unit testing best practices in .NET",           "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests",                    "title": "Integration testing in ASP.NET Core",           "source": "Microsoft Learn"},

    # 클라우드 & 마이크로서비스
    {"url": "https://learn.microsoft.com/en-us/dotnet/architecture/microservices/",                    "title": ".NET Microservices Architecture",               "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/dotnet/aspire/get-started/aspire-overview",             "title": ".NET Aspire overview",                          "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience",                "title": "HTTP resilience in .NET",                       "source": "Microsoft Learn"},

    # 시크릿 & 보안
    {"url": "https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/introduction",    "title": "ASP.NET Core Data Protection",                 "source": "Microsoft Learn"},
    {"url": "https://learn.microsoft.com/en-us/aspnet/core/security/cors",                             "title": "CORS in ASP.NET Core",                          "source": "Microsoft Learn"},

    # Channel9 / YouTube 영상 대신 devblogs 기술 포스트
    {"url": "https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-9/",               "title": "Performance Improvements in .NET 9",            "source": "Microsoft .NET Blog"},
    {"url": "https://devblogs.microsoft.com/dotnet/announcing-dotnet-9/",                              "title": "Announcing .NET 9",                             "source": "Microsoft .NET Blog"},
    {"url": "https://devblogs.microsoft.com/dotnet/announcing-csharp-13/",                             "title": "Announcing C# 13",                              "source": "Microsoft .NET Blog"},
    {"url": "https://devblogs.microsoft.com/dotnet/dotnet-and-ai-overview/",                           "title": ".NET and AI Overview",                          "source": "Microsoft .NET Blog"},
    {"url": "https://devblogs.microsoft.com/dotnet/system-text-json-in-dotnet-9/",                    "title": "System.Text.Json improvements in .NET 9",       "source": "Microsoft .NET Blog"},
]


# ─────────────────────────────────────────────────────────────
# URL 추적 (재사용 방지)
# ─────────────────────────────────────────────────────────────

def load_used_urls() -> set:
    path = Path(USED_URLS_FILE)
    if path.exists():
        with open(path, "r", encoding="utf-8") as f:
            return set(json.load(f))
    return set()


def save_used_urls(used_urls: set) -> None:
    Path(USED_URLS_FILE).parent.mkdir(parents=True, exist_ok=True)
    with open(USED_URLS_FILE, "w", encoding="utf-8") as f:
        json.dump(sorted(list(used_urls)), f, indent=2, ensure_ascii=False)


# ─────────────────────────────────────────────────────────────
# RSS 피드에서 기사 수집
# ─────────────────────────────────────────────────────────────

def clean_html(text: str) -> str:
    return re.sub(r"<[^>]+>", "", text or "").strip()


def fetch_rss_articles(used_urls: set) -> list:
    articles = []
    headers = {"User-Agent": "Mozilla/5.0 (compatible; DotNetInsightBot/1.0; +https://github.com)"}

    for feed_info in sorted(RSS_FEEDS, key=lambda x: x["priority"]):
        try:
            feed = feedparser.parse(feed_info["url"], request_headers=headers)
            for entry in feed.entries[:20]:
                url = (entry.get("link") or "").strip()
                if not url or url in used_urls:
                    continue

                title = clean_html(entry.get("title", ""))
                summary = clean_html(entry.get("summary", ""))[:800]

                # .NET 관련 키워드 필터 (Reddit은 필터 없이 허용)
                if "reddit" not in feed_info["url"]:
                    keywords = ["dotnet", ".net", "csharp", "c#", "asp", "blazor",
                                "ef core", "nuget", "visual studio", "rider", "maui",
                                "azure", "aspire", "linq", "async", "wpf", "winui"]
                    combined = (title + summary).lower()
                    if not any(k in combined for k in keywords):
                        continue

                articles.append({
                    "url": url,
                    "title": title,
                    "summary": summary,
                    "source": feed_info["name"],
                })
        except Exception as e:
            print(f"  ⚠️  RSS 피드 오류 [{feed_info['name']}]: {e}")

    return articles


# ─────────────────────────────────────────────────────────────
# 폴백 주제 선택
# ─────────────────────────────────────────────────────────────

def get_fallback_articles(used_urls: set, count: int) -> list:
    available = [t for t in FALLBACK_TOPICS if t["url"] not in used_urls]
    random.shuffle(available)
    return [
        {
            "url": t["url"],
            "title": t["title"],
            "summary": f"Official documentation: {t['title']}",
            "source": t["source"],
        }
        for t in available[:count]
    ]


# ─────────────────────────────────────────────────────────────
# AI 콘텐츠 생성 프롬프트
# ─────────────────────────────────────────────────────────────

def build_prompt(article: dict) -> str:
    today = datetime.now(KST).strftime("%Y년 %m월 %d일")
    return f"""당신은 .NET 개발자를 위한 기술 뉴스레터 작성 전문가입니다.

아래 참고 자료를 바탕으로 한국어 교육용 뉴스레터 콘텐츠를 마크다운 형식으로 작성해주세요.

- 참고 URL: {article['url']}
- 제목: {article['title']}
- 요약: {article['summary']}
- 출처: {article['source']}

---

다음 구조를 반드시 지켜서 작성하세요.

# [주제를 잘 나타내는 한국어 제목]

## 📌 개요
이 기능/주제가 무엇인지, 왜 .NET 개발자에게 중요한지 3~4문장으로 설명합니다.

## 🔍 핵심 내용
주요 특징이나 변경사항 4~6개를 목록으로 설명합니다. 각 항목에 충분한 설명을 포함하세요.

## 💻 코드 예시
실제로 동작하는 C# 코드를 작성합니다. 최소 20줄 이상, 주석을 꼭 포함하세요.

```csharp
// 코드 예시 (주석 포함, 여러 케이스 시연)
```

## 📊 실행 결과
위 코드를 실행했을 때의 정확한 예상 출력을 보여줍니다.

```
출력 결과
```

## 🚀 실무 활용 방법
실제 프로젝트에서 어떻게 활용할 수 있는지 구체적인 시나리오 2~3가지를 설명합니다.

## ⚠️ 주의사항 및 팁
사용 시 주의해야 할 점, 성능·호환성·버전 관련 팁을 2~4개 목록으로 작성합니다.

## 📚 더 알아보기
관련 공식 문서, 추가 학습 자료 링크를 2~4개 제시합니다.

---
*출처: {article['source']} | 생성일: {today}*
"""


# ─────────────────────────────────────────────────────────────
# AI API 호출
# ─────────────────────────────────────────────────────────────

def generate_with_gemini(article: dict, api_key: str) -> str:
    import google.generativeai as genai

    genai.configure(api_key=api_key)
    model = genai.GenerativeModel("gemini-2.0-flash-lite")
    response = model.generate_content(build_prompt(article))
    return response.text


def generate_with_openai(article: dict, api_key: str) -> str:
    from openai import OpenAI

    client = OpenAI(api_key=api_key)
    response = client.chat.completions.create(
        model="gpt-4o-mini",
        messages=[
            {
                "role": "system",
                "content": ".NET 개발자를 위한 기술 뉴스레터를 작성하는 전문가입니다. 교육적이고 실용적인 내용을 한국어로 작성합니다.",
            },
            {"role": "user", "content": build_prompt(article)},
        ],
        max_tokens=3000,
        temperature=0.7,
    )
    return response.choices[0].message.content


def generate_content(
    article: dict,
    gemini_key: Optional[str],
    openai_key: Optional[str],
) -> Optional[str]:
    if gemini_key:
        try:
            return generate_with_gemini(article, gemini_key)
        except Exception as e:
            print(f"  ⚠️  Gemini 오류: {e}")
            if openai_key:
                print("  → OpenAI로 폴백합니다...")
    if openai_key:
        try:
            return generate_with_openai(article, openai_key)
        except Exception as e:
            print(f"  ⚠️  OpenAI 오류: {e}")
    return None


# ─────────────────────────────────────────────────────────────
# 파일 저장
# ─────────────────────────────────────────────────────────────

def make_filename(title: str) -> str:
    """영문 제목 기반으로 파일명 생성 (소문자, 하이픈)"""
    name = title.lower()
    name = re.sub(r"[^\w\s-]", "", name)
    name = re.sub(r"[\s_]+", "-", name)
    name = re.sub(r"-+", "-", name).strip("-")
    return name[:60] + ".md"


def save_insight(date_str: str, article: dict, content: str) -> Path:
    folder = Path(INSIGHTS_DIR) / date_str
    folder.mkdir(parents=True, exist_ok=True)

    filename = make_filename(article["title"])
    filepath = folder / filename

    # 중복 파일명 처리
    counter = 1
    while filepath.exists():
        filepath = folder / f"{Path(filename).stem}-{counter}.md"
        counter += 1

    # 파일 상단에 참고 URL 명시 (요구사항)
    header = (
        f"> 🔗 **참고 자료**: {article['url']}\n"
        f"> **출처**: {article['source']}\n\n"
        f"---\n\n"
    )

    with open(filepath, "w", encoding="utf-8") as f:
        f.write(header + content)

    print(f"  ✅ 저장: {filepath}")
    return filepath


def create_daily_index(date_str: str, articles: list, files: list) -> None:
    """날짜 폴더의 README.md (일일 인덱스) 생성"""
    today = datetime.now(KST)
    weekdays = ["월", "화", "수", "목", "금", "토", "일"]
    weekday = weekdays[today.weekday()]

    folder = Path(INSIGHTS_DIR) / date_str
    index_path = folder / "README.md"

    lines = [
        f"# 📰 .NET Daily Insight — {today.strftime('%Y년 %m월 %d일')} ({weekday}요일)\n\n",
        "오늘의 .NET 학습 주제 3가지를 확인하세요.\n\n",
        "## 오늘의 주제\n\n",
    ]

    for i, (article, filepath) in enumerate(zip(articles, files), 1):
        lines.append(f"| {i} | [{article['title']}](./{filepath.name}) | {article['source']} |\n")

    # 테이블 헤더를 앞에 삽입
    lines.insert(3, "| # | 주제 | 출처 |\n")
    lines.insert(4, "|---|------|------|\n")

    lines.append(f"\n---\n*📅 생성 시각: {today.strftime('%Y-%m-%d %H:%M')} KST*\n")

    with open(index_path, "w", encoding="utf-8") as f:
        f.writelines(lines)

    print(f"  ✅ 인덱스 생성: {index_path}")


# ─────────────────────────────────────────────────────────────
# 메인
# ─────────────────────────────────────────────────────────────

def main():
    print("=" * 55)
    print("  🚀  .NET Daily Insight Generator")
    print("=" * 55)

    gemini_key = os.environ.get("GEMINI_API_KEY", "").strip() or None
    openai_key = os.environ.get("OPENAI_API_KEY", "").strip() or None

    if not gemini_key and not openai_key:
        print("❌ GEMINI_API_KEY 또는 OPENAI_API_KEY 가 설정되지 않았습니다.")
        sys.exit(1)

    api_label = "Gemini" if gemini_key else "OpenAI"
    print(f"✅ API: {api_label}")

    today = datetime.now(KST)
    date_str = today.strftime("%Y-%m-%d")
    print(f"📅 날짜: {date_str} (KST)\n")

    # 이미 사용된 URL 로드
    used_urls = load_used_urls()
    print(f"📋 기존 사용 URL 수: {len(used_urls)}")

    # RSS에서 기사 수집
    print("\n🔍 RSS 피드 수집 중...")
    articles = fetch_rss_articles(used_urls)
    print(f"   → {len(articles)}개 신규 기사 발견")

    # 부족한 경우 폴백으로 보충
    shortage = max(0, TARGET_COUNT - len(articles))
    if shortage > 0:
        fallbacks = get_fallback_articles(used_urls, shortage)
        articles.extend(fallbacks)
        print(f"   → 폴백 {len(fallbacks)}개 추가")

    selected = articles[:TARGET_COUNT]

    if not selected:
        print("⚠️  사용 가능한 기사가 없습니다. 종료합니다.")
        sys.exit(0)

    print(f"\n✨ 선택된 주제 {len(selected)}개:")
    for i, a in enumerate(selected, 1):
        print(f"   {i}. [{a['source']}] {a['title']}")

    # 콘텐츠 생성 & 저장
    print("\n🤖 AI 콘텐츠 생성 중...\n")
    saved_files = []

    for i, article in enumerate(selected, 1):
        print(f"[{i}/{len(selected)}] {article['title']}")
        content = generate_content(article, gemini_key, openai_key)

        if content:
            filepath = save_insight(date_str, article, content)
            saved_files.append(filepath)
            used_urls.add(article["url"])
        else:
            print("  ❌ 콘텐츠 생성 실패")

        # API 레이트 리밋 방지
        if i < len(selected):
            time.sleep(2)

    if saved_files:
        print(f"\n📁 일일 인덱스 생성 중...")
        create_daily_index(date_str, selected[: len(saved_files)], saved_files)

        save_used_urls(used_urls)
        print(f"\n🎉 완료! {len(saved_files)}개 인사이트 생성 → insight/{date_str}/")
    else:
        print("\n❌ 생성된 파일이 없습니다.")
        sys.exit(1)


if __name__ == "__main__":
    main()
