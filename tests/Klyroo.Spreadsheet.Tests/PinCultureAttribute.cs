using System.Globalization;
using System.Reflection;
using Xunit;
using Xunit.v3;

namespace Klyroo.Spreadsheet.Tests;

// Tests that assert the English texts of the component pin the culture they assert, so the result does not depend on the
// language of the machine that runs them. The previous culture comes back after each test.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
internal sealed class PinCultureAttribute(string name = "en-US") : BeforeAfterTestAttribute
{
    private CultureInfo? _culture;
    private CultureInfo? _uiCulture;

    public override void Before(MethodInfo methodUnderTest, IXunitTest test)
    {
        _culture = CultureInfo.CurrentCulture;
        _uiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = new CultureInfo(name);
        CultureInfo.CurrentUICulture = new CultureInfo(name);
    }

    public override void After(MethodInfo methodUnderTest, IXunitTest test)
    {
        if (_culture is not null) CultureInfo.CurrentCulture = _culture;
        if (_uiCulture is not null) CultureInfo.CurrentUICulture = _uiCulture;
    }
}
