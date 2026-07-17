namespace OMS.API.Tests.Integration;

public sealed class IntegrationScenariosTests : TestBase
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task IntegrationAuthRegistrationLoginAndRbacScenariosWork()
    {
        await using var factory = new IntegrationApplicationFactory();
        using var client = factory.CreateClient();
        var registerRequest = new RegisterRequest(
            "new.operator@example.com",
            "StrongPassword123!",
            "New Operator");

        var registerResponse = await client.PostAsync("/api/v1/auth/register", CreateJsonContent(registerRequest));
        var duplicateResponse = await client.PostAsync("/api/v1/auth/register", CreateJsonContent(registerRequest));
        var loginResponse = await client.PostAsync(
            "/api/v1/auth/login",
            CreateJsonContent(new LoginRequest("new.operator@example.com", "StrongPassword123!")));
        var invalidLoginResponse = await client.PostAsync(
            "/api/v1/auth/login",
            CreateJsonContent(new LoginRequest("new.operator@example.com", "WrongPassword123!")));
        var protectedResponse = await client.GetAsync("/api/v1/categories");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await LoginForTokenAsync(client, "sales1@example.com", IntegrationTestPassword));
        var nonAdminAdminEndpointResponse = await client.GetAsync("/api/v1/users");

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(ExtractString(await loginResponse.Content.ReadAsStringAsync(), "data", "accessToken")));
        Assert.Equal(HttpStatusCode.Unauthorized, invalidLoginResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, protectedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, nonAdminAdminEndpointResponse.StatusCode);
    }


    [Fact]
    [Trait("Category", "Integration")]
    public async Task IntegrationAdminCanCreateCategoryProductAndDuplicateSkuConflicts()
    {
        await using var factory = new IntegrationApplicationFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await LoginForTokenAsync(client, "admin@example.com", IntegrationTestPassword));

        var categoryResponse = await client.PostAsync(
            "/api/v1/categories",
            CreateJsonContent(new CreateCategoryRequest("Integration Category", "Created by integration test.")));
        var categoryId = ExtractGuid(await categoryResponse.Content.ReadAsStringAsync(), "data", "id");
        var productRequest = new CreateProductRequest(
            "INT-001",
            "Integration Product",
            "pcs",
            25m,
            4,
            categoryId,
            IntegrationSeedData.SupplierId);

        var productResponse = await client.PostAsync("/api/v1/products", CreateJsonContent(productRequest));
        var productBody = await productResponse.Content.ReadAsStringAsync();
        var duplicateSkuResponse = await client.PostAsync("/api/v1/products", CreateJsonContent(productRequest));

        Assert.Equal(HttpStatusCode.Created, categoryResponse.StatusCode);
        Assert.True(productResponse.StatusCode == HttpStatusCode.Created, $"{productBody}{Environment.NewLine}{GetIntegrationLogs()}");
        Assert.Equal(HttpStatusCode.Conflict, duplicateSkuResponse.StatusCode);
    }


    [Fact]
    [Trait("Category", "Integration")]
    public async Task IntegrationPositionalRecordRequestsUseMvcParameterValidationMetadata()
    {
        await using var factory = new IntegrationApplicationFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await LoginForTokenAsync(client, "admin@example.com", IntegrationTestPassword));

        var validSupplierResponse = await client.PostAsync(
            "/api/v1/suppliers",
            CreateJsonContent(new CreateSupplierRequest(
                "Tech Supplier",
                "supplier@example.com",
                "081234567890",
                "Jakarta")));
        var invalidCreateSupplierResponse = await client.PostAsync(
            "/api/v1/suppliers",
            CreateJsonContent(new CreateSupplierRequest(
                "Invalid Supplier",
                "not-an-email",
                "081234567890",
                "Jakarta")));
        var invalidUpdateSupplierResponse = await client.PutAsync(
            $"/api/v1/suppliers/{IntegrationSeedData.SupplierId}",
            CreateJsonContent(new UpdateSupplierRequest(
                "Updated Supplier",
                "not-an-email",
                "081234567890",
                "Jakarta")));
        var invalidCategoryResponse = await client.PutAsync(
            $"/api/v1/categories/{IntegrationSeedData.CategoryId}",
            CreateJsonContent(new UpdateCategoryRequest(new string('C', 101), "Invalid length.")));
        var invalidProductResponse = await client.PutAsync(
            $"/api/v1/products/{IntegrationSeedData.ProductId}",
            CreateJsonContent(new UpdateProductRequest(
                "INT-VALIDATION",
                "Invalid Product",
                "pcs",
                -1m,
                1,
                IntegrationSeedData.CategoryId,
                IntegrationSeedData.SupplierId)));
        var invalidCreateUserResponse = await client.PostAsync(
            "/api/v1/users",
            CreateJsonContent(new CreateUserRequest(
                "not-an-email",
                "StrongPassword123!",
                "Invalid User",
                SystemRoleNames.SalesOperator)));
        var invalidUpdateUserResponse = await client.PutAsync(
            $"/api/v1/users/{IntegrationSeedData.SalesUserId}",
            CreateJsonContent(new UpdateUserRequest("not-an-email", "Sales User")));
        var invalidUpdateRoleResponse = await client.PatchAsync(
            $"/api/v1/users/{IntegrationSeedData.SalesUserId}/role",
            CreateJsonContent(new UpdateUserRoleRequest(string.Empty)));

        Assert.Equal(HttpStatusCode.Created, validSupplierResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidCreateSupplierResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidUpdateSupplierResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidCategoryResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidProductResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidCreateUserResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidUpdateUserResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidUpdateRoleResponse.StatusCode);
    }


    [Fact]
    [Trait("Category", "Integration")]
    public async Task IntegrationAdminCreateUsersReferencesExistingRolesWithoutInsertingDuplicateRoles()
    {
        await using var factory = new IntegrationApplicationFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await LoginForTokenAsync(client, "admin@example.com", IntegrationTestPassword));

        var roleCountBefore = await CountRolesAsync(factory);
        var supervisorEmail = "created.supervisor@example.com";
        var salesEmail = "created.sales@example.com";

        var supervisorResponse = await client.PostAsync(
            "/api/v1/users",
            CreateJsonContent(new CreateUserRequest(
                supervisorEmail,
                "StrongPassword123!",
                "Created Supervisor",
                SystemRoleNames.Supervisor)));
        var salesResponse = await client.PostAsync(
            "/api/v1/users",
            CreateJsonContent(new CreateUserRequest(
                salesEmail,
                "StrongPassword123!",
                "Created Sales",
                SystemRoleNames.SalesOperator)));
        var duplicateEmailResponse = await client.PostAsync(
            "/api/v1/users",
            CreateJsonContent(new CreateUserRequest(
                supervisorEmail,
                "StrongPassword123!",
                "Duplicate Supervisor",
                SystemRoleNames.Supervisor)));
        var invalidRoleResponse = await client.PostAsync(
            "/api/v1/users",
            CreateJsonContent(new CreateUserRequest(
                "invalid.role@example.com",
                "StrongPassword123!",
                "Invalid Role",
                "manager")));

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleCountAfter = await dbContext.Roles.CountAsync();
        var createdSupervisor = await dbContext.Users
            .AsNoTracking()
            .SingleAsync(user => user.Email == supervisorEmail);
        var createdSales = await dbContext.Users
            .AsNoTracking()
            .SingleAsync(user => user.Email == salesEmail);
        var supervisorUsersCreated = await dbContext.Users.CountAsync(user => user.Email == supervisorEmail);
        var salesUsersCreated = await dbContext.Users.CountAsync(user => user.Email == salesEmail);

        Assert.Equal(HttpStatusCode.Created, supervisorResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, salesResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicateEmailResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, invalidRoleResponse.StatusCode);
        Assert.Equal(roleCountBefore, roleCountAfter);
        Assert.Equal(1, supervisorUsersCreated);
        Assert.Equal(1, salesUsersCreated);
        Assert.Equal(IntegrationSeedData.SupervisorRoleId, createdSupervisor.RoleId);
        Assert.Equal(IntegrationSeedData.SalesRoleId, createdSales.RoleId);
    }


    [Fact]
    [Trait("Category", "Integration")]
    public async Task IntegrationDailySalesReportReadEndpointReturnsPersistedReportWithRoleAccess()
    {
        await using var factory = new IntegrationApplicationFactory();
        var reportDate = new DateOnly(2026, 7, 19);
        var missingDate = new DateOnly(2026, 7, 18);

        await SeedDailySalesReportAsync(factory, reportDate);

        using var client = factory.CreateClient();
        var unauthenticatedResponse = await client.GetAsync($"/api/v1/reports/daily-sales?date={reportDate:yyyy-MM-dd}");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await LoginForTokenAsync(client, "admin@example.com", IntegrationTestPassword));
        var adminResponse = await client.GetAsync($"/api/v1/reports/daily-sales?date={reportDate:yyyy-MM-dd}");
        var adminBody = await adminResponse.Content.ReadAsStringAsync();
        var missingDateParameterResponse = await client.GetAsync("/api/v1/reports/daily-sales");
        var invalidDateParameterResponse = await client.GetAsync("/api/v1/reports/daily-sales?date=not-a-date");
        var reportsBeforeMissingRead = await CountDailySalesReportsAsync(factory);
        var missingResponse = await client.GetAsync($"/api/v1/reports/daily-sales?date={missingDate:yyyy-MM-dd}");
        var reportsAfterMissingRead = await CountDailySalesReportsAsync(factory);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await LoginForTokenAsync(client, "supervisor@example.com", IntegrationTestPassword));
        var supervisorResponse = await client.GetAsync($"/api/v1/reports/daily-sales?date={reportDate:yyyy-MM-dd}");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await LoginForTokenAsync(client, "sales1@example.com", IntegrationTestPassword));
        var salesResponse = await client.GetAsync($"/api/v1/reports/daily-sales?date={reportDate:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticatedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missingDateParameterResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidDateParameterResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, supervisorResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, salesResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
        Assert.Equal(reportsBeforeMissingRead, reportsAfterMissingRead);
        Assert.Equal(reportDate, DateOnly.Parse(ExtractString(adminBody, "data", "reportDate")));
        Assert.Equal(2, ExtractInt(adminBody, "data", "totalOrders"));
        Assert.Equal(125.50m, ExtractElement(adminBody, "data", "totalRevenue").GetDecimal());
        Assert.Equal(1, ExtractArrayLength(adminBody, "data", "items"));
        Assert.Equal(IntegrationSeedData.ProductId, ExtractGuid(adminBody, "data", "items", 0, "productId"));
        Assert.Equal("SEED-001", ExtractString(adminBody, "data", "items", 0, "productSku"));
        Assert.Equal("Seed Product", ExtractString(adminBody, "data", "items", 0, "productName"));
        Assert.Equal(5, ExtractInt(adminBody, "data", "items", 0, "quantitySold"));
        Assert.Equal(125.50m, ExtractElement(adminBody, "data", "items", 0, "revenue").GetDecimal());
    }


    [Fact]
    [Trait("Category", "Integration")]
    public async Task IntegrationOrderLifecycleStockOwnershipAndInvalidTransitionsWork()
    {
        await using var factory = new IntegrationApplicationFactory();
        using var client = factory.CreateClient();
        var salesToken = await LoginForTokenAsync(client, "sales1@example.com", IntegrationTestPassword);
        var otherSalesToken = await LoginForTokenAsync(client, "sales2@example.com", IntegrationTestPassword);
        var supervisorToken = await LoginForTokenAsync(client, "supervisor@example.com", IntegrationTestPassword);
        var adminToken = await LoginForTokenAsync(client, "admin@example.com", IntegrationTestPassword);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", salesToken);
        var orderResponse = await client.PostAsync(
            "/api/v1/orders",
            CreateJsonContent(new CreateOrderRequest(
                IntegrationSeedData.CustomerId,
                "idr",
                [new CreateOrderItemRequest(IntegrationSeedData.ProductId, 2)])));
        var orderBody = await orderResponse.Content.ReadAsStringAsync();
        var orderId = ExtractGuid(orderBody, "data", "id");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherSalesToken);
        var otherOperatorReadResponse = await client.GetAsync($"/api/v1/orders/{orderId}");
        var otherOperatorCancelResponse = await client.PostAsync(
            $"/api/v1/orders/{orderId}/cancel",
            CreateJsonContent(new CancelOrderRequest("Not my order.")));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", supervisorToken);
        var approveResponse = await client.PostAsync($"/api/v1/orders/{orderId}/approve", null);
        var approveBody = await approveResponse.Content.ReadAsStringAsync();
        var shipResponse = await client.PostAsync(
            $"/api/v1/orders/{orderId}/ship",
            CreateJsonContent(new ShipOrderRequest(" JNE-123456 ")));
        var deliverResponse = await client.PostAsync($"/api/v1/orders/{orderId}/deliver", null);
        var invalidTransitionResponse = await client.PostAsync($"/api/v1/orders/{orderId}/approve", null);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var stockAfterDelivery = await GetProductStockAsync(client, IntegrationSeedData.ProductId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", salesToken);
        var insufficientStockResponse = await client.PostAsync(
            "/api/v1/orders",
            CreateJsonContent(new CreateOrderRequest(
                IntegrationSeedData.CustomerId,
                "IDR",
                [new CreateOrderItemRequest(IntegrationSeedData.ProductId, 999)])));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var stockAfterInsufficientOrder = await GetProductStockAsync(client, IntegrationSeedData.ProductId);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", salesToken);
        var cancellableOrderResponse = await client.PostAsync(
            "/api/v1/orders",
            CreateJsonContent(new CreateOrderRequest(
                IntegrationSeedData.CustomerId,
                "IDR",
                [new CreateOrderItemRequest(IntegrationSeedData.ProductId, 1)])));
        var cancellableOrderId = ExtractGuid(await cancellableOrderResponse.Content.ReadAsStringAsync(), "data", "id");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var stockAfterCancellableCreate = await GetProductStockAsync(client, IntegrationSeedData.ProductId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", salesToken);
        var cancelResponse = await client.PostAsync(
            $"/api/v1/orders/{cancellableOrderId}/cancel",
            CreateJsonContent(new CancelOrderRequest(" Customer requested cancellation. ")));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var stockAfterCancellation = await GetProductStockAsync(client, IntegrationSeedData.ProductId);

        Assert.Equal(HttpStatusCode.Created, orderResponse.StatusCode);
        Assert.Equal("IDR", ExtractString(orderBody, "data", "currencyCode"));
        Assert.Equal(HttpStatusCode.NotFound, otherOperatorReadResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherOperatorCancelResponse.StatusCode);
        Assert.True(approveResponse.StatusCode == HttpStatusCode.OK, $"{approveBody}{Environment.NewLine}{GetIntegrationLogs()}");
        Assert.Equal(HttpStatusCode.OK, shipResponse.StatusCode);
        Assert.Equal("JNE-123456", ExtractString(await shipResponse.Content.ReadAsStringAsync(), "data", "trackingNumber"));
        Assert.Equal(HttpStatusCode.OK, deliverResponse.StatusCode);
        Assert.Equal("Delivered", ExtractString(await deliverResponse.Content.ReadAsStringAsync(), "data", "status"));
        Assert.Equal(HttpStatusCode.Conflict, invalidTransitionResponse.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, insufficientStockResponse.StatusCode);
        Assert.Equal(stockAfterDelivery, stockAfterInsufficientOrder);
        Assert.Equal(HttpStatusCode.Created, cancellableOrderResponse.StatusCode);
        Assert.Equal(stockAfterDelivery - 1, stockAfterCancellableCreate);
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
        Assert.Equal(stockAfterDelivery, stockAfterCancellation);
    }


    [Fact]
    [Trait("Category", "Integration")]
    public async Task IntegrationOrderFilteringSortingAndPaginationWork()
    {
        await using var factory = new IntegrationApplicationFactory();
        using var client = factory.CreateClient();
        var salesToken = await LoginForTokenAsync(client, "sales1@example.com", IntegrationTestPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", salesToken);

        var firstResponse = await client.PostAsync(
            "/api/v1/orders",
            CreateJsonContent(new CreateOrderRequest(
                IntegrationSeedData.CustomerId,
                "IDR",
                [new CreateOrderItemRequest(IntegrationSeedData.ProductId, 1)])));
        var secondResponse = await client.PostAsync(
            "/api/v1/orders",
            CreateJsonContent(new CreateOrderRequest(
                IntegrationSeedData.CustomerId,
                "IDR",
                [new CreateOrderItemRequest(IntegrationSeedData.SecondProductId, 1)])));
        var firstOrderNumber = ExtractString(await firstResponse.Content.ReadAsStringAsync(), "data", "orderNumber");
        var secondOrderNumber = ExtractString(await secondResponse.Content.ReadAsStringAsync(), "data", "orderNumber");

        var listResponse = await client.GetAsync(
            $"/api/v1/orders?status=Pending&customerId={IntegrationSeedData.CustomerId}&sortBy=orderNumber&sortDirection=asc&page=1&pageSize=1");
        var listBody = await listResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal(1, ExtractArrayLength(listBody, "data", "items"));
        Assert.Equal(string.CompareOrdinal(firstOrderNumber, secondOrderNumber) <= 0 ? firstOrderNumber : secondOrderNumber,
            ExtractString(listBody, "data", "items", 0, "orderNumber"));
        Assert.Equal(1, ExtractInt(listBody, "data", "pagination", "page"));
        Assert.Equal(1, ExtractInt(listBody, "data", "pagination", "pageSize"));
        Assert.Equal(2, ExtractInt(listBody, "data", "pagination", "totalItems"));
        Assert.True(ExtractBool(listBody, "data", "pagination", "hasNextPage"));
    }


    [Fact]
    [Trait("Category", "Integration")]
    public async Task IntegrationExchangeRateRequiresAuthenticationAndExternalFailureMapsTo503()
    {
        await using var factory = new IntegrationApplicationFactory { ThrowExternalFailure = true };
        using var client = factory.CreateClient();

        var unauthenticatedResponse = await client.GetAsync("/api/v1/exchange-rates?from=USD&to=IDR");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await LoginForTokenAsync(client, "admin@example.com", IntegrationTestPassword));
        var failedResponse = await client.GetAsync("/api/v1/exchange-rates?from=USD&to=IDR");

        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticatedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failedResponse.StatusCode);
    }


    private static async Task<int> CountRolesAsync(IntegrationApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await dbContext.Roles.CountAsync();
    }


    private static async Task SeedDailySalesReportAsync(
        IntegrationApplicationFactory factory,
        DateOnly reportDate)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var report = new DailySalesReport
        {
            ReportDate = reportDate,
            TotalOrders = 2,
            TotalRevenue = 125.50m,
            GeneratedAtUtc = new DateTime(2026, 7, 20, 0, 1, 0, DateTimeKind.Utc)
        };

        report.Items.Add(new DailySalesReportItem
        {
            ProductId = IntegrationSeedData.ProductId,
            ProductSku = "SEED-001",
            ProductName = "Seed Product",
            QuantitySold = 5,
            Revenue = 125.50m
        });

        await dbContext.DailySalesReports.AddAsync(report);
        await dbContext.SaveChangesAsync();
    }


    private static async Task<int> CountDailySalesReportsAsync(IntegrationApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await dbContext.DailySalesReports.CountAsync();
    }

}
