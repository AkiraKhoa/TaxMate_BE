# Hướng Dẫn Kiểm Thử & Giải Phóng Tài Khoản SePay BankHub Sandbox

Tài liệu này hướng dẫn cách dọn dẹp, khôi phục và giải phóng các Số tài khoản thử nghiệm trên **SePay BankHub Sandbox** dành cho Developer và các AI Agent làm việc tiếp theo trên dự án **TaxMate**.

---

## 1. Vấn Đề Thường Gặp Khi Test Môi Trường Local (Dev/Sandbox)

Trong quá trình phát triển dự án, khi bạn **xóa/reset Database local** hoặc chạy lại Migration (`dotnet ef database update`):
- Toàn bộ các mã `SePayCompanyXid` và `SePayBankAccountXid` lưu dưới DB local sẽ bị mất.
- **TUY NHIÊN**, trên Server thử nghiệm của SePay Sandbox (`bankhub-api-sandbox.sepay.vn`), các tài khoản ngân hàng thử nghiệm (ACB, MBBank, VietinBank, KienLongBank...) vẫn đang ở trạng thái `ACTIVE` và được gán với các `company_xid` cũ dưới tài khoản đối tác (`client_id:client_secret`).

### 🔴 Các biểu hiện lỗi:
1. **Lỗi Trùng STK (khi liên kết mới)**: 
   - SePay BankHub quy định **1 Số tài khoản ngân hàng chỉ được thuộc về 1 Company XID** tại một thời điểm.
   - Khi bạn tạo Cửa hàng mới dưới local và cố gắng liên kết lại một STK thử nghiệm cũ, SePay Sandbox sẽ chặn trên giao diện WebView và báo:  
     > *"Số tài khoản này đã được liên kết trước đó"*.
2. **Lỗi 400 Validation Error (khi gửi request Hủy liên kết)**:
   - Nếu gọi API Hủy liên kết mà truyền `company_xid` của Cửa hàng mới nhưng `bank_account_xid` lại thuộc về `company_xid` cũ trên SePay, SePay API sẽ trả về lỗi:
     ```json
     {
       "status": 400,
       "error": 400,
       "messages": {
         "bank_account_xid": "Tài khoản ngân hàng không tồn tại hoặc không thuộc công ty này"
       }
     }
     ```

---

## 2. Giải Pháp Triệt Để (Quy Trình 2 Bước)

Để giải phóng hoàn toàn các STK bị kẹt trên SePay Sandbox và test lại luồng từ đầu, bạn chỉ cần thực hiện 2 bước sau:

### 🟢 Bước 1: Kéo dữ liệu kẹt từ SePay Server về DB local
Gọi API tiện ích trên Backend TaxMate:
```http
POST /api/PaymentAccount/sepay-recover-all
```
- **Cơ chế**: Endpoint này truy vấn trực tiếp Server SePay (`GET /v1/bank-account?per_page=100`) không qua DB local. Nó lấy toàn bộ các `bank_account_xid` và `company_xid` đang lưu trên SePay Sandbox, tự động map và nạp lại vào bảng `PaymentAccounts` local.

### 🟢 Bước 2: Gọi API Hủy liên kết để SePay giải phóng STK
1. Sau Bước 1, danh sách các tài khoản ngân hàng đã xuất hiện lại đầy đủ trong DB local.
2. Gọi API lấy URL Hủy liên kết cho tài khoản cần xóa:
   ```http
   GET /api/PaymentAccount/sepay-disconnect-url?paymentAccountId={id}
   ```
   - **Cơ chế**: Backend tự động gọi `GET /v1/bank-account/{xid}` lên SePay để lấy **chính xác `company_xid` thực sự sở hữu tài khoản trên SePay Server**, sau đó tạo Hosted Link Hủy liên kết (`purpose: UNLINK_BANK_ACCOUNT`).
3. Mở URL nhận được trong WebView/Trình duyệt và bấm **Xác nhận Hủy liên kết**.
4. **KẾT QUẢ**: Server SePay sẽ chuyển trạng thái STK đó thành `unlinked`/`inactive` và giải phóng STK đó hoàn toàn trên SePay Sandbox. Bạn có thể test liên kết mới mượt mà!

---

## 3. Danh Sách Các API Liên Quan Đến SePay Trong Codebase

| HTTP Method | Endpoint API | Mô tả |
|---|---|---|
| `GET` | `/api/PaymentAccount/sepay-connect-url?businessId={id}` | Lấy WebView URL để liên kết ngân hàng mới với SePay. |
| `GET` | `/api/PaymentAccount/sepay-disconnect-url?paymentAccountId={id}` | Lấy WebView URL để hủy liên kết ngân hàng với SePay. |
| `POST` | `/api/PaymentAccount/sepay-sync?businessId={id}` | Đồng bộ danh sách tài khoản ngân hàng từ SePay về Cửa hàng cụ thể. |
| `POST` | `/api/PaymentAccount/sepay-recover-all` | **(Dev Only)** Phục hồi toàn bộ STK bị kẹt trên SePay Server về DB local. |
| `POST` | `/api/webhook/payment/bankhub` | Webhook nhận sự kiện tự động từ SePay (IPN, BANK_ACCOUNT_LINKED, UNLINKED...). |

---

## 4. Vị Trí Code Cần Chú Ý Trong Mã Nguồn

- **[ISePayService.cs](file:///g:/ChuaTeThatNghiep/BE/src/TaxMate.Service/Interfaces/ISePayService.cs)** & **[SePayService.cs](file:///g:/ChuaTeThatNghiep/BE/src/TaxMate.Service/Services/SePayService.cs)**: Chứa toàn bộ các hàm giao tiếp trực tiếp với SePay BankHub API (`/v1/token`, `/v1/company/create`, `/v1/link-token/create`, `/v1/bank-account`).
- **[PaymentAccountService.cs](file:///g:/ChuaTeThatNghiep/BE/src/TaxMate.Service/Services/PaymentAccountService.cs)**: Chứa logic xử lý DB local, hàm `RecoverAllFromSePayAsync` và `GetSePayDisconnectUrlAsync`.
- **[PaymentAccountController.cs](file:///g:/ChuaTeThatNghiep/BE/src/TaxMate.API/Controllers/PaymentAccountController.cs)**: Expose các RESTful API endpoints cho Frontend/Swagger gọi.
