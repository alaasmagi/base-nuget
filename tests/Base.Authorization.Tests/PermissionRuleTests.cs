using Base.Authorization;
using Xunit;

public class PermissionRuleTests
{
    [Theory]
    [InlineData("courses.create")]
    [InlineData("courses.view-all")]
    [InlineData("course-materials.edit")]
    public void AcceptsModuleDotAction(string value)
    {
        Assert.True(PermissionNames.IsValid(value));
        Assert.Equal("perm:" + value, PermissionPolicyNames.For(value));
    }

    [Theory]
    [InlineData("course-management")]
    [InlineData("courses.create.extra")]
    [InlineData("courses..create")]
    [InlineData(".create")]
    [InlineData("courses.")]
    [InlineData(" .create")]
    [InlineData("courses. ")]
    [InlineData(".")]
    public void RejectsRoleNamesExtraDotsAndEmptySides(string value)
    {
        Assert.False(PermissionNames.IsValid(value));
        Assert.Throws<ArgumentException>(() => PermissionPolicyNames.For(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsMissingValue(string? value)
    {
        Assert.False(PermissionNames.IsValid(value));
    }

    [Fact]
    public void ModuleAndActionOverloadEnforcesTheSameRule()
    {
        Assert.Equal("perm:courses.create", PermissionPolicyNames.For("courses", Actions.Create));
        Assert.Throws<ArgumentException>(() => PermissionPolicyNames.For("courses.archive", Actions.Create));
        Assert.Throws<ArgumentException>(() => PermissionPolicyNames.For("courses", "create.all"));
    }

    [Fact]
    public void RequirePermissionAttributeRejectsRoleName()
    {
        Assert.Throws<ArgumentException>(() => new RequirePermissionAttribute("course-management"));
    }
}
