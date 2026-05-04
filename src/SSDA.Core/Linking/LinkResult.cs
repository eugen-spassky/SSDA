namespace SSDA.Core.Linking;

/// <summary>
/// Outcome of <see cref="AuthenticatorLinker.AddAuthenticatorAsync"/>.
/// </summary>
public enum LinkResult
{
    /// <summary>The authenticator was registered server-side; the user must now enter the
    /// SMS code that Steam sent and pass it to <c>FinalizeAsync</c>.</summary>
    AwaitingFinalization,

    /// <summary>The account has no verified phone number — the user must add one via the
    /// Steam mobile app or website before linking.</summary>
    MustProvidePhoneNumber,

    /// <summary>The account already has an authenticator attached. Remove it through Steam
    /// support first.</summary>
    AuthenticatorPresent,

    /// <summary>Anything else Steam returns. Treat as fatal for this attempt.</summary>
    GeneralFailure,
}

/// <summary>
/// Outcome of <see cref="AuthenticatorLinker.FinalizeAsync"/>.
/// </summary>
public enum FinalizeResult
{
    /// <summary>The authenticator is fully linked. Persist the maFile now.</summary>
    Success,

    /// <summary>Steam rejected the SMS code (status 89). Re-prompt and retry.</summary>
    BadSMSCode,

    /// <summary>Steam asked for too many retries while clock-aligning (status 88 + 10 attempts).
    /// The user's clock is too far off Steam's clock.</summary>
    UnableToGenerateCorrectCodes,

    /// <summary>Anything else Steam returns. Treat as fatal for this attempt.</summary>
    GeneralFailure,
}
