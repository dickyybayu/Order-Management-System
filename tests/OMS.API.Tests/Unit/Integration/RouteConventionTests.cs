namespace OMS.API.Tests.Unit;

public sealed class RouteConventionTests : TestBase
{
    [Fact]
    public void ApiRoutePrefixConventionPrefixesControllerRoutes()
    {
        var convention = new ApiRoutePrefixConvention("api/v1");
        var application = new ApplicationModel();
        var controller = new ControllerModel(typeof(TestController).GetTypeInfo(), []);
        var selector = new SelectorModel
        {
            AttributeRouteModel = new AttributeRouteModel(new RouteAttribute("diagnostics"))
        };

        controller.Selectors.Add(selector);
        application.Controllers.Add(controller);

        convention.Apply(application);

        Assert.Equal("api/v1/diagnostics", selector.AttributeRouteModel?.Template);
    }
}

