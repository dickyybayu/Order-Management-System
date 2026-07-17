namespace OMS.API.Tests.Unit;

public sealed class SharedModelsTests : TestBase
{
    [Fact]
    public void ApiResponseCreatesSuccessfulResponse()
    {
        var response = ApiResponse<string>.Ok("payload", "Created successfully.");

        Assert.True(response.Success);
        Assert.Equal("Created successfully.", response.Message);
        Assert.Equal("payload", response.Data);
    }


    [Fact]
    public void PaginationRequestUsesDocumentedDefaults()
    {
        var request = new PaginationRequest();

        Assert.Equal(1, request.Page);
        Assert.Equal(20, request.PageSize);
        Assert.Equal(0, request.Skip);
    }


    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void PaginationRequestValidatesBounds(int page, int pageSize)
    {
        var request = new PaginationRequest
        {
            Page = page,
            PageSize = pageSize
        };

        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            validationResults,
            validateAllProperties: true);

        Assert.False(isValid);
    }


    [Fact]
    public void PaginationMetadataCalculatesPageState()
    {
        var metadata = new PaginationMetadata(page: 2, pageSize: 10, totalItems: 25);

        Assert.Equal(3, metadata.TotalPages);
        Assert.True(metadata.HasPreviousPage);
        Assert.True(metadata.HasNextPage);
    }


    [Fact]
    public void AuditableEntityUsesGuidIdentifierAndActiveDefault()
    {
        var entity = new TestAuditableEntity();

        Assert.NotEqual(Guid.Empty, entity.Id);
        Assert.True(entity.IsActive);
        Assert.Equal(default, entity.CreatedAtUtc);
        Assert.Null(entity.UpdatedAtUtc);
    }


    [Fact]
    public void SortDirectionSupportsDocumentedValues()
    {
        Assert.True(Enum.IsDefined(SortDirection.Asc));
        Assert.True(Enum.IsDefined(SortDirection.Desc));
    }

}

