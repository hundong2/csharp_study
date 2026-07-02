# 예시 출력: csharp_study README.md

아래는 `generate-readme-map` 스킬이 생성한 실제 README.md 예시입니다.
(2026-07-02 기준 csharp_study 저장소)

---

## 구조 특징

- **빠른 탐색 표**: 맨 위에 유형별 앵커 링크 테이블 배치
- **섹션별 분류**: 이모지 + 설명 헤더, blockquote 부연설명, Markdown 표
- **10개 유형 분류**: 언어기초, 코드예제, WPF, 비동기, Blazor, AI, Daily Insight, 빌드, 아키텍처, 일일기록
- **버전별 서브테이블**: C# 2.0~10.0 히스토리를 별도 표로 구성

## 빠른 탐색 표 패턴

```markdown
## 📌 빠른 탐색

| 유형 | 바로가기 |
|------|----------|
| 🔤 C# 언어 기초 | [기초 개념](#-c-언어-기초) |
| 🧪 코드 예제 | [예제 모음](#-코드-예제) |
| 🖥️ WPF / UI | [WPF 학습](#️-wpf--ui) |
| ⚡ 비동기 / 병렬 | [비동기 학습](#-비동기--병렬-처리) |
| 🌐 Blazor / Web | [웹 학습](#-blazor--web) |
| 🤖 Semantic Kernel | [AI 학습](#-semantic-kernel--ai) |
| 📰 Daily Insight | [뉴스레터](#-net-daily-insight) |
| 🛠️ 빌드 / 환경 설정 | [빌드 정보](#️-빌드--환경-설정) |
| 📚 아키텍처 / 심화 | [심화 학습](#-아키텍처--심화-학습) |
| 📅 일일 학습 기록 | [데일리 스터디](#-일일-학습-기록) |
```

## 섹션 패턴

```markdown
## 🔤 C# 언어 기초

> C# 언어 문법, .NET 런타임 개념, 버전별 신기능 정리

| 문서 | 설명 |
|------|------|
| [basic.md](./basic.md) | .NET 런타임 구조, CTS/CLS/CIL 기초 개념 |
| [csharstudy/BCL.md](./csharstudy/BCL.md) | Base Class Library 정리 |
...

> 🔗 [C# Language Feature Status (공식)](https://github.com/dotnet/roslyn/blob/main/docs/Language%20Feature%20Status.md)
```

## 앵커 링크 규칙 (GitHub Flavored Markdown)

| 헤더 텍스트 | 앵커 |
|------------|------|
| `## 🔤 C# 언어 기초` | `#-c-언어-기초` |
| `## 🖥️ WPF / UI` | `#️-wpf--ui` |
| `## ⚡ 비동기 / 병렬 처리` | `#-비동기--병렬-처리` |
| `## 🛠️ 빌드 / 환경 설정` | `#️-빌드--환경-설정` |
