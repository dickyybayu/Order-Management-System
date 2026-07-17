namespace OMS.API.Tests.Unit;

public sealed class ExchangeRateTests : TestBase
{
    [Fact]
    public void FrankfurterTypedClientIsRegisteredWithTimeout()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = TestApplicationFactory.JwtOptions.Issuer,
                ["Jwt:Audience"] = TestApplicationFactory.JwtOptions.Audience,
                ["Jwt:SigningKey"] = TestApplicationFactory.JwtOptions.SigningKey,
                ["Jwt:ExpirationMinutes"] = TestApplicationFactory.JwtOptions.ExpirationMinutes.ToString(),
                ["Frankfurter:BaseUrl"] = "https://example.test/",
                ["Frankfurter:TimeoutSeconds"] = "7",
                ["Frankfurter:RetryCount"] = "2"
            })
            .Build();

        services.AddLogging();
        services.AddOmsAuthenticationServices(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<FrankfurterOptions>();
        var client = serviceProvider.GetRequiredService<IExchangeRateClient>();

        Assert.Equal("https://example.test/", options.BaseUrl);
        Assert.Equal(7, options.TimeoutSeconds);
        Assert.Equal(2, options.RetryCount);
        Assert.IsType<FrankfurterExchangeRateClient>(client);
    }


    [Fact]
    public async Task FrankfurterClientUsesRelativeUrlNormalizesCurrenciesAndMapsResponse()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"amount\":1,\"base\":\"USD\",\"date\":\"2026-07-17\",\"rates\":{\"IDR\":16250.25}}",
                Encoding.UTF8,
                "application/json")
        });
        var client = CreateFrankfurterClient(handler);
        using var cancellationTokenSource = new CancellationTokenSource();

        var response = await client.GetLatestRateAsync("usd", "idr", cancellationTokenSource.Token);

        Assert.Equal("USD", response.FromCurrency);
        Assert.Equal("IDR", response.ToCurrency);
        Assert.Equal(16250.25m, response.Rate);
        Assert.Equal("Frankfurter", response.Source);
        Assert.Equal(new DateOnly(2026, 7, 17), response.RateDate);
        Assert.Equal("/latest?from=USD&to=IDR", handler.Requests.Single().RequestUri?.PathAndQuery);
        Assert.True(handler.CancellationTokens.Single().CanBeCanceled);
    }


    [Fact]
    public void FrankfurterTypedClientUsesPollyRetryAndCircuitBreakerPolicies()
    {
        var source = File.ReadAllText(FindRepositoryFile("src", "OMS.API", "Extensions", "AuthenticationServiceCollectionExtensions.cs"));

        Assert.Contains(".AddPolicyHandler((serviceProvider, _) => CreateFrankfurterRetryPolicy(serviceProvider))", source, StringComparison.Ordinal);
        Assert.Contains(".AddPolicyHandler((serviceProvider, _) => CreateFrankfurterCircuitBreakerPolicy(serviceProvider))", source, StringComparison.Ordinal);
        Assert.Contains(".WaitAndRetryAsync(", source, StringComparison.Ordinal);
        Assert.Contains(".CircuitBreakerAsync(", source, StringComparison.Ordinal);
    }


    [Fact]
    public async Task FrankfurterClientMapsFinalTransientAndNonTransientFailures()
    {
        var transientClient = CreateFrankfurterClient(new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var badRequestClient = CreateFrankfurterClient(new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest)));

        await Assert.ThrowsAsync<ExternalServiceException>(
            () => transientClient.GetLatestRateAsync("USD", "IDR", CancellationToken.None));
        await Assert.ThrowsAsync<ExternalServiceException>(
            () => badRequestClient.GetLatestRateAsync("USD", "IDR", CancellationToken.None));
    }


    [Fact]
    public async Task FrankfurterClientHandlesMalformedResponsesAndNetworkFailuresSafely()
    {
        var malformedClient = CreateFrankfurterClient(new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"rates\":{}}", Encoding.UTF8, "application/json")
            }));

        await Assert.ThrowsAsync<ExternalServiceException>(
            () => malformedClient.GetLatestRateAsync("USD", "IDR", CancellationToken.None));

        var networkClient = CreateFrankfurterClient(
            new FakeHttpMessageHandler(_ => throw new HttpRequestException("Network unavailable.")),
            retryCount: 0);

        await Assert.ThrowsAsync<ExternalServiceException>(
            () => networkClient.GetLatestRateAsync("USD", "IDR", CancellationToken.None));
    }


    [Fact]
    public async Task ExchangeRatesEndpointReturnsUnauthorizedWithoutToken()
    {
        await using var factory = new ExchangeRateApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/exchange-rates?from=USD&to=IDR");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }


    [Fact]
    public async Task AuthenticatedUserCanRequestExchangeRateWithSafeResponse()
    {
        await using var factory = new ExchangeRateApplicationFactory();
        using var client = factory.CreateClient();
        var user = CreateTokenUser(SystemRoleNames.SalesOperator);
        var token = new JwtTokenService(Options.Create(TestApplicationFactory.JwtOptions))
            .CreateAccessToken(user, SystemRoleNames.SalesOperator);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await client.GetAsync("/api/v1/exchange-rates?from=usd&to=idr");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"fromCurrency\":\"USD\"", body, StringComparison.Ordinal);
        Assert.Contains("\"toCurrency\":\"IDR\"", body, StringComparison.Ordinal);
        Assert.Contains("\"rate\":16000", body, StringComparison.Ordinal);
        Assert.DoesNotContain("rates", body, StringComparison.Ordinal);
    }


    [Theory]
    [InlineData("US", "IDR")]
    [InlineData("US1", "IDR")]
    [InlineData("USD", "I")]
    public async Task CurrencyServiceRejectsInvalidCurrencyCodes(string from, string to)
    {
        var service = new CurrencyService(new FakeExchangeRateClient());

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.GetExchangeRateAsync(from, to, CancellationToken.None));
    }


    [Fact]
    public async Task IdenticalCurrenciesReturnIdentityRateWithoutExternalCall()
    {
        var client = new FakeExchangeRateClient();
        var service = new CurrencyService(client);

        var response = await service.GetExchangeRateAsync("usd", "USD", CancellationToken.None);

        Assert.Equal("USD", response.FromCurrency);
        Assert.Equal("USD", response.ToCurrency);
        Assert.Equal(1m, response.Rate);
        Assert.Equal("Identity", response.Source);
        Assert.Equal(0, client.CallCount);
    }


    [Fact]
    public async Task ExchangeRateExternalFailureReturnsServiceUnavailableProblemDetails()
    {
        await using var factory = new ExchangeRateApplicationFactory { ThrowExternalFailure = true };
        using var client = factory.CreateClient();
        var user = CreateTokenUser(SystemRoleNames.Admin);
        var token = new JwtTokenService(Options.Create(TestApplicationFactory.JwtOptions))
            .CreateAccessToken(user, SystemRoleNames.Admin);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await client.GetAsync("/api/v1/exchange-rates?from=USD&to=IDR");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("External service unavailable", body, StringComparison.Ordinal);
    }


    [Fact]
    public void ExchangeRatesControllerIsThinAndDoesNotExposeExternalDto()
    {
        var controllerSource = File.ReadAllText(FindRepositoryFile("src", "OMS.API", "Http", "API", "Version1", "ExchangeRate", "Controllers", "ExchangeRatesController.cs"));
        var responseProperties = typeof(ExchangeRateResponse).GetProperties().Select(property => property.Name).ToArray();

        Assert.DoesNotContain("Frankfurter", controllerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", controllerSource, StringComparison.Ordinal);
        Assert.Contains(nameof(ExchangeRateResponse.FromCurrency), responseProperties);
        Assert.DoesNotContain(responseProperties, property => property == nameof(FrankfurterLatestResponse.Rates));
    }

}

