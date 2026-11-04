# API và đối chiếu nghiệp vụ — Tuần 06

Base URL: `http://localhost:<port>` (thay `<port>` bằng cổng được `dotnet run` in ra). API cũng hiển thị trong Swagger UI tại `/swagger` khi chạy Development.

## Danh sách API

| Method | Endpoint | Hành vi | Thành công |
|---|---|---|---|
| GET | `/api/customers` | Xem danh sách Customer | `200` |
| GET | `/api/customers/{id}` | Xem chi tiết Customer | `200`, không có: `404` |
| POST | `/api/customers` | Tạo Customer | `201`, dữ liệu sai: `400` |
| PUT | `/api/customers/{id}` | Cập nhật tên/trạng thái Customer | `200`, sai: `400`, không có: `404` |
| GET | `/api/contracts` | Xem danh sách Contract | `200` |
| GET | `/api/contracts/{id}` | Xem Contract và thời điểm gửi nếu đã gửi | `200`, không có: `404` |
| POST | `/api/contracts` | Tạo Contract ở trạng thái `Draft` | `201`, dữ liệu sai: `400` |
| PUT | `/api/contracts/{id}` | Cập nhật Contract nếu trạng thái cho phép | `200`, dữ liệu sai: `400`, không có: `404`, trạng thái sai: `409` |
| POST | `/api/contracts/{id}/submit` | Kiểm tra CTR-02 và bàn giao mô phỏng | `200`, thiếu điều kiện: `422`, sai trạng thái/gửi lặp: `409`, không có: `404` |
| GET | `/api/price-lists` | Xem danh sách PriceList | `200` |
| GET | `/api/price-lists/{id}` | Xem PriceList | `200`, không có: `404` |
| POST | `/api/price-lists` | Tạo PriceList | `201`, dữ liệu sai: `400` |
| GET | `/api/price-lists/{id}/items` | Xem PriceItem của PriceList | `200`, không có PriceList: `404` |
| POST | `/api/price-lists/{id}/items` | Thêm PriceItem vào PriceList | `201`, dữ liệu sai: `400`, mã trùng: `409`, PriceList không có: `404` |

Các lỗi nghiệp vụ trả JSON theo dạng:

```json
{
  "code": "submission_validation_failed",
  "message": "Contract does not meet CTR-02 submission requirements. No changes were saved.",
  "details": ["CTR-02 demo assumption: ContentSummary is required."]
}
```

## Request/response mẫu

### Tạo Customer

`POST /api/customers`

```json
{
  "name": "Công ty Demo Tuần 06",
  "status": "Active"
}
```

Trả `201 Created` với Customer vừa tạo. Có thể bỏ `status`; mặc định là `Active`.

### Tạo Contract Draft còn thiếu điều kiện Submit

`POST /api/contracts` (dùng `customerId` vừa nhận ở bước trước; mã hợp đồng phải chưa được dùng):

```json
{
  "contractNo": "HD-DEMO-006",
  "customerId": 3,
  "startDate": "2026-11-01",
  "endDate": "2027-10-31"
}
```

Trả `201 Created`, `status` luôn là `Draft`. `contentSummary`, `attachmentFileName` được phép để trống khi lưu nháp.

Số `3` chỉ minh họa database mới có sẵn hai Customer seed; nếu đã chạy demo trước đó, dùng `customerId` trong response và chọn `contractNo` chưa tồn tại.

### Gửi khi chưa đủ điều kiện

`POST /api/contracts/{id}/submit` không cần body. Với Contract mẫu ở trên, API trả `422 Unprocessable Entity` và liệt kê hai giả định CTR-02 còn thiếu: `ContentSummary`, `AttachmentFileName`. Contract vẫn giữ `Draft`; không tạo `ContractSubmission`.

### Bổ sung thông tin và cập nhật

`PUT /api/contracts/{id}`:

```json
{
  "contractNo": "HD-DEMO-006",
  "customerId": 3,
  "startDate": "2026-11-01",
  "endDate": "2027-10-31",
  "contentSummary": "Cung cấp dịch vụ logistics theo phạm vi đã thống nhất.",
  "attachmentFileName": "HD-DEMO-006.pdf"
}
```

Trả `200 OK`; `status` vẫn là `Draft`.

### Gửi thành công

Gọi lại `POST /api/contracts/{id}/submit`. Trả `200 OK`, ví dụ:

```json
{
  "contractId": 3,
  "status": "PendingApproval",
  "submittedAtUtc": "2026-11-05T10:00:00Z",
  "workflowMessage": "Approval hand-off was recorded. Reviewer assignment and approval steps are not implemented in this demo."
}
```

`GET /api/contracts/{id}` trả Contract với `status: PendingApproval` và `submittedAtUtc` đã ghi. `ContractSubmission` có khóa chính đồng thời là khóa ngoại `ContractId`, nên mỗi Contract chỉ có tối đa một bản ghi gửi mô phỏng. Submit lần nữa trả `409 Conflict`, không tạo bản ghi thứ hai. Cập nhật Contract đã `PendingApproval` cũng trả `409`.

### Pricing

`POST /api/price-lists`:

```json
{
  "name": "Bảng giá demo tuần 06",
  "effectiveDate": "2026-11-01"
}
```

Thêm PriceItem bằng `POST /api/price-lists/{id}/items`:

```json
{
  "serviceCode": "SVC-DEMO-006",
  "unitPrice": 150000
}
```

`UnitPrice` phải lớn hơn 0; `ServiceCode` là duy nhất trong cùng PriceList. Dùng `GET /api/price-lists/{id}/items` để xem các dòng giá.

## Đối chiếu US03, Acceptance Criteria và quy tắc

| Yêu cầu | Đoạn xử lý | Kết quả kiểm tra |
|---|---|---|
| AC01 — người không có quyền không được gửi | Phase 1 xác định tác nhân là Nhân viên Kinh doanh, nhưng solution chưa có tài khoản, đăng nhập hoặc chính sách phân quyền. API hiện chưa xác thực người gọi, nên chưa thể từ chối đúng theo vai trò hoặc kiểm thử AC01. | Đây là phần chưa hoàn thành; cần bổ sung danh tính và quyền trước khi tuyên bố AC01 đạt. Không giả lập quyền bằng một header không an toàn. |
| AC02 — trạng thái không hợp lệ thì từ chối | `ContractService.SubmitAsync` chỉ chấp nhận `Draft`; `UpdateAsync` dùng `ContractStatuses.CanEdit`. | Submit/update ở trạng thái khác trả `409`; không đổi Contract. |
| AC03 / CTR-02 — thiếu điều kiện thì không tạo quy trình | `ContractService.GetSubmissionErrors` kiểm tra Customer Active, khoảng ngày hợp lệ, nội dung và tên tệp demo. | Trả `422` với `details`; status và bảng `ContractSubmission` không đổi. |
| AC04 — hợp lệ thì tạo quy trình và chờ duyệt | `ContractService.SubmitAsync` ghi `ContractSubmission` và đổi status sang `PendingApproval` trong cùng `SaveChanges`. | `200` kèm `SubmittedAtUtc`; GET Contract cho thấy trạng thái mới. |
| CTR-01 — chỉ sửa Draft hoặc RevisionRequested | `ContractService.UpdateAsync` kiểm tra `ContractStatuses.CanEdit`; request không có trường `status`. | Sửa `PendingApproval` bị từ chối `409`; client không thể dùng endpoint cập nhật để đặt `Approved`. |
| CTR-02 — Customer, ngày hiệu lực, nội dung/tệp hợp lệ | `ValidateEditableFieldsAsync` kiểm tra Customer Active, ngày bắt đầu/kết thúc và cấu trúc dữ liệu; `GetSubmissionErrors` kiểm tra đủ điều kiện khi Submit. | Draft có thể lưu thiếu nội dung/tệp để demo lỗi Submit; lỗi không ghi thay đổi. |
| CTR-03 — không chuyển Draft thẳng sang Approved | Contract mới luôn được tạo ở `Draft`; `SubmitAsync` chỉ chuyển sang `PendingApproval`; DTO cập nhật không nhận status. | Không có API tuần 06 nào chuyển thẳng sang `Approved`. |
| AC05 / A4 — gửi lặp hoặc cạnh tranh không tạo xử lý mới | Một-một `ContractSubmission` dùng `ContractId` làm khóa chính; trạng thái cũng đổi khỏi Draft. | Verify đã kiểm tra gửi lặp tuần tự trả `409`. Khóa chính bảo vệ không tạo hai bản ghi khi có cạnh tranh; kịch bản gửi đồng thời chưa được chạy trong demo này. |

## Giả định và giới hạn demo

- Phase 1 chưa xác định trường nội dung/tệp bắt buộc. Demo giả định `ContentSummary` không rỗng và có `AttachmentFileName`; đây là giả định cần xác nhận, không phải quy tắc chính thức.
- `AttachmentFileName` chỉ lưu tên tệp metadata, chưa upload hoặc lưu file bytes.
- Khoảng hiệu lực hợp lệ trong demo khi hai ngày có giá trị và `EndDate >= StartDate`; chưa tự đặt quy tắc “phải bắt đầu trong tương lai”.
- `ContractSubmission` ghi một lần bàn giao và thời điểm UTC. Chưa có reviewer, các bước phê duyệt, lịch sử nhiều phiên, hoặc xử lý kết quả duyệt.
- Chưa có xác thực/phân quyền cho tác nhân Nhân viên Kinh doanh; AC01 chưa được triển khai. Cần có hệ thống danh tính và chính sách quyền để xác định tài khoản nào được submit.

## Checklist quay demo

1. Mở Swagger, tạo Customer Active.
2. Tạo Contract Draft cho Customer đó, không truyền `contentSummary` và `attachmentFileName`.
3. Submit và cho thấy `422`, hai điều kiện thiếu, Contract vẫn Draft.
4. Cập nhật Contract với hai trường demo, sau đó Submit lại và cho thấy `PendingApproval`.
5. GET Contract và đối chiếu `ContractSubmission` trong PostgreSQL.
6. Submit lần nữa để cho thấy `409` và chỉ một bản ghi submission.

Hai script PowerShell nằm trong `docs/demo/`:

- `demo-week06.ps1` chạy lần lượt luồng Customer → Draft thiếu điều kiện → cập nhật → Submit thành công → Submit lặp; script ghi log theo mục vào `week06-demo-log.txt`.
- `verify-week06.ps1` gửi các request kiểm tra Customer, Contract, submit workflow và pricing; báo cáo có từng mục `PASS`/`FAIL` cùng tổng số thực tế trong `week06-verification-report.txt`. Script tạo một số bản ghi có tên duy nhất trong database API đang dùng; chỉ chạy với database phát triển có thể thêm dữ liệu.

Chạy khi API đang mở và database đã áp dụng migration tuần 06:

```powershell
.\docs\demo\demo-week06.ps1 -BaseUrl http://localhost:5209
.\docs\demo\verify-week06.ps1 -BaseUrl http://localhost:5209
```

Thay cổng nếu terminal của API hiển thị cổng khác. Các file output chỉ được tạo khi bạn chủ động chạy script. Nếu file output mặc định đang bị khóa, script tự chọn tên có hậu tố riêng. Báo cáo chỉ phản ánh các request thực sự chạy; số lượng PASS có thể thay đổi theo kết quả API.
