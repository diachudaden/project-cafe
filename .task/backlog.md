# Task backlog

This backlog is intentionally small and requirement-driven. It stores the project’s active tasks and dependencies without creating speculative work beyond the current repo scope.

## Current backlog

| ID | Status | Title | Notes |
| --- | --- | --- | --- |
| TASK-001 | Completed | Establish GitHub Copilot workflow for Cafe CR | Creates the repository-level rules, prompts, and task structure.
| TASK-002 | Đang làm | Tạo API host, Swagger và tích hợp module thanh toán | API dùng schema bootstrap/entity mẫu; domain/auth/schema thực vẫn cần xác nhận trước production; xem [TASK-002](./TASK-002-integration.md).
| TASK-003 | Planned | Verify end-to-end payment behavior and reporting flows | Requires runtime validation with test data or a live environment.
| TASK-004 | Đang làm | Cấu hình thông tin SQL local qua file `.env` được Git bỏ qua | `.env` được ignore, mẫu và script EF đã thêm; tích hợp runtime API thật còn phụ thuộc TASK-002.
| TASK-005 | Bị chặn | Tích hợp EF Core và tạo, kiểm tra, áp dụng migration Payments | Đã scaffold migration/bootstrap và SQL script; chỉ áp dụng sau khi xác nhận database trống/dev và schema mẫu phù hợp; xem [TASK-005](./TASK-005-payments-migration.md).

## Working rules

- Keep tasks linked to the canonical spec in [../spec.md](../spec.md) and [../docs/payments/SPEC.md](../docs/payments/SPEC.md).
- Prefer narrow tasks with explicit acceptance criteria and verification commands.
- Treat missing or conflicting requirements as blocked rather than guessed behavior.
