using Foodies.Api;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace Foodies.Tests;
public class PhonePolicyTests {
    [Theory]
    [InlineData(false,false,true)]
    [InlineData(true,false,false)]
    [InlineData(false,true,true)]
    [InlineData(true,true,true)]
    public void Sms_enabling_always_restores_verification(bool allow, bool enabled, bool required) {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["PhoneVerification:AllowUnverifiedOrders"] = allow.ToString(), ["Sms:Enabled"] = enabled.ToString()
        }).Build();
        Assert.Equal(required,new PhonePolicy(config).RequiresVerification);
    }
}
