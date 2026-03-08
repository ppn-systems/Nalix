// Ensures environment variables are set before Directories is touched.
namespace Nalix.Shared.Tests.Environment;

public sealed class DirectoriesFixture
{
    public System.String BaseDir;

    public DirectoriesFixture()
    {
        // 1) Tạo base dir tạm
        BaseDir = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "NalixTest_" + System.Guid.NewGuid().ToString("N"));

        try { _ = System.IO.Directory.CreateDirectory(BaseDir); } catch { }

        // 2) Set ENV mà Directories đọc
        System.Environment.SetEnvironmentVariable("NALIX_BASE_PATH", BaseDir);
        System.Environment.SetEnvironmentVariable("NALIX_DATA_PATH", System.IO.Path.Join(BaseDir, "data"));
        System.Environment.SetEnvironmentVariable("NALIX_LOGS_PATH", System.IO.Path.Join(BaseDir, "data", "logs"));
        System.Environment.SetEnvironmentVariable("NALIX_TEMP_PATH", System.IO.Path.Join(BaseDir, "data", "tmp"));
        System.Environment.SetEnvironmentVariable("NALIX_CONFIG_PATH", System.IO.Path.Join(BaseDir, "data", "config"));
        System.Environment.SetEnvironmentVariable("NALIX_STORAGE_PATH", System.IO.Path.Join(BaseDir, "data", "storage"));
        System.Environment.SetEnvironmentVariable("NALIX_DB_PATH", System.IO.Path.Join(BaseDir, "data", "db"));
        System.Environment.SetEnvironmentVariable("NALIX_TEMP_RETENTION_DAYS", "1");

        // 3) Đặt override trước khi bất kỳ Lazy nào evaluate
        Nalix.Common.Environment.Directories.SetBasePathOverride(BaseDir);

        // 4) Ép chạy static ctor ngay bây giờ (đã có ENV + override)
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
            typeof(Nalix.Common.Environment.Directories).TypeHandle);
    }
}
