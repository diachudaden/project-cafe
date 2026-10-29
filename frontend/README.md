# Frontend workspace

Workspace cho ba ứng dụng Vite + React + TypeScript: `admin`, `pos` và `customer`.

## Yêu cầu

- Node.js `20.19+` hoặc `22.12+`
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
npm run build
```

Mỗi ứng dụng cũng có thể chạy lệnh riêng bằng npm workspace, ví dụ:

```powershell
npm run build --workspace=@chonrieng/admin
```

Hiện các ứng dụng chỉ là bộ khung định tuyến để xác minh workspace. Màn hình nghiệp vụ, dữ liệu giả, UI dùng chung và tích hợp đăng nhập thuộc các task tiếp theo.
