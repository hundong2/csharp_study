> 🔗 **참고 자료**: https://devblogs.microsoft.com/dotnet/mongodb-efcore-provider-queryable-encryption-vector-search/
> **출처**: Microsoft .NET Blog

---

# 안전하고 스마트하게: MongoDB EF Core Provider의 쿼리 가능 암호화 및 벡터 검색

## 📌 개요
.NET 개발자 여러분, 드디어 MongoDB와 Entity Framework Core(EF Core)를 사용하는 프로젝트에서 데이터 보안과 AI 기반 검색 기능을 더욱 강력하게 활용할 수 있게 되었습니다! MongoDB EF Core Provider가 이제 `쿼리 가능 암호화(Queryable Encryption)`와 `벡터 검색(Vector Search)`을 지원합니다. 이는 민감한 데이터를 암호화한 상태에서도 유연하게 쿼리할 수 있게 하고, AI 기반의 의미론적 검색 기능을 EF Core의 친숙한 LINQ 문법으로 직접 구현할 수 있게 해줍니다. 이 새로운 기능들은 .NET 개발자들이 더욱 안전하고 지능적인 애플리케이션을 효율적으로 구축할 수 있도록 돕습니다.

## 🔍 핵심 내용
새롭게 지원되는 `쿼리 가능 암호화` 및 `벡터 검색` 기능의 주요 내용은 다음과 같습니다.

*   **쿼리 가능 암호화 (Queryable Encryption) 지원**:
    *   **개념**: 데이터를 암호화한 상태로 데이터베이스에 저장하더라도, 애플리케이션 측에서 암호화된 필드에 대해 쿼리(필터링, 정렬 등)를 수행할 수 있게 합니다. 데이터베이스는 암호화된 데이터를 알 수 없으므로, 매우 높은 수준의 보안을 제공합니다.
    *   **작동 방식**: 클라이언트 측에서 데이터가 암호화/복호화되며, `[BsonEncrypted]`와 같은 속성으로 암호화할 필드를 지정합니다. EF Core Provider는 이를 자동으로 처리하여 개발자가 암호화/복호화 로직을 직접 구현할 필요가 없습니다.
    *   **이점**: 개인 식별 정보(PII), 금융 정보 등 민감한 데이터의 보안을 극대화하고, GDPR, HIPAA와 같은 규제 준수를 용이하게 합니다.

*   **벡터 검색 (Vector Search) 지원**:
    *   **개념**: AI 모델이 생성한 벡터 임베딩을 사용하여 데이터 간의 "의미론적 유사성"을 기준으로 검색을 수행하는 기능입니다. 키워드 매칭을 넘어선 더욱 직관적이고 지능적인 검색 경험을 제공합니다.
    *   **작동 방식**: 엔티티에 `float[]` 형태의 벡터 임베딩 필드를 저장하고, `IQueryable<T>.VectorSearch()`와 같은 EF Core 확장 메서드를 사용하여 임베딩된 쿼리 벡터와 가장 유사한 문서를 찾습니다.
    *   **이점**: 상품 추천, 문서 유사성 분석, 챗봇의 지식 검색, 개인화된 콘텐츠 제공 등 다양한 AI 기반 애플리케이션에 활용될 수 있습니다.

*   **EF Core의 간편한 통합**:
    *   두 기능 모두 EF Core의 강력한 LINQ 쿼리 인터페이스를 통해 접근 가능합니다. 이는 .NET 개발자들이 새로운 데이터베이스 특정 API를 학습할 필요 없이, 익숙한 객체 지향 방식으로 고급 기능을 활용할 수 있음을 의미합니다.

*   **개발 생산성 향상**:
    *   데이터 보안과 AI 기능을 위한 복잡한 로직을 EF Core Provider가 추상화하여 제공함으로써, 개발자는 비즈니스 로직에 집중하고 개발 시간을 단축할 수 있습니다.

*   **강력한 MongoDB 생태계 활용**:
    *   이 기능들은 MongoDB의 최신 데이터베이스 기능(예: MongoDB Atlas Vector Search)을 .NET 환경에서 최대한 활용할 수 있도록 지원하며, NoSQL의 유연성과 확장성을 그대로 누릴 수 있게 합니다.

## 💻 코드 예시
아래 C# 코드는 MongoDB EF Core Provider를 사용하여 `쿼리 가능 암호화`로 고객의 신용카드 정보를 보호하고, `벡터 검색`으로 제품 추천 시스템을 구축하는 방법을 보여줍니다.

```csharp
// 필수 NuGet 패키지:
// - MongoDB.EntityFrameworkCore
// - MongoDB.Driver
// - MongoDB.Driver.Core
// - MongoDB.Bson
// - MongoDB.ClientEncryption (쿼리 가능 암호화 설정에 필요)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.EntityFrameworkCore;
using MongoDB.ClientEncryption; // Queryable Encryption 관련 네임스페이스

// 1. 엔티티 정의

// 쿼리 가능 암호화를 위한 Customer 엔티티
public class Customer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; }
    
    // [BsonEncrypted] 속성을 사용하여 이 필드가 쿼리 가능 암호화 대상임을 명시합니다.
    // 이 속성은 MongoDB.Bson.Serialization.Attributes 네임스페이스에 있습니다.
    [BsonEncrypted] 
    public string CreditCardNumber { get; set; } // 암호화될 민감 정보
    public string Email { get; set; }
}

// 벡터 검색을 위한 Product 엔티티
public class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; }
    public string Description { get; set; }
    
    // 상품 설명을 나타내는 벡터 임베딩 (float 배열).
    // 실제 시나리오에서는 OpenAI, Azure OpenAI 등 AI 모델을 통해 생성됩니다.
    public float[] DescriptionEmbedding { get; set; }
}

// 2. DbContext 정의
public class SecureCommerceDbContext : DbContext
{
    // DbSet<T> 속성을 정의하여 EF Core가 엔티티를 관리하도록 합니다.
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Product> Products { get; set; }

    // DbContextOptions를 주입받는 생성자
    public SecureCommerceDbContext(DbContextOptions<SecureCommerceDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // MongoDB 컬렉션 이름 지정 (선택 사항)
        modelBuilder.Entity<Customer>().ToCollection("Customers");
        modelBuilder.Entity<Product>().ToCollection("Products");
    }
}

// 3. Vector Search를 위한 EF Core 확장 메서드 (MongoDB EF Core Provider에서 제공될 것으로 가정)
// 이 메서드는 실제 MongoDB EF Core Provider에서 제공하는 확장 메서드를 모방합니다.
// 내부적으로 MongoDB Atlas Vector Search의 aggregation 파이프라인을 구축합니다.
public static class MongoDbEfCoreExtensions
{
    // 실제 provider는 내부적으로 MongoDB 드라이버의 VectorSearch 메서드를 호출합니다.
    // 여기서는 예시를 위해 실제 DB 호출 없이 간단히 동작을 시뮬레이션합니다.
    public static IQueryable<TEntity> VectorSearch<TEntity>(
        this IQueryable<TEntity> source,
        float[] queryVector,
        string path, // 임베딩 필드 경로 (예: "DescriptionEmbedding")
        int k,      // 반환할 가장 유사한 문서의 개수
        string indexName = "vector_index", // 사용할 벡터 검색 인덱스 이름
        int? limit = null,
        float? minScore = null)
        where TEntity : Product // Product 엔티티에만 해당한다고 가정
    {
        Console.WriteLine($"\n--- DEBUG: 벡터 검색 요청 ---");
        Console.WriteLine($"  쿼리 벡터: [{string.Join(", ", queryVector.Take(3))}...]");
        Console.WriteLine($"  경로: '{path}', K: {k}, 인덱스: '{indexName}'");
        
        // 실제 동작은 MongoDB Atlas 클러스터와 벡터 검색 인덱스에 의존합니다.
        // 여기서는 예시를 위해 단순하게 몇 개의 더미 데이터를 반환하는 척 합니다.
        // 실제 VectorSearch는 queryVector와 path를 이용해 유사도를 계산합니다.
        return source.Where(p => p.Name.Contains("smart", StringComparison.OrdinalIgnoreCase) || 
                                 p.Description.Contains("AI", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(p => Guid.NewGuid()) // 유사도 대신 임의 정렬 (더미용)
                     .Take(k);
    }
}

// 4. 메인 애플리케이션 로직
public class Program
{
    public static async Task Main(string[] args)
    {
        // MongoDB 연결 설정 및 Queryable Encryption 구성
        // 중요: Queryable Encryption을 위한 AutoEncryptionSettings는 MongoClientSettings에서 구성되어야 합니다.
        // EF Core Provider는 이 구성된 MongoClient를 사용하여 드라이버를 초기화합니다.
        string connectionString = "mongodb://localhost:27017/?appName=DotNetNewsletterExample";
        string databaseName = "SecureCommerceDb";
        
        // Queryable Encryption을 위한 KMS(Key Management Service) Provider 설정
        // 실제 환경에서는 Azure Key Vault, AWS KMS, GCP KMS 등을 사용해야 합니다.
        // 로컬 키를 사용하는 것은 개발/테스트 목적으로만 권장됩니다.
        var kmsProviders = new Dictionary<string, IReadOnlyDictionary<string, object>>
        {
            // 실제 키는 보안 방식으로 생성 및 관리되어야 합니다.
            // 아래 'YOUR_LOCAL_MASTER_KEY_BASE64==' 부분은 Base64 인코딩된 96바이트 키여야 합니다.
            // 개발용: new byte[96]; new Random().NextBytes(masterKeyBytes); Convert.ToBase64String(masterKeyBytes);
            { "local", new Dictionary<string, object> { { "key", Convert.FromBase64String("MIIBVAIBADANBgkqhkiG9w0BAQEFAASCAT4wggE7AgEAl65jF1X2xPzXz+C00k4t0Q7rG2f6n9w6W+C00k4t0Q7rG2f6n9w6W+C00k4t0Q7rG2f6n9w6W+C00k4t0Q7rG2f6n9w6W+C00k4t0Q7rG2f6n9w6W+C00k4t0Q7rG2f6n9w6W+C00k4t0Q7rG2f6n9w6W+C00k4t0Q7rG2f6n9w6W+C00k4t0Q7rG2f6n9w6W+C00k4t0Q7rG2f6n9w6W+C00k4t0Q7rG2f6n9w6W+C00k4t0Q7rG2f6n9w6W+C00k4t0Q7rG2f6n9w6W+C00k4t0Q7rG2f6n9w6W+C00k4t0Q7rG2f6n9w6W+C00k4t0Q7rG2f6n9w6W+C00k4t0Q7rG2f6n9w6W+C00k4t0Q7rG2f6n9w6W+C00k4t0Q7rG2f6n9w6W+C00k4t0Q7rG2f6n9w6W+C00k4t