# Solution architecture — QTKD, Tuần 05–06

```mermaid
flowchart LR
    Client["Swagger UI / Postman"] --> Controllers["API Controllers<br/>CustomersController<br/>ContractsController<br/>PricingController"]
    Controllers --> Services["Services<br/>CustomerService<br/>ContractService<br/>PricingService"]
    Services --> DbContext["Data access<br/>QtkdDbContext"]
    DbContext --> Models["Entities<br/>Customer · Contract<br/>ContractSubmission<br/>PriceList · PriceItem"]
    DbContext --> Database[("PostgreSQL<br/>Customers · Contracts<br/>ContractSubmission<br/>PriceLists · PriceItems")]
```

Controllers nhận HTTP request và trả HTTP response. Services xử lý validation và nghiệp vụ; `ContractService` chỉ cho sửa Draft/RevisionRequested và mô phỏng một lần bàn giao sang PendingApproval. `QtkdDbContext` dùng Entity Framework Core để đọc/ghi các entity trong PostgreSQL.
