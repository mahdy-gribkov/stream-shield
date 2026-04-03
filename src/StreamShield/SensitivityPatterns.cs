using System;
using System.Collections.Generic;

namespace StreamShield;

enum SensitivityKind
{
    None,
    Password,
    SensitiveText
}

static class SensitivityPatterns
{
    private static readonly string[] Keywords =
    [
        // Financial
        "card number", "credit card", "debit card", "cc number", "card num",
        "cardnumber", "cc-number", "ccnum", "creditcard", "debitcard",
        "card-number", "card_number", "cc_number", "cc-num",
        "cvv", "cvc", "csc", "cvv2", "cvc2", "cid",
        "security code", "securitycode", "card code", "cardcode",
        "expiry", "expiration", "exp date", "expdate",
        "card-expiry", "card_expiry", "card-exp",
        "iban", "routing number", "routing", "account number",
        "accountnumber", "account-number", "account_number",
        "bank account", "bank code", "branch code",
        "sort code", "sortcode", "sort-code",
        "swift", "swift-code", "bic", "bic-code",
        "pin number",

        // Identity
        "ssn", "social security", "social-security",
        "passport", "national id", "national-id",
        "driver license", "driver-license", "drivers license", "driver's license",
        "license number", "license-number",
        "tax id", "tax-id", "ein", "tin",
        "id number", "id-number", "idnumber",
        "state id", "green card", "greencard",
        "visa number", "visa-number",
        "dob", "date of birth", "date-of-birth",
        "member id", "member-id", "memberid",
        "patient id", "patient-id", "patient_id",
        "insurance number", "insurance-number",
        "maiden",

        // Authentication
        "password",
        "secret", "secret key", "secret-key",
        "token", "access token", "access-token",
        "api key", "api-key", "apikey",
        "private key", "private-key",
        "client secret", "client-secret",
        "bearer", "bearer-token",
        "authorization", "auth code", "authcode",
        "recovery code", "recovery-code",
        "backup code", "backup-code",
        "verification code", "verification-code",
        "passphrase", "credential", "oauth",

        // MFA / OTP
        "pin", "otp", "totp", "mfa",
        "2fa", "one-time", "mfa code",
        "pin-input",

        // Hebrew (Israeli payment/identity forms)
        "\u05ea.\u05d6",                     // ת.ז (Teudat Zehut / national ID)
        "\u05ea\u05e2\u05d5\u05d3\u05ea \u05d6\u05d4\u05d5\u05ea", // תעודת זהות
        "\u05db\u05e8\u05d8\u05d9\u05e1 \u05d0\u05e9\u05e8\u05d0\u05d9", // כרטיס אשראי (credit card)
        "\u05de\u05e1\u05e4\u05e8 \u05db\u05e8\u05d8\u05d9\u05e1", // מספר כרטיס (card number)
        "\u05e1\u05d9\u05e1\u05de\u05d4",   // סיסמה (password)
        "\u05e1\u05d9\u05e1\u05de\u05d0",   // סיסמא (password alt spelling)
        "\u05e7\u05d5\u05d3 \u05d0\u05d1\u05d8\u05d7\u05d4", // קוד אבטחה (security code)
        "\u05e7\u05d5\u05d3 \u05e1\u05d5\u05d3\u05d9", // קוד סודי (secret code)
        "\u05ea\u05d5\u05e7\u05e3",         // תוקף (expiry)
        "\u05de\u05e1\u05e4\u05e8 \u05d7\u05e9\u05d1\u05d5\u05df" // מספר חשבון (account number)
    ];

    private static List<string>? _customPatterns;

    public static void LoadCustomPatterns(List<string> patterns)
    {
        _customPatterns = patterns;
    }

    public static bool IsSensitiveField(string? name, string? automationId,
        string? labeledByName = null, string? helpText = null)
    {
        ReadOnlySpan<string?> texts = [name, automationId, labeledByName, helpText];

        foreach (string? text in texts)
            if (MatchesAny(text, Keywords))
                return true;

        if (_customPatterns is { Count: > 0 })
            foreach (string? text in texts)
                if (MatchesAny(text, _customPatterns))
                    return true;

        return false;
    }

    private static bool MatchesAny(string? value, IReadOnlyList<string> keywords)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        for (int i = 0; i < keywords.Count; i++)
        {
            if (value.Contains(keywords[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
