# Solution architecture — QTKD, Tuần 05–07

```mermaid
flowchart LR
    Client["Swagger UI / Postman"] --> Controllers["API Controllers<br/>CustomersController<br/>ContractsController<br/>PricingController"]
    Controllers --> Services["Services<br/>CustomerService<br/>ContractService<br/>PricingService"]
    Services --> Rules["Domain rules<br/>ContractRules<br/>CTR-01 · CTR-02 · CTR-03"]
    Services --> DbContext["Data access<br/>QtkdDbContext"]
    DbContext --> Models["Entities<br/>Customer · Contract<br/>ContractSubmission<br/>PriceList · PriceItem"]
    DbContext --> Database[("PostgreSQL<br/>Customers · Contracts<br/>ContractSubmission<br/>PriceLists · PriceItems")]
```

Controllers nhận HTTP request và trả HTTP response; mọi lỗi đều dùng chung dạng `{code, message, details}`. Services xử lý validation và điều phối nghiệp vụ. Các quy tắc hợp đồng nằm riêng trong `Domain/ContractRules.cs` (hàm thuần, không truy cập database, để unit test trực tiếp):

- `CanEdit` — CTR-01: chỉ sửa khi Draft hoặc RevisionRequested.
- `GetSubmissionErrors` — CTR-02: Customer Active, khoảng hiệu lực hợp lệ, nội dung/tệp (giả định demo).
- `CanTransition` — CTR-03: bảng chuyển trạng thái lấy từ sơ đồ máy trạng thái Phase 1; Draft → Approved không có trong bảng nên bị từ chối.

`ContractService` gọi các quy tắc này trước khi lưu và chỉ thực hiện chuyển Draft → PendingApproval. `QtkdDbContext` dùng Entity Framework Core để đọc/ghi các entity trong PostgreSQL; `Contract.Version` (cột hệ thống `xmin`) là concurrency token để hai request đồng thời không ghi đè nhau.

## Kiểm thử (tuần 07)

```mermaid
flowchart LR
    Unit["QTKD.UnitTests<br/>Ctr01 · Ctr02 · Ctr03<br/>ApiErrorFormat"] --> Rules["Domain/ContractRules<br/>Common/ApiErrorCodes"]
    Integration["QTKD.IntegrationTests<br/>WebApplicationFactory&lt;Program&gt;"] -->|HTTP trong bộ nhớ| Api["QTKD.Api<br/>Controllers → Services → DbContext"]
    Api --> TestDb[("PostgreSQL<br/>qtkd_test")]
    Integration -->|"QtkdDbContext riêng:<br/>đọc lại dữ liệu đã lưu"| TestDb
```

- **Unit test** kiểm tra quy tắc nghiệp vụ, không cần database.
- **Integration test** chạy toàn bộ API thật trên database kiểm thử `qtkd_test`, được xóa và tạo lại từ migration mỗi lần chạy. Test đối chiếu cả response lẫn dữ liệu thật trong database.
- Database phát triển `qtkd` không bị test sử dụng. Chi tiết: [week-07-testing.md](week-07-testing.md).
