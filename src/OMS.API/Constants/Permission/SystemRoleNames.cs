namespace OMS.API.Constants.Permission;

public static class SystemRoleNames
{
    public const string Admin = "admin";
    public const string Supervisor = "supervisor";
    public const string SalesOperator = "sales_operator";

    public static readonly IReadOnlyCollection<string> All =
    [
        Admin,
        Supervisor,
        SalesOperator
    ];
}
