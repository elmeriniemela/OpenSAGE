using System.IO;
using Xunit;

namespace OpenSage.Tests;

public class ConfigurationTests
{
    [Theory]
    [InlineData("UseAlternateMouse = yes", true)]
    [InlineData("usealternatemouse = TRUE", true)]
    [InlineData("UseAlternateMouse = no", false)]
    [InlineData("UseAlternateMouse = false", false)]
    public void ReadsOriginalMouseOption(string text, bool alternate)
    {
        var config = new Configuration { UseAlternateMouse = !alternate };
        using var reader = new StringReader("Resolution = 1024 768\r\n" + text);

        Assert.True(config.LoadMouseOptions(reader));
        Assert.Equal(alternate, config.UseAlternateMouse);
    }

    [Fact]
    public void MissingOrInvalidMouseOptionPreservesConfiguration()
    {
        var config = new Configuration { UseAlternateMouse = true };
        using var reader = new StringReader("UseAlternateMouse = invalid\nScrollFactor = 60");

        Assert.False(config.LoadMouseOptions(reader));
        Assert.True(config.UseAlternateMouse);
    }

    [Fact]
    public void PersonalOptionsOverrideInstallationAndBundleOptions()
    {
        var root = Directory.CreateTempSubdirectory("opensage-mouse-options-");
        try
        {
            var installation = Directory.CreateDirectory(Path.Combine(root.FullName, "Generals"));
            var personal = Directory.CreateDirectory(Path.Combine(root.FullName, "UserData"));
            var bundleOptions = Path.Combine(root.FullName, "options.ini");
            var installationOptions = Path.Combine(installation.FullName, "Options.ini");
            var personalOptions = Path.Combine(personal.FullName, "Options.ini");
            File.WriteAllText(bundleOptions, "UseAlternateMouse = yes");
            var config = new Configuration();

            config.LoadMouseOptions(installation.FullName, personal.FullName);
            Assert.True(config.UseAlternateMouse);

            File.WriteAllText(installationOptions, "UseAlternateMouse = no");
            config.LoadMouseOptions(installation.FullName, personal.FullName);
            Assert.False(config.UseAlternateMouse);

            File.WriteAllText(personalOptions, "UseAlternateMouse = yes");
            config.LoadMouseOptions(installation.FullName, personal.FullName);
            Assert.True(config.UseAlternateMouse);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}
