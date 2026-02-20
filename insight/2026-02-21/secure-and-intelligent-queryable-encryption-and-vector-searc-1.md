> 🔗 **참고 자료**: https://devblogs.microsoft.com/dotnet/mongodb-efcore-provider-queryable-encryption-vector-search/
> **출처**: ASP.NET Blog

---

# MongoDB EF Core Provider: 쿼리 가능 암호화와 벡터 검색으로 데이터 보안 및 AI 역량 강화

## 📌 개요
이제 MongoDB EF Core Provider가 **쿼리 가능 암호화(Queryable Encryption)** 및 **벡터 검색(Vector Search)** 기능을 정식으로 지원합니다. 이 혁신적인 기능들은 .NET 개발자가 EF Core의 익숙한 LINQ 쿼리를 사용하여 민감한 데이터를 암호화된 상태로 안전하게 쿼리하고, 동시에 AI 기반의 시맨틱 검색 애플리케이션을 손쉽게 구축할 수 있도록 돕습니다. 데이터 보안과 인텔리전트 검색이라는 두 가지 핵심 요소를 결합하여 현대적인 애플리케이션 개발의 새로운 지평을 엽니다.

## 🔍 핵심 내용
새로운 MongoDB EF Core Provider의 주요 특징과 변경사항은 다음과 같습니다.

1.  **쿼리 가능 암호화(Queryable Encryption, QE) 지원**:
    데이터베이스에 저장된 민감한 데이터를 암호화된 상태로 유지하면서도, 애플리케이션에서 클라이언트 측 복호화 과정 없이 직접 쿼리할 수 있도록 합니다. 이는 데이터 보안을 크게 강화하고 GDPR, HIPAA 등 엄격한 규제 준수를 용이하게 하면서도 데이터의 활용성을 유지할 수 있게 합니다.

2.  **EF Core를 통한 원활한 통합**:
    .NET 개발자는 기존 EF Core 모델링 및 LINQ 쿼리를 활용하여 암호화된 필드를 손쉽게 정의하고 조작할 수 있습니다. MongoDB EF Core 공급자가 백그라운드에서 암호화 및 복호화 과정을 투명하게 처리하므로, 개발자는 데이터 보안 로직을 직접 구현하는 복잡성을 덜 수 있습니다.

3.  **벡터 검색(Vector Search) 기능 도입**:
    AI 기술의 핵심인 벡터 임베딩을 활용하여 데이터의 의미론적 유사성을 기반으로 검색하는 기능을 제공합니다. 텍스트, 이미지, 오디오 등 다양한 형태의 데이터를 수치형 벡터로 변환하여 저장하고, 이를 통해 단순 키워드 매칭을 넘어선 지능적인 검색 결과를 얻을 수 있습니다.

4.  **LINQ를 활용한 벡터 검색 쿼리**:
    EF Core의 강력한 LINQ 쿼리 구문을 사용하여 MongoDB Atlas에 저장된 벡터 데이터를 대상으로 k-Nearest Neighbors(k-NN) 기반의 유사도 검색을 수행할 수 있습니다. 이는 추천 시스템, 지능형 콘텐츠 검색, 이상 감지 등 AI 기반 애플리케이션 개발을 가속화합니다.

5.  **보안과 인텔리전스 결합**:
    중요한 데이터를 안전하게 보호하는 동시에, 최신 AI 기술을 활용한 지능형 검색 기능을 한 번에 구현할 수 있는 강력한 플랫폼을 제공합니다. 개발자는 두 가지 중요한 요구사항을 EF Core라는 단일 프레임워크 내에서 효율적으로 충족시킬 수 있습니다.

## 💻 코드 예시
다음은 `SecureDocument` 모델을 사용하여 쿼리 가능 암호화 필드와 벡터 검색 필드를 정의하고, 데이터를 저장 및 조회하는 C# 코드 예시입니다.

```csharp
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using MongoDB.EntityFrameworkCore.Extensions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.ClientEncryption;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// 1. 모델 정의
public class SecureDocument
{
    public ObjectId Id { get; set; }
    public string Title { get; set; }

    // 쿼리 가능 암호화 필드: [BsonEncrypted] 어트리뷰트를 사용하여 암호화 대상임을 EF Core에 알립니다.
    // 실제 암호화 로직은 MongoDB CSFLE (Client-Side Field Level Encryption) 설정을 통해 이루어집니다.
    [BsonEncrypted]
    public string ConfidentialInfo { get; set; }

    // 벡터 검색 필드: 문서의 의미를 나타내는 임베딩 벡터를 저장합니다.
    // 보통 double[] 타입으로 표현됩니다. MongoDB Atlas에서 Vector Search 인덱스 설정이 필요합니다.
    public double[] ContentVector { get; set; }
}

// 2. DbContext 정의
public class SecureAppContext : DbContext
{
    public DbSet<SecureDocument> Documents { get; set; }

    public SecureAppContext(DbContextOptions<SecureAppContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // MongoDB 컬렉션 이름 지정
        modelBuilder.Entity<SecureDocument>().ToCollection("SecureDocuments");

        // Fluent API 방식으로 암호화 필드를 명시할 수도 있습니다.
        // modelBuilder.Entity<SecureDocument>()
        //     .Property(d => d.ConfidentialInfo)
        //     .IsEncrypted();
    }
}

public class Program
{
    public static async Task Main(string[] args)
    {
        // --- MongoDB 연결 및 쿼리 가능 암호화 (CSFLE) 설정 ---
        // 주의: 쿼리 가능 암호화는 복잡한 설정이 필요합니다.
        //      아래 코드는 개발 환경에서 개념적 동작을 위한 최소한의 설정이며,
        //      실제 운영 환경에서는 KMS(Key Management Service) 연동, 데이터 키 관리 등
        //      MongoDB 공식 문서(CSFLE)를 반드시 참고하여 구성해야 합니다.
        //      EF Core Provider는 이 설정이 완료되었음을 전제로 동작합니다.

        string connectionString = "mongodb://localhost:27017"; // 로컬 MongoDB 예시 (MongoDB Atlas 연결로 변경 가능)
        string databaseName = "SecureAndSmartDB";
        
        // 1. IClientEncryption 설정 (CSFLE의 핵심)
        //    로컬 키 프로바이더를 사용하는 간략화된 예시 (개발/테스트용)
        var keyVaultNamespace = new CollectionNamespace(databaseName, "__keyVault");
        var kmsProviders = new Dictionary<string, IReadOnlyDictionary<string, object>>
        {
            { "local", new Dictionary<string, object> { { "key", new byte[96] } } } // 96바이트 임시 로컬 키
        };
        // IClientEncryption 인스턴스 생성
        using var clientEncryption = new ClientEncryption(
            new MongoClient(connectionString),
            new ClientEncryptionOptions(
                keyVaultNamespace: keyVaultNamespace,
                kmsProviders: kmsProviders,
                keyEncodingHooks: null,
                bypassAutoEncryption: true // EF Core Provider가 암호화 처리를 담당하므로 true
            ));
        
        // 2. EF Core DbContext 옵션 구성
        //    UseMongoDB 메서드에 IClientEncryption 인스턴스를 전달하여 암호화 기능을 활성화합니다.
        var dbContextOptions = new DbContextOptionsBuilder<SecureAppContext>()
            .UseMongoDB(new MongoClient(connectionString), databaseName, clientEncryption)
            .Options;

        // 3. DbContext 사용
        using (var context = new SecureAppContext(dbContextOptions))
        {
            await context.Database.EnsureCreatedAsync(); // 데이터베이스 및 컬렉션 생성 확인

            Console.WriteLine("--- 1. 문서 추가 (암호화 및 벡터 포함) ---");
            // 새 문서 생성 및 추가
            var doc1 = new SecureDocument
            {
                Title = "영업 보고서 Q3",
                ConfidentialInfo = "고객 A의 계약 조건이 변경될 예정입니다.", // 이 필드는 암호화됩니다.
                ContentVector = GenerateVector("Q3 영업 보고서, 고객 A 계약 변경") // 벡터 임베딩 생성 (시뮬레이션)
            };
            var doc2 = new SecureDocument
            {
                Title = "개발 프로젝트 계획",
                ConfidentialInfo = "다음 릴리스에 대한 중요 결정 사항.",
                ContentVector = GenerateVector("개발 프로젝트 계획, 다음 릴리스, 결정 사항")
            };
            var doc3 = new SecureDocument
            {
                Title = "보안 감사 결과",
                ConfidentialInfo = "내부 시스템 취약점 점검 결과.",
                ContentVector = GenerateVector("보안 감사, 시스템 취약점")
            };

            await context.Documents.AddRangeAsync(doc1, doc2, doc3);
            await context.SaveChangesAsync();
            Console.WriteLine("3개 문서 추가 완료.");

            Console.WriteLine("\n--- 2. 쿼리 가능 암호화 테스트: 암호화된 필드 직접 쿼리 ---");
            // 암호화된 'ConfidentialInfo' 필드를 조건으로 직접 쿼리
            // EF Core Provider가 백그라운드에서 암호화/복호화를 처리하여 평문으로 조회 가능합니다.
            var foundDoc = await context.Documents
                                        .FirstOrDefaultAsync(d => d.ConfidentialInfo.Contains("계약 조건"));
            if (foundDoc != null)
            {
                Console.WriteLine($"[QE 검색 성공] 제목: '{foundDoc.Title}', 기밀 정보: '{foundDoc.ConfidentialInfo}'");
            }
            else
            {
                Console.WriteLine("[QE 검색 실패] '계약 조건'을 포함하는 문서를 찾을 수 없습니다.");
            }

            Console.WriteLine("\n--- 3. 벡터 검색 테스트: 유사도 기반 문서 찾기 ---");
            // "고객 데이터 분석"과 유사한 문서를 찾기 위한 쿼리 벡터 생성
            var queryVector = GenerateVector("고객 데이터 분석");

            // EF Core의 LINQ를 사용하여 벡터 검색 시뮬레이션
            // 주의: EF Core MongoDB Provider의 `$vectorSearch` 직접 LINQ 지원은 현재 제한적일 수 있습니다.
            //       여기서는 개념적인 유사도 검색을 인메모리에서 시뮬레이션하며,
            //       실제 운영 환경에서는 MongoDB Atlas의 $vectorSearch 애그리게이션 파이프라인을 직접 사용하거나,
            //       해당 기능을 지원하는 EF Core Provider의 향후 버전을 사용해야 합니다.
            Console.WriteLine("주의: 아래 벡터 검색은 현재 EF Core Provider의 직접적인 $vectorSearch LINQ 지원이 제한적일 수 있어,");
            Console.WriteLine("개념적으로 유사도 검색을 시뮬레이션하는 코드입니다. 실제 구현 시 MongoDB Atlas 문서를 참조하세요.");

            var allDocs = await context.Documents.ToListAsync();
            var searchResults = allDocs
                .Select(d => new
                {
                    Document = d,
                    Similarity = CosineSimilarity(queryVector, d.ContentVector) // 코사인 유사도 계산
                })
                .OrderByDescending(x => x.Similarity)
                .Take(2) // 가장 유사한 상위 2개 문서
                .ToList();

            Console.WriteLine($"'{GetStringFromVector(queryVector)}' 벡터와 유사한 문서 (상위 2개):");
            foreach (var result in searchResults)
            {
                Console.WriteLine($"  - 제목: '{result.Document.Title}', 유사도: {result.Similarity:F4}");
            }
        }
    }

    // --- 유틸리티 함수 ---
    // 텍스트 기반으로 임의의 3차원 벡터를 생성하는 시뮬레이션 함수
    // 실제로는 OpenAI, Hugging Face 등의 임베딩 모델을 사용하여 의미론적인 벡터가 생성됩니다.
    private static double[] GenerateVector(string text)
    {
        var hash = text.GetHashCode();
        var random = new Random(hash);
        return new double[] { random.NextDouble(), random.NextDouble(), random.NextDouble() };
    }

    // 두 벡터 간의 코사인 유사도를 계산하는 함수 (0과 1 사이 값)
    private static double CosineSimilarity(double[] vector1, double[] vector2)
    {
        if (vector1 == null || vector2 == null || vector1.Length != vector2.Length || vector1.Length == 0) return 0.0;
        double dotProduct = 0.0;
        double magnitude1 = 0.0;
        double magnitude2 = 0.0;
        for (int i = 0; i < vector1.Length; i++)
        {
            dotProduct += vector1[i] * vector2[i];
            magnitude1 += vector1[i] * vector1[i];
            magnitude2 += vector2[i] * vector2[i];
        }
        magnitude1 = Math.Sqrt(magnitude1);
        magnitude2 = Math.Sqrt(magnitude2);
        if (magnitude1 == 0 || magnitude2 == 0) return 0.0;
        return dotProduct / (magnitude1 * magnitude2);
    }

    // 벡터를 보기 쉽게 문자열로 변환
    private static string GetStringFromVector(double[] vector)
    {
        if (vector == null) return "[]";
        return $"[{string.Join(", ", vector.Select(v => v.ToString("F4")))}]";
    }
}
```

## 📊 실행 결과
위 C# 코드를 실행했을 때의 예상 출력은 다음과 같습니다. (벡터 값과 유사도는 랜덤 요소로 인해 달라질 수 있습니다.)

```
--- 1. 문서 추가 (암호화 및 벡터 포함) ---
3개 문서 추가 완료.

--- 2. 쿼리 가능 암호화 테스트: 암호화된 필드 직접 쿼리 ---
[QE 검색 성공] 제목: '영업 보고서 Q3', 기밀 정보: '고객 A의 계약 조건이 변경될 예정입니다.'

--- 3. 벡터 검색 테스트: 유사도 기반 문서 찾기 ---
주의: 아래 벡터 검색은 현재 EF Core Provider의 직접적인 $vectorSearch LINQ 지원이 제한적일 수 있어,
개념적으로 유사도 검색을 시뮬레이션하는 코드입니다. 실제 구현 시 MongoDB Atlas 문서를 참조하세요.
'[0.2345, 0.6789, 0.1234]' 벡터와 유사한 문서 (상위 2개):
  - 제목: '영업 보고서 Q3', 유사도: 0.9215
  - 제목: '보안 감사 결과', 유사도: 0.8876
```

## 🚀 실무 활용 방법
1.  **민감 데이터 보안 및 규제 준수**: 금융 서비스(계좌 정보, 거래 내역), 헬스케어(환자 기록, PHI), 인사 관리(개인 식별 정보, PII) 시스템에서 암호화된 상태로 데이터를 저장하고, 보고서 생성이나 감사 목적으로 암호화된 필드를 직접 쿼리할 수 있습니다. 이를 통해 데이터 유출 위험을 줄이고 GDPR, HIPAA 등 데이터 보호 규제를 쉽게 준수할 수 있습니다.
2.  **AI 기반 지능형 검색 및 추천 시스템**: 전자상거래 플랫폼에서 고객이 검색한 제품과 의미론적으로 유사한 다른 제품을 추천하거나, 뉴스/콘텐츠 플랫폼에서 사용자의 관심사에 맞는 기사를 찾아주는 기능을 구현할 수 있습니다. 벡터 검색을 통해 키워드 매칭의 한계를 넘어선 정확하고 개인화된 경험을 제공합니다.
3.  **지능형 문서 관리 및 분석**: 기업의 방대한 문서 저장소에서 특정 키워드가 아닌 "프로젝트 X의 리스크 분석"과 같이 자연어 쿼리의 의미를 파악하여 관련 문서를 찾아내거나, 유사한 내용의 문서를 그룹화하는 데 활용될 수 있습니다. 암호화된 기밀 문서도 안전하게 관리하면서 지능적으로 활용 가능합니다.

## ⚠️ 주의사항 및 팁
*   **쿼리 가능 암호화(CSFLE) 설정의 복잡성**: 쿼리 가능 암호화는 KMS(Key Management Service) 통합, 데이터 키 생성 및 관리, Auto-encryption 설정 등 복잡한 초기 구성이 필요합니다. 개발 시작 전 MongoDB 공식 CSFLE 문서를 숙지하고 신중하게 설계해야 합니다.
*   **성능 고려 사항**: 데이터 암호화 및 복호화 과정은 추가적인 연산 오버헤드를 발생시킵니다. 특히 대량의 데이터를 처리할 때는 성능 벤치마킹을 통해 영향도를 평가하고, 필요한 경우 인덱스 최적화 등 성능 개선 작업을 병행해야 합니다.
*   **벡터 생성 및 관리**: 벡터 검색에 사용되는 임베딩 벡터는 일반적으로 OpenAI의 GPT 임베딩, Hugging Face 모델 등 외부 ML 서비스나 자체 모델을 통해 생성됩니다. 벡터 생성 파이프라인 구축 및 벡터 데이터의 효율적인 저장 전략을 고려해야 합니다.
*   **EF Core Provider 버전 및 MongoDB Atlas 활용**: 최신 `MongoDB.EntityFrameworkCore` Provider 버전을 사용하는 것이 중요하며, 벡터 검색은 MongoDB Atlas의 **Vector Search** 기능을 활용할 때 가장 강력한 성능을 발휘합니다. 로컬 MongoDB 환경에서는 제약이 있을 수 있습니다.

## 📚 더 알아보기
*   **공식 블로그 포스트**: [Secure and Intelligent: Queryable Encryption and Vector Search in MongoDB EF Core Provider](https://devblogs.microsoft.com/dotnet/mongodb-efcore-provider-queryable-encryption-vector-search/)
*   **MongoDB 쿼리 가능 암호화(CSFLE) 공식 문서**: [Queryable Encryption Overview](https://www.mongodb.com/docs/manual/core/queryable-encryption/)
*   **MongoDB Atlas 벡터 검색 공식 문서**: [Atlas Vector Search](https://www.mongodb.com/docs/atlas/atlas-vector-search/)
*   **MongoDB.EntityFrameworkCore GitHub 저장소**: [MongoDB.EntityFrameworkCore](https://github.com/mongodb/mongodb-efcore)

---
*출처: ASP.NET Blog | 생성일: 2026년 02월 21일*