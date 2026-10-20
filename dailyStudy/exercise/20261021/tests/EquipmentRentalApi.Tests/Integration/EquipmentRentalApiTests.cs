using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using EquipmentRentalApi.Presentation;
using EquipmentRentalApi.Tests.TestDoubles;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EquipmentRentalApi.Tests.Integration;

/// <summary>
/// WebApplicationFactory의 TestServer를 통해 routing, JSON, DI, Application, Repository를 함께 검증합니다.
/// </summary>
[TestClass]
public sealed class EquipmentRentalApiTests
{
    /// <summary>
    /// 상태 endpoint가 실제 routing과 JSON 직렬화를 거쳐 서비스 정보를 반환하는지 검증합니다.
    /// </summary>
    /// <returns>HTTP 요청, JSON 읽기, assertion이 끝날 때 완료되는 Task를 반환합니다.</returns>
    [TestMethod]
    // async 테스트는 실제 HTTP 비동기 흐름을 await하고 완료 여부를 Task로 test runner에 전달합니다.
    public async Task GetRoot_ReturnsServiceMetadata()
    {
        // Arrange: 테스트마다 새 Factory를 만들어 Singleton Repository 상태가 새로 시작되게 합니다.
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/");

        // Assert: 상태 계약을 먼저 확인한 뒤 JSON을 읽어 실패 원인을 transport와 body로 구분합니다.
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.IsNotNull(body);
        var serviceNode = body["service"];
        var testRunnerNode = body["testRunner"];
        Assert.IsNotNull(serviceNode);
        Assert.IsNotNull(testRunnerNode);
        Assert.AreEqual("EquipmentRentalApi", serviceNode.GetValue<string>());
        Assert.AreEqual("Microsoft.Testing.Platform", testRunnerNode.GetValue<string>());
    }

    /// <summary>
    /// 유효한 JSON이 전체 pipeline을 지나 201, Location, 정규화된 응답을 만드는지 검증합니다.
    /// </summary>
    /// <returns>HTTP 생성 요청과 응답 계약 검증이 끝날 때 완료되는 Task를 반환합니다.</returns>
    [TestMethod]
    public async Task PostRental_WithValidBody_ReturnsCreatedContract()
    {
        // Arrange
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();
        // with 식은 기본 요청 record에서 이 시나리오에 필요한 두 값만 바꾼 새 객체를 만듭니다.
        var request = ValidRequest() with
        {
            ReservationId = " res-http-001 ",
            EquipmentId = " eq-laptop-007 "
        };

        // Act: PostAsJsonAsync는 DTO를 JSON으로 직렬화하고 application/json header를 설정합니다.
        using var response = await client.PostAsJsonAsync("/rentals", request);

        // Assert
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        var rental = await response.Content.ReadFromJsonAsync<RentalResponse>();
        Assert.IsNotNull(rental);
        Assert.IsNotNull(response.Headers.Location);
        Assert.AreEqual("/rentals/RES-HTTP-001", response.Headers.Location.OriginalString);
        Assert.AreEqual("RES-HTTP-001", rental.ReservationId);
        Assert.AreEqual("EQ-LAPTOP-007", rental.EquipmentId);
        Assert.AreEqual(TestData.FixedNow, rental.CreatedAtUtc);
    }

    /// <summary>
    /// 첫 예약과 겹치는 두 번째 요청이 409와 안정적인 업무 오류 코드를 반환하는지 검증합니다.
    /// </summary>
    /// <returns>두 HTTP 요청과 Problem Details 검증이 끝날 때 완료되는 Task를 반환합니다.</returns>
    [TestMethod]
    public async Task PostRental_WithOverlap_ReturnsConflictProblemDetails()
    {
        // Arrange
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();
        var first = ValidRequest();
        var second = first with { ReservationId = "RES-HTTP-002" };

        // Act
        using var firstResponse = await client.PostAsJsonAsync("/rentals", first);
        using var secondResponse = await client.PostAsJsonAsync("/rentals", second);

        // Assert
        Assert.AreEqual(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.Conflict, secondResponse.StatusCode);
        var problem = await secondResponse.Content.ReadFromJsonAsync<JsonObject>();
        Assert.IsNotNull(problem);
        var codeNode = problem["code"];
        var fieldNode = problem["field"];
        Assert.IsNotNull(codeNode);
        Assert.IsNotNull(fieldNode);
        Assert.AreEqual("rental.equipment_unavailable", codeNode.GetValue<string>());
        Assert.AreEqual("equipmentId", fieldNode.GetValue<string>());
    }

    /// <summary>
    /// 종료가 시작보다 앞선 입력이 400과 Domain 오류 계약으로 변환되는지 검증합니다.
    /// </summary>
    /// <returns>HTTP 요청과 오류 JSON 검증이 끝날 때 완료되는 Task를 반환합니다.</returns>
    [TestMethod]
    public async Task PostRental_WithInvalidWindow_ReturnsBadRequest()
    {
        // Arrange
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();
        var request = ValidRequest() with
        {
            StartsAt = TestData.FixedNow.AddDays(1),
            EndsAt = TestData.FixedNow.AddHours(1)
        };

        // Act
        using var response = await client.PostAsJsonAsync("/rentals", request);

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.IsNotNull(problem);
        var codeNode = problem["code"];
        var fieldNode = problem["field"];
        Assert.IsNotNull(codeNode);
        Assert.IsNotNull(fieldNode);
        Assert.AreEqual("rental.invalid_window", codeNode.GetValue<string>());
        Assert.AreEqual("endsAt", fieldNode.GetValue<string>());
    }

    /// <summary>
    /// 잘못된 media type과 잘린 JSON이 endpoint 전에 각각 415와 400으로 거절되는지 검증합니다.
    /// </summary>
    /// <returns>두 전송 오류의 HTTP 상태와 공개 오류 코드 검증이 끝날 때 완료되는 Task를 반환합니다.</returns>
    [TestMethod]
    public async Task PostRental_WithTransportErrors_ReturnsFrameworkProblemDetails()
    {
        // Arrange
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();
        using var textContent = new StringContent("{}", Encoding.UTF8, "text/plain");
        using var truncatedJson = new StringContent("{", Encoding.UTF8, "application/json");

        // Act: 두 요청은 모두 endpoint 업무 코드에 들어가기 전의 HTTP 전송 경계를 확인합니다.
        using var mediaTypeResponse = await client.PostAsync("/rentals", textContent);
        using var jsonResponse = await client.PostAsync("/rentals", truncatedJson);

        // Assert
        Assert.AreEqual(HttpStatusCode.UnsupportedMediaType, mediaTypeResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest, jsonResponse.StatusCode);
        var jsonProblem = await jsonResponse.Content.ReadFromJsonAsync<JsonObject>();
        Assert.IsNotNull(jsonProblem);
        var codeNode = jsonProblem["code"];
        Assert.IsNotNull(codeNode);
        Assert.AreEqual("http.bad_request", codeNode.GetValue<string>());
    }

    /// <summary>
    /// 존재하지 않는 예약 조회가 404와 공개용 오류 코드를 반환하는지 검증합니다.
    /// </summary>
    /// <returns>HTTP 조회와 Problem Details 검증이 끝날 때 완료되는 Task를 반환합니다.</returns>
    [TestMethod]
    public async Task GetRental_WithUnknownId_ReturnsNotFound()
    {
        // Arrange
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/rentals/RES-NOT-FOUND");

        // Assert
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.IsNotNull(problem);
        var codeNode = problem["code"];
        Assert.IsNotNull(codeNode);
        Assert.AreEqual("rental.not_found", codeNode.GetValue<string>());
    }

    /// <summary>
    /// 여러 HTTP 테스트가 공유할 유효한 요청 DTO를 고정 시각 기준으로 만듭니다.
    /// </summary>
    /// <returns>노트북을 한 시간 뒤부터 하루 동안 빌리는 CreateRentalRequest를 반환합니다.</returns>
    private static CreateRentalRequest ValidRequest()
    {
        return new CreateRentalRequest(
            "RES-HTTP-001",
            "EQ-LAPTOP-007",
            "USER-100",
            "laptop",
            "신입 개발자 통합 테스트 실습",
            TestData.FixedNow.AddHours(1),
            TestData.FixedNow.AddDays(1));
    }
}
