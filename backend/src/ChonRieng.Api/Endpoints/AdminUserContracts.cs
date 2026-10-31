using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

namespace ChonRieng.Api.Endpoints;

/// <summary>Thông tin an toàn của tài khoản trong danh sách quản trị.</summary>
public sealed record AdminUserResponse(Guid Id, string Email, string FullName, string? PhoneNumber, DateTimeOffset? LockoutEnd);

/// <summary>Thông tin an toàn và vai trò của tài khoản dành cho quản trị viên.</summary>
public sealed record AdminUserDetailResponse(Guid Id, string Email, string FullName, string? PhoneNumber, DateTimeOffset? LockoutEnd, IReadOnlyList<string> Roles);

/// <summary>Dữ liệu tạo tài khoản người dùng bởi quản trị viên.</summary>
public sealed record CreateAdminUserRequest(
    [property: Required, EmailAddress, MaxLength(256), Description("Địa chỉ email dùng làm tên đăng nhập.")] string Email,
    [property: Required, MinLength(12), MaxLength(128), Description("Mật khẩu ban đầu; tối thiểu 12 ký tự, có chữ hoa, chữ thường, số và ký tự đặc biệt. Không được trả lại trong response.")] string Password,
    [property: Required, MaxLength(150), Description("Họ tên hiển thị của người dùng.")] string FullName,
    [property: Phone, MaxLength(32), Description("Số điện thoại tùy chọn.")] string? PhoneNumber,
    [property: Required, MinLength(1), Description("Một hoặc nhiều vai trò: Admin, BranchManager, Staff, Customer. Các endpoint chỉ cấp quyền theo policy cụ thể; gán vai trò không tự tạo EmployeeBranch.")] IReadOnlyList<string> Roles);

/// <summary>Dữ liệu cập nhật hồ sơ và danh sách vai trò của tài khoản.</summary>
public sealed record UpdateAdminUserRequest(
    [property: Required, EmailAddress, MaxLength(256), Description("Địa chỉ email dùng làm tên đăng nhập.")] string Email,
    [property: Required, MaxLength(150), Description("Họ tên hiển thị của người dùng.")] string FullName,
    [property: Phone, MaxLength(32), Description("Số điện thoại tùy chọn.")] string? PhoneNumber,
    [property: Required, MinLength(1), Description("Danh sách vai trò đầy đủ sau cập nhật: Admin, BranchManager, Staff, Customer. Cho phép nhiều vai trò.")] IReadOnlyList<string> Roles);
