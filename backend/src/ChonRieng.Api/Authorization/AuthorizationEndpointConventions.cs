namespace ChonRieng.Api.Authorization;

public sealed record AuthorizationDescriptionMetadata(string Description);

public static class AuthorizationEndpointConventions
{
    public static RouteGroupBuilder RequireBranchScope(this RouteGroupBuilder group)
    {
        return group
            .RequireAuthorization("BranchScope")
            .WithDescription(
                "Yêu cầu đăng nhập bằng vai trò Admin, BranchManager hoặc Staff. Người dùng chỉ truy cập dữ liệu thuộc chi nhánh đã được gán trong EmployeeBranch; Admin được truy cập toàn hệ thống. Endpoint phải lọc truy vấn theo phạm vi chi nhánh ở phía máy chủ.")
            .WithMetadata(new AuthorizationDescriptionMetadata(
                "Admin có phạm vi toàn hệ thống. BranchManager và Staff chỉ được truy cập branchId có trong token; API phải lọc dữ liệu theo cùng phạm vi."));
    }

    public static RouteGroupBuilder RequireCustomerOwnership(this RouteGroupBuilder group)
    {
        return group
            .RequireAuthorization("CustomerOwnResource")
            .WithDescription(
                "Yêu cầu đăng nhập. Khách hàng chỉ truy cập tài nguyên của chính mình qua tham số userId hoặc customerId; Admin được truy cập theo quyền quản trị.")
            .WithMetadata(new AuthorizationDescriptionMetadata(
                "Customer chỉ được truy cập hồ sơ/tài nguyên thuộc chính mình qua route userId hoặc customerId. Admin được truy cập theo quyền quản trị."));
    }
}
