using System;

using Amazon.S3;

using AutoFixture;
using AutoFixture.AutoNSubstitute;
using AutoFixture.NUnit3;

internal class AutoAttribute : AutoDataAttribute
{
    public AutoAttribute()
        : base(Create)
    {
    }

    public static IFixture Create()
    {
        var fixture = new Fixture();
        fixture.Customize(new AutoNSubstituteCustomization());
        fixture.Customizations.Add(new OptionsRelay());
        fixture.Customizations.Insert(-1, new TargetRelay());
        return fixture;
    }
}
