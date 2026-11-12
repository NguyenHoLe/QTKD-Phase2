# Nhật ký lỗi — QTKD Phase 2

Các lỗi được phát hiện khi rà soát lại mã nguồn tuần 06 trước khi viết test tuần 07 (12/11/2026). Mọi phép thử bên dưới chạy trên một database tạm `qtkd_review_tmp`, không dùng database `qtkd` đang phát triển; database tạm đã được xóa sau khi kiểm tra.

| Mã | Tóm tắt | Mức độ | Trạng thái | Commit sửa |
|---|---|---|---|---|
| BUG-01 | Tạo trùng `ContractNo` / `ServiceCode` đồng thời trả `500` | Trung bình | Đã sửa | `a818a36` |
| BUG-02 | Lỗi đọc request và lỗi `404` không theo mẫu lỗi chung, lộ tên kiểu nội bộ | Thấp | Đã sửa | `a818a36` |
| BUG-03 | Sửa và Submit cùng lúc có thể vi phạm CTR-01/CTR-02 | Trung bình | Đã sửa | `a818a36` |

## BUG-01 — Tạo trùng đồng thời trả HTTP 500

**Cách tái hiện**

1. Chạy API, gửi cùng lúc 8 request `POST /api/contracts` có cùng `contractNo` (ví dụ `HD-RACE-1`), lặp 3 lần với 3 mã khác nhau.
2. Gửi cùng lúc 8 request `POST /api/price-lists/1/items` có cùng `serviceCode`.

**Kết quả trước khi sửa**

- Contract: 3 × `201`, 14 × `400`, **7 × `500`** trên 24 request.
- PriceItem: 1 × `201`, 2 × `409`, **5 × `500`** trên 8 request.
- Log server: `Npgsql.PostgresException: 23505: duplicate key value violates unique constraint "IX_Contracts_ContractNo"`.

Dữ liệu không bị trùng, vì unique index đã chặn. Nhưng client nhận lỗi `500` thay vì lỗi nghiệp vụ.

**Nguyên nhân**

Service kiểm tra trùng bằng `AnyAsync(...)` rồi mới `SaveChangesAsync()`. Hai request đến cùng lúc đều qua bước kiểm tra trước khi request kia kịp lưu. Request lưu sau bị PostgreSQL từ chối (SQLSTATE `23505`), và `DbUpdateException` không được bắt ở đâu cả.

**Cách sửa**

- Thêm `Data/DbUpdateExceptionExtensions.IsUniqueViolation()` để nhận biết lỗi `23505`.
- `ContractService.CreateAsync` / `UpdateAsync`: bắt lỗi này và trả cùng lỗi như khi kiểm tra trùng thông thường (`400 validation_failed`, `"ContractNo must be unique."`).
- `PricingService.AddPriceItemAsync`: bắt lỗi này và trả `409 conflict` như khi `ServiceCode` đã tồn tại.

**Kết quả sau khi sửa** (cùng kịch bản): Contract 3 × `201`, 21 × `400`, 0 × `500`; PriceItem 1 × `201`, 7 × `409`, 0 × `500`.

## BUG-02 — Mẫu phản hồi lỗi không thống nhất

**Cách tái hiện**

1. `POST /api/contracts` với body `{"contractNo":"X","customerId":1,"startDate":"abc","endDate":"2027-01-01"}`.
2. `GET /api/contracts/999999`.

**Kết quả trước khi sửa**

- (1) trả ProblemDetails mặc định của ASP.NET (`type`, `title`, `errors`...) thay vì `{code, message, details}`. Thông báo còn lộ tên kiểu nội bộ: `"The JSON value could not be converted to QTKD.Api.Dtos.SaveContractRequest..."`.
- (2) trả ProblemDetails `{"title":"Not Found","status":404}`, không có `code`/`message` như các lỗi khác.

Điều này trái với yêu cầu thống nhất mẫu phản hồi lỗi (bước 9, tuần 06).

**Nguyên nhân**

Lỗi bind dữ liệu do `[ApiController]` tự tạo trước khi vào controller. Các action `GET {id}` gọi `NotFound()` không kèm body.

**Cách sửa**

- `Program.cs`: cấu hình `InvalidModelStateResponseFactory` để trả `ApiErrorResponse` với `code = invalid_request`. `details` chỉ nêu tên trường (ví dụ `"startDate: value is missing or has an invalid format."`).
- Thêm `UseExceptionHandler` để lỗi ngoài dự kiến trả `500 internal_error`, không lộ stack trace.
- `ApiControllerBase.NotFoundError(...)`: các `GET {id}` trả `404 not_found` kèm message.
- Gom mã lỗi vào `Common/ApiErrorCodes.cs` và ghi bảng mã lỗi vào `docs/week-06-api.md`.

**Kết quả sau khi sửa**

- (1) → `400 {"code":"invalid_request","message":"The request body could not be read.","details":["startDate: value is missing or has an invalid format."]}`.
- (2) → `404 {"code":"not_found","message":"Contract 999999 was not found.","details":null}`.

## BUG-03 — Sửa và Submit cùng lúc vi phạm CTR-01/CTR-02

**Cách phát hiện**

Ban đầu lỗi được phát hiện khi đọc code. Ở tuần 07, integration test `ConcurrentUpdateAndSubmit_NeverBreakCtr01OrCtr02` đã **tái hiện được lỗi trên mã nguồn trước khi sửa** (commit `19012a5`), ngay vòng đầu tiên: `Round 0: both the edit and the submit succeeded.` Nghĩa là cùng một hợp đồng vừa được gửi trình duyệt, vừa bị sửa nội dung.

**Kịch bản**

Hợp đồng Draft đang đủ điều kiện. Request A gọi `PUT` để xóa `contentSummary`; request B gọi `POST .../submit`.

- Nếu B đọc dữ liệu trước khi A lưu, B kiểm tra CTR-02 trên dữ liệu cũ, rồi cả hai cùng lưu. Kết quả: hợp đồng `PendingApproval` nhưng thiếu nội dung, vi phạm CTR-02.
- Nếu A đọc trước khi B lưu, A kiểm tra CTR-01 lúc hợp đồng còn Draft rồi ghi đè nội dung sau khi hợp đồng đã `PendingApproval`, vi phạm CTR-01.

**Nguyên nhân**

Các quy tắc được kiểm tra trên dữ liệu đọc ở đầu request. Lệnh `UPDATE` không kiểm tra xem bản ghi có bị thay đổi kể từ lúc đọc hay không.

**Cách sửa**

- Thêm `Contract.Version` ánh xạ tới cột hệ thống `xmin` của PostgreSQL, làm concurrency token (`IsRowVersion()`). Migration `ContractConcurrencyToken` không tạo cột mới, chỉ ghi lịch sử migration.
- Khi bản ghi đã đổi kể từ lúc đọc, EF Core ném `DbUpdateConcurrencyException` và không lưu gì. `UpdateAsync` và `SubmitAsync` trả `409 conflict`.

**Kết quả sau khi sửa**

Chạy 20 lần `PUT` (xóa nội dung) song song với `submit` trên 20 hợp đồng khác nhau: 0 hợp đồng `PendingApproval` thiếu nội dung. Đồng thời, 10 request submit song song trên mỗi hợp đồng (3 hợp đồng) cho đúng 1 × `200` và 9 × `409` mỗi hợp đồng.

## Kiểm tra hồi quy sau khi sửa

- `docs/demo/verify-week06.ps1`: **94/94 PASS**.
- `docs/demo/demo-week06.ps1`: luồng đầy đủ đạt (`422` → `200` → `PendingApproval` → `409`).

## Test tự động hồi quy (tuần 07)

Mỗi lỗi có integration test tương ứng trong `tests/QTKD.IntegrationTests`. Cùng bộ test được chạy trên hai phiên bản mã nguồn:

| Lỗi | Test | Trên `19012a5` (trước khi sửa) | Sau khi sửa |
|---|---|---|---|
| BUG-01 | `ConcurrentCreatesWithSameContractNo_NeverReturn500_AndStoreOneContract` | Lỗi — `23505 ... "IX_Contracts_ContractNo"` | Đạt |
| BUG-01 | `ConcurrentAddsOfSameServiceCode_NeverReturn500_AndStoreOneItem` | Lỗi — `23505 ... "PK_PriceItems"` | Đạt |
| BUG-02 | `CreateCustomerWithInvalidJson_ReturnsSharedErrorShape`, `CreateContractWithInvalidDate_ReturnsSharedErrorShape`, `GetUnknownCustomer_Returns404WithErrorBody`, `GetUnknownContract_Returns404WithErrorBody` | Lỗi (4 test) | Đạt |
| BUG-03 | `ConcurrentUpdateAndSubmit_NeverBreakCtr01OrCtr02` | Lỗi — `Round 0: both the edit and the submit succeeded.` | Đạt |

Tổng cộng trên bản cũ: 7 test lỗi, 39 test đạt. Sau khi sửa: 46/46 đạt. Chi tiết ở [week-07-testing.md](week-07-testing.md).
