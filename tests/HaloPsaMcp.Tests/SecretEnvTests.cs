using HaloPsaMcp.Modules.Common.Security;
using Xunit;

namespace HaloPsaMcp.Tests;

public class SecretEnvTests : IDisposable {
    private readonly string? _priorEnv;
    private readonly string? _priorFile;
    private readonly string _tempFile;

    public SecretEnvTests() {
        _priorEnv = Environment.GetEnvironmentVariable("TEST_SECRET");
        _priorFile = Environment.GetEnvironmentVariable("TEST_SECRET_FILE");
        _tempFile = Path.GetTempFileName();
        File.WriteAllText(_tempFile, "from-file\n");
    }

    public void Dispose() {
        Environment.SetEnvironmentVariable("TEST_SECRET", _priorEnv);
        Environment.SetEnvironmentVariable("TEST_SECRET_FILE", _priorFile);
        File.Delete(_tempFile);
    }

    [Fact]
    public void Get_prefers_file_over_env() {
        Environment.SetEnvironmentVariable("TEST_SECRET", "from-env");
        Environment.SetEnvironmentVariable("TEST_SECRET_FILE", _tempFile);
        Assert.Equal("from-file", SecretEnv.Get("TEST_SECRET"));
    }

    [Fact]
    public void Get_falls_back_to_env_when_no_file() {
        Environment.SetEnvironmentVariable("TEST_SECRET_FILE", null);
        Environment.SetEnvironmentVariable("TEST_SECRET", "from-env");
        Assert.Equal("from-env", SecretEnv.Get("TEST_SECRET"));
    }
}
