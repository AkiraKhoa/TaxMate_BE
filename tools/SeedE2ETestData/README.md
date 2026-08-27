# SeedE2ETestData

Tool này chỉ bootstrap master data cho bộ runbook E2E:

- 3 owner và 5 business.
- Tax-profile precondition.
- Product category, product, price, ingredient và BOM.
- Income/expense categories.
- Cash/Bank accounts ở trạng thái chưa xác nhận số dư đầu.
- Tax-profile precondition và ID category ổn định cho các flow TKN 2026.

Tool cố ý **không** tạo:

- Transaction, Income, Expense hoặc TaxPayment.
- InventoryMovement hoặc MoneyMovement.
- TaxPeriod, calculation, declaration, obligation hoặc snapshot.
- RevenueThresholdAlert.
- Migration/schema changes.

Lý do: các record trên phải được tạo qua workflow để kiểm chứng việc nối sổ; direct insert sẽ bỏ qua chính logic cần test. POS hiện cũng chưa có test clock/ngày nghiệp vụ tường minh cho dữ liệu lịch sử.

## Chạy sau khi build đã pass

Database phải được migrate tới `20260826150000_Shot3PersistTknQttBridgeChoice`. Tool không tự migrate schema.

```powershell
dotnet run --project tools/SeedE2ETestData/SeedE2ETestData.csproj
```

Tất cả test owner dùng mật khẩu `Test@123456`:

- `owner-a-income@taxmate.test`
- `owner-b-threshold@taxmate.test`
- `owner-c-refund@taxmate.test`

## An toàn và chạy lại

- Không DROP, ALTER, TRUNCATE, EnsureCreated, Migrate hoặc raw SQL.
- Nếu chưa có marker, toàn manifest được insert trong một transaction.
- Ở mode mặc định, nếu đã có marker, tool kiểm tra toàn bộ owner/business/product/account/BOM trước khi no-op.
- Nếu manifest chỉ tồn tại một phần hoặc sai ownership, tool dừng và không tự sửa/xóa dữ liệu.
- Profile Owner B chỉ là precondition fixture; nó không chứng minh B26-00 hay các transition đã pass.
- Bước verify kiểm tra cả tax profile và 10 income category định danh; marker có nhưng profile/category lệch ngoài hai state hợp lệ của Owner C thì tool dừng, không tự sửa.

Sau bootstrap, làm theo `TEST_RUNBOOK_INDEX.md` và từng file `TEST_FLOW_OWNER_*.md` ở workspace root.

## Manifest TKN 2026

Ba owner hiện có được tái sử dụng; không cần thêm owner chỉ dành riêng cho TKN:

| Owner | Precondition được seed | Vai trò TKN |
|---|---|---|
| A | `Over1BTo3B`, `IncomeBased`, effective year `2026` | Negative control: lịch TKN phải báo profile không tương thích; giữ làm flow QTT năm bình thường. |
| B | `AtOrBelow1B`, không có PIT method, `FirstHalfOfTaxYear/2026` | TKN sáu tháng đầu/cuối, đúng ngưỡng 1 tỷ, vượt 1 đồng, cộng doanh thu hai business và chuyển task TKN thành `NotApplicable`. |
| C | `Over1BTo3B`, `IncomeBased`, effective year `2025` | Nguồn lịch sử IncomeBased để test downward transition sang TKN năm, sau đó chọn `Later`/`Refund`/`Offset` cho QTT bridge. |

Các profile trên là điểm bắt đầu. Mọi thay đổi profile khi kết luận năm phải đi qua annual-conclusion preview/confirm của ứng dụng. Controlled fixture Owner C bên dưới chỉ giữ lại cho test thấp tầng hoặc phục hồi môi trường, không dùng trong manual E2E chuẩn.

Trong test service/repository cô lập không chạy được API, sau khi đã tạo lịch sử IncomeBased qua workflow có thể dùng fixture opt-in sau:

```powershell
dotnet run --project tools/SeedE2ETestData/SeedE2ETestData.csproj -- --prepare-owner-c-annual-tkn
```

Mode này chỉ đổi sáu field tax-profile của Owner C thành `AtOrBelow1B`, method/effective year null và `BeforeTaxYear/2026`. Trước khi đổi, tool bắt buộc kiểm tra:

- Owner C còn đúng initial IncomeBased fixture hoặc đã ở đúng target state idempotent.
- C1 vẫn là business duy nhất của Owner C.
- Doanh thu owner-wide năm 2026 đúng `800.000.000` theo dataset và không có blocker nguồn cơ bản (Order thiếu invoice hoặc manual BusinessRevenue không dương).
- Đã có `TaxCalculation` IncomeBased `Completed` cho đủ bốn quý 2026 của Owner C, được tạo trước đó qua workflow.
- Chưa có `TaxPeriod` TKN 2026 khi profile còn ở initial state.

Validation và update chạy trong serializable transaction. Chạy lại khi đã ở target state là no-op. Đây chỉ là **controlled fixture setup**; kết quả không được dùng làm bằng chứng rằng annual-conclusion workflow đã pass.

### ID nguồn ổn định

Các khoản thu dùng để dựng doanh thu phải được tạo qua UI/API Income. Chọn category `Doanh thu kinh doanh`; category `Khoản thu không phải doanh thu` là negative control và không được cộng vào ngưỡng.

| Business | BusinessRevenue category | NonRevenueCashIn category |
|---|---|---|
| A1 | `a1000000-0000-4000-8000-000000006101` | `a1000000-0000-4000-8000-000000006201` |
| A2 | `a1000000-0000-4000-8000-000000006102` | `a1000000-0000-4000-8000-000000006202` |
| B1 | `b1000000-0000-4000-8000-000000006101` | `b1000000-0000-4000-8000-000000006201` |
| B2 | `b1000000-0000-4000-8000-000000006102` | `b1000000-0000-4000-8000-000000006202` |
| C1 | `c1000000-0000-4000-8000-000000006101` | `c1000000-0000-4000-8000-000000006201` |

Sản phẩm B1/B2 có đơn giá fixture `1` đồng để test chính xác biên `1.000.000.000` và `+1` mà không bị rounding. Có thể dùng manual BusinessRevenue để tránh tạo order có quantity phi thực tế; dù chọn cách nào, record nguồn vẫn phải được tạo qua workflow.

## Các case TKN mà manifest hỗ trợ

### B — hộ mới, nửa năm và vượt ngưỡng

1. Khi chưa tạo doanh thu, mở task `tkn-2026-firsthalf`: preview phải cảnh báo doanh thu bằng `0`; chỉ được close khi tester xác nhận warning.
2. Tạo BusinessRevenue có ngày nghiệp vụ trong `01/01–30/06/2026` (ví dụ 600 triệu trên B1): task FirstHalf được mở, close, calculate và tạo/nộp TKN qua API/UI.
3. Tạo thêm BusinessRevenue trong `01/07–31/12/2026` trên B2 để tổng owner-wide chính xác `1.000.000.000`: vẫn thuộc TKN; kiểm tra phép cộng hai business mà không backdate vào FirstHalf đã khóa.
4. Tạo thêm đúng `1` đồng BusinessRevenue trong SecondHalf: threshold engine phải phát hiện `Crossed1B`, và lịch TKN chưa nộp của phần còn lại phải thành `NotApplicable`; TKN FirstHalf đã nộp vẫn được giữ nguyên. Code hiện chưa có API xác nhận transition profile, nên chỉ kết luận được alert/N-A; không đánh dấu bước xác nhận transition là pass.
5. Gọi QTT-next-step trên TKN FirstHalf phải bị từ chối vì bridge chỉ áp dụng sau cửa sổ cuối năm.

Các bước 2–4 là các nhánh stateful. Nếu muốn kiểm từng biên độc lập, dùng database test sạch được bootstrap lại; tool không reset hoặc sửa ngược dữ liệu flow.

### C — TKN năm sau downward transition và QTT bridge

1. Tạo doanh thu năm 2026 của C1 qua workflow với tổng không quá 1 tỷ.
2. Tạo lịch sử `01/CNKD`/calculation/declaration IncomeBased và `TaxPayment` PIT `Completed` qua các workflow hiện có nếu kiểm nhánh nộp thừa. Payment phải truy được `SourceTaxMethod = IncomeBased`; không direct insert payment hoặc snapshot.
3. Sau khi năm kết thúc và hoàn tất đủ bốn quý, dùng annual-conclusion card/API để xác nhận. Profile hiện hành thành `AtOrBelow1B`, method/effective year null, commencement `BeforeTaxYear/2026`; lịch sử IncomeBased vẫn nằm trong snapshot cũ. Chỉ dùng `--prepare-owner-c-annual-tkn` cho test thấp tầng không thể gọi API.
4. Mở task `tkn-2026-annual`, preview, close, calculate, tạo declaration và đánh dấu đã nộp bên ngoài qua workflow TKN.
5. Không có PIT IncomeBased đã trả: QTT-next-step không có choices và không tạo QTT.
6. Có PIT IncomeBased đã trả: QTT-next-step trả ba choice `Later`, `Refund`, `Offset`. `Later` không tạo/submit QTT và phải còn sau reload; `Refund` dùng account C1 `c1000000-0000-4000-8000-000000000502`; `Offset` đi qua màn QTT và phân bổ đủ số nộp thừa. Manual E2E dùng nghĩa vụ ngoài MST `0312345680`, tên `Lê Hoài Thu`, mã `C-EXT-2026-001`, nội dung `Nghĩa vụ thuế test ngoài TaxMate`, amount `12.000.000`.
7. Payment PIT thiếu snapshot phương pháp nguồn phải bật `RequiresPaymentSourceReview` và chặn tạo draft Refund/Offset.

Owner C cố ý bắt đầu ở IncomeBased và không có commencement data vì database chỉ cho commencement trên profile `AtOrBelow1B`. Bước transition của ứng dụng phải ghi cặp commencement/year; seed không được tạo trước một trạng thái profile bất hợp lệ.

## Phần bắt buộc tạo thủ công/UI/API

Manifest không đánh dấu bất kỳ case nào là đã pass. Tester vẫn phải tạo qua workflow:

- Order hoặc manual Income và ngày nghiệp vụ tương ứng.
- Threshold alert và thao tác xác nhận transition.
- TKN TaxPeriod, close, calculation, declaration, submit/mark-submitted.
- Quarterly/annual TaxPeriod phục vụ QTT.
- `TaxPayment` PIT Completed và snapshot nguồn IncomeBased.
- TaxDeclarationObligation chỉ cần cho variant Offset nội bộ; manual spine dùng nghĩa vụ ngoài deterministic nên không seed obligation.
- QTT calculation/declaration và lựa chọn `Later`/`Refund`/`Offset`.

Không thêm trực tiếp các row này vào seeder: làm vậy sẽ bỏ qua chính projector, threshold engine, period lifecycle, snapshot và bridge cần kiểm thử.
