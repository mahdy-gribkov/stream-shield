using System;
using System.Collections.Generic;
using Xunit;

namespace StreamShield.Tests;

public class SensitivityPatternsTests
{
    public SensitivityPatternsTests()
    {
        SensitivityPatterns.LoadCustomPatterns([]);
        SensitivityPatterns.SetActiveCategories([.. Enum.GetValues<PatternCategory>()]);
    }

    // --- Authentication ---

    [Theory]
    [InlineData("Secret Key")]
    [InlineData("API Key")]
    [InlineData("Private Key")]
    [InlineData("Access Token")]
    [InlineData("Client Secret")]
    [InlineData("Bearer Token")]
    [InlineData("Recovery Code")]
    [InlineData("Backup Code")]
    [InlineData("Passphrase")]
    [InlineData("Credential")]
    [InlineData("OAuth Settings")]
    public void AuthKeywords_DetectedByName(string name)
    {
        Assert.True(SensitivityPatterns.IsSensitiveField(name, null));
    }

    // --- Financial ---

    [Theory]
    [InlineData("Card Number")]
    [InlineData("Credit Card")]
    [InlineData("CVV")]
    [InlineData("CVC")]
    [InlineData("CVV2")]
    [InlineData("CID")]
    [InlineData("Security Code")]
    [InlineData("IBAN")]
    [InlineData("Routing Number")]
    [InlineData("Account Number")]
    [InlineData("Sort Code")]
    [InlineData("SWIFT")]
    [InlineData("Bank Code")]
    [InlineData("Branch Code")]
    [InlineData("PIN Number")]
    public void FinancialKeywords_DetectedByName(string name)
    {
        Assert.True(SensitivityPatterns.IsSensitiveField(name, null));
    }

    // --- Identity ---

    [Theory]
    [InlineData("SSN")]
    [InlineData("Social Security")]
    [InlineData("Passport")]
    [InlineData("National ID")]
    [InlineData("Driver License")]
    [InlineData("Drivers License")]
    [InlineData("License Number")]
    [InlineData("Tax ID")]
    [InlineData("ID Number")]
    [InlineData("State ID")]
    [InlineData("Green Card")]
    [InlineData("Visa Number")]
    [InlineData("Date of Birth")]
    [InlineData("DOB")]
    [InlineData("Member ID")]
    [InlineData("Patient ID")]
    [InlineData("Insurance Number")]
    [InlineData("Maiden Name")]
    public void IdentityKeywords_DetectedByName(string name)
    {
        Assert.True(SensitivityPatterns.IsSensitiveField(name, null));
    }

    // --- OTP / MFA ---

    [Theory]
    [InlineData("OTP")]
    [InlineData("PIN")]
    [InlineData("Auth Code")]
    [InlineData("2FA")]
    [InlineData("Verification Code")]
    [InlineData("MFA Code")]
    [InlineData("TOTP")]
    public void OtpMfaKeywords_DetectedByName(string name)
    {
        Assert.True(SensitivityPatterns.IsSensitiveField(name, null));
    }

    // --- AutomationId matching (unified list covers all) ---

    [Theory]
    [InlineData("cc-number")]
    [InlineData("ccnum")]
    [InlineData("card-number")]
    [InlineData("cvv")]
    [InlineData("cvc")]
    [InlineData("ssn")]
    [InlineData("otp")]
    [InlineData("api-key")]
    [InlineData("apikey")]
    [InlineData("secret-key")]
    [InlineData("access-token")]
    [InlineData("client-secret")]
    [InlineData("recovery-code")]
    [InlineData("driver-license")]
    [InlineData("national-id")]
    [InlineData("patient-id")]
    [InlineData("member-id")]
    [InlineData("date-of-birth")]
    [InlineData("license-number")]
    [InlineData("visa-number")]
    public void IdKeywords_DetectedByAutomationId(string automationId)
    {
        Assert.True(SensitivityPatterns.IsSensitiveField(null, automationId));
    }

    // --- LabeledBy detection (Google password pattern) ---

    [Theory]
    [InlineData("Password")]
    [InlineData("Enter your password")]
    [InlineData("Secret Key")]
    [InlineData("Card Number")]
    [InlineData("Social Security Number")]
    public void LabeledByName_DetectedWhenNameAndIdEmpty(string labeledByName)
    {
        Assert.True(SensitivityPatterns.IsSensitiveField(null, null, labeledByName));
    }

    // --- HelpText detection ---

    [Theory]
    [InlineData("Enter your password")]
    [InlineData("Enter your SSN")]
    [InlineData("Credit card number")]
    public void HelpText_DetectedWhenOtherPropsEmpty(string helpText)
    {
        Assert.True(SensitivityPatterns.IsSensitiveField(null, null, null, helpText));
    }

    // --- Case insensitivity ---

    [Fact]
    public void CaseInsensitive()
    {
        Assert.True(SensitivityPatterns.IsSensitiveField("CARD NUMBER", null));
        Assert.True(SensitivityPatterns.IsSensitiveField("card number", null));
        Assert.True(SensitivityPatterns.IsSensitiveField(null, "CC-NUMBER"));
        Assert.True(SensitivityPatterns.IsSensitiveField(null, null, "PASSWORD"));
    }

    // --- False positives ---

    [Theory]
    [InlineData("Email")]
    [InlineData("email-address")]
    [InlineData("First Name")]
    [InlineData("Last Name")]
    [InlineData("Address")]
    [InlineData("City")]
    [InlineData("State")]
    [InlineData("Zip Code")]
    [InlineData("Phone Number")]
    [InlineData("Country")]
    [InlineData("Company")]
    [InlineData("Username")]
    [InlineData("Search")]
    [InlineData("Comment")]
    [InlineData("Message")]
    public void CommonFields_NotDetected(string name)
    {
        Assert.False(SensitivityPatterns.IsSensitiveField(name, null));
    }

    // --- Null / empty ---

    [Fact]
    public void NullInputs_ReturnsFalse()
    {
        Assert.False(SensitivityPatterns.IsSensitiveField(null, null));
        Assert.False(SensitivityPatterns.IsSensitiveField(null, null, null, null));
    }

    [Fact]
    public void EmptyStrings_ReturnsFalse()
    {
        Assert.False(SensitivityPatterns.IsSensitiveField("", "", "", ""));
    }

    // --- Custom patterns ---

    [Fact]
    public void CustomPattern_Detected()
    {
        SensitivityPatterns.LoadCustomPatterns(["my-secret-field", "company-internal"]);

        Assert.True(SensitivityPatterns.IsSensitiveField("Enter my-secret-field value", null));
        Assert.True(SensitivityPatterns.IsSensitiveField(null, "company-internal-key"));
    }

    [Fact]
    public void CustomPattern_ChecksAllProperties()
    {
        SensitivityPatterns.LoadCustomPatterns(["custom-sensitive"]);

        Assert.True(SensitivityPatterns.IsSensitiveField(null, null, "custom-sensitive label"));
        Assert.True(SensitivityPatterns.IsSensitiveField(null, null, null, "custom-sensitive hint"));
    }

    [Fact]
    public void CustomPatternCleared_NoLongerDetected()
    {
        SensitivityPatterns.LoadCustomPatterns(["custom-keyword"]);
        Assert.True(SensitivityPatterns.IsSensitiveField("custom-keyword input", null));

        SensitivityPatterns.LoadCustomPatterns([]);
        Assert.False(SensitivityPatterns.IsSensitiveField("custom-keyword input", null));
    }

    // --- Hebrew keywords ---

    [Theory]
    [InlineData("\u05ea.\u05d6")]                      // ת.ז
    [InlineData("\u05ea\u05e2\u05d5\u05d3\u05ea \u05d6\u05d4\u05d5\u05ea")] // תעודת זהות
    [InlineData("\u05db\u05e8\u05d8\u05d9\u05e1 \u05d0\u05e9\u05e8\u05d0\u05d9")] // כרטיס אשראי
    [InlineData("\u05e1\u05d9\u05e1\u05de\u05d4")]     // סיסמה
    [InlineData("\u05e7\u05d5\u05d3 \u05d0\u05d1\u05d8\u05d7\u05d4")] // קוד אבטחה
    public void HebrewKeywords_Detected(string name)
    {
        Assert.True(SensitivityPatterns.IsSensitiveField(name, null));
    }

    // --- Substring matching ---

    [Fact]
    public void SubstringMatch_DetectedInLongerName()
    {
        Assert.True(SensitivityPatterns.IsSensitiveField("Enter your card number here", null));
        Assert.True(SensitivityPatterns.IsSensitiveField("billing_cvv_input", null));
        Assert.True(SensitivityPatterns.IsSensitiveField(null, "input-cc-number-field"));
    }

    // --- Name now checked against all keywords (was a bug) ---

    [Theory]
    [InlineData("cardnumber")]
    [InlineData("accountnumber")]
    [InlineData("sortcode")]
    [InlineData("apikey")]
    public void Name_MatchesFormerIdOnlyKeywords(string name)
    {
        Assert.True(SensitivityPatterns.IsSensitiveField(name, null));
    }

    // --- Category filtering ---

    [Fact]
    public void DisabledCategory_NotDetected()
    {
        var categories = new HashSet<PatternCategory>(Enum.GetValues<PatternCategory>());
        categories.Remove(PatternCategory.Financial);
        SensitivityPatterns.SetActiveCategories(categories);

        Assert.False(SensitivityPatterns.IsSensitiveField("Card Number", null));
        Assert.True(SensitivityPatterns.IsSensitiveField("Password", null));
    }

    [Fact]
    public void OnlyOneCategory_OnlyThatDetected()
    {
        SensitivityPatterns.SetActiveCategories(new HashSet<PatternCategory> { PatternCategory.MfaOtp });

        Assert.True(SensitivityPatterns.IsSensitiveField("PIN", null));
        Assert.False(SensitivityPatterns.IsSensitiveField("Card Number", null));
        Assert.False(SensitivityPatterns.IsSensitiveField("Password", null));
    }

    [Fact]
    public void EmptyCategories_NothingBuiltInDetected()
    {
        SensitivityPatterns.SetActiveCategories(new HashSet<PatternCategory>());

        Assert.False(SensitivityPatterns.IsSensitiveField("Card Number", null));

        SensitivityPatterns.LoadCustomPatterns(["my-custom"]);
        Assert.True(SensitivityPatterns.IsSensitiveField("my-custom field", null));
    }

    [Fact]
    public void ReEnableCategory_DetectedAgain()
    {
        var categories = new HashSet<PatternCategory>(Enum.GetValues<PatternCategory>());
        categories.Remove(PatternCategory.Identity);
        SensitivityPatterns.SetActiveCategories(categories);

        Assert.False(SensitivityPatterns.IsSensitiveField("SSN", null));

        SensitivityPatterns.SetActiveCategories([.. Enum.GetValues<PatternCategory>()]);

        Assert.True(SensitivityPatterns.IsSensitiveField("SSN", null));
    }
}
