# Solution architecture — QTKD, Tuần 05

```mermaid
flowchart LR
    Client["Swagger UI / Postman"] --> Controllers["API Controllers<br/>CustomersController<br/>ContractsController"]
    Controllers --> Services["Services<br/>CustomerService<br/>ContractService"]
    Services --> DbContext["Data access<br/>QtkdDbContext"]
    DbContext --> Models["Entities<br/>Customer · Contract<br/>PriceList · PriceItem"]
    DbContext --> Database[("PostgreSQL<br/>Customers · Contracts<br/>PriceLists · PriceItems")]
```

Controllers nhận HTTP request và trả HTTP response. Services xử lý các thao tác ứng dụng. `QtkdDbContext` dùng Entity Framework Core để đọc/ghi các entity trong PostgreSQL.
