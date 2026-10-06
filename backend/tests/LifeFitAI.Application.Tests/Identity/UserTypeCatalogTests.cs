using FluentAssertions;
using LifeFitAI.Domain.Identity;

namespace LifeFitAI.Application.Tests.Identity;

public sealed class UserTypeCatalogTests
{
    [Fact]
    public void Create_StoresTheCatalogFields()
    {
        var roleId = Guid.Parse("11111111-1111-4111-8111-111111111004");

        var type = UserType.Create(
            Guid.Parse("55555555-5555-4555-8555-555555555005"),
            UserTypeCodes.Coach,
            "Coach",
            "Leads workouts and training sessions.",
            UserTypeCategories.Staff,
            roleId,
            50,
            true);

        type.Code.Should().Be("COACH");
        type.Name.Should().Be("Coach");
        type.Category.Should().Be(UserTypeCategories.Staff);
        type.RoleId.Should().Be(roleId);
        type.SortOrder.Should().Be(50);
        type.IsActive.Should().BeTrue();
    }
}
