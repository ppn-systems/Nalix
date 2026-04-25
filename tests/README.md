# Test Guide

Thư mục `tests/` chứa toàn bộ automated tests của Nalix và được tổ chức theo từng nhóm trách nhiệm:

- `Nalix.Framework.Tests`: unit tests cho `Nalix.Common`, `Nalix.Framework`, và các hành vi runtime nền tảng.
- `Nalix.Runtime.Pipeline.Tests`: tests cho `Nalix.Runtime.Pipeline`.
- `Nalix.Analyzers.Tests`: kiểm thử analyzer/code fix.

## Chạy test

Chạy toàn bộ test:

```powershell
dotnet test .\tests\Nalix.Tests.sln
```

Chạy theo project:

```powershell
dotnet test .\tests\Nalix.Framework.Tests\Nalix.Framework.Tests.csproj
dotnet test .\tests\Nalix.Network.Test\Nalix.Network.Tests.csproj
dotnet test .\tests\Nalix.Analyzers.Tests\Nalix.Analyzers.Tests.csproj
```

## Quy ước test

- Ưu tiên đặt tên theo mẫu `Method_State_Expectation`.
- Mỗi test chỉ nên kiểm tra một hành vi công khai rõ ràng.
- Dữ liệu lặp lại nên đưa vào helper hoặc `TheoryData<>` thay vì `object[]` thô.
- Test không nên phụ thuộc thứ tự chạy hoặc trạng thái còn sót lại từ test khác.
- Khi test runtime cần dependency chung, hãy dùng shared config trong [tests/Directory.Build.props](/E:/Cs/Nalix/tests/Directory.Build.props).

## Khi thêm test mới

- Thêm test vào đúng project theo domain.
- Nếu test cần package dùng chung cho mọi test project, thêm vào [tests/Directory.Build.props](/E:/Cs/Nalix/tests/Directory.Build.props) thay vì lặp lại từng `.csproj`.
- Nếu chỉ một project cần package riêng, khai báo ngay trong `.csproj` của project đó.
- Ưu tiên smoke test nhỏ, deterministic, và dễ đọc trước khi thêm scenario phức tạp.
