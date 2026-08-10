namespace Infinium.Persistence;

public static class FinalObjectAuthorityPolicy
{
    public const string Identity = "final-object-authority-rule/1.0.0";

    public static bool IsAuthorized(
        bool operationSupported,
        bool capabilityFreshAtUse,
        bool finalObjectIdentityProven,
        bool finalObjectOwnerRootAuthorized) =>
        operationSupported
        && capabilityFreshAtUse
        && finalObjectIdentityProven
        && finalObjectOwnerRootAuthorized;

    public static void RequireAuthorized(
        bool operationSupported,
        bool capabilityFreshAtUse,
        bool finalObjectIdentityProven,
        bool finalObjectOwnerRootAuthorized)
    {
        if (!IsAuthorized(
                operationSupported, capabilityFreshAtUse,
                finalObjectIdentityProven, finalObjectOwnerRootAuthorized))
        {
            throw new InvalidOperationException("The final opened object is outside current write authority.");
        }
    }
}
