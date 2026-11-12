# Kiểm thử — Tuần 07

Tuần 07 không xây thêm chức năng. Bộ test kiểm tra chính mã nguồn tuần 06 (Customer, Contract, Submit US03, Pricing). Lỗi phát hiện được đã sửa và ghi trong [bug-log.md](bug-log.md).

## 1. Tổ chức bộ test

```
tests/
├── QTKD.UnitTests/                 Unit test — quy tắc nghiệp vụ, không cần database
│   ├── Ctr01EditRuleTests.cs        CTR-01  ContractRules.CanEdit
│   ├── Ctr02SubmissionRuleTests.cs  CTR-02  ContractRules.GetSubmissionErrors
│   ├── Ctr03TransitionRuleTests.cs  CTR-03  ContractRules.CanTransition (bảng chuyển trạng thái)
│   └── ApiErrorFormatTests.cs       Mẫu lỗi chung khi request không đọc được (BUG-02)
└── QTKD.IntegrationTests/          Integration test — gọi API thật + PostgreSQL thật
    ├── Infrastructure/              WebApplicationFactory, database kiểm thử, helper
    ├── CustomerApiTests.cs          TC01, TC02
    ├── ContractApiTests.cs          TC03, TC04, TC07
    ├── SubmitContractApiTests.cs    TC05, TC06, TC08, luồng demo tuần 06
    ├── ConcurrencyRegressionTests.cs  Hồi quy BUG-01, BUG-03
    └── PricingApiTests.cs           PriceList / PriceItem
```

**Unit test** gọi trực tiếp lớp `QTKD.Api.Domain.ContractRules`. Đây là các hàm thuần, nhận một `Contract` và trả kết quả, nên không cần database hay HTTP.

**Integration test** dùng `WebApplicationFactory<Program>` để chạy chính API (cùng `Program.cs`, controller, service, EF Core) trong bộ nhớ, kết nối tới PostgreSQL thật:

- **Database riêng:** test dùng `qtkd_test`, đọc từ khóa `ConnectionStrings:QtkdTest` (user-secrets của project test hoặc biến môi trường `ConnectionStrings__QtkdTest`). API trong test chạy ở môi trường `Testing`, nên không đọc user-secrets của nhà phát triển và không thể kết nối nhầm vào `qtkd`.
- **Chặn chạy nhầm:** test xóa và tạo lại database mỗi lần chạy, nên `TestDatabase` từ chối chạy nếu tên database không chứa chữ `test`. Điều này đã được kiểm tra: với `Database=qtkd_guardcheck`, test dừng với thông báo `Refusing to run integration tests against database 'qtkd_guardcheck'`.
- **Dữ liệu đầu vào cố định:** đầu mỗi lần chạy, database được dựng lại từ toàn bộ migration nên luôn có đúng dữ liệu mẫu. Mỗi test tự tạo Customer/Contract với mã duy nhất, không phụ thuộc thứ tự chạy.
- **Kiểm tra cả ba lớp:** mỗi test kiểm tra HTTP status, nội dung response (gồm `code`/`details` của lỗi) và dữ liệu thật trong PostgreSQL. Dữ liệu được đọc bằng một `QtkdDbContext` riêng, độc lập với API. Test không kết luận thành công chỉ vì API trả `200`.

## 2. Cách chạy

```powershell
# Một lần trên mỗi máy: chuỗi kết nối tới database kiểm thử (tên phải chứa "test")
dotnet user-secrets set "ConnectionStrings:QtkdTest" "Host=localhost;Port=5432;Database=qtkd_test;Username=postgres;Password=YOUR_POSTGRES_PASSWORD" --project tests/QTKD.IntegrationTests

# Chạy toàn bộ test (database qtkd_test được tạo tự động)
dotnet test QTKD.sln

# Xuất file TRX như trong docs/test-results
dotnet test tests/QTKD.UnitTests --logger "trx;LogFileName=week07-UnitTests.trx" --results-directory docs/test-results
dotnet test tests/QTKD.IntegrationTests --logger "trx;LogFileName=week07-IntegrationTests.trx" --results-directory docs/test-results

# Chạy riêng một test case (cả unit lẫn integration), tiện khi demo từng TC
dotnet test QTKD.sln --filter "TC=TC07"
# Chạy riêng các test của một quy tắc / một lỗi
dotnet test QTKD.sln --filter "Rule=CTR-02"
dotnet test QTKD.sln --filter "Bug=BUG-03"
```

Mỗi test được gắn trait `TC` (TC01–TC08, `Demo`), `Rule` (CTR-01..03, unit test) hoặc `Bug` (BUG-01..03). Ví dụ `--filter "TC=TC07"` chạy 12 unit test và 2 integration test.

## 3. Kết quả

Chạy ngày 12/11/2026, .NET SDK 10.0.401, PostgreSQL 18:

| Project | Tổng | Đạt | Lỗi | Minh chứng |
|---|---|---|---|---|
| QTKD.UnitTests | 46 | 46 | 0 | [week07-UnitTests.trx](test-results/week07-UnitTests.trx) |
| QTKD.IntegrationTests | 46 | 46 | 0 | [week07-IntegrationTests.trx](test-results/week07-IntegrationTests.trx) |

Output console đầy đủ: [dotnet-test-console.txt](test-results/dotnet-test-console.txt). Bộ integration test được chạy thêm 5 lần liên tiếp, lần nào cũng đạt 46/46, nên các test chạy song song cho kết quả ổn định.

## 4. Bảng TC01–TC08

Tất cả 8 test case đều được **tự động hóa**. Mỗi test case có test ở mức quy tắc (unit) nếu áp dụng được, và test qua API + database (integration).

| Test | Dữ liệu/thao tác | Kết quả mong đợi | Test tự động | Kết quả |
|---|---|---|---|---|
| TC01 | Tạo Customer hợp lệ | `201`, đọc lại được từ DB | **Integration:** `TC01_CreateValidCustomer_IsStoredAndCanBeReadBack` (status, body, header `Location`, dòng trong bảng `Customers`, GET lại); `TC01_CreateCustomerWithoutStatus_DefaultsToActive` | Pass |
| TC02 | Tạo Customer thiếu thông tin bắt buộc | `400`, có thông báo lỗi, không lưu | **Integration:** `TC02_CreateCustomerWithoutName_...` (4 trường hợp: thiếu, `null`, rỗng, khoảng trắng; số Customer trong DB không đổi); `TC02_CreateCustomerWithInvalidStatus_IsRejected` (2); `TC02_CreateCustomerWithNameOver200Characters_IsRejected` | Pass |
| TC03 | Sửa Contract ở Draft | Thành công khi dữ liệu hợp lệ | **Unit:** `CanEdit_ReturnsTrue_ForDraftAndRevisionRequested` (3). **Integration:** `TC03_UpdateDraftContract_WithValidData_Succeeds_AndIsStored` (mọi trường đã đổi trong DB, vẫn Draft); `TC03_UpdateDraftContract_WithInvalidData_IsRejected_AndDataStays` | Pass |
| TC04 | Sửa Contract ở PendingApproval | Bị từ chối, dữ liệu giữ nguyên | **Unit:** `CanEdit_ReturnsFalse_ForAnyOtherStatus` (7). **Integration:** `TC04_UpdatePendingApprovalContract_IsRejected_AndDataStays` (`409`, message nêu CTR-01; số hợp đồng, nội dung, tệp, trạng thái trong DB không đổi) | Pass |
| TC05 | Submit Contract thiếu điều kiện CTR-02 | Bị từ chối, vẫn là Draft | **Unit:** 10 test `GetSubmissionErrors_Rejects...` + `ListsEveryUnmetCondition`. **Integration:** `TC05_SubmitWithoutRequiredContentAndFile_...` (`422` + 2 điều kiện, vẫn Draft, không có `ContractSubmission`); `TC05_SubmitMissingOneRequiredItem_NamesThatItem` (2); `TC05_SubmitWhenCustomerBecameInactive_...` | Pass |
| TC06 | Submit Contract hợp lệ | Chuyển sang PendingApproval | **Unit:** `CanTransition(Draft → PendingApproval)`, `GetSubmissionErrors_ReturnsNoErrors_...`. **Integration:** `TC06_SubmitValidDraft_MovesToPendingApproval_AndRecordsHandOff` (`200`, body, status trong DB, đúng 1 `ContractSubmission` cùng thời điểm, GET lại) | Pass |
| TC07 | Thử chuyển Draft thẳng Approved | Bị từ chối | **Unit:** `CanTransition(Draft → Approved) = false`; `OnlyPendingApproval_CanMoveToApproved`; `Submit_FromDraft_LeadsOnlyToPendingApproval`. **Integration:** `TC07_CreateContractWithApprovedStatus_StillStartsInDraft`; `TC07_UpdateDraftContractToApproved_DoesNotChangeStatus` (gửi `"status":"Approved"`, DB vẫn Draft) | Pass |
| TC08 | Submit lặp một Contract | Không tạo thêm kết quả xử lý trùng | **Unit:** `CanTransition(PendingApproval → PendingApproval) = false`. **Integration:** `TC08_SubmitSameContractTwice_...` (lần 2 trả `409`, DB chỉ 1 `ContractSubmission`, thời điểm gửi không đổi); `TC08_ConcurrentSubmits_CreateExactlyOneHandOff` (10 request đồng thời: đúng 1 × `200`, 9 × `409`, 1 bản ghi) | Pass |

Ngoài TC01–TC08 còn có các test khác:
- `DemoFlow_CustomerToContractToSubmit`: toàn bộ luồng demo tuần 06 dưới dạng một test tự động.
- Hồi quy cho BUG-01/02/03.
- Pricing: đơn giá phải lớn hơn 0, `ServiceCode` không trùng trong một PriceList, liên kết PriceItem–PriceList đúng, PriceList không tồn tại trả `404`.
- Dữ liệu mẫu tuần 05.

## 5. Đối chiếu Acceptance Criteria US03 và quy tắc

| Yêu cầu | Test | Ghi chú |
|---|---|---|
| AC01 — không có quyền thì từ chối | **Chưa tự động hóa, chưa kiểm thử được** | Solution chưa có đăng nhập/phân quyền (đã ghi ở tuần 06). Đây là phần còn thiếu, không phải test bị bỏ qua. |
| AC02 — sai trạng thái thì từ chối | TC04, TC08 (sequential), `CanTransition_ReturnsFalse_...` | `409 conflict` |
| AC03 — thiếu CTR-02 thì trả danh sách điều kiện | TC05 | `422` + `details` liệt kê đủ điều kiện chưa đạt |
| AC04 — hợp lệ thì chuyển Chờ phê duyệt | TC06 | `ContractSubmission` mô phỏng "approval instance" |
| AC05 — gửi lặp/đồng thời không tạo nhiều phiên | TC08 (2 test) | Cả tuần tự lẫn đồng thời |
| CTR-01 | TC03, TC04, `ConcurrentUpdateAndSubmit_NeverBreakCtr01OrCtr02` | |
| CTR-02 | TC05, TC06, `ConcurrentUpdateAndSubmit_...` | Nội dung/tệp là giả định demo |
| CTR-03 | TC06, TC07 | |

## 6. Lỗi đã tìm thấy

Chi tiết cách tái hiện, nguyên nhân và cách sửa ở [bug-log.md](bug-log.md). Cả ba lỗi được sửa trong commit `a818a36`.

| Mã | Lỗi | Test hồi quy |
|---|---|---|
| BUG-01 | Tạo trùng `ContractNo`/`ServiceCode` đồng thời trả `500` | `ConcurrentCreatesWithSameContractNo_...`, `ConcurrentAddsOfSameServiceCode_...` |
| BUG-02 | Lỗi đọc request và `404` không theo mẫu lỗi chung, lộ tên kiểu nội bộ | `CreateCustomerWithInvalidJson_...`, `CreateContractWithInvalidDate_...`, `GetUnknown..._Returns404WithErrorBody`, `ApiErrorFormatTests` |
| BUG-03 | Sửa và Submit đồng thời vi phạm CTR-01/CTR-02 | `ConcurrentUpdateAndSubmit_NeverBreakCtr01OrCtr02` |

**Bằng chứng test phát hiện được lỗi.** Bộ integration test hiện tại được chạy lại trên mã nguồn tuần 06 trước khi sửa (commit `19012a5`). Kết quả: **7 test hồi quy lỗi, 39 test chức năng đạt**. Các lỗi đó gồm:
- `23505 ... "IX_Contracts_ContractNo"` và `23505 ... "PK_PriceItems"` (BUG-01);
- `Round 0: both the edit and the submit succeeded.` (BUG-03);
- 4 test về mẫu lỗi (BUG-02).

Trên mã nguồn sau khi sửa, cả 46 test đều đạt.

Trong lần chạy trên bản cũ, project test phải dùng file `QTKD.Api.csproj` đã cố định phiên bản package. File gốc khai báo `Version="10.*"`, khiến project test nhận EF Core 10.0.4 trong khi API dùng 10.0.15, và build lỗi `CS1705`. Tuần 07 đã cố định phiên bản package để mọi máy restore giống nhau.

## 7. Chạy lại luồng demo tuần 06 sau khi sửa lỗi

Sau khi áp migration `ContractConcurrencyToken` vào database phát triển `qtkd`, hai script tuần 06 được chạy lại với API thật (`http://localhost:5209`):

- [week07-rerun-week06-demo-log.txt](test-results/week07-rerun-week06-demo-log.txt): tạo Customer `201` → tạo Draft `201` → Submit thiếu dữ liệu `422` → bổ sung `200` → Submit `200` → GET thấy `PendingApproval` → Submit lặp `409`.
- [week07-rerun-week06-verification-report.txt](test-results/week07-rerun-week06-verification-report.txt): **94/94 PASS**.

## 8. Hạn chế và lỗi còn tồn tại

- **AC01 (phân quyền) chưa triển khai.** Chưa có tài khoản/đăng nhập nên không kiểm thử được. Cần đặc tả quyền (vấn đề mở Q1 của Phase 1) trước khi làm.
- **Danh sách nội dung/tệp bắt buộc của CTR-02 vẫn là giả định demo** (`ContentSummary`, `AttachmentFileName`). Tệp chỉ lưu tên, chưa upload.
- **Mỗi Contract chỉ có tối đa một `ContractSubmission`.** Khi làm luồng RevisionRequested → Draft → gửi lại, cần đổi sang bảng nhiều phiên gửi.
- **Test đồng thời mang tính xác suất.** Nếu không có lỗi thì test luôn đạt. Nếu có lỗi, test có thể không bắt được ở mọi lần chạy, nhưng trên bản cũ đã bắt được ngay ở lần thử đầu tiên. BUG-03 được thử 15 vòng mỗi lần chạy.
- **Một số phản hồi do framework sinh ra không theo mẫu lỗi chung.** URL không khớp route nào (ví dụ `/api/contracts/abc`) trả `404` không có body. Sai `Content-Type` trả `415` dạng ProblemDetails. Mọi lỗi từ các endpoint đã liệt kê đều dùng `{code, message, details}`.
- **Khi chạy API thật, log có thể có dòng `fail: Microsoft.EntityFrameworkCore...`** lúc có request đồng thời bị database chặn. Đây là log của EF Core cho các lỗi mà service đã xử lý chủ động (trả `400`/`409`), không phải lỗi hệ thống. Trong integration test, log EF Core được tắt để output dễ đọc.
