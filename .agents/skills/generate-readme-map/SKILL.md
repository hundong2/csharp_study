---
name: generate-readme-map
description: "프로젝트 폴더 구조를 분석하고 README.md에 네비게이션 맵을 생성합니다. Use when: README 작성, 프로젝트 문서화, 폴더 구조 맵 생성, 하이퍼링크 목차 만들기, 폴더별 유형별 네비게이션 구성, project map, readme navigation, folder structure documentation."
argument-hint: "대상 폴더 경로 (기본값: 현재 워크스페이스 루트)"
---

# README 프로젝트 맵 생성 스킬

프로젝트 폴더 구조를 탐색하고, 유형별로 분류된 하이퍼링크 네비게이션 맵을 README.md에 작성합니다.

## 언제 사용하나요

- "README에 폴더 구조 맵 만들어줘"
- "프로젝트 설명 네비게이션 추가해줘"
- "폴더별 하이퍼링킹 정리해줘"
- "프로젝트 문서화 해줘"
- "README map 작성해줘"

## 절차

### 1단계: 폴더 구조 파악

1. `list_dir`로 루트 폴더의 모든 항목을 나열한다.
2. 주요 폴더 각각에 대해 `list_dir`로 하위 구조를 확인한다.
3. 기존 README.md, 각 폴더의 README.md 등 핵심 문서를 `read_file`로 읽어 목적을 파악한다.
4. `.md` 문서 파일을 `grep_search`로 검색해 콘텐츠 유형을 보강한다.

### 2단계: 유형별 분류

아래 기준으로 폴더/파일을 분류한다:

| 유형 | 분류 기준 |
|------|-----------|
| 언어 기초 | 문법, 개념 설명 `.md`, 버전별 히스토리 |
| 코드 예제 | `.csproj`, `.sln`, `Program.cs` 포함 폴더 |
| UI / WPF | `xaml`, `MainWindow`, `App.xaml` 포함 폴더 |
| 비동기 / 병렬 | `async`, `await`, `Task`, `TPL` 관련 |
| 웹 / Blazor | `Blazor`, `WebApplication`, `Razor` 관련 |
| AI / ML | `SemanticKernel`, `Bot`, `AI` 관련 |
| 일일 기록 | 날짜 형식 폴더 (`YYYYMMDD`, `YYYY-MM-DD`) |
| 빌드 / 환경 | `build.md`, `*.sh`, `*.sln`, install 관련 |
| 아키텍처 / 심화 | Architecture, Design Pattern, Performance 관련 |

### 3단계: README.md 작성

다음 구조로 README.md를 작성(또는 덮어쓰기)한다:

```markdown
# 프로젝트명

한 줄 설명

---

## 📌 빠른 탐색

| 유형 | 바로가기 |
|------|----------|
| 🔤 언어 기초 | [링크](#섹션-앵커) |
...

---

## 🔤 섹션 제목

> 섹션 설명

| 문서 / 폴더 | 설명 |
|------------|------|
| [파일명](./경로) | 내용 요약 |
...
```

### 4단계: 링크 검증 규칙

- 모든 링크는 워크스페이스 루트 기준 **상대 경로** 사용 (`./폴더/파일.md`)
- 폴더 링크는 trailing slash 포함 (`./폴더/`)
- 존재하지 않는 파일은 링크하지 않는다
- 앵커 링크(`#섹션`)는 GitHub Flavored Markdown 규칙 준수
  - 소문자, 공백→하이픈, 특수문자 제거
  - 이모지는 앵커에서 제거

### 5단계: 품질 체크

작성 후 다음을 확인한다:

- [ ] 빠른 탐색 표의 모든 앵커가 실제 섹션과 일치하는가
- [ ] 각 행의 링크 경로가 실제 파일/폴더와 일치하는가
- [ ] 섹션 제목이 유형을 명확히 표현하는가
- [ ] 중복 링크가 없는가

## 출력 형식 가이드라인

- 섹션 구분: `---` 수평선 사용
- 섹션 헤더: `## 이모지 제목` 형식
- 표: GitHub Markdown 표 형식
- 설명 인용: `>` blockquote로 섹션 부연설명 추가
- 외부 링크는 표 하단에 `> 🔗 [이름](URL)` 형식으로 별도 표기

## 참고 예시

- [generate-readme-map 스킬이 생성한 README.md 예시](./references/example-output.md)
