using Microsoft.AspNetCore.Identity;

namespace ChonRieng.Api.Services;

public static class IdentityErrorFormatter
{
    public static Dictionary<string, string[]> ToValidationErrors(IdentityResult result)
    {
        return new Dictionary<string, string[]>
        {
            ["Identity"] = result.Errors.Select(Translate).ToArray(),
        };
    }

    private static string Translate(IdentityError error)
    {
        return error.Code switch
        {
            "DuplicateEmail" => "Email này đã được sử dụng.",
            "DuplicateUserName" => "Tên đăng nhập này đã được sử dụng.",
            "InvalidEmail" => "Địa chỉ email không hợp lệ.",
            "PasswordTooShort" => "Mật khẩu chưa đạt độ dài tối thiểu.",
            "PasswordRequiresDigit" => "Mật khẩu phải có ít nhất một chữ số.",
            "PasswordRequiresLower" => "Mật khẩu phải có ít nhất một chữ thường.",
            "PasswordRequiresUpper" => "Mật khẩu phải có ít nhất một chữ hoa.",
            "PasswordRequiresNonAlphanumeric" => "Mật khẩu phải có ít nhất một ký tự đặc biệt.",
            _ => $"Không thể cập nhật tài khoản (mã lỗi: {error.Code}).",
        };
    }
}
