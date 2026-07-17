namespace OMS.API.Constants.Permission;

public static class AuthorizationPolicies
{
    public const string AdminOnly = "AdminOnly";
    public const string SupervisorOnly = "SupervisorOnly";
    public const string SalesOperatorOnly = "SalesOperatorOnly";

    public const string CustomerWrite = "CustomerWrite";

    public const string OrderCreate = "OrderCreate";

    public const string OrderApprove = "OrderApprove";

    public const string OrderShip = "OrderShip";

    public const string OrderDeliver = "OrderDeliver";

    public const string OrderCancel = "OrderCancel";

    public const string ReportingRead = "ReportingRead";
}
