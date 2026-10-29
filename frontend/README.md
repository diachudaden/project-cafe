# Frontend workspace

Workspace cho ba ứng dụng Vite + React + TypeScript: `admin`, `pos` và `customer`, cùng package UI dùng chung `@chonrieng/ui` và auth dùng chung `@chonrieng/auth`.

## Yêu cầu

- Node.js `20.19.x`, `22.12+` hoặc `24+` (Vitest hiện chưa hỗ trợ Node.js 23)
- npm `10+`

## Cài đặt

Chạy tại thư mục `frontend`:

```powershell
npm install
```

## Cấu hình API

Mỗi ứng dụng đọc `VITE_API_BASE_URL`. Sao chép `.env.example` thành `.env.local` trong thư mục ứng dụng tương ứng và điều chỉnh URL nếu cần:

```powershell
Copy-Item apps/admin/.env.example apps/admin/.env.local
Copy-Item apps/pos/.env.example apps/pos/.env.local
Copy-Item apps/customer/.env.example apps/customer/.env.local
```

Giá trị mặc định trong các tệp mẫu là `http://localhost:5000`. Đây là URL cấu hình công khai cho frontend, không phải secret. Không đặt mật khẩu, signing key hoặc thông tin xác thực trong biến `VITE_*`, vì các giá trị này được đóng gói vào mã phía trình duyệt.

Backend Development mặc định cho phép các origin `http://localhost:5173`, `http://localhost:5174` và `http://localhost:5175`. Nếu đổi cổng hoặc host frontend, cập nhật `Cors:Origins` trong cấu hình backend theo môi trường. Cách cấu hình signing key, mật khẩu seed và tài khoản local nằm trong [README gốc](../README.md); không chép mật khẩu vào frontend.

## Chạy ứng dụng

Chạy một lệnh trong thư mục `frontend` cho từng ứng dụng:

```powershell
npm run dev:admin
npm run dev:pos
npm run dev:customer
```

Các địa chỉ mặc định lần lượt là `http://localhost:5173`, `http://localhost:5174` và `http://localhost:5175`.

## Kiểm tra kiểu và build

```powershell
npm run typecheck
npm test
npm run build
```

Mỗi ứng dụng cũng có thể chạy lệnh riêng bằng npm workspace, ví dụ:

```powershell
npm run build --workspace=@chonrieng/admin
```

## Đăng nhập và phiên

- Ba app dùng chung form đăng nhập React Hook Form + Zod và gửi `identifier`, `password`, `client` tới API backend thật. Tài khoản không đúng ứng dụng bị từ chối; `BranchManager` chỉ lọc dashboard theo các `branchIds` được cấp.
- Khi mở hoặc tải lại app, frontend gọi refresh bằng cookie `HttpOnly`, sau đó lấy người dùng qua `/me`. Các route nghiệp vụ được bảo vệ cho đến khi phiên hợp lệ.
- Access token chỉ được giữ trong state bộ nhớ và gửi qua header `Authorization` khi gọi `/me`; frontend không ghi access token, mật khẩu hoặc refresh cookie vào browser storage.
- Đăng xuất gọi API logout, xóa state phiên trong bộ nhớ và quay về trang đăng nhập. OTP khách hàng, PIN POS và đăng ký công khai chưa được duyệt nên không có trong luồng này.

## Màn hình hiện có

- **Admin:** dashboard doanh thu/tỷ lệ sử dụng/cảnh báo mẫu; lọc dashboard theo chi nhánh; danh sách chi nhánh chỉ đọc. Lịch ca, thu chi và báo cáo hiển thị trạng thái chưa có trong phiên bản này.
- **POS:** sơ đồ ghế theo trạng thái; chọn ghế trống để mở sheet check-in mẫu. Xác nhận chỉ cập nhật state trong bộ nhớ của trang, không tạo giao dịch hoặc lưu dữ liệu.
- **Customer:** danh sách quán mẫu, trạng thái quét bill chưa khả dụng, điểm thành viên mẫu và trang hồ sơ hiển thị thông tin người dùng từ `/me`.
- Cả ba app luôn hiển thị thông báo rằng số liệu nghiệp vụ là dữ liệu mẫu.

Các mảng dữ liệu mẫu nằm sau module service theo feature. UI đăng nhập dùng chung ở `packages/ui`; logic phiên/API auth dùng chung ở `packages/auth`; auth không dùng mock data. Không sinh Orval client trong TASK-006.

Các thông tin về doanh thu, giờ hoạt động, ghế và điểm chỉ phục vụ xem trước; chúng không phải dữ liệu lưu trên backend. Luồng check-in POS hiện chỉ thay đổi trạng thái ghế trong phiên chạy trang.
