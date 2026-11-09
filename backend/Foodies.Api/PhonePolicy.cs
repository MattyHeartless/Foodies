namespace Foodies.Api;

public class PhonePolicy(IConfiguration configuration) {
    // Enabling SMS always restores verification, even if the temporary flag remains set.
    public bool RequiresVerification => configuration.GetValue<bool>("Sms:Enabled")
        || !configuration.GetValue<bool>("PhoneVerification:AllowUnverifiedOrders");
}
